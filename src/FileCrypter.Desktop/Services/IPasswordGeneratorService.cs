namespace FileCrypter.App.Services;

public interface IPasswordGeneratorService
{
    string GenerateRandomPassword();

    string GenerateMemorablePassphrase();
}
