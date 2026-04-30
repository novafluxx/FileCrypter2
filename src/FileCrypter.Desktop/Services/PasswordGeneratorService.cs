using System.Security.Cryptography;
using System.Text;

namespace FileCrypter.Desktop.Services;

public sealed class PasswordGeneratorService : IPasswordGeneratorService
{
    public const string RandomPasswordCharacters =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+[]{}";

    public const int MemorablePassphraseWordCount = 8;

    public static readonly IReadOnlyList<string> MemorablePassphraseWords =
    [
        "anchor", "apricot", "atlas", "bamboo", "beacon", "binary", "canyon", "cedar",
        "cipher", "cobalt", "copper", "coral", "delta", "ember", "falcon", "forest",
        "galaxy", "harbor", "hazel", "indigo", "jacket", "juniper", "lantern", "lunar",
        "marble", "meadow", "nebula", "onyx", "orchid", "pepper", "plasma", "prairie",
        "quartz", "raven", "river", "saffron", "signal", "silver", "summit", "timber",
        "topaz", "velvet", "violet", "walnut", "willow", "window", "yellow", "zenith",
        "acorn", "admiral", "alpine", "amber", "anvil", "arctic", "autumn", "avenue",
        "basil", "blossom", "border", "breeze", "bronze", "button", "camera", "carbon",
        "castle", "celery", "cherry", "circle", "comet", "cotton", "cradle", "crystal",
        "desert", "dragon", "engine", "fabric", "feather", "fennel", "garden", "ginger",
        "glacier", "golden", "gravel", "guitar", "hammer", "helmet", "island", "ivory",
        "jasper", "kettle", "lagoon", "lemon", "magnet", "maple", "matrix", "mitten",
        "mosaic", "mountain", "nickel", "ocean", "olive", "orbit", "paper", "parcel",
        "pencil", "picnic", "planet", "pollen", "rabbit", "radar", "rocket", "saddle",
        "salmon", "satin", "shadow", "shelter", "silicon", "sonar", "spiral", "spring",
        "stone", "stream", "sugar", "sunset", "tablet", "temple", "thunder", "ticket",
        "tulip", "tunnel", "turtle", "valley", "vector", "voyage", "winter", "wizard",
        "wonder", "zephyr", "artist", "basket", "biscuit", "blanket", "bridge", "browser",
        "bucket", "cabinet", "candle", "canvas", "carrot", "cavern", "cinnamon", "cloud",
        "coffee", "compass", "cookie", "curtain", "diamond", "domino", "dynamo", "editor",
        "farmer", "filter", "finger", "flame", "fossil", "funnel", "garlic", "gentle",
        "goblet", "granite", "harmony", "horizon", "icon", "igloo", "ink", "iron",
        "jelly", "kernel", "kiwi", "ladder", "laser", "liberty", "linen", "locket",
        "mango", "mantle", "marker", "meteor", "mirror", "mixer", "model", "modern",
        "motion", "motor", "museum", "napkin", "needle", "noodle", "notebook", "number",
        "object", "orange", "opal", "palace", "parade", "pattern", "pebble", "phrase",
        "piano", "pillow", "pixel", "pocket", "potato", "powder", "puzzle", "quiet",
        "radio", "ribbon", "ripple", "sandal", "school", "season", "secret", "sensor",
        "sesame", "socket", "solar", "spindle", "square", "stable", "station", "studio",
        "summer", "system", "tango", "tennis", "thread", "throne", "tomato", "travel",
        "trophy", "victory", "violin", "wisdom", "zipper", "badge", "banker", "barrel",
        "basic", "battery", "branch", "budget", "butter", "captain", "carpet", "center",
    ];

    public string GenerateRandomPassword()
    {
        return GenerateFromAlphabet(RandomPasswordCharacters, 24);
    }

    public string GenerateMemorablePassphrase()
    {
        string[] words = new string[MemorablePassphraseWordCount];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = MemorablePassphraseWords[RandomNumberGenerator.GetInt32(MemorablePassphraseWords.Count)];
        }

        return string.Join('-', words);
    }

    private static string GenerateFromAlphabet(string alphabet, int length)
    {
        if (string.IsNullOrEmpty(alphabet))
        {
            throw new InvalidOperationException("Password alphabet must contain at least one character.");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be positive.");
        }

        StringBuilder builder = new(length);
        for (int index = 0; index < length; index++)
        {
            builder.Append(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        }

        return builder.ToString();
    }
}
