# Audio Switcher for Macro Deck 3

Switch the Windows default output and microphone, and control per-app volume and mute, from Macro Deck 3.

This is a port of [VolumeMixerPlugin](https://github.com/DiegoFdezAb/MacroDeckVolumeMixerPlugin) by
DiegoFdezAb, the Macro Deck 2 plugin, rewritten for the Macro Deck 3 out-of-process plugin SDK. The
default-device switching code is based on the original. Windows only.

## Install

Download the `.macroDeckPlugin` file from the [latest release](https://github.com/Justblair/macrodeck-audio-switcher/releases/latest),
then install it from file in Macro Deck 3. Macro Deck warns that the plugin is not signed by the Store;
that is expected for a plugin installed outside the Store.

## Actions

| Action | What it does |
| --- | --- |
| Set default output | Switch to one playback device, or toggle between two |
| Set default microphone | Switch to one recording device, or toggle between two |
| Set default output and microphone | Both in one press; each side can switch, toggle or be left unchanged |
| Set app volume | Set one app's volume (0-100%) |
| App volume up / down | Change one app's volume by an adjustable step (default 5%) |
| Mute or unmute app | Toggle, mute or unmute one app |
| Refresh devices | Re-read the default devices and the app list |

Device actions can set the default for all Windows roles (Console, Multimedia, Communications) or for
Multimedia only. App actions pick an app by process name; a name can be typed for an app that is not
running yet. An action reports a failure, rather than doing nothing silently, when a device is
unplugged or an app has no audio session.

## Variables

| Variable | Value |
| --- | --- |
| `audioswitcher_default_output` | Default playback device |
| `audioswitcher_comm_output` | Default communications playback device |
| `audioswitcher_default_mic` | Default recording device |
| `audioswitcher_comm_mic` | Default communications recording device |
| `audioswitcher_app_<app>_volume` | An app's volume in percent (writable) |
| `audioswitcher_app_<app>_muted` | Whether an app is muted (writable) |

Per-app variables appear under **Apps** in the variable picker for every app that currently has an
audio session. Because they are writable, a slider or switch widget can drive them directly.

## Changes from the Macro Deck 2 plugin

- Runs as a separate process on the Macro Deck 3 SDK; configuration screens are declared parameters.
- Mute action gained explicit mute and unmute modes; volume steps are adjustable.
- App volume and mute are writable variables, and app variables no longer depend on which buttons use them.
- Failures are reported on the button and in the log instead of being ignored.

## Building

See [DEVELOPMENT.md](DEVELOPMENT.md) for building, debugging against Macro Deck and packaging.

## Licence

MIT. See [LICENSE](LICENSE). Original work copyright DiegoFdezAb.

