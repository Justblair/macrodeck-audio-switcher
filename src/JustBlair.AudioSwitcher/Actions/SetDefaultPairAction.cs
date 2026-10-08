using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using static JustBlair.AudioSwitcher.Actions.ActionSupport;

namespace JustBlair.AudioSwitcher.Actions;

/// <summary>Sets the default output and microphone in one press, for example a headset in and out.
/// Each side can be a single device, a toggle, or left alone.</summary>
internal sealed class SetDefaultPairAction(ILogger logger) : IActionDefinition, IDynamicOptionsActionDefinition
{
	private const string OutputModeParameter = "outputMode";
	private const string OutputParameter = "output";
	private const string Output2Parameter = "output2";
	private const string InputModeParameter = "inputMode";
	private const string InputParameter = "input";
	private const string Input2Parameter = "input2";
	private const string AllRolesParameter = "allRoles";

	private readonly ILogger _logger = logger.ForContext<SetDefaultPairAction>();

	public string Id => "set-default-output-and-microphone";

	public LocalizedText Name => Strings.Actions.SetDefaultPair.Name();

	public LocalizedText Description => Strings.Actions.SetDefaultPair.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice(OutputModeParameter, [ToggleOption, SingleOption, UnchangedOption],
			label: Strings.Fields.OutputMode.Label(), defaultValue: ToggleMode, required: true),
		ActionParameter.DynamicChoice(OutputParameter, label: Strings.Fields.Output.Label())
			.OnlyWhen(OutputModeParameter, ToggleMode, SingleMode),
		ActionParameter.DynamicChoice(Output2Parameter, label: Strings.Fields.Output2.Label())
			.OnlyWhen(OutputModeParameter, ToggleMode),
		ActionParameter.Choice(InputModeParameter, [ToggleOption, SingleOption, UnchangedOption],
			label: Strings.Fields.InputMode.Label(), defaultValue: ToggleMode, required: true),
		ActionParameter.DynamicChoice(InputParameter, label: Strings.Fields.Input.Label())
			.OnlyWhen(InputModeParameter, ToggleMode, SingleMode),
		ActionParameter.DynamicChoice(Input2Parameter, label: Strings.Fields.Input2.Label())
			.OnlyWhen(InputModeParameter, ToggleMode),
		ActionParameter.Toggle(AllRolesParameter, label: Strings.Fields.AllRoles.Label(), defaultValue: true)
	];

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> DeviceOptions(context.ParameterName is InputParameter or Input2Parameter ? AudioFlow.Input : AudioFlow.Output, _logger);

	public IActionExecutor CreateExecutor() => new Executor(_logger);

	private sealed class Executor(ILogger logger) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var parameters = context.Parameters;
			var allRoles = Values.ReadBool(parameters.GetValueOrDefault(AllRolesParameter)) ?? true;

			var output = ApplySide(parameters, AudioFlow.Output, OutputModeParameter, OutputParameter, Output2Parameter,
				Strings.Fields.Output.Label(), allRoles);
			var input = ApplySide(parameters, AudioFlow.Input, InputModeParameter, InputParameter, Input2Parameter,
				Strings.Fields.Input.Label(), allRoles);

			// Both sides are attempted so one unplugged device does not block the other.
			return Task.FromResult(output.ErrorCode is not null ? output : input);
		}

		private ActionResult ApplySide(IReadOnlyDictionary<string, object> parameters, AudioFlow flow, string modeName,
			string deviceName, string device2Name, LocalizedText deviceLabel, bool allRoles)
		{
			var mode = Values.ReadString(parameters.GetValueOrDefault(modeName)) ?? ToggleMode;
			if (mode == UnchangedMode)
			{
				return ActionResult.Success();
			}

			var result = DeviceSwitch.Apply(flow, mode == ToggleMode,
				Values.ReadString(parameters.GetValueOrDefault(deviceName)),
				Values.ReadString(parameters.GetValueOrDefault(device2Name)),
				allRoles);
			return FromSwitch(result, deviceLabel, logger);
		}
	}
}

