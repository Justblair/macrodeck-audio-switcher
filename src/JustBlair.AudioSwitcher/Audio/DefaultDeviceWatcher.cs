using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace JustBlair.AudioSwitcher.Audio;

/// <summary>Windows reports default-device changes from any source (this plugin, the taskbar, Sound
/// settings), which keeps the cached names current without querying Windows on every poll.</summary>
internal sealed class DefaultDeviceWatcher : IMMNotificationClient, IDisposable
{
	private readonly MMDeviceEnumerator _enumerator = new();
	private readonly Action _onDefaultsChanged;
	private bool _registered;

	public DefaultDeviceWatcher(Action onDefaultsChanged)
	{
		_onDefaultsChanged = onDefaultsChanged;
		_registered = _enumerator.RegisterEndpointNotificationCallback(this) == 0;
	}

	public bool IsRegistered => _registered;

	// Called on a Windows audio thread that expects a quick return, so the work goes to the thread pool.
	public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => Task.Run(_onDefaultsChanged);

	// A renamed or unplugged device can change what a default reads as.
	public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Task.Run(_onDefaultsChanged);

	public void OnDeviceAdded(string pwstrDeviceId) { }

	public void OnDeviceRemoved(string deviceId) => Task.Run(_onDefaultsChanged);

	public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

	public void Dispose()
	{
		if (_registered)
		{
			_enumerator.UnregisterEndpointNotificationCallback(this);
			_registered = false;
		}

		_enumerator.Dispose();
	}
}
