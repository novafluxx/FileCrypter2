using System.Text.Json.Serialization;

namespace FileCrypter.Core.Settings;

public enum FileCrypterThemePreference
{
    [JsonStringEnumMemberName("system")]
    System = 0,

    [JsonStringEnumMemberName("light")]
    Light = 1,

    [JsonStringEnumMemberName("dark")]
    Dark = 2,
}
