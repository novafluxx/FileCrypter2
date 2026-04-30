namespace FileCrypter.Desktop.Services;

public interface IPasswordGeneratorService
{
    string GenerateRandomPassword();

    string GenerateMemorablePassphrase();
}
