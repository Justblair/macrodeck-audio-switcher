using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using static JustBlair.AudioSwitcher.Actions.ActionSupport;

namespace JustBlair.AudioSwitcher.Actions;

/// <summary>Base for the per-app actions: every one picks an app by process name.</summary>
internal abstract class AppActionBase(ILogger logger) : IActionDefinition, IDynamicOptionsActionDefinition
{
	protected const string AppParameter = "app";

	protected ILogger Logger { get; } = logger;

	public abstract string Id { get; }

	public abstract LocalizedText Name { get; }

	public abstract LocalizedText Description { get; }

	public abstract IReadOnlyList<ActionParameter> Parameters { get; }

	protected static ActionParameter AppPicker { get; } =
		ActionParameter.DynamicChoice(AppParameter, label: Strings.Fields.App.Label(), required: true);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> AppOptions(Logger);

	public IActionExecutor CreateExecutor() => new Executor(this);

	protected abstract int Change(string app, IReadOnlyDictionary<string, object> parameters);

	private sealed class Executor(AppActionBase action) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
			=> Task.FromResult(ChangeApp(
				Values.ReadString(context.Parameters.GetValueOrDefault(AppParameter)),
				app => action.Change(app, context.Parameters),
				action.Logger));
	}
}

internal sealed class SetAppVolumeAction(ILogger logger) : AppActionBase(logger)
{
	private const string VolumeParameter = "volume";

	public override string Id => "set-app-volume";

	public override LocalizedText Name => Strings.Actions.SetAppVolume.Name();

	public override LocalizedText Description => Strings.Actions.SetAppVolume.Description();

	public override IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		AppPicker,
		ActionParameter.Slider(VolumeParameter, 0, 100, label: Strings.Fields.Volume.Label(), step: 1, defaultValue: 50)
	];

	protected override int Change(string app, IReadOnlyDictionary<string, object> parameters)
		=> AppSessions.SetVolume(app, (int)Math.Round(Values.ReadNumber(parameters.GetValueOrDefault(VolumeParameter)) ?? 50));
}

/// <summary>Volume up and volume down, as separate actions like the Macro Deck 2 plugin had.</summary>
internal sealed class AdjustAppVolumeAction(bool up, ILogger logger) : AppActionBase(logger)
{
	private const string StepParameter = "step";
	private const int DefaultStep = 5;

	public override string Id => up ? "app-volume-up" : "app-volume-down";

	public override LocalizedText Name => up ? Strings.Actions.AppVolumeUp.Name() : Strings.Actions.AppVolumeDown.Name();

	public override LocalizedText Description => up ? Strings.Actions.AppVolumeUp.Description() : Strings.Actions.AppVolumeDown.Description();

	public override IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		AppPicker,
		ActionParameter.Slider(StepParameter, 1, 25, label: Strings.Fields.Step.Label(), step: 1, defaultValue: DefaultStep)
	];

	protected override int Change(string app, IReadOnlyDictionary<string, object> parameters)
	{
		var step = (int)Math.Round(Values.ReadNumber(parameters.GetValueOrDefault(StepParameter)) ?? DefaultStep);
		return AppSessions.AdjustVolume(app, up ? step : -step);
	}
}

internal sealed class MuteAppAction(ILogger logger) : AppActionBase(logger)
{
	private const string MuteModeParameter = "muteMode";
	private const string MuteMode = "mute";
	private const string UnmuteMode = "unmute";

	public override string Id => "mute-app";

	public override LocalizedText Name => Strings.Actions.MuteApp.Name();

	public override LocalizedText Description => Strings.Actions.MuteApp.Description();

	public override IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		AppPicker,
		ActionParameter.Choice(MuteModeParameter,
			[
				new ActionParameterOption { Value = ToggleMode, Label = Strings.MuteModes.Toggle() },
				new ActionParameterOption { Value = MuteMode, Label = Strings.MuteModes.Mute() },
				new ActionParameterOption { Value = UnmuteMode, Label = Strings.MuteModes.Unmute() }
			],
			label: Strings.Fields.MuteMode.Label(), defaultValue: ToggleMode, required: true)
	];

	protected override int Change(string app, IReadOnlyDictionary<string, object> parameters)
		=> AppSessions.SetMute(app, Values.ReadString(parameters.GetValueOrDefault(MuteModeParameter)) switch
		{
			MuteMode => true,
			UnmuteMode => false,
			_ => null
		});
}

