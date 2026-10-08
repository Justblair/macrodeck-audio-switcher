using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace JustBlair.AudioSwitcher.Audio;

internal sealed record AppSession(string ProcessName, int Volume, bool Muted);

/// <summary>
/// Per-application volume, addressed by process name as the Macro Deck 2 plugin did. An app can own
/// sessions on several playback devices; changes apply to all of them and reads report the first.
/// </summary>
internal static class AppSessions
{
	private static readonly Lock ProcessNamesLock = new();
	private static Dictionary<uint, string> _processNames = [];

	public static IReadOnlyList<AppSession> Snapshot()
	{
		var result = new Dictionary<string, AppSession>(StringComparer.OrdinalIgnoreCase);
		ForEachSession((name, session) =>
		{
			if (!result.ContainsKey(name))
			{
				var volume = session.SimpleAudioVolume;
				result[name] = new AppSession(name, (int)Math.Round(volume.Volume * 100), volume.Mute);
			}
		});

		return [.. result.Values.OrderBy(s => s.ProcessName, StringComparer.OrdinalIgnoreCase)];
	}

	public static AppSession? Find(string processName)
		=> Snapshot().FirstOrDefault(s => string.Equals(s.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

	/// <summary>Returns how many sessions were changed; 0 means the app has no audio session.</summary>
	public static int Apply(string processName, Action<SimpleAudioVolume> change)
	{
		var changed = 0;
		ForEachSession((name, session) =>
		{
			if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
			{
				change(session.SimpleAudioVolume);
				changed++;
			}
		});

		return changed;
	}

	public static int SetVolume(string processName, int percent)
		=> Apply(processName, v => v.Volume = Math.Clamp(percent, 0, 100) / 100f);

	public static int AdjustVolume(string processName, int deltaPercent)
		=> Apply(processName, v => v.Volume = Math.Clamp(v.Volume + (deltaPercent / 100f), 0f, 1f));

	public static int SetMute(string processName, bool? mute)
		=> Apply(processName, v => v.Mute = mute ?? !v.Mute);

	private static void ForEachSession(Action<string, AudioSessionControl> visit)
	{
		var seenNames = new Dictionary<uint, string>();
		using var enumerator = new MMDeviceEnumerator();
		foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
		{
			using (device)
			{
				var sessions = device.AudioSessionManager.Sessions;
				for (var i = 0; i < sessions.Count; i++)
				{
					using var session = sessions[i];
					var pid = session.GetProcessID;
					if (pid == 0 || ProcessNameOf(pid) is not { } name)
					{
						continue;
					}

					seenNames[pid] = name;
					visit(name, session);
				}
			}
		}

		// Keeps the cache to processes that still have sessions, so a reused pid cannot inherit a stale name.
		lock (ProcessNamesLock)
		{
			_processNames = seenNames;
		}
	}

	private static string? ProcessNameOf(uint pid)
	{
		lock (ProcessNamesLock)
		{
			if (_processNames.TryGetValue(pid, out var cached))
			{
				return cached;
			}
		}

		try
		{
			using var process = Process.GetProcessById((int)pid);
			return string.IsNullOrWhiteSpace(process.ProcessName) ? null : process.ProcessName;
		}
		catch (ArgumentException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}
}
