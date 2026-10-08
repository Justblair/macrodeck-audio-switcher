using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using static JustBlair.AudioSwitcher.Actions.ActionSupport;

namespace JustBlair.AudioSwitcher.Actions;

/// <summary>Sets the default output or microphone, or toggles between two. One class serves both
/// directions; only the id, wording and device list differ.</summary>
internal sealed class SetDefaultDeviceAction : IActionDefinition, IDynamicOptionsActionDefinition
{
	private const string ModeParameter = "mode";
	private const string DeviceParameter = "device";
	private const string Device2Parameter = "device2";
	private const string AllRolesParameter = "allRoles";

	private readonly AudioFlow _flow;
	private readonly ILogger _logger;

	public SetDefaultDeviceAction(AudioFlow flow, ILogger logger)
	{
		_flow = flow;
		_logger = logger.ForContext<SetDefaultDeviceAction>();

		// The output action's id and parameter names predate the microphone one and are bound to buttons.
		(Id, Name, Description) = flow == AudioFlow.Output
			? ("set-default-output", Strings.Actions.SetDefaultOutput.Name(), Strings.Actions.SetDefaultOutput.Description())
			: ("set-default-microphone", Strings.Actions.SetDefaultMicrophone.Name(), Strings.Actions.SetDefaultMicrophone.Description());

		Parameters =
		[
			ActionParameter.Choice(ModeParameter, [ToggleOption, SingleOption],
				label: Strings.Fields.Mode.Label(), defaultValue: ToggleMode, required: true),
			ActionParameter.DynamicChoice(DeviceParameter, label: Strings.Fields.Device.Label(), required: true),
			ActionParameter.DynamicChoice(Device2Parameter, label: Strings.Fields.Device2.Label())
				.OnlyWhen(ModeParameter, ToggleMode),
			ActionParameter.Toggle(AllRolesParameter, label: Strings.Fields.AllRoles.Label(), defaultValue: true)
		];
	}

	public string Id { get; }

	public LocalizedText Name { get; }

	public LocalizedText Description { get; }

	public IReadOnlyList<ActionParameter> Parameters { get; }

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> DeviceOptions(_flow, _logger);

	public IActionExecutor CreateExecutor() => new Executor(_flow, _logger);

	private sealed class Executor(AudioFlow flow, ILogger logger) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var parameters = context.Parameters;
			var result = DeviceSwitch.Apply(
				flow,
				(Values.ReadString(parameters.GetValueOrDefault(ModeParameter)) ?? ToggleMode) == ToggleMode,
				Values.ReadString(parameters.GetValueOrDefault(DeviceParameter)),
				Values.ReadString(parameters.GetValueOrDefault(Device2Parameter)),
				Values.ReadBool(parameters.GetValueOrDefault(AllRolesParameter)) ?? true);

			return Task.FromResult(FromSwitch(result, Strings.Fields.Device.Label(), logger));
		}
	}
}
