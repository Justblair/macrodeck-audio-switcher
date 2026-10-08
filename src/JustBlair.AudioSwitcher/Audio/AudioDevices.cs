using NAudio.CoreAudioApi;

namespace JustBlair.AudioSwitcher.Audio;

internal enum AudioFlow
{
	Output,
	Input
}

internal sealed record AudioDevice(string Id, string Name);

/// <summary>Endpoint queries and switching. Each call creates and disposes its own enumerator, so
/// nothing COM-related is held across threads.</summary>
internal static class AudioDevices
{
	public static IReadOnlyList<AudioDevice> GetActiveDevices(AudioFlow flow)
	{
		using var enumerator = new MMDeviceEnumerator();
		var result = new List<AudioDevice>();
		foreach (var device in enumerator.EnumerateAudioEndPoints(ToDataFlow(flow), DeviceState.Active))
		{
			using (device)
			{
				result.Add(new AudioDevice(device.ID, device.FriendlyName));
			}
		}

		return result;
	}

	public static AudioDevice? GetDefaultDevice(AudioFlow flow, Role role = Role.Multimedia)
	{
		using var enumerator = new MMDeviceEnumerator();
		var dataFlow = ToDataFlow(flow);
		if (!enumerator.HasDefaultAudioEndpoint(dataFlow, role))
		{
			return null;
		}

		using var device = enumerator.GetDefaultAudioEndpoint(dataFlow, role);
		return new AudioDevice(device.ID, device.FriendlyName);
	}

	/// <summary>Returns the first failing HRESULT, or 0 if every role was set. Endpoint ids are unique
	/// across playback and capture, so one call serves both.</summary>
	public static int SetDefaultDevice(string deviceId, bool allRoles)
	{
		ERole[] roles = allRoles
			? [ERole.Console, ERole.Multimedia, ERole.Communications]
			: [ERole.Multimedia];

		foreach (var role in roles)
		{
			var hr = PolicyConfig.SetDefaultEndpoint(deviceId, role);
			if (hr != 0)
			{
				return hr;
			}
		}

		return 0;
	}

	private static DataFlow ToDataFlow(AudioFlow flow) => flow == AudioFlow.Input ? DataFlow.Capture : DataFlow.Render;
}
