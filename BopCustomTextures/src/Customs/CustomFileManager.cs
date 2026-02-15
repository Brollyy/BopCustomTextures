using BopCustomTextures.Logging;
using System.Collections.Generic;
using System.IO;

namespace BopCustomTextures.Customs;

/// <summary>
/// Manages source files in custom mixtapes, including routines to load them and save them in future mixtapes.
/// </summary>
/// <param name="logger">Plugin-specific logger</param>
/// <param name="tempPath">Where to temporarily save source files in custom mixtape while custom mixtape is loaded</param>
public class CustomFileManager(ILogger logger, string tempPath) : BaseCustomManager(logger)
{
    public string tempPath = tempPath;
    public static FileStream tempLock = null;
    public readonly HashSet<string> referencedTopLevelDirectories = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    public bool WriteDirectory(string path)
    {
        if (tempLock != null)
        {
            var subpaths = Directory.EnumerateDirectories(tempPath);
            foreach (var subpath in subpaths)
            {
                string topLevelName = Path.GetFileName(subpath);
                if (referencedTopLevelDirectories.Contains(topLevelName))
                {
                    CopyDirectory(subpath, Path.Combine(path, subpath.Substring(tempPath.Length + 1)));
                }
            }
            return true;
        }
        return false;
    }

    public void SetReferencedTopLevelDirectories(
        CustomTexReferenceFrame frame,
        IEnumerable<string> defaultTexturePackPaths,
        IEnumerable<string> defaultScenePackPaths
    )
    {
        referencedTopLevelDirectories.Clear();

        foreach (var path in defaultTexturePackPaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }
        foreach (var path in defaultScenePackPaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }

        foreach (var path in frame.TexturePackPaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }
        foreach (var path in frame.ScenePackPaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }
        foreach (var path in frame.TextureOverridePaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }
        foreach (var path in frame.SceneOverridePaths)
        {
            AddTopLevelDirectoryReference(path, referencedTopLevelDirectories);
        }
    }

    public static void AddTopLevelDirectoryReference(string referencedPath, HashSet<string> referencedDirectories)
    {
        if (string.IsNullOrWhiteSpace(referencedPath))
        {
            return;
        }
        if (Path.IsPathRooted(referencedPath))
        {
            return;
        }

        string normalized = referencedPath.Replace('\\', '/').Trim();
        int slashIndex = normalized.IndexOf('/');
        if (slashIndex <= 0)
        {
            // A pack path can be a direct top-level directory like "tex_alt".
            // Ignore obvious file-like values in that case.
            if (!normalized.Contains("."))
            {
                referencedDirectories.Add(normalized);
            }
            return;
        }

        string topLevel = normalized.Substring(0, slashIndex);
        if (!string.IsNullOrWhiteSpace(topLevel))
        {
            referencedDirectories.Add(topLevel);
        }
    }

    public void CopyDirectory(string path, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            CopyDirectory(dir, Path.Combine(dest, dir.Substring(path.Length + 1)));
        }
        foreach (var file in Directory.EnumerateFiles(path))
        {
            File.Copy(file, Path.Combine(dest, file.Substring(path.Length + 1)));
        }
    }

    public void BackupDirectory(string path, string dest)
    {
        if (tempLock == null)
        {
            Directory.CreateDirectory(tempPath);
            tempLock = new FileStream(Path.Combine(tempPath, ".tmp"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        CopyDirectory(path, Path.Combine(tempPath, dest));
    }

    public void DeleteTempDirectory()
    {
        if (tempLock != null)
        {
            tempLock.Close();
            tempLock = null;
            Directory.Delete(tempPath, true);
        }
        referencedTopLevelDirectories.Clear();
    }

    public static void CleanUpTempDirectories(string tempParentPath)
    {
        if (!Directory.Exists(tempParentPath))
        {
            Directory.CreateDirectory(tempParentPath);
            return;
        }
        foreach (string otherTempPath in Directory.EnumerateDirectories(tempParentPath))
        {
            try
            {
                // check temp directory isn't being used by other Bits & Bops instance.
                LockFileLocked(Path.Combine(otherTempPath, ".tmp"));
                Directory.Delete(otherTempPath, true);
            }
            catch { }
        }
    }

    public static void LockFileLocked(string path)
    {
        // attempt to create a file to see if a temp directory is being used by another Bits & Bops instance. 
        using var _ = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public static bool ShouldBackupDirectory()
    {
        return TempoSceneManager.GetActiveSceneKey() == SceneKey.MixtapeEditor;
    }
}
