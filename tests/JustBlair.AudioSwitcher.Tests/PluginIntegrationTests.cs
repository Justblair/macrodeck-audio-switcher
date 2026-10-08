using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace JustBlair.AudioSwitcher.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[Test]
	public async Task Toggle_mode_fails_without_a_second_device()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-output",
			new Dictionary<string, object?> { ["mode"] = "toggle", ["device"] = "{device-1}" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Toggle_mode_fails_when_both_devices_are_the_same()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-output",
			new Dictionary<string, object?> { ["mode"] = "toggle", ["device"] = "{a}", ["device2"] = "{a}" });

		Assert.That(outcome.Succeeded, Is.False);
	}
	[Test]
	public async Task Every_ported_action_is_registered()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		foreach (var id in new[] { "set-default-microphone", "set-app-volume", "app-volume-up", "app-volume-down", "mute-app" })
		{
			var outcome = await harness.Actions.ExecuteAsync(id, new Dictionary<string, object?>());
			Assert.That(outcome.Succeeded, Is.False, $"{id} should refuse to run without its required parameters");
		}
	}

	[Test]
	public async Task Microphone_toggle_fails_when_both_devices_are_the_same()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-microphone",
			new Dictionary<string, object?> { ["mode"] = "toggle", ["device"] = "{a}", ["device2"] = "{a}" });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Pair_with_both_sides_unchanged_is_a_successful_no_op()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-default-output-and-microphone",
			new Dictionary<string, object?> { ["outputMode"] = "unchanged", ["inputMode"] = "unchanged" });

		Assert.That(outcome.Succeeded, Is.True);
	}

	[Test]
	public async Task App_volume_reports_an_app_without_a_session()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"set-app-volume",
			new Dictionary<string, object?> { ["app"] = "no-such-app-audioswitcher-test", ["volume"] = 40 });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[TestCase("Spotify", "spotify")]
	[TestCase("msedgewebview2", "msedgewebview2")]
	[TestCase("Discord PTB", "discord-ptb")]
	[TestCase("7zFM", "x-7zfm")]
	[TestCase("___", "unknown")]
	public void App_slugs_are_valid_local_ids(string processName, string expected)
	{
		Assert.That(Variables.AppVariables.Slug(processName), Is.EqualTo(expected));
	}
}
/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:uk.justblair.audio-switcher"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_action_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.SetDefaultOutput.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}

