namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Provides functionality to manage config files.
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        ///     Provides functionality to manage the launcher INI config file.
        /// </summary>
        public static class IniFormat
        {
            /// <summary>
            ///     Overrides INI file parameters.
            /// </summary>
            /// <param name="iniMap">
            ///     The INI map.
            /// </param>
            /// <param name="path">
            ///     The INI file path.
            /// </param>
            public static void Overrides(Dictionary<string, Dictionary<string, string>> iniMap, string path)
            {
                if (iniMap?.Any() != true)
                    return;
                var file = PathEx.Combine(path);
                try
                {
                    if (!PathEx.IsValidPath(file))
                        throw new NotSupportedException();
                    var dir = Path.GetDirectoryName(file);
                    if (string.IsNullOrEmpty(dir))
                        throw new ArgumentNullException(dir);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    if (!File.Exists(file))
                        File.Create(file).Close();
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                    return;
                }
                foreach (var data in iniMap)
                {
                    var section = data.Key;
                    foreach (var pair in iniMap[section])
                    {
                        var key = pair.Key;
                        var value = pair.Value;
                        Ini.WriteDirect(section, key, value, path);
                    }
                }
            }

            /// <summary>
            ///     Provides functionality to manage the launcher config file.
            /// </summary>
            public static class Launcher
            {
                private static bool? _forceAdmin, _forceNonAdmin, _hideInTaskbar;
                private static string _startArguments, _windowTitle;
                private static WinApi.ShowWindowFlags _windowState;

                /// <summary>
                ///     Gets the force admin value.
                /// </summary>
                public static bool ForceAdmin
                {
                    get
                    {
                        if (_forceAdmin == default(bool?))
                            _forceAdmin = Ini.Read("Settings", nameof(ForceAdmin), false, Attributes.ConfigPath);
                        return _forceAdmin == true;
                    }
                    set => _forceAdmin = value;
                }

                /// <summary>
                ///     Gets the force non-admin value.
                /// </summary>
                public static bool ForceNonAdmin
                {
                    get
                    {
                        if (_forceNonAdmin == default(bool?))
                            _forceNonAdmin = Ini.Read("Settings", nameof(ForceNonAdmin), false, Attributes.ConfigPath);
                        return _forceNonAdmin == true;
                    }
                    set => _forceNonAdmin = value;
                }

                /// <summary>
                ///     Gets the hide in taskbar value.
                /// </summary>
                public static bool HideInTaskbar
                {
                    get
                    {
                        if (_hideInTaskbar == default(bool?))
                            _hideInTaskbar = Ini.Read("Settings", nameof(HideInTaskbar), false, Attributes.ConfigPath);
                        return _hideInTaskbar == true;
                    }
                }

                /// <summary>
                ///     Gets or sets the start arguments.
                /// </summary>
                public static string StartArguments
                {
                    get
                    {
                        if (_startArguments != default(string))
                            return _startArguments;
                        _startArguments = Ini.Read("Settings", nameof(StartArguments), "{0}", Attributes.ConfigPath);
                        try
                        {
                            var sorted = Ini.Read("Settings", $"Sorted{nameof(StartArguments)}", false, Attributes.ConfigPath);
                            _startArguments = string.Format(_startArguments, EnvironmentEx.CommandLine(sorted));
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        return _startArguments;
                    }
                    set => _startArguments = value;
                }

                /// <summary>
                ///     Gets the window state value.
                /// </summary>
                public static WinApi.ShowWindowFlags WindowState
                {
                    get
                    {
                        if (_windowState != default(WinApi.ShowWindowFlags))
                            return _windowState;
                        var state = Ini.Read("Settings", nameof(WindowState), "ShowNormal", Attributes.ConfigPath);
                        Enum.TryParse(state, out _windowState);
                        return _windowState;
                    }
                }

                /// <summary>
                ///     Gets the window title that is used to find the handle to apply
                ///     <see cref="HideInTaskbar"/> and/or <see cref="WindowState"/>
                ///     config settings.
                /// </summary>
                public static string WindowTitle
                {
                    get
                    {
                        if (_windowTitle == default(string))
                            _windowTitle = Ini.Read("Settings", nameof(WindowTitle), string.Empty, Attributes.ConfigPath);
                        return _windowTitle;
                    }
                }
            }
        }
    }
}
