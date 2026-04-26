using System.Text.Json.Serialization;

namespace FileCrypter.Core.Settings;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(FileCrypterSettings))]
internal sealed partial class FileCrypterSettingsJsonContext : JsonSerializerContext;
