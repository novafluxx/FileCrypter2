namespace FileCrypter.Core;

internal interface IFileCrypterRandomSource
{
    void Fill(Span<byte> destination);
}
