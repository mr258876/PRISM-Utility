namespace PRISM_Utility.Core.Services;

public enum DngNativeStatus
{
    Unknown = -1,
    Ok = 0,
    InvalidArgument = 1,
    NotImplemented = 2,
    UnsupportedFormat = 3,
    IoError = 4,
    InternalError = 5
}

public enum DngWriteFailureKind
{
    NativeStatus,
    NativeAbiContractMismatch,
    UnknownNativeStatus,
    MissingLibrary,
    ArchitectureMismatch,
    MissingExport,
    NativeErrorRetrievalFailure
}

public enum DngDeploymentFailureKind
{
    MissingLibrary,
    ArchitectureMismatch,
    MissingExport
}

public sealed class DngWriteException : InvalidOperationException
{
    private DngWriteException(
        string message,
        DngWriteFailureKind failureKind,
        DngNativeStatus? nativeStatus,
        int? nativeStatusCode,
        string? nativeErrorMessage,
        DngDeploymentFailureKind? deploymentFailure,
        int? errorRetrievalStatusCode,
        Exception? innerException)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        NativeStatus = nativeStatus;
        NativeStatusCode = nativeStatusCode;
        NativeErrorMessage = nativeErrorMessage;
        DeploymentFailure = deploymentFailure;
        ErrorRetrievalStatusCode = errorRetrievalStatusCode;
    }

    public DngWriteFailureKind FailureKind { get; }

    public DngNativeStatus? NativeStatus { get; }

    public DngNativeStatus? Status => NativeStatus;

    public int? NativeStatusCode { get; }

    public int? StatusCode => NativeStatusCode;

    public string? NativeErrorMessage { get; }

    public DngDeploymentFailureKind? DeploymentFailure { get; }

    public DngDeploymentFailureKind? DeploymentFailureKind => DeploymentFailure;

    public int? ErrorRetrievalStatusCode { get; }

    internal static DngWriteException ForNativeStatus(int statusCode, string? nativeErrorMessage)
    {
        var nativeStatus = MapStatus(statusCode);
        var failureKind = ResolveNativeFailureKind(statusCode, nativeStatus, nativeErrorMessage);
        var message = string.IsNullOrWhiteSpace(nativeErrorMessage)
            ? $"Native DNG writer failed with status code {statusCode}."
            : $"Native DNG writer failed with status code {statusCode}: {nativeErrorMessage}";

        return new DngWriteException(
            message,
            failureKind,
            nativeStatus,
            statusCode,
            string.IsNullOrWhiteSpace(nativeErrorMessage) ? null : nativeErrorMessage,
            null,
            null,
            null);
    }

    internal static DngWriteException ForDeployment(DngDeploymentFailureKind deploymentFailure, Exception innerException)
    {
        var (failureKind, message) = deploymentFailure switch
        {
            DngDeploymentFailureKind.MissingLibrary =>
                (DngWriteFailureKind.MissingLibrary, $"Native DNG writer library '{DngNativeConstants.NativeLibraryName}' was not found next to the application output."),
            DngDeploymentFailureKind.ArchitectureMismatch =>
                (DngWriteFailureKind.ArchitectureMismatch, $"Native DNG writer library '{DngNativeConstants.NativeLibraryName}' has an architecture mismatch with the current process."),
            DngDeploymentFailureKind.MissingExport =>
                (DngWriteFailureKind.MissingExport, $"Native DNG writer library '{DngNativeConstants.NativeLibraryName}' does not expose the required DNG bridge export."),
            _ => throw new ArgumentOutOfRangeException(nameof(deploymentFailure), deploymentFailure, null)
        };

        return new DngWriteException(
            message,
            failureKind,
            null,
            null,
            null,
            deploymentFailure,
            null,
            innerException);
    }

    internal static DngWriteException ForErrorMessageRetrievalFailure(
        int primaryStatusCode,
        int? retrievalStatusCode,
        DngDeploymentFailureKind? deploymentFailure,
        Exception? innerException = null)
    {
        var primaryStatus = MapStatus(primaryStatusCode);
        var retrievalDescription = retrievalStatusCode is null
            ? deploymentFailure is null
                ? "the native error message could not be retrieved"
                : $"the native error message could not be retrieved because of a {deploymentFailure} deployment failure"
            : $"the native error message function returned status code {retrievalStatusCode.Value}";
        var message = $"Native DNG writer failed with status code {primaryStatusCode}, but {retrievalDescription}.";

        return new DngWriteException(
            message,
            DngWriteFailureKind.NativeErrorRetrievalFailure,
            primaryStatus,
            primaryStatusCode,
            null,
            deploymentFailure,
            retrievalStatusCode,
            innerException);
    }

    private static DngNativeStatus MapStatus(int statusCode)
        => statusCode switch
        {
            0 => DngNativeStatus.Ok,
            1 => DngNativeStatus.InvalidArgument,
            2 => DngNativeStatus.NotImplemented,
            3 => DngNativeStatus.UnsupportedFormat,
            4 => DngNativeStatus.IoError,
            5 => DngNativeStatus.InternalError,
            _ => DngNativeStatus.Unknown
        };

    private static DngWriteFailureKind ResolveNativeFailureKind(
        int statusCode,
        DngNativeStatus nativeStatus,
        string? nativeErrorMessage)
    {
        if (nativeStatus == DngNativeStatus.Unknown)
            return DngWriteFailureKind.UnknownNativeStatus;

        if (statusCode == 1 && nativeErrorMessage is
            "Request structSize is smaller than PrismDngWriteRequestV2."
            or "Unsupported DNG bridge ABI version.")
        {
            return DngWriteFailureKind.NativeAbiContractMismatch;
        }

        return DngWriteFailureKind.NativeStatus;
    }
}
