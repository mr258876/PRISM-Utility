namespace PRISM_Utility.Core.Services;

public sealed class ScanOutputFileTransaction : IDisposable
{
    private bool _published;
    private string? _backupPath;
    private readonly Action? _beforeTemporaryMove;
    private readonly Action? _beforeBackupDelete;

    public ScanOutputFileTransaction(string targetPath, Action? beforeTemporaryMove = null, Action? beforeBackupDelete = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        TargetPath = targetPath;
        var directory = Path.GetDirectoryName(targetPath) ?? throw new ArgumentException("Output target must include a directory.", nameof(targetPath));
        var extension = Path.GetExtension(targetPath);
        TemporaryPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(targetPath)}.{Guid.NewGuid():N}.scan-temp{extension}");
        using var reservation = new FileStream(TemporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _beforeTemporaryMove = beforeTemporaryMove;
        _beforeBackupDelete = beforeBackupDelete;
    }

    public string TargetPath { get; }
    public string TemporaryPath { get; }

    public void Publish(CancellationToken cancellationToken, bool retainBackup = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var movedExistingTarget = false;
        try
        {
            if (File.Exists(TargetPath))
            {
                _backupPath = $"{TemporaryPath}.backup";
                File.Move(TargetPath, _backupPath, true);
                movedExistingTarget = true;
            }
            _beforeTemporaryMove?.Invoke();
            File.Move(TemporaryPath, TargetPath, true);
            _published = true;
        }
        catch
        {
            if (movedExistingTarget && _backupPath is not null && File.Exists(_backupPath))
                File.Move(_backupPath, TargetPath, true);
            throw;
        }

        if (!retainBackup)
        {
            try
            {
                DeleteBackup();
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    public void Rollback()
    {
        if (_published && File.Exists(TargetPath))
            File.Delete(TargetPath);
        if (_backupPath is not null && File.Exists(_backupPath))
            File.Move(_backupPath, TargetPath, true);
        _published = false;
    }

    public void Complete() => DeleteBackup();

    public void Dispose()
    {
        if (!_published && _backupPath is not null && File.Exists(_backupPath))
            File.Move(_backupPath, TargetPath, true);
        if (!_published && File.Exists(TemporaryPath))
            File.Delete(TemporaryPath);
        if (_published)
        {
            try
            {
                DeleteBackup();
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private void DeleteBackup()
    {
        if (_backupPath is not null && File.Exists(_backupPath))
        {
            _beforeBackupDelete?.Invoke();
            File.Delete(_backupPath);
        }
    }
}
