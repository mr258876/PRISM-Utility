using System.Runtime.InteropServices;

using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class DngWriterService : IDngWriterService
{
    private readonly IDngNativeApi _nativeApi;

    public DngWriterService()
        : this(new PrismDngNativeApi())
    {
    }

    internal DngWriterService(IDngNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    public void WriteRawDng(DngWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        ArgumentNullException.ThrowIfNull(request.PixelData);

        DngWriteRequestValidator.Validate(request);

        var pixelHandle = GCHandle.Alloc(request.PixelData, GCHandleType.Pinned);
        try
        {
            var nativeRequest = DngNativeRequestBuilder.Build(request, pixelHandle.AddrOfPinnedObject());
            int status;
            try
            {
                status = _nativeApi.WriteFromBuffer(ref nativeRequest);
            }
            catch (DllNotFoundException exception)
            {
                throw DngWriteException.ForDeployment(DngDeploymentFailureKind.MissingLibrary, exception);
            }
            catch (BadImageFormatException exception)
            {
                throw DngWriteException.ForDeployment(DngDeploymentFailureKind.ArchitectureMismatch, exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw DngWriteException.ForDeployment(DngDeploymentFailureKind.MissingExport, exception);
            }

            if (status != DngNativeConstants.StatusOk)
                throw BuildNativeFailure(status);
        }
        finally
        {
            if (pixelHandle.IsAllocated)
                pixelHandle.Free();
        }
    }

    private DngWriteException BuildNativeFailure(int status)
    {
        var buffer = new char[DngNativeConstants.ErrorMessageBufferChars];
        try
        {
            var retrievalStatus = _nativeApi.GetLastErrorMessage(buffer, (uint)buffer.Length);
            if (retrievalStatus != DngNativeConstants.StatusOk)
            {
                return DngWriteException.ForErrorMessageRetrievalFailure(status, retrievalStatus, null);
            }

            var terminatorIndex = Array.IndexOf(buffer, '\0');
            if (terminatorIndex < 0)
                terminatorIndex = buffer.Length;

            var nativeMessage = new string(buffer, 0, terminatorIndex);
            return DngWriteException.ForNativeStatus(status, nativeMessage);
        }
        catch (DllNotFoundException exception)
        {
            return DngWriteException.ForErrorMessageRetrievalFailure(
                status,
                null,
                DngDeploymentFailureKind.MissingLibrary,
                exception);
        }
        catch (BadImageFormatException exception)
        {
            return DngWriteException.ForErrorMessageRetrievalFailure(
                status,
                null,
                DngDeploymentFailureKind.ArchitectureMismatch,
                exception);
        }
        catch (EntryPointNotFoundException exception)
        {
            return DngWriteException.ForErrorMessageRetrievalFailure(
                status,
                null,
                DngDeploymentFailureKind.MissingExport,
                exception);
        }
        catch (Exception exception)
        {
            return DngWriteException.ForErrorMessageRetrievalFailure(status, null, null, exception);
        }
    }
}
