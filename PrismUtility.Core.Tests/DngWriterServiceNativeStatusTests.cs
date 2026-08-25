using System.Runtime.InteropServices;

using PRISM_Utility.Core.Services;

using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "DNG001")]
public sealed class DngWriterServiceNativeStatusTests
{
    [Fact]
    public void WriteRawDng_ValidRequest_UsesInjectedNativeApi()
    {
        var nativeApi = new FakeDngNativeApi();
        var service = new DngWriterService(nativeApi);

        service.WriteRawDng(DngWriterServiceTestData.CreateRequest());

        Assert.Equal(1, nativeApi.WriteCallCount);
        Assert.Equal(0, nativeApi.ErrorMessageCallCount);
        Assert.Equal(2u, nativeApi.CapturedRequest.AbiVersion);
        Assert.Equal((uint)Marshal.SizeOf<NativePrismDngWriteRequestV2>(), nativeApi.CapturedRequest.StructSize);
    }

    [Theory]
    [InlineData(1, DngNativeStatus.InvalidArgument)]
    [InlineData(2, DngNativeStatus.NotImplemented)]
    [InlineData(3, DngNativeStatus.UnsupportedFormat)]
    [InlineData(4, DngNativeStatus.IoError)]
    [InlineData(5, DngNativeStatus.InternalError)]
    [InlineData(99, DngNativeStatus.Unknown)]
    [Trait("Category", "NativeStatus")]
    public void WriteRawDng_NativeFailure_PreservesTypedStatus(int statusCode, DngNativeStatus expectedStatus)
    {
        var nativeApi = new FakeDngNativeApi { WriteStatus = statusCode, ErrorMessage = "native failure" };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(statusCode, exception.NativeStatusCode);
        Assert.Equal(expectedStatus, exception.NativeStatus);
        Assert.Equal(
            expectedStatus == DngNativeStatus.Unknown
                ? DngWriteFailureKind.UnknownNativeStatus
                : DngWriteFailureKind.NativeStatus,
            exception.FailureKind);
        Assert.Equal("native failure", exception.NativeErrorMessage);
        Assert.Equal(1, nativeApi.ErrorMessageCallCount);
    }

    [Theory]
    [InlineData("Request structSize is smaller than PrismDngWriteRequestV2.")]
    [InlineData("Unsupported DNG bridge ABI version.")]
    [Trait("Category", "NativeStatus")]
    public void WriteRawDng_ExactNativeAbiContractMessage_UsesAbiContractFailureKind(string nativeMessage)
    {
        var nativeApi = new FakeDngNativeApi { WriteStatus = 1, ErrorMessage = nativeMessage };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(DngWriteFailureKind.NativeAbiContractMismatch, exception.FailureKind);
        Assert.Equal(DngNativeStatus.InvalidArgument, exception.NativeStatus);
        Assert.Equal(1, exception.NativeStatusCode);
        Assert.Equal(nativeMessage, exception.NativeErrorMessage);
    }

    [Fact]
    [Trait("Category", "NativeStatus")]
    public void WriteRawDng_OtherInvalidArgumentMessage_RemainsNativeStatusFailure()
    {
        var nativeApi = new FakeDngNativeApi { WriteStatus = 1, ErrorMessage = "Output path must not be empty." };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(DngWriteFailureKind.NativeStatus, exception.FailureKind);
        Assert.Equal(DngNativeStatus.InvalidArgument, exception.NativeStatus);
        Assert.Equal(1, exception.NativeStatusCode);
        Assert.Equal("Output path must not be empty.", exception.NativeErrorMessage);
    }

    [Fact]
    public void WriteRawDng_NativeErrorMessageRetrievalReturnsFailure_PreservesPrimaryStatus()
    {
        var nativeApi = new FakeDngNativeApi
        {
            WriteStatus = 4,
            ErrorMessageStatus = 1
        };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(DngWriteFailureKind.NativeErrorRetrievalFailure, exception.FailureKind);
        Assert.Equal(DngNativeStatus.IoError, exception.NativeStatus);
        Assert.Equal(4, exception.NativeStatusCode);
        Assert.Equal(1, exception.ErrorRetrievalStatusCode);
        Assert.Null(exception.NativeErrorMessage);
    }

