using System.Globalization;
using System.Text.Json;

namespace JustBlair.AudioSwitcher;

/// <summary>Parameter and variable values arrive as whatever the wire produced (a CLR primitive, a
/// string or a JsonElement), so every read goes through here.</summary>
internal static class Values
{
	public static string? ReadString(object? value) => value switch
	{
		null => null,
		string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
		JsonElement { ValueKind: JsonValueKind.String } e => ReadString(e.GetString()),
		_ => null
	};

	public static bool? ReadBool(object? value) => value switch
	{
		bool b => b,
		string s when bool.TryParse(s, out var parsed) => parsed,
		JsonElement { ValueKind: JsonValueKind.True } => true,
		JsonElement { ValueKind: JsonValueKind.False } => false,
		JsonElement { ValueKind: JsonValueKind.String } e => ReadBool(e.GetString()),
		_ => null
	};

	public static double? ReadNumber(object? value) => value switch
	{
		double d => d,
		float f => f,
		int i => i,
		long l => l,
		decimal m => (double)m,
		string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
		JsonElement { ValueKind: JsonValueKind.Number } e => e.GetDouble(),
		JsonElement { ValueKind: JsonValueKind.String } e => ReadNumber(e.GetString()),
		_ => null
	};
}
