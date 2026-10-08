namespace JustBlair.AudioSwitcher.Audio;

internal enum DeviceSwitchStatus
{
	Switched,
	MissingDevice,
	NeedTwoDevices,
	DeviceUnavailable,
	Refused
}

internal readonly record struct DeviceSwitchResult(DeviceSwitchStatus Status, string? DeviceId = null, string? DeviceName = null, int HResult = 0);

/// <summary>The single-device and toggle rules shared by every default-device action.</summary>
internal static class DeviceSwitch
{
	public static DeviceSwitchResult Apply(AudioFlow flow, bool toggle, string? device1, string? device2, bool allRoles)
	{
		if (string.IsNullOrWhiteSpace(device1))
		{
			return new DeviceSwitchResult(DeviceSwitchStatus.MissingDevice);
		}

		var target = device1;
		if (toggle)
		{
			if (string.IsNullOrWhiteSpace(device2) || device2 == device1)
			{
				return new DeviceSwitchResult(DeviceSwitchStatus.NeedTwoDevices);
			}

			// Same rule as the Macro Deck 2 plugin: on device 1 (or neither) go to device 2, otherwise device 1.
			var current = AudioDevices.GetDefaultDevice(flow)?.Id;
			target = current == device2 ? device1 : device2;
		}

		var device = AudioDevices.GetActiveDevices(flow).FirstOrDefault(d => d.Id == target);
		if (device is null)
		{
			return new DeviceSwitchResult(DeviceSwitchStatus.DeviceUnavailable, target);
		}

		var hr = AudioDevices.SetDefaultDevice(target, allRoles);
		return hr == 0
			? new DeviceSwitchResult(DeviceSwitchStatus.Switched, target, device.Name)
			: new DeviceSwitchResult(DeviceSwitchStatus.Refused, target, device.Name, hr);
	}
}
