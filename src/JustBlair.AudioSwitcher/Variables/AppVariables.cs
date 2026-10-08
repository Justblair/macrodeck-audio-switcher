using System.Text.RegularExpressions;
using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace JustBlair.AudioSwitcher.Variables;

/// <summary>
/// Per-app volume and mute as a browsable catalog: every app with an audio session appears, so nothing
/// has to be declared ahead of time. Both are writable, so a slider or switch widget can drive them.
/// Subscribed values are pushed on change; reads go straight to Windows.
/// </summary>
internal sealed partial class AppVariables(ILogger logger) : IDisposable
{
	private const string VolumeKind = "volume";
	private const string MutedKind = "muted";
	private static readonly TimeSpan PushInterval = TimeSpan.FromMilliseconds(500);

	private readonly ILogger _logger = logger.ForContext<AppVariables>();
	private readonly Lock _stateLock = new();
	private readonly Dictionary<string, object?> _lastPublished = [];
	private HashSet<string> _subscribed = [];
	private HashSet<string> _knownApps = new(StringComparer.Ordinal);
	private IVariableSink? _sink;
	private CancellationTokenSource? _pushLoop;
	private bool _pushFailing;

	[GeneratedRegex("^app-(?<slug>[a-z][a-z0-9]*(?:-[a-z0-9]+)*)-(?<kind>volume|muted)$")]
	private static partial Regex AppVariableId();

	[GeneratedRegex("[^a-z0-9]+")]
	private static partial Regex NonSlugCharacters();

	public static bool Owns(string localId) => AppVariableId().IsMatch(localId);

	public void Attach(IVariableSink sink) => _sink = sink;

