using JustBlair.AudioSwitcher.Actions;
using JustBlair.AudioSwitcher.Audio;
using JustBlair.AudioSwitcher.Variables;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace JustBlair.AudioSwitcher;

/// <summary>
/// Port of DiegoFdezAb's Macro Deck 2 VolumeMixerPlugin: default output and microphone switching, and
/// per-app volume and mute. Eager variables carry the default devices; a catalog carries per-app values.
/// </summary>
public sealed class PluginIntegration : IPluginIntegration, IVariableProvider, IDisposable
{
	private readonly ILogger _logger;
	private readonly DefaultDeviceVariables _defaults;
	private readonly AppVariables _apps;

	public PluginIntegration(ILogger logger)
	{
		_logger = logger.ForContext<PluginIntegration>();
		_defaults = new DefaultDeviceVariables(logger);
		_apps = new AppVariables(logger);

		Actions =
		[
			new SetDefaultDeviceAction(AudioFlow.Output, logger),
			new SetDefaultDeviceAction(AudioFlow.Input, logger),
			new SetDefaultPairAction(logger),
			new SetAppVolumeAction(logger),
			new AdjustAppVolumeAction(up: true, logger),
			new AdjustAppVolumeAction(up: false, logger),
			new MuteAppAction(logger),
			new RefreshDevicesAction(RefreshAll)
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	/// <summary>Runs again on reconnects and config changes; Start is idempotent.</summary>
	public Task InitializeAsync(IIntegrationContext context)
	{
		_defaults.Start();
		_logger.Information("Initialized. Default output: {Device}", _defaults.DefaultOutputName ?? "(none)");
		return Task.CompletedTask;
	}

	public Task ShutdownAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_defaults.Dispose();
		_apps.Dispose();
	}

	public IReadOnlyList<VariableDefinition> Variables => DefaultDeviceVariables.Definitions;

	public bool SupportsPush => true;

	public bool SupportsCatalog => true;

	public bool SupportsSearch => true;

	public string CatalogName => "Apps";

	public Task OnAttachedAsync(IVariableSink sink, CancellationToken cancellationToken)
	{
		_apps.Attach(sink);
		return Task.CompletedTask;
	}

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		if (DefaultDeviceVariables.Owns(localId))
		{
			return ValueTask.FromResult(_defaults.Read(localId));
		}

		return ValueTask.FromResult(AppVariables.Owns(localId) ? SafeRead(localId) : VariableReading.Unavailable);
	}

	public ValueTask<VariableWriteResult> SetValueAsync(string localId, object? value, CancellationToken cancellationToken = default)
	{
		if (!AppVariables.Owns(localId))
		{
			return ValueTask.FromResult(VariableWriteResult.NotWritable());
		}

		try
		{
			return ValueTask.FromResult(AppVariables.Write(localId, value));
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "Writing {VariableId} failed.", localId);
			return ValueTask.FromResult(VariableWriteResult.Failed());
		}
	}

	public ValueTask<VariableCatalogPage> DiscoverAsync(VariableCatalogQuery query, CancellationToken cancellationToken)
	{
		try
		{
			return ValueTask.FromResult(new VariableCatalogPage { Items = _apps.Discover(query.Search) });
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "Listing audio sessions for the variable catalog failed.");
			return ValueTask.FromResult(VariableCatalogPage.Empty);
		}
	}

	public ValueTask<VariableDefinition?> ResolveAsync(string localId, CancellationToken cancellationToken)
		=> ValueTask.FromResult(AppVariables.Resolve(localId));

	public ValueTask<IReadOnlyList<VariableValue>> SubscribeAsync(IReadOnlyCollection<string> localIds, CancellationToken cancellationToken)
	{
		try
		{
			return ValueTask.FromResult(_apps.Subscribe(localIds));
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "Subscribing to app variables failed.");
			return ValueTask.FromResult<IReadOnlyList<VariableValue>>([.. localIds.Select(VariableValue.Unavailable)]);
		}
	}

	private VariableReading SafeRead(string localId)
	{
		try
		{
			return AppVariables.Read(localId);
		}
		catch (Exception exception)
		{
			_logger.Warning(exception, "Reading {VariableId} failed.", localId);
			return VariableReading.Unavailable;
		}
	}

	private void RefreshAll()
	{
		_defaults.Refresh();
		_apps.InvalidateCatalog();
	}
}

