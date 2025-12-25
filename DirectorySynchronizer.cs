using System.Security.Cryptography;

namespace csharpcopy;

public class DirectorySynchronizer(string sourcePath, string replicaPath, Logger logger)
{
    private readonly string _sourcePath = Path.GetFullPath(sourcePath);
    private readonly string _replicaPath = Path.GetFullPath(replicaPath);
    private readonly Logger _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public void SynchronizeAll()
    {
        if (!_syncLock.Wait(0))
        {
            _logger.Log("Synchronization skipped: previous synchronization still in progress");
            return;
        }

        try
        {
            if (string.Equals(_sourcePath, _replicaPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log($"ERROR: Source and replica directories cannot be the same: {_sourcePath}");
                return;
            }

            if (_replicaPath.StartsWith(_sourcePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                _replicaPath.StartsWith(_sourcePath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log($"ERROR: Replica directory cannot be a subdirectory of source directory");
                return;
            }

            if (!Directory.Exists(_sourcePath))
            {
                _logger.Log($"ERROR: Source directory does not exist: {_sourcePath}");
                return;
            }

            if (!Directory.Exists(_replicaPath))
            {
                try
                {
                    _logger.Log($"Creating replica directory: {_replicaPath}");
                    Directory.CreateDirectory(_replicaPath);
                }
                catch (Exception ex)
                {
                    _logger.Log($"ERROR: Failed to create replica directory {_replicaPath}: {ex.Message}");
                    return;
                }
            }

            _logger.Log($"Starting synchronization from {_sourcePath} to {_replicaPath}");

            SynchronizeDir(_sourcePath, _replicaPath);

            RemoveExtraFiles(_sourcePath, _replicaPath);

            _logger.Log("Synchronization completed");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void SynchronizeDir(string sourceDir, string replicaDir)
    {
        if (!Directory.Exists(replicaDir))
        {
            try
            {
                Directory.CreateDirectory(replicaDir);
                _logger.Log($"Created directory: {replicaDir}");
            }
            catch (Exception ex)
            {
                _logger.Log($"ERROR: Failed to create directory {replicaDir}: {ex.Message}");
                return;
            }
        }

        var sourceFiles = Directory.GetFiles(sourceDir);

        foreach (var sourceFile in sourceFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var replicaFile = Path.Combine(replicaDir, fileName);

            bool needsCopy = false;

            if (!File.Exists(replicaFile))
            {
                needsCopy = true;
                _logger.Log($"New file detected: {fileName}");
            }
            else
            {
                if (FilesAreDifferent(sourceFile, replicaFile))
                {
                    needsCopy = true;
                    _logger.Log($"File modified: {fileName}");
                }
            }

            if (needsCopy)
            {
                try
                {
                    File.Copy(sourceFile, replicaFile, overwrite: true);
                    _logger.Log($"Copied: {fileName}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"ERROR: Failed to copy file {fileName}: {ex.Message}");
                }
            }
        }

        var sourceSubdirs = Directory.GetDirectories(sourceDir);
        foreach (var sourceSubdir in sourceSubdirs)
        {
            var dirName = Path.GetFileName(sourceSubdir);
            var replicaSubdir = Path.Combine(replicaDir, dirName);
            SynchronizeDir(sourceSubdir, replicaSubdir);
        }
    }

    private void RemoveExtraFiles(string sourceDir, string replicaDir)
    {
        if (!Directory.Exists(replicaDir))
        {
            return;
        }

        if (!Directory.Exists(sourceDir))
        {
            try
            {
                Directory.Delete(replicaDir, recursive: true);
                _logger.Log($"Deleted directory: {Path.GetFileName(replicaDir)}");
            }
            catch (Exception ex)
            {
                _logger.Log($"ERROR: Failed to delete directory {Path.GetFileName(replicaDir)}: {ex.Message}");
            }
            return;
        }

        var sourceFiles = Directory.GetFiles(sourceDir).Select(Path.GetFileName).ToHashSet();
        var replicaFiles = Directory.GetFiles(replicaDir);

        foreach (var replicaFile in replicaFiles)
        {
            var fileName = Path.GetFileName(replicaFile);
            if (!sourceFiles.Contains(fileName))
            {
                try
                {
                    File.Delete(replicaFile);
                    _logger.Log($"Deleted file: {fileName}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"ERROR: Failed to delete file {fileName}: {ex.Message}");
                }
            }
        }

        var sourceSubdirs = Directory.GetDirectories(sourceDir).Select(Path.GetFileName).ToHashSet();
        var replicaSubdirs = Directory.GetDirectories(replicaDir).ToList();

        foreach (var replicaSubdir in replicaSubdirs)
        {
            var dirName = Path.GetFileName(replicaSubdir);
            if (!sourceSubdirs.Contains(dirName))
            {
                try
                {
                    Directory.Delete(replicaSubdir, recursive: true);
                    _logger.Log($"Deleted directory: {dirName}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"ERROR: Failed to delete directory {dirName}: {ex.Message}");
                }
            }
            else
            {
                var sourceSubdir = Path.Combine(sourceDir, dirName);
                RemoveExtraFiles(sourceSubdir, replicaSubdir); // Recursive call.
            }
        }
    }

    private bool FilesAreDifferent(string file1, string file2)
    {
        try
        {
            var fileInfo1 = new FileInfo(file1);
            var fileInfo2 = new FileInfo(file2);

            if (fileInfo1.Length != fileInfo2.Length)
            {
                return true;
            }

            var hash1 = CalculateFileHash(file1);
            var hash2 = CalculateFileHash(file2);
            return !hash1.SequenceEqual(hash2);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error comparing files {file1} and {file2}: {ex.Message}");
            return true;
        }
    }

    private static byte[] CalculateFileHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream);
    }
}

