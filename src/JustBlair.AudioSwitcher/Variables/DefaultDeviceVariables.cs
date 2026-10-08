using System.Collections.Concurrent;
using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Sdk.Variables;
using NAudio.CoreAudioApi;
using Serilog;

namespace JustBlair.AudioSwitcher.Variables;

/// <summary>
/// The four default-device names. The host polls eager variables and this beta ignores pushed values
/// for them, so the poll is fast and only reads a cache that the Windows change notification keeps
/// current. Without the notification, every read goes to Windows instead.
/// </summary>
internal sealed class DefaultDeviceVariables(ILogger logger) : IDisposable
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

	private static readonly (string Id, AudioFlow Flow, Role Role)[] Sources =
	[
		("default-output", AudioFlow.Output, Role.Multimedia),
		("comm-output", AudioFlow.Output, Role.Communications),
		("default-mic", AudioFlow.Input, Role.Multimedia),
		("comm-mic", AudioFlow.Input, Role.Communications)
	];

	private readonly ILogger _logger = logger.ForContext<DefaultDeviceVariables>();
	private readonly ConcurrentDictionary<string, string?> _names = new();
	private readonly Lock _watcherLock = new();
	private DefaultDeviceWatcher? _watcher;
	private volatile bool _cacheIsLive;

	// Variable names are persisted in user data, so they never change. default_output predates the rest.
	public static IReadOnlyList<VariableDefinition> Definitions { get; } =
	[
		VariableDefinition.Eager("audioswitcher_default_output", VariableType.Text, refreshInterval: PollInterval)
			with { Id = "default-output", DisplayName = Strings.Variables.DefaultOutput.DisplayName() },
		VariableDefinition.Eager("audioswitcher_comm_output", VariableType.Text, refreshInterval: PollInterval)
			with { Id = "comm-output", DisplayName = Strings.Variables.CommOutput.DisplayName() },
		VariableDefinition.Eager("audioswitcher_default_mic", VariableType.Text, refreshInterval: PollInterval)
			with { Id = "default-mic", DisplayName = Strings.Variables.DefaultMic.DisplayName() },
		VariableDefinition.Eager("audioswitcher_comm_mic", VariableType.Text, refreshInterval: PollInterval)
			with { Id = "comm-mic", DisplayName = Strings.Variables.CommMic.DisplayName() }
	];

	public static bool Owns(string localId) => Sources.Any(s => s.Id == localId);

	public string? DefaultOutputName => _names.GetValueOrDefault("default-output");

	public void Start()
	{
		lock (_watcherLock)
		{
			if (_watcher is null)
			{
				try
				{
					_watcher = new DefaultDeviceWatcher(Refresh);
					_cacheIsLive = _watcher.IsRegistered;
				}
				catch (Exception exception)
				{
					_logger.Warning(exception, "Device-change notifications unavailable; reading Windows on every poll.");
					_cacheIsLive = false;
				}

				if (!_cacheIsLive)
				{
					_logger.Warning("Could not register for device-change notifications; reading Windows on every poll.");
				}
			}
		}

		Refresh();
	}

	public void Refresh()
	{
		foreach (var source in Sources)
		{
			try
			{
				_names[source.Id] = AudioDevices.GetDefaultDevice(source.Flow, source.Role)?.Name;
			}
			catch (Exception exception)
			{
				_logger.Warning(exception, "Could not read the default device for {VariableId}.", source.Id);
				_names[source.Id] = null;
			}
		}
	}

	public VariableReading Read(string localId)
	{
		if (!_cacheIsLive)
		{
			Refresh();
		}

		return _names.GetValueOrDefault(localId) is { } name ? VariableReading.Of(name) : VariableReading.Unavailable;
	}

	public void Dispose()
	{
		lock (_watcherLock)
		{
			_watcher?.Dispose();
			_watcher = null;
			_cacheIsLive = false;
		}
	}
}
