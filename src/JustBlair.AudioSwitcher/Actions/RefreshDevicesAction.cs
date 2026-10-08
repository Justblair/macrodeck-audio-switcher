using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace JustBlair.AudioSwitcher.Actions;

/// <summary>Kept from the Macro Deck 2 plugin. Rarely needed now that changes are tracked live, but it
/// re-reads every default device and refreshes the app list in the variable picker.</summary>
internal sealed class RefreshDevicesAction(Action refresh) : IActionDefinition
{
	public string Id => "refresh-devices";

	public LocalizedText Name => Strings.Actions.RefreshDevices.Name();

	public LocalizedText Description => Strings.Actions.RefreshDevices.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(refresh);

	private sealed class Executor(Action refresh) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			refresh();
			return ActionResult.SucceededTask;
		}
	}
}
