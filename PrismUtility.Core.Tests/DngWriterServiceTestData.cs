using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;

namespace PrismUtility.Core.Tests;

internal static class DngWriterServiceTestData
{
    internal static DngWriteRequest CreateRequest(uint width = 2, uint height = 2)
        => new(
            OutputPath: "output.dng",
            PixelData: new byte[checked((int)(width * height))],
            Width: width,
            Height: height,
            RowStrideBytes: width,
            BitsPerSample: 8,
            SamplesPerPixel: 1,
            PixelLayout: DngPixelLayout.RawMosaic,
            CfaPattern: DngCfaPattern.Rggb);

    internal static Exception CreateException(Type exceptionType)
        => Activator.CreateInstance(exceptionType, "native deployment failure") as Exception
            ?? throw new InvalidOperationException($"Unable to create {exceptionType.Name}.");
}

internal sealed class FakeDngNativeApi : IDngNativeApi
{
    public int WriteStatus { get; init; }

    public int ErrorMessageStatus { get; init; }

    public string? ErrorMessage { get; init; }

    public Exception? WriteException { get; init; }

    public Exception? ErrorMessageException { get; init; }

    public int WriteCallCount { get; private set; }

    public int ErrorMessageCallCount { get; private set; }

    public NativePrismDngWriteRequestV2 CapturedRequest { get; private set; }

    public int WriteFromBuffer(ref NativePrismDngWriteRequestV2 request)
    {
        WriteCallCount++;
        CapturedRequest = request;
        if (WriteException is not null)
            throw WriteException;

        return WriteStatus;
    }

    public int GetLastErrorMessage(char[] buffer, uint bufferChars)
    {
        ErrorMessageCallCount++;
        if (ErrorMessageException is not null)
            throw ErrorMessageException;

        if (ErrorMessageStatus != 0)
            return ErrorMessageStatus;

        if (ErrorMessage is not null)
            ErrorMessage.AsSpan().CopyTo(buffer);

        return 0;
    }
}