	public IReadOnlyList<VariableDefinition> Discover(string? search)
	{
		var apps = AppSessions.Snapshot();
		RememberApps(apps);

		return
		[
			.. apps
				.Where(app => string.IsNullOrWhiteSpace(search) || app.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase))
				.SelectMany(app => new[] { Define(app.ProcessName, VolumeKind), Define(app.ProcessName, MutedKind) })
		];
	}

	/// <summary>Resolves a stored id even while its app is closed, so a bound widget keeps its binding.</summary>
	public static VariableDefinition? Resolve(string localId)
	{
		if (!TryParse(localId, out var slug, out var kind))
		{
			return null;
		}

		var app = FindBySlug(slug);
		return Define(app?.ProcessName ?? slug, kind);
	}

	public static VariableReading Read(string localId)
		=> TryParse(localId, out var slug, out var kind) ? ReadingOf(FindBySlug(slug), kind) : VariableReading.Unavailable;

	public static VariableWriteResult Write(string localId, object? value)
	{
		if (!TryParse(localId, out var slug, out var kind))
		{
			return VariableWriteResult.NotFound();
		}

		if (FindBySlug(slug) is not { } app)
		{
			return VariableWriteResult.Unavailable();
		}

		if (kind == VolumeKind)
		{
			return Values.ReadNumber(value) is { } percent
				? Applied(AppSessions.SetVolume(app.ProcessName, (int)Math.Round(percent)))
				: VariableWriteResult.InvalidValue();
		}

		return Values.ReadBool(value) is { } mute
			? Applied(AppSessions.SetMute(app.ProcessName, mute))
			: VariableWriteResult.InvalidValue();
	}

	public IReadOnlyList<VariableValue> Subscribe(IReadOnlyCollection<string> localIds)
	{
		var ids = localIds.Where(Owns).ToHashSet(StringComparer.Ordinal);
		var apps = AppSessions.Snapshot();
		var values = ids.Select(id => ValueOf(id, apps)).ToList();

		lock (_stateLock)
		{
			_subscribed = ids;
			_lastPublished.Clear();
			foreach (var value in values)
			{
				_lastPublished[value.Id] = value.Reading.Value;
			}

			if (ids.Count > 0 && _pushLoop is null)
			{
				_pushLoop = new CancellationTokenSource();
				_ = PushChangesAsync(_pushLoop.Token);
			}
		}

		return values;
	}

	public void InvalidateCatalog()
	{
		if (_sink is { } sink)
		{
			_ = sink.InvalidateCatalogAsync(CancellationToken.None)
				.ContinueWith(t => _logger.Warning(t.Exception, "Invalidating the app catalog failed."), TaskContinuationOptions.OnlyOnFaulted);
		}
	}

	public void Dispose()
	{
		lock (_stateLock)
		{
			_pushLoop?.Cancel();
			_pushLoop?.Dispose();
			_pushLoop = null;
		}
	}

	private async Task PushChangesAsync(CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(PushInterval);
		try
		{
			while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
			{
				await PushOnceAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task PushOnceAsync(CancellationToken cancellationToken)
	{
		try
		{
			var apps = AppSessions.Snapshot();
			if (RememberApps(apps))
			{
				InvalidateCatalog();
			}

			List<VariableValue> changed;
			lock (_stateLock)
			{
				changed = [];
				foreach (var id in _subscribed)
				{
					var value = ValueOf(id, apps);
					if (!_lastPublished.TryGetValue(id, out var last) || !Equals(last, value.Reading.Value))
					{
						_lastPublished[id] = value.Reading.Value;
						changed.Add(value);
					}
				}
			}

			if (changed.Count > 0 && _sink is { } sink)
			{
				await sink.PublishAsync(changed, cancellationToken).ConfigureAwait(false);
			}

			_pushFailing = false;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			if (!_pushFailing)
			{
				_logger.Warning(exception, "Publishing app volumes failed; retrying quietly until it recovers.");
				_pushFailing = true;
			}
		}
	}

	/// <summary>Returns true when the set of apps with sessions changed since the last look.</summary>
	private bool RememberApps(IReadOnlyList<AppSession> apps)
	{
		var current = apps.Select(a => Slug(a.ProcessName)).ToHashSet(StringComparer.Ordinal);
		lock (_stateLock)
		{
			if (current.SetEquals(_knownApps))
			{
				return false;
			}

			_knownApps = current;
			return true;
		}
	}

	private static VariableValue ValueOf(string localId, IReadOnlyList<AppSession> apps)
	{
		if (!TryParse(localId, out var slug, out var kind))
		{
			return VariableValue.Unavailable(localId);
		}

		var app = apps.FirstOrDefault(a => Slug(a.ProcessName) == slug);
		return VariableValue.Of(localId, ReadingOf(app, kind));
	}

	private static VariableReading ReadingOf(AppSession? app, string kind) => app switch
	{
		null => VariableReading.Unavailable,
		_ when kind == VolumeKind => VariableReading.Of(app.Volume, 0, 100, 1),
		_ => VariableReading.Of(app.Muted)
	};

	private static VariableWriteResult Applied(int sessionsChanged)
		=> sessionsChanged > 0 ? VariableWriteResult.Applied() : VariableWriteResult.Unavailable();

	private static AppSession? FindBySlug(string slug)
		=> AppSessions.Snapshot().FirstOrDefault(a => Slug(a.ProcessName) == slug);

	private static bool TryParse(string localId, out string slug, out string kind)
	{
		var match = AppVariableId().Match(localId);
		slug = match.Groups["slug"].Value;
		kind = match.Groups["kind"].Value;
		return match.Success;
	}

	/// <summary>Turns a process name into the lowercase kebab form local ids require.</summary>
	internal static string Slug(string processName)
	{
		var slug = NonSlugCharacters().Replace(processName.ToLowerInvariant(), "-").Trim('-');
		if (slug.Length == 0)
		{
			return "unknown";
		}

		return char.IsAsciiLetter(slug[0]) ? slug : "x-" + slug;
	}

	private static VariableDefinition Define(string appName, string kind)
	{
		var slug = Slug(appName);
		var id = $"app-{slug}-{kind}";
		var name = $"audioswitcher_app_{slug.Replace('-', '_')}_{kind}";

		return kind == VolumeKind
			? VariableDefinition.OnDemand(id, VariableType.Numeric) with
			{
				Name = name,
				DisplayName = Strings.Variables.AppVolume.DisplayName(appName),
				Unit = "%",
				SemanticKind = VariableSemanticKinds.Percentage,
				Write = new VariableWriteCapability()
			}
			: VariableDefinition.OnDemand(id, VariableType.Boolean) with
			{
				Name = name,
				DisplayName = Strings.Variables.AppMuted.DisplayName(appName),
				Write = new VariableWriteCapability()
			};
	}
}


