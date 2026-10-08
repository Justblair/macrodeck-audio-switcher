using JustBlair.AudioSwitcher.Audio;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;

namespace JustBlair.AudioSwitcher.Actions;

/// <summary>Option lists and result mapping shared by the device and app actions.</summary>
internal static class ActionSupport
{
	public const string ToggleMode = "toggle";
	public const string SingleMode = "single";
	public const string UnchangedMode = "unchanged";

	public static ActionParameterOption ToggleOption { get; } = new() { Value = ToggleMode, Label = Strings.Modes.Toggle() };
	public static ActionParameterOption SingleOption { get; } = new() { Value = SingleMode, Label = Strings.Modes.Single() };
	public static ActionParameterOption UnchangedOption { get; } = new() { Value = UnchangedMode, Label = Strings.Modes.Unchanged() };

	/// <summary>Device names are the user's own content, so they travel as literal labels.</summary>
	public static Task<DynamicOptionsResult> DeviceOptions(AudioFlow flow, ILogger logger)
	{
		try
		{
			return Task.FromResult(new DynamicOptionsResult
			{
				Options = [.. AudioDevices.GetActiveDevices(flow).Select(d => new ActionParameterOption { Value = d.Id, Label = d.Name })],
				CacheSeconds = 5
			});
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Could not list {Flow} devices.", flow);
			return Task.FromResult(new DynamicOptionsResult { Options = [] });
		}
	}

	/// <summary>Lists apps with an audio session now, and accepts a typed name so a button can target an
	/// app that is not running yet.</summary>
	public static Task<DynamicOptionsResult> AppOptions(ILogger logger)
	{
		try
		{
			return Task.FromResult(new DynamicOptionsResult
			{
				Options = [.. AppSessions.Snapshot().Select(a => new ActionParameterOption { Value = a.ProcessName, Label = a.ProcessName })],
				AllowsCustomValue = true,
				CacheSeconds = 5
			});
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Could not list audio sessions.");
			return Task.FromResult(new DynamicOptionsResult { Options = [], AllowsCustomValue = true });
		}
	}

	public static ActionResult FromSwitch(DeviceSwitchResult result, LocalizedText deviceLabel, ILogger logger)
	{
		switch (result.Status)
		{
			case DeviceSwitchStatus.Switched:
				logger.Information("Default device set to {DeviceName}.", result.DeviceName);
				return ActionResult.Success();
			case DeviceSwitchStatus.MissingDevice:
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, MacroDeckStrings.Validation.Required(deviceLabel));
			case DeviceSwitchStatus.NeedTwoDevices:
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Errors.NeedTwoDevices());
			case DeviceSwitchStatus.DeviceUnavailable:
				logger.Warning("Audio device {DeviceId} is not connected.", result.DeviceId);
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.DeviceUnavailable());
			default:
				logger.Error("SetDefaultEndpoint failed for {DeviceName} with HRESULT 0x{Hr:X8}.", result.DeviceName, result.HResult);
				return ActionResult.Failed(ActionErrorCodes.ProviderRejected, Strings.Errors.SwitchFailed());
		}
	}

	/// <summary>Runs an app change and reports truthfully when the app has no audio session.</summary>
	public static ActionResult ChangeApp(string? app, Func<string, int> change, ILogger logger)
	{
		if (app is null)
		{
			return ActionResult.Failed(ActionErrorCodes.InvalidParameter, MacroDeckStrings.Validation.Required(Strings.Fields.App.Label()));
		}

		try
		{
			return change(app) > 0
				? ActionResult.Success()
				: ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.AppNotPlaying(app));
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Changing the audio session of {App} failed.", app);
			return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.AppChangeFailed(app));
		}
	}
}
