namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Provides functionality to manage files and directories.
    /// </summary>
    public static class FileManager
    {
        /// <summary>
        ///     Enables/disables the directory redirection.
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redirect will be enabled or disabled.
        /// </param>
        /// <param name="dirMap">
        ///     The diretory map.
        ///     <para>
        ///         The key is used for the source directory, and the value is used for the
        ///         destination directory.
        ///     </para>
        /// </param>
        public static void DirRedirection(PortalizerActions option, Dictionary<string, string> dirMap)
        {
            if (dirMap?.Any() != true)
                return;
            var hash = nameof(DirRedirection).Encrypt();
            var hPath = PathEx.Combine(Attributes.TempDir, hash);
            switch (option)
            {
                case PortalizerActions.Disable:
                    FileEx.TryDelete(hPath);
                    break;
                default:
                    if (File.Exists(hPath))
                        DirRedirection(PortalizerActions.Disable, dirMap);
                    FileEx.Create(hPath);
                    break;
            }
            foreach (var data in dirMap)
            {
                if (string.IsNullOrWhiteSpace(data.Key) || string.IsNullOrWhiteSpace(data.Value))
                    continue;
                var srcPath = PathEx.Combine(data.Key);
                var destPath = PathEx.Combine(data.Value);
                DirectoryEx.SetAttributes(srcPath, FileAttributes.Normal);
                var backupPath = $"{srcPath}-{{{EnvironmentEx.MachineId}}}.backup";
                switch (option)
                {
                    case PortalizerActions.Disable:
                        DirectoryEx.SetAttributes(backupPath, FileAttributes.Normal);
                        if (DirectoryEx.DestroySymbolicLink(srcPath, true))
                            continue;
                        try
                        {
                            if (Directory.Exists(srcPath))
                            {
                                DirectoryEx.SetAttributes(srcPath, FileAttributes.Normal);
                                DirectoryEx.Copy(srcPath, destPath, true, true);
                                Directory.Delete(srcPath, true);
                            }
                            if (Directory.Exists(backupPath))
                            {
                                DirectoryEx.Copy(backupPath, srcPath, true, true);
                                Directory.Delete(backupPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        break;
                    default:
                        if (DirectoryEx.CreateSymbolicLink(srcPath, destPath, true))
                        {
                            DirectoryEx.SetAttributes(backupPath, FileAttributes.Normal);
                            continue;
                        }
                        try
                        {
                            if (Directory.Exists(srcPath))
                            {
                                if (!Directory.Exists(backupPath))
                                {
                                    DirectoryEx.Move(srcPath, backupPath);
                                    DirectoryEx.SetAttributes(backupPath, FileAttributes.Hidden);
                                }
                                if (Directory.Exists(srcPath))
                                    Directory.Delete(srcPath, true);
                            }
                            if (Directory.Exists(destPath))
                            {
                                DirectoryEx.SetAttributes(destPath, FileAttributes.Normal);
                                DirectoryEx.Copy(destPath, srcPath);
                            }
                            if (!Directory.Exists(srcPath))
                                Directory.CreateDirectory(srcPath);
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        break;
                }
            }
        }

        /// <summary>
        ///     Removes the specified directories.
        /// </summary>
        /// <param name="paths">
        ///     The directories to remove.
        /// </param>
        public static void RemoveDirs(params string[] paths)
        {
            foreach (var path in paths)
                try
                {
                    var dir = PathEx.Combine(default(char[]), path);
                    if (dir.Contains("*"))
                    {
                        var levels = dir.Split(Path.DirectorySeparatorChar);
                        var sublvls = levels.SkipWhile(x => !x.Contains('*')).ToArray();
                        var pattern = sublvls.First();

                        dir = PathEx.Combine(levels.TakeWhile(x => !x.Contains('*')).ToArray());
                        if (!Directory.Exists(dir))
                            continue;
                        var dirs = Directory.GetDirectories(dir, pattern, SearchOption.TopDirectoryOnly)
                                            .OrderBy(x => x, new Comparison.AlphanumericComparer())
                                            .Reverse().ToArray();
                        if (dirs.Length < 2)
                            continue;
                        var subDir = string.Empty;
                        if (sublvls.Length > 1)
                            subDir = PathEx.Combine(sublvls.Skip(1).ToArray());
                        if (!string.IsNullOrEmpty(subDir))
                            dirs[0] = PathEx.Combine(dirs.First(), subDir);
                        else
                            dirs = dirs.Skip(1).ToArray();
                        foreach (var item in dirs)
                        {
                            if (!Directory.Exists(item))
                                continue;
                            Directory.Delete(item, true);
                        }
                        continue;
                    }
                    if (!Directory.Exists(dir))
                        continue;
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
        }

        /// <summary>
        ///     Enables/disables the file redirection.
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redirect will be enabled or disabled.
        /// </param>
        /// <param name="fileMap">
        ///     The file map.
        ///     <para>
        ///         The key is used for the source file, and the value is used for the destination
        ///         file.
        ///     </para>
        /// </param>
        /// <param name="simple">
        ///     true to copy &amp; paste the specified files; otherwise, false to create symbolic links.
        /// </param>
        public static void FileRedirection(PortalizerActions option, Dictionary<string, string> fileMap, bool simple = false)
        {
            if (fileMap?.Any() != true)
                return;
            var hash = nameof(FileRedirection).Encrypt();
            var hPath = PathEx.Combine(Attributes.TempDir, hash);
            switch (option)
            {
                case PortalizerActions.Disable:
                    FileEx.TryDelete(hPath);
                    break;
                default:
                    if (File.Exists(hPath))
                        FileRedirection(PortalizerActions.Disable, fileMap, simple);
                    FileEx.Create(hPath);
                    break;
            }
            foreach (var data in fileMap)
            {
                if (string.IsNullOrWhiteSpace(data.Key) || string.IsNullOrWhiteSpace(data.Value))
                    continue;
                var srcPath = data.Key;
                string srcFile;
                try
                {
                    srcFile = Path.GetFileName(srcPath);
                    if (string.IsNullOrEmpty(srcFile))
                        throw new ArgumentNullException(nameof(srcFile));
                    if (!srcFile.Contains('*'))
                        srcPath = PathEx.Combine(srcPath);
                    else
                    {
                        var srcDir = PathEx.GetDirectoryName(srcPath);
                        if (string.IsNullOrEmpty(srcDir))
                            throw new ArgumentNullException(nameof(srcDir));
                        srcDir = PathEx.Combine(srcDir);
                        srcPath = string.Concat(srcDir, Path.DirectorySeparatorChar, srcFile);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(srcFile))
                    continue;
                var destDir = PathEx.Combine(data.Value);
                var destPath = PathEx.Combine(destDir, srcFile);
                bool doSimple;
                try
                {
                    if (srcPath.Contains('*'))
                        doSimple = true;
                    else
                    {
                        doSimple = simple;
                        if (!File.Exists(destPath))
                        {
                            if (string.IsNullOrEmpty(destDir))
                                throw new ArgumentNullException(nameof(destDir));
                            if (!Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);
                            File.Create(destPath).Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                    continue;
                }
                var backupPath = $"{srcPath}-{{{EnvironmentEx.MachineId}}}.backup";
                switch (option)
                {
                    case PortalizerActions.Disable:
                        if (doSimple)
                        {
                            try
                            {
                                if (srcPath.Contains('*'))
                                {
                                    var dir = PathEx.GetDirectoryName(srcPath);
                                    if (string.IsNullOrEmpty(dir))
                                        continue;
                                    var pattern = Path.GetFileName(srcPath);
                                    if (string.IsNullOrEmpty(pattern))
                                        continue;
                                    dir = PathEx.Combine(dir);
                                    foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                                    {
                                        var name = Path.GetFileName(file);
                                        if (string.IsNullOrEmpty(name))
                                            continue;
                                        var path = PathEx.Combine(destDir, name);
                                        if (File.Exists(path))
                                            File.Delete(path);
                                        File.Move(file, path);
                                    }
                                    continue;
                                }
                                srcPath = PathEx.Combine(srcPath);
                                if (File.Exists(srcPath))
                                {
                                    File.Copy(srcPath, destPath, true);
                                    if (File.Exists(destPath))
                                        File.Delete(srcPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Write(ex);
                            }
                            continue;
                        }
                        FileEx.SetAttributes(backupPath, FileAttributes.Normal);
                        if (Elevation.IsAdministrator && FileEx.DestroySymbolicLink(srcPath, true))
                            continue;
                        try
                        {
                            srcPath = PathEx.Combine(srcPath);
                            if (File.Exists(srcPath))
                            {
                                FileEx.SetAttributes(srcPath, FileAttributes.Normal);
                                File.Copy(srcPath, destPath, true);
                                File.Delete(srcPath);
                            }
                            if (File.Exists(backupPath))
                            {
                                File.Copy(backupPath, srcPath, true);
                                File.Delete(backupPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        break;
                    default:
                        if (doSimple)
                        {
                            try
                            {
                                if (srcPath.Contains('*'))
                                {
                                    var pattern = Path.GetFileName(srcPath);
                                    if (string.IsNullOrEmpty(pattern))
                                        continue;
                                    foreach (var file in Directory.EnumerateFiles(destDir, pattern, SearchOption.TopDirectoryOnly))
                                    {
                                        var dir = Path.GetDirectoryName(srcPath);
                                        if (string.IsNullOrEmpty(dir))
                                            continue;
                                        var name = Path.GetFileName(file);
                                        if (string.IsNullOrEmpty(name))
                                            continue;
                                        var path = PathEx.Combine(dir, name);
                                        File.Copy(file, path, true);
                                    }
                                    continue;
                                }
                                srcPath = PathEx.Combine(srcPath);
                                if (File.Exists(destPath) && (!File.Exists(srcPath) || File.GetLastWriteTime(destPath) > File.GetLastWriteTime(srcPath)))
                                    File.Copy(destPath, srcPath, true);
                            }
                            catch (Exception ex)
                            {
                                Log.Write(ex);
                            }
                            continue;
                        }
                        if (Elevation.IsAdministrator && FileEx.CreateSymbolicLink(srcPath, destPath, true))
                        {
                            FileEx.SetAttributes(backupPath, FileAttributes.Hidden);
                            continue;
                        }
                        try
                        {
                            srcPath = PathEx.Combine(srcPath);
                            if (File.Exists(srcPath))
                            {
                                if (!File.Exists(backupPath))
                                {
                                    File.Copy(srcPath, backupPath);
                                    FileEx.SetAttributes(backupPath, FileAttributes.Hidden);
                                }
                                File.Delete(srcPath);
                            }
                            if (!File.Exists(destPath))
                            {
                                var dir = Path.GetDirectoryName(destPath);
                                if (string.IsNullOrEmpty(dir))
                                    continue;
                                if (!Directory.Exists(dir))
                                    Directory.CreateDirectory(dir);
                                File.Create(destPath).Close();
                            }
                            FileEx.SetAttributes(backupPath, FileAttributes.Normal);
                            File.Copy(destPath, srcPath, true);
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        break;
                }
            }
        }

        /// <summary>
        ///     Enables/disables the file redirection using the system command prompt.
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redirect will be enabled or disabled.
        /// </param>
        /// <param name="fileMap">
        ///     The file map.
        ///     <para>
        ///         The key is used for the source file, and the value is used for the destination
        ///         file.
        ///     </para>
        /// </param>
        public static void FileSecureRedirection(PortalizerActions option, Dictionary<string, string> fileMap)
        {
            if (fileMap?.Any() != true)
                return;
            var hash = nameof(FileSecureRedirection).Encrypt();
            var hPath = PathEx.Combine(Attributes.TempDir, hash);
            switch (option)
            {
                case PortalizerActions.Disable:
                    FileEx.TryDelete(hPath);
                    break;
                default:
                    if (File.Exists(hPath))
                        FileSecureRedirection(PortalizerActions.Disable, fileMap);
                    FileEx.Create(hPath);
                    break;
            }
            foreach (var data in fileMap)
            {
                if (string.IsNullOrWhiteSpace(data.Key) || string.IsNullOrWhiteSpace(data.Value))
                    continue;
                var srcPath = PathEx.Combine(data.Key);
                var destPath = PathEx.Combine(data.Value);
                if (!File.Exists(srcPath) || !PathEx.IsValidPath(srcPath) || !PathEx.IsValidPath(destPath))
                    continue;
                switch (option)
                {
                    case PortalizerActions.Disable:
                        ProcessEx.SendHelper.Delete(destPath, Elevation.IsAdministrator);
                        break;
                    default:
                        ProcessEx.SendHelper.Copy(srcPath, destPath, Elevation.IsAdministrator);
                        break;
                }
            }
        }
    }
}
