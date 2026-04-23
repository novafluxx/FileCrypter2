using System.Security.Cryptography;
using System.Text;

namespace FileCrypter.App.Services;

public sealed class PasswordGeneratorService : IPasswordGeneratorService
{
    public const string RandomPasswordCharacters =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+[]{}";

    public static readonly IReadOnlyList<string> MemorablePassphraseWords =
    [
        "anchor", "apricot", "atlas", "bamboo", "beacon", "binary", "canyon", "cedar",
        "cipher", "cobalt", "copper", "coral", "delta", "ember", "falcon", "forest",
        "galaxy", "harbor", "hazel", "indigo", "jacket", "juniper", "lantern", "lunar",
        "marble", "meadow", "nebula", "onyx", "orchid", "pepper", "plasma", "prairie",
        "quartz", "raven", "river", "saffron", "signal", "silver", "summit", "timber",
        "topaz", "velvet", "violet", "walnut", "willow", "window", "yellow", "zenith",
    ];

    public string GenerateRandomPassword()
    {
        return GenerateFromAlphabet(RandomPasswordCharacters, 24);
    }

    public string GenerateMemorablePassphrase()
    {
        string[] words = new string[5];
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
