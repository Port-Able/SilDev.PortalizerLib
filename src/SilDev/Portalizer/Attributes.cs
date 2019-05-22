namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Provides functionality for managing app attributes.
    /// </summary>
    public static class Attributes
    {
        private static string _appDir,
                              _appPath,
                              _configPath,
                              _dataDir,
                              _defIni,
                              _defReg,
                              _defSettingsDir,
                              _defSettingsDir32,
                              _defSettingsDir64,
                              _forceReg,
                              _instanceHashPath,
                              _regFilePath,
                              _regPath,
                              _rmReg,
                              _settingsDir,
                              _tempDir,
                              _updaterPath;

        private static string[] _appWaitDirs,
                                _ignoredProcesses,
                                _regKeys,
                                _rmDirs,
                                _rmRegKeys;

        private static int? _appWaitFull;

        private static Dictionary<string, string> _config,
                                                  _dirMap,
                                                  _fileMap,
                                                  _fileSecureMap;

        private static bool? _elevateFirst,
                             _fileMapSimple;

        private static Dictionary<string, string> Config
        {
            get
            {
                if (_config == default(Dictionary<string, string>))
                    _config = new Dictionary<string, string>();
                return _config;
            }
            set => _config = value;
        }

        /// <summary>
        ///     Gets the app directory.
        /// </summary>
        public static string AppDir
        {
            get
            {
                if (_appDir == default(string))
#if x86
                    _appDir = PathEx.Combine(GetValue(nameof(AppDir)));
#else
                    _appDir = PathEx.Combine(GetValue(nameof(AppDir) + 64));
#endif
                return _appDir;
            }
            set => _appDir = value;
        }

        /// <summary>
        ///     Gets or sets the app path.
        /// </summary>
        public static string AppPath
        {
            get
            {
                if (_appPath != default(string))
                    return _appPath;
#if x86
                _appPath = GetValue(nameof(AppPath));
#else
                _appPath = GetValue(nameof(AppPath) + 64);
#endif
                if (!_appPath.ContainsEx('*', '?'))
                    _appPath = PathEx.Combine(_appPath);
                else
                    try
                    {
                        var dir = Path.GetDirectoryName(_appPath);
                        if (string.IsNullOrEmpty(dir))
                            throw new ArgumentNullException(nameof(dir));
                        dir = PathEx.Combine(dir);
                        if (!PathEx.IsValidPath(dir))
                            throw new NotSupportedException();
                        var name = Path.GetFileName(_appPath);
                        if (string.IsNullOrEmpty(name))
                            throw new ArgumentNullException(nameof(name));
                        if (!name.ContainsEx('*', '?'))
                            throw new NotSupportedException();
                        while (name.Contains("**"))
                            name = name.Replace("**", "*");
                        var paths = Directory.GetFiles(dir, name, SearchOption.TopDirectoryOnly);
                        if (paths.Length == 0)
                            throw new ArgumentOutOfRangeException(nameof(paths));
                        var sorted = paths.OrderBy(x => x, new Comparison.AlphanumericComparer());
                        var path = sorted.LastOrDefault();
                        if (string.IsNullOrEmpty(path))
                            throw new ArgumentNullException(nameof(path));
                        if (!PathEx.IsValidPath(path))
                            throw new NotSupportedException();
                        _appPath = path;
                    }
                    catch (Exception ex)
                    {
                        Log.Write(ex);
                    }
                if (!_appPath.EndsWithEx(".jar"))
                    return _appPath;
                JavaHandler.Find(out var javaPath);
                if (!File.Exists(javaPath))
                    return _appPath;
                ConfigManager.IniFormat.Launcher.StartArguments = $"{JavaHandler.StartParameter} \"{_appPath}\" {ConfigManager.IniFormat.Launcher.StartArguments}".Trim();
                _appPath = javaPath;
                return _appPath;
            }
            set => _appPath = value;
        }

        /// <summary>
        ///     Gets or sets extended directories containing executables whose running
        ///     processes are to be maintained.
        /// </summary>
        public static string[] AppWaitDirs
        {
            get
            {
                if (_appWaitDirs == default(string[]))
                    _appWaitDirs = GetValue(nameof(AppWaitDirs))?.SplitNewLine();
                return _appWaitDirs;
            }
            set => _appWaitDirs = value;
        }

        /// <summary>
        ///     Determines how to search and wait for running processes.
        /// </summary>
        public static int AppWaitFull
        {
            get
            {
                if (_appWaitFull != default(int?))
                    return (int)_appWaitFull;
                var str = GetValue(nameof(AppWaitFull))?.ToLower();
                switch (str)
                {
                    case "null":
                        _appWaitFull = -1;
                        break;
                    case "true":
                        _appWaitFull = 1;
                        break;
                    case "extended":
                        _appWaitFull = 2;
                        break;
                    default:
                        _appWaitFull = 0;
                        break;
                }
                return (int)_appWaitFull;
            }
        }

        /// <summary>
        ///     Gets the launcher config path.
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                if (_configPath == default(string))
                    _configPath = Path.ChangeExtension(PathEx.LocalPath, ".ini");
                return _configPath;
            }
        }

        /// <summary>
        ///     Gets a unique hash code for the current instance.
        /// </summary>
        public static string CurrentHash { get; } = $"{{{Math.Abs(PathEx.LocalPath.GetHashCode())}}}";

        /// <summary>
        ///     Gets the data directory.
        /// </summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir != default(string))
                    return _dataDir;
                _dataDir = GetValue(nameof(DataDir));
                if (string.IsNullOrWhiteSpace(_dataDir))
                    _dataDir = "%CurDir%\\Data";
                _dataDir = PathEx.Combine(_dataDir);
                return _dataDir;
            }
        }

        /// <summary>
        ///     Gets the content of the default launcher config.
        /// </summary>
        public static string DefIni
        {
            get
            {
                if (_defIni == default(string))
                    _defIni = TextEx.FormatNewLine(GetValue(nameof(DefIni)));
                return _defIni;
            }
        }

        /// <summary>
        ///     Gets the content of the default registry settings.
        /// </summary>
        public static string DefReg
        {
            get
            {
                if (_defReg != default(string))
                    return _defReg;
                try
                {
                    _defReg = string.Concat(GetValue(nameof(DefReg)), Environment.NewLine,
#if x86
                                            GetValue(nameof(DefReg) + 32)
#else
                                            GetValue(nameof(DefReg) + 64)
#endif
                        ).Trim();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _defReg;
            }
        }

        /// <summary>
        ///     Gets the directory that contains the default settings for 32-bit only.
        /// </summary>
        public static string DefSettingsDir32
        {
            get
            {
                if (_defSettingsDir32 == default(string))
                    _defSettingsDir32 = DefSettingsDir + 32;
                return _defSettingsDir32;
            }
        }

        /// <summary>
        ///     Gets the directory that contains the default settings for 64-bit only.
        /// </summary>
        public static string DefSettingsDir64
        {
            get
            {
                if (_defSettingsDir64 == default(string))
                    _defSettingsDir64 = DefSettingsDir + 64;
                return _defSettingsDir64;
            }
        }

        /// <summary>
        ///     Gets the directory redirect map.
        /// </summary>
        public static Dictionary<string, string> DirMap
        {
            get
            {
                if (_dirMap != default(Dictionary<string, string>))
                    return _dirMap;

                _dirMap = new Dictionary<string, string>();
                foreach (var dirMaps in new[]
                {
                    GetValue(nameof(DirMap) + "App"),
#if x86
                    GetValue(nameof(DirMap) + "App32")
#else
                    GetValue(nameof(DirMap) + "App64")
#endif
                })
                    foreach (var destDir in dirMaps.SplitNewLine(true))
                    {
                        if (string.IsNullOrEmpty(destDir) || _dirMap.ContainsKey(destDir))
                            continue;
#if x86
                        var srcDir = PathEx.Combine(PathEx.LocalDir, "App", "_Environment", destDir.RemoveChar('%'));
#else
                        var srcDir = PathEx.Combine(PathEx.LocalDir, "App", "_Environment64", destDir.RemoveChar('%'));
                        if (!Directory.Exists(srcDir))
                            srcDir = PathEx.Combine(PathEx.LocalDir, "App", "_Environment", destDir.RemoveChar('%'));
#endif
                        _dirMap.Add(destDir, srcDir);
                    }

                foreach (var dirMaps in new[]
                {
                    GetValue(nameof(DirMap)),
#if x86
                    GetValue(nameof(DirMap) + 32)
#else
                    GetValue(nameof(DirMap) + 64)
#endif
                })
                    foreach (var destDir in dirMaps.SplitNewLine(true))
                    {
                        if (string.IsNullOrEmpty(destDir) || _dirMap.ContainsKey(destDir) || !ContainsEnvVar(destDir))
                            continue;
                        var srcDir = GetEnvironmentPath(destDir);
                        _dirMap.Add(destDir, srcDir);
                    }

                return _dirMap;
            }
        }

        /// <summary>
        ///     Gets or sets the option that determines that the first launcher must be elevated.
        /// </summary>
        public static bool ElevateFirst
        {
            get
            {
                if (_elevateFirst == default(bool?))
                    _elevateFirst = GetValue(nameof(ElevateFirst)).EqualsEx("True");
                return _elevateFirst == true;
            }
            set => _elevateFirst = value;
        }

        /// <summary>
        ///     Gets the file redirect map.
        /// </summary>
        public static Dictionary<string, string> FileMap
        {
            get
            {
                if (_fileMap != default(Dictionary<string, string>))
                    return _fileMap;

                _fileMap = new Dictionary<string, string>();
                foreach (var fileMaps in new[]
                {
                    GetValue(nameof(FileMap)),
#if x86
                    GetValue(nameof(FileMap) + 32)
#else
                    GetValue(nameof(FileMap) + 64)
#endif
                })
                    foreach (var destFile in fileMaps.SplitNewLine(true))
                    {
                        if (string.IsNullOrEmpty(destFile) || !ContainsEnvVar(destFile))
                            continue;
                        var destDir = Path.GetDirectoryName(destFile);
                        var srcDir = GetEnvironmentPath(destDir);
                        _fileMap.Add(destFile, srcDir);
                    }

                return _fileMap;
            }
        }

        /// <summary>
        ///     Gets the option that determines how the file map redirect works.
        /// </summary>
        public static bool FileMapSimple
        {
            get
            {
                if (_fileMapSimple == default(bool?))
                    _fileMapSimple = GetValue(nameof(FileMapSimple)).EqualsEx("True");
                return _fileMapSimple == true;
            }
        }

        /// <summary>
        ///     Gets the file secure redirect map.
        /// </summary>
        public static Dictionary<string, string> FileSecureMap
        {
            get
            {
                if (_fileSecureMap != default(Dictionary<string, string>))
                    return _fileSecureMap;

                _fileSecureMap = new Dictionary<string, string>();
                foreach (var fileMaps in new[]
                {
                    GetValue(nameof(FileSecureMap)),
#if x86
                    GetValue(nameof(FileSecureMap) + 32)
#else
                    GetValue(nameof(FileSecureMap) + 64)
#endif
                })
                    foreach (var filePair in fileMaps.SplitNewLine(true))
                        try
                        {
                            if (string.IsNullOrEmpty(filePair) || !filePair.Contains('>') || filePair.Count(x => x == '>') > 1)
                                continue;
                            var filePaths = filePair.Split('>');
                            var srcFile = filePaths.FirstOrDefault()?.Trim();
                            if (string.IsNullOrEmpty(srcFile) || !ContainsEnvVar(srcFile))
                                continue;
                            var destFile = filePaths.LastOrDefault()?.Trim();
                            if (string.IsNullOrEmpty(destFile) || srcFile.EqualsEx(destFile) || !ContainsEnvVar(destFile))
                                continue;
                            _fileSecureMap.Add(srcFile, destFile);
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }

                return _fileSecureMap;
            }
        }

        /// <summary>
        ///     Gets the registry overwrite config.
        /// </summary>
        public static string ForceReg
        {
            get
            {
                if (_forceReg != default(string))
                    return _forceReg;
                try
                {
                    _forceReg = string.Concat(GetValue(nameof(ForceReg)), Environment.NewLine,
#if x86
                                              GetValue(nameof(ForceReg) + 32)
#else
                                              GetValue(nameof(ForceReg) + 64)
#endif
                        ).Trim();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _forceReg;
            }
        }

        /// <summary>
        ///     Gets the directory that contains the default settings.
        /// </summary>
        public static string DefSettingsDir
        {
            get
            {
                if (_defSettingsDir == default(string))
                    _defSettingsDir = PathEx.Combine(GetValue(nameof(DefSettingsDir)));
                return _defSettingsDir;
            }
        }

        /// <summary>
        ///     Gets the process names to skip.
        /// </summary>
        public static string[] IgnoredProcesses
        {
            get
            {
                if (_ignoredProcesses != default(string[]))
                    return _ignoredProcesses;
                _ignoredProcesses = GetValue(nameof(IgnoredProcesses)).Split(",", StringSplitOptions.RemoveEmptyEntries);
                return _ignoredProcesses;
            }
        }

        /// <summary>
        ///     Gets the path for the hash that determines that this instance is fully loaded.
        /// </summary>
        public static string InstanceHashPath
        {
            get
            {
                if (_instanceHashPath == default(string))
                    _instanceHashPath = Path.Combine(TempDir, nameof(ProcessManager).Encrypt());
                return _instanceHashPath;
            }
        }

        /// <summary>
        ///     Gets the registry settings file path.
        /// </summary>
        public static string RegFilePath
        {
            get
            {
                if (_regFilePath == default(string))
                    _regFilePath = PathEx.Combine(DataDir, "settings.reg");
                return _regFilePath;
            }
        }

        /// <summary>
        ///     Gets the registry keys to redirect.
        /// </summary>
        public static string[] RegKeys
        {
            get
            {
                if (_regKeys != default(string[]))
                    return _regKeys;
                try
                {
                    _regKeys = string.Concat(GetValue(nameof(RegKeys)), Environment.NewLine,
#if x86
                                             GetValue(nameof(RegKeys) + 32)
#else
                                             GetValue(nameof(RegKeys) + 64)
#endif
                        ).SplitNewLine(true).ToArray();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _regKeys;
            }
        }

        /// <summary>
        ///     Gets the base registry path.
        /// </summary>
        public static string RegBasePath =>
            "HKCU\\Software\\Portable Apps Suite";

        /// <summary>
        ///     Gets the launcher registry path.
        /// </summary>
        public static string RegPath
        {
            get
            {
                if (_regPath == default(string))
                    _regPath = $"{RegBasePath}\\{ProcessEx.CurrentName}";
                return _regPath;
            }
        }

        /// <summary>
        ///     Gets the registry keys to remove when the app is closed.
        /// </summary>
        public static string[] RmDirs
        {
            get
            {
                if (_rmDirs != default(string[]))
                    return _rmDirs;
                try
                {
                    _rmDirs = string.Concat(GetValue(nameof(RmDirs)), Environment.NewLine,
#if x86
                                            GetValue(nameof(RmDirs) + 32)
#else
                                            GetValue(nameof(RmDirs) + 64)
#endif
                        ).SplitNewLine(true).ToArray();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _rmDirs;
            }
        }

        /// <summary>
        ///     Gets the registry remove config.
        /// </summary>
        public static string RmReg
        {
            get
            {
                if (_rmReg != default(string))
                    return _rmReg;
                try
                {
                    _rmReg = string.Concat(GetValue(nameof(RmReg)), Environment.NewLine,
#if x86
                                           GetValue(nameof(RmReg) + 32)
#else
                                           GetValue(nameof(RmReg) + 64)
#endif
                        ).Trim();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _rmReg;
            }
        }

        /// <summary>
        ///     Gets the registry keys to remove when the app is closed.
        /// </summary>
        public static string[] RmRegKeys
        {
            get
            {
                if (_rmRegKeys != default(string[]))
                    return _rmRegKeys;
                try
                {
                    _rmRegKeys = string.Concat(GetValue(nameof(RmRegKeys)), Environment.NewLine,
#if x86
                                               GetValue(nameof(RmRegKeys) + 32)
#else
                                               GetValue(nameof(RmRegKeys) + 64)
#endif
                        ).SplitNewLine(true).ToArray();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
                return _rmRegKeys;
            }
        }

        /// <summary>
        ///     Gets the settings directory.
        /// </summary>
        public static string SettingsDir
        {
            get
            {
                if (_settingsDir != default(string))
                    return _settingsDir;
                _settingsDir = GetValue(nameof(SettingsDir));
                if (string.IsNullOrWhiteSpace(_settingsDir))
                    _settingsDir = "%CurDir%\\Data";
                _settingsDir = PathEx.Combine(_settingsDir);
                return _settingsDir;
            }
        }

        /// <summary>
        ///     Gets the temp directory.
        /// </summary>
        public static string TempDir
        {
            get
            {
                if (_tempDir == default(string))
                    _tempDir = PathEx.Combine(DataDir, "Temp");
                return _tempDir;
            }
        }

        /// <summary>
        ///     Gets the start arguments.
        /// </summary>
        public static string StartArguments =>
            ConfigManager.IniFormat.Launcher.StartArguments;

        /// <summary>
        ///     Gets the update tool path.
        /// </summary>
        public static string UpdaterPath
        {
            get
            {
                if (_updaterPath == default(string))
#if x86
                    _updaterPath = PathEx.Combine(GetValue(nameof(UpdaterPath)));
#else
                    _updaterPath = PathEx.Combine(GetValue(nameof(UpdaterPath) + 64));
#endif
                return _updaterPath;
            }
        }

        private static bool ContainsEnvVar(string path) =>
            path?.Length > 2 && path.Count(x => x == '%') == 2 && path.StartsWith("%") && (path.Substring(1).Contains($"%{Path.DirectorySeparatorChar}") || path.EndsWith("%"));

        /// <summary>
        ///     Returns the environment path of the specified path.
        /// </summary>
        /// <param name="path">
        ///     The full directory name or file path to handle.
        /// </param>
        /// <param name="app">
        ///     true to returns app environment path; otherwise, false to returns the
        ///     data environment path.
        /// </param>
        /// <param name="app64">
        ///     true to returns app environment path for 64-bit; otherwise, false to
        ///     returns the data environment path.
        /// </param>
        public static string GetEnvironmentPath(string path, bool app = false, bool app64 = false)
        {
            var str = path;
            if (!ContainsEnvVar(str))
                str = EnvironmentEx.GetVariablePathFull(str);
            if (ContainsEnvVar(str))
                str = PathEx.Combine(app ? "%CurDir%\\App" : DataDir, app ? (app64 ? "_Environment64" : "_Environment") : "Environment", str.RemoveChar('%'));
            return str;
        }

        /// <summary>
        ///     Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">
        ///     The key of the value to get.
        /// </param>
        public static string GetValue(string key) => 
            Config.TryGetValue(key, out var value) ? value : string.Empty;

        /// <summary>
        ///     Initialize a binary config.
        /// </summary>
        /// <param name="configData">
        ///     The bytes of the config.
        /// </param>
        /// <param name="reverse">
        ///     true, if the bytes are reversed; otherwise, false.
        /// </param>
        /// <param name="unzip">
        ///     true, if the bytes are compressed; otherwise, false.
        /// </param>
        public static void Initialize(byte[] configData, bool reverse = true, bool unzip = true)
        {
            if (configData == default(byte[]))
                throw new ArgumentNullException(nameof(configData));
            var bytes = configData;
            if (reverse)
                bytes = bytes.Reverse().ToArray();
            if (unzip)
                bytes = bytes.Unzip();
            Config = bytes?.DeserializeObject<Dictionary<string, string>>();
        }
    }
}
