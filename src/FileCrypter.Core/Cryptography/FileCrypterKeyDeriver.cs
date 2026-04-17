using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FileCrypter.Core.Format;
using Konscious.Security.Cryptography;

namespace FileCrypter.Core.Cryptography;

internal static class FileCrypterKeyDeriver
{
    private static readonly byte[] CredentialDomain = "FileCrypter.Argon2id.Credential.v1"u8.ToArray();

    public static byte[] DerivePasswordOnlyKey(string password, FileCrypterHeader header)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] preimage = CreateCredentialPreimage(passwordBytes);

        try
        {
            using var argon2 = new Argon2id(preimage)
            {
                Salt = header.Salt.ToArray(),
                MemorySize = checked((int)header.Argon2MemoryKiB),
                Iterations = checked((int)header.Argon2Iterations),
                DegreeOfParallelism = checked((int)header.Argon2Parallelism),
            };

            return argon2.GetBytes(FileCrypterFormatConstants.DerivedKeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(preimage);
        }
    }

    private static byte[] CreateCredentialPreimage(ReadOnlySpan<byte> passwordBytes)
    {
        checked
        {
            int preimageLength =
                sizeof(uint) + CredentialDomain.Length +
                sizeof(uint) + passwordBytes.Length +
                sizeof(uint);

            byte[] preimage = new byte[preimageLength];
            Span<byte> destination = preimage;

            WriteLengthPrefixed(CredentialDomain, ref destination);
            WriteLengthPrefixed(passwordBytes, ref destination);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, sizeof(uint)), 0);

            return preimage;
        }
    }

    private static void WriteLengthPrefixed(ReadOnlySpan<byte> value, ref Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, sizeof(uint)), (uint)value.Length);
        destination = destination.Slice(sizeof(uint));
        value.CopyTo(destination);
        destination = destination.Slice(value.Length);
    }
}
