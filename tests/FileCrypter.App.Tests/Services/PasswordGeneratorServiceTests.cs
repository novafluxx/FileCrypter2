using FileCrypter.App.Services;

namespace FileCrypter.App.Tests.Services;

public sealed class PasswordGeneratorServiceTests
{
    [Fact]
    public void GenerateRandomPassword_ReturnsTwentyFourAllowedCharacters()
    {
        var generator = new PasswordGeneratorService();

        string password = generator.GenerateRandomPassword();

        Assert.Equal(24, password.Length);
        Assert.All(password, character => Assert.Contains(character, PasswordGeneratorService.RandomPasswordCharacters));
    }

    [Fact]
    public void GenerateMemorablePassphrase_ReturnsExpectedNumberOfHyphenSeparatedWords()
    {
        var generator = new PasswordGeneratorService();

        string passphrase = generator.GenerateMemorablePassphrase();

        string[] words = passphrase.Split('-');
        Assert.Equal(PasswordGeneratorService.MemorablePassphraseWordCount, words.Length);
        Assert.All(words, word => Assert.Contains(word, PasswordGeneratorService.MemorablePassphraseWords));
    }

    [Fact]
    public void GenerateMemorablePassphrase_ProvidesAtLeastSixtyBitsOfSearchSpace()
    {
        double searchSpaceBits = Math.Log2(PasswordGeneratorService.MemorablePassphraseWords.Count)
            * PasswordGeneratorService.MemorablePassphraseWordCount;

        Assert.True(searchSpaceBits >= 60);
    }

    [Fact]
    public void GenerateRandomPassword_ProducesVaryingValuesAcrossRepeatedCalls()
    {
        var generator = new PasswordGeneratorService();

        string[] passwords = Enumerable.Range(0, 12)
            .Select(_ => generator.GenerateRandomPassword())
            .ToArray();

        Assert.True(passwords.Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void GenerateMemorablePassphrase_ProducesVaryingValuesAcrossRepeatedCalls()
    {
        var generator = new PasswordGeneratorService();

        string[] passphrases = Enumerable.Range(0, 12)
            .Select(_ => generator.GenerateMemorablePassphrase())
            .ToArray();

        Assert.True(passphrases.Distinct(StringComparer.Ordinal).Count() > 1);
    }
}