    [Fact]
    public void WriteRawDng_NativeErrorMessageRetrievalThrows_PreservesPrimaryStatus()
    {
        var nativeApi = new FakeDngNativeApi
        {
            WriteStatus = 5,
            ErrorMessageException = new InvalidOperationException("error retrieval failed")
        };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(DngWriteFailureKind.NativeErrorRetrievalFailure, exception.FailureKind);
        Assert.Equal(DngNativeStatus.InternalError, exception.NativeStatus);
        Assert.Equal(5, exception.NativeStatusCode);
        Assert.Null(exception.ErrorRetrievalStatusCode);
        Assert.Same(nativeApi.ErrorMessageException, exception.InnerException);
    }

    [Theory]
    [InlineData(typeof(DllNotFoundException), DngDeploymentFailureKind.MissingLibrary)]
    [InlineData(typeof(BadImageFormatException), DngDeploymentFailureKind.ArchitectureMismatch)]
    public void WriteRawDng_NativeWriteLoaderFailure_IsTyped(Type exceptionType, DngDeploymentFailureKind expectedFailure)
        => AssertNativeWriteDeploymentFailure(exceptionType, expectedFailure);

    [Fact]
    public void WriteRawDng_EntryPointNotFoundMeansMissingRequiredExportOrEntryPointContractMismatch_IsTyped()
        => AssertNativeWriteDeploymentFailure(typeof(EntryPointNotFoundException), DngDeploymentFailureKind.MissingExport);

    private static void AssertNativeWriteDeploymentFailure(Type exceptionType, DngDeploymentFailureKind expectedFailure)
    {
        var nativeApi = new FakeDngNativeApi
        {
            WriteException = DngWriterServiceTestData.CreateException(exceptionType)
        };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(expectedFailure, exception.DeploymentFailure);
        Assert.Equal(expectedFailure switch
        {
            DngDeploymentFailureKind.MissingLibrary => DngWriteFailureKind.MissingLibrary,
            DngDeploymentFailureKind.ArchitectureMismatch => DngWriteFailureKind.ArchitectureMismatch,
            DngDeploymentFailureKind.MissingExport => DngWriteFailureKind.MissingExport,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedFailure))
        }, exception.FailureKind);
        Assert.IsType(exceptionType, exception.InnerException);
        Assert.Null(exception.NativeStatusCode);
    }

    [Theory]
    [InlineData(typeof(DllNotFoundException), DngDeploymentFailureKind.MissingLibrary)]
    [InlineData(typeof(BadImageFormatException), DngDeploymentFailureKind.ArchitectureMismatch)]
    public void WriteRawDng_ErrorMessageLoaderFailure_IsTypedAndPreservesPrimaryStatus(Type exceptionType, DngDeploymentFailureKind expectedFailure)
        => AssertErrorMessageDeploymentFailure(exceptionType, expectedFailure);

    [Fact]
    public void WriteRawDng_ErrorMessageEntryPointNotFoundMeansMissingRequiredExportOrEntryPointContractMismatch_IsTypedAndPreservesPrimaryStatus()
        => AssertErrorMessageDeploymentFailure(typeof(EntryPointNotFoundException), DngDeploymentFailureKind.MissingExport);

    private static void AssertErrorMessageDeploymentFailure(Type exceptionType, DngDeploymentFailureKind expectedFailure)
    {
        var nativeApi = new FakeDngNativeApi
        {
            WriteStatus = 3,
            ErrorMessageException = DngWriterServiceTestData.CreateException(exceptionType)
        };
        var service = new DngWriterService(nativeApi);

        var exception = Assert.Throws<DngWriteException>(() => service.WriteRawDng(DngWriterServiceTestData.CreateRequest()));

        Assert.Equal(DngWriteFailureKind.NativeErrorRetrievalFailure, exception.FailureKind);
        Assert.Equal(expectedFailure, exception.DeploymentFailure);
        Assert.Equal(DngNativeStatus.UnsupportedFormat, exception.NativeStatus);
        Assert.Equal(3, exception.NativeStatusCode);
        Assert.IsType(exceptionType, exception.InnerException);
    }
}
