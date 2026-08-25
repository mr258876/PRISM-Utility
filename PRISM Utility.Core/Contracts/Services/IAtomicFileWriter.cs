namespace PRISM_Utility.Core.Contracts.Services;

public interface IAtomicFileWriter
{
    void Write(string folderPath, string fileName, string content);
}
