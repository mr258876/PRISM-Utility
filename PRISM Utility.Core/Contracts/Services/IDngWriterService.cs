using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IDngWriterService
{
    void WriteRawDng(DngWriteRequest request);
}
