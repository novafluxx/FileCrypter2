using FileCrypter.Core;

namespace FileCrypter.Desktop.Services;

public sealed record InspectFileResult(
    FileCrypterPayloadKind PayloadKind,
    bool IsKeyFileRequired,
    bool IsCompressed,
    int FormatVersion);
