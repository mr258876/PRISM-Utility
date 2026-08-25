using System.Text;
using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Core.Services;

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    public void Write(string folderPath, string fileName, string content)
    {
        Directory.CreateDirectory(folderPath);

        var targetPath = Path.Combine(folderPath, fileName);
        var tempPath = Path.Combine(folderPath, $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            WriteAndFlush(tempPath, content);

            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, targetPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void WriteAndFlush(string tempPath, string content)
    {
        using (var stream = new FileStream(
                   tempPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   bufferSize: 4096,
                   FileOptions.WriteThrough))
        {
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
            }

            stream.Flush(flushToDisk: true);
        }
    }
}
