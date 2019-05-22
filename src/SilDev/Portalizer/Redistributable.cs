namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Forms;
    using Languages;

    /// <summary>
    ///     Provides functionality to manage redistributable packages.
    /// </summary>
    public static class Redistributable
    {
        private static EnvironmentEx.RedistFlags[] _flags;

        private static EnvironmentEx.RedistFlags[] GetFlags()
        {
            if (_flags != default(EnvironmentEx.RedistFlags[]))
                return _flags;
            var dir = PathEx.Combine(PathEx.LocalDir, "_CommonRedist", "vcredist");
            if (!Directory.Exists(dir))
                return null;
            var list = new List<EnvironmentEx.RedistFlags>();
            var years = new[]
            {
                "2008",
                "2010",
                "2012",
                "2013",
                "2015",
                "2017"
            };
            try
            {
                var archs = new[]
                {
                    "x86"
#if x64
                    ,
                    "x64"
#endif
                };
                foreach (var year in years)
                    list.AddRange(archs.Select(arch => new { arch, path = PathEx.Combine(dir, year, $"vcredist_{arch}.exe") }).Where(s => File.Exists(s.path))
                                       .Select(t => (EnvironmentEx.RedistFlags)Enum.Parse(typeof(EnvironmentEx.RedistFlags), $"VC{year}{t.arch.ToUpper()}")));
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
            _flags = list.Count > 0 ? list.ToArray() : null;
            return _flags;
        }

        /// <summary>
        ///     Enables/disables the specified redistributable packages.
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redistributable packages will be
        ///     enabled or disabled.
        /// </param>
        /// <param name="versions">
        ///     The redistributable package versions to handle.
        /// </param>
        public static void Handler(PortalizerActions option, params EnvironmentEx.RedistFlags[] versions)
        {
            if (versions == null)
                return;
            var dict = new Dictionary<EnvironmentEx.RedistFlags, Dictionary<int, List<int>>>();
            foreach (var version in versions)
                try
                {
                    var data = Enum.GetName(typeof(EnvironmentEx.RedistFlags), version)?.Split('X')
                                   .Select(s => new string(s.Where(char.IsDigit).ToArray())).ToArray();
                    if (data == null)
                        throw new ArgumentNullException(nameof(data));
                    if (data.Length != 2)
                        throw new ArgumentOutOfRangeException(nameof(data));
                    var year = Convert.ToInt32(data.First());
                    if (year < 2005)
                        throw new ArgumentOutOfRangeException(nameof(year));
                    var arch = Convert.ToInt32(data.Last());
                    if (arch != 64 && arch != 86)
                        throw new ArgumentOutOfRangeException(nameof(arch));
                    if (!dict.ContainsKey(version))
                        dict.Add(version, new Dictionary<int, List<int>>());
                    if (!dict[version].ContainsKey(year))
                        dict[version].Add(year, new List<int>());
                    dict[version][year].Add(arch);
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
            foreach (var data in dict)
            {
                var version = data.Key;
                if (version == EnvironmentEx.RedistFlags.VC2005X86 ||
                    version == EnvironmentEx.RedistFlags.VC2005X64)
                    continue;
                foreach (var vars in data.Value)
                {
                    var year = vars.Key;
                    foreach (var arch in vars.Value)
                    {
                        var path = PathEx.Combine(PathEx.LocalDir, $"_CommonRedist\\vcredist\\{year}\\vcredist_x{arch}.exe");
                        if (!File.Exists(path))
                            return;
                        var iniPath = Path.ChangeExtension(PathEx.LocalPath, ".ini");
                        string args;
                        switch (option)
                        {
                            case PortalizerActions.Disable:
                                if (Ini.ReadDirect("Redist", version.ToString(), iniPath).EqualsEx("True"))
                                    return;
                                switch (year)
                                {
                                    case 2008:
                                        args = "/qu";
                                        break;
                                    case 2010:
                                    case 2012:
                                        args = "/uninstall /q /norestart";
                                        break;
                                    default:
                                        args = "/uninstall /quiet /norestart";
                                        break;
                                }
                                using (var p = ProcessEx.Start(path, args, Elevation.IsAdministrator, false))
                                    if (p?.HasExited == false)
                                        p.WaitForExit();
                                break;
                            default:
                                if (Ini.ReadDirect("Redist", version.ToString(), iniPath).EqualsEx("False"))
                                    Elevation.RestartAsAdministrator(EnvironmentEx.CommandLine(false));
                                if (EnvironmentEx.Redist.IsInstalled(version))
                                {
                                    Ini.WriteDirect("Redist", version.ToString(), true, iniPath);
                                    break;
                                }
                                if (!Ini.ReadDirect("Redist", version.ToString(), iniPath).EqualsEx("True", "False"))
                                {
                                    MessageBoxEx.TopMost = true;
                                    MessageBoxEx.ButtonText.OverrideEnabled = true;
                                    MessageBoxEx.ButtonText.Yes = Strings.RedistRequestButtonYes;
                                    MessageBoxEx.ButtonText.No = Strings.RedistRequestButtonNo;
                                    MessageBoxEx.ButtonText.Cancel = Strings.RedistRequestButtonCancel;
                                    var msg = string.Format(Strings.RedistRequestMessage, year, arch);
                                    var result = MessageBoxEx.Show(msg, AssemblyInfo.Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                                    if (result == DialogResult.Cancel)
                                    {
                                        Handler(PortalizerActions.Disable, versions);
                                        Environment.Exit(Environment.ExitCode);
                                    }
                                    Ini.WriteDirect("Redist", version.ToString(), result == DialogResult.Yes, iniPath);
                                    if (result != DialogResult.Yes)
                                        Elevation.RestartAsAdministrator(EnvironmentEx.CommandLine(false));
                                }
                                var notifyBox = new NotifyBox();
                                notifyBox.Show(string.Format(Strings.RedistProgressNotify, year, arch), AssemblyInfo.Title, NotifyBoxStartPosition.Center);
                                switch (year)
                                {
                                    case 2008:
                                        args = "/q";
                                        break;
                                    case 2010:
                                    case 2012:
                                        args = "/q /norestart";
                                        break;
                                    default:
                                        args = "/install /quiet /norestart";
                                        break;
                                }
                                using (var p = ProcessEx.Start(path, args, Elevation.IsAdministrator, false))
                                    if (p?.HasExited == false)
                                        p.WaitForExit();
                                notifyBox.Close();
                                if (!EnvironmentEx.Redist.IsInstalled(version))
                                {
                                    Environment.ExitCode = 1;
                                    Environment.Exit(Environment.ExitCode);
                                }
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Enables/disables the specified redistributable packages.
        ///     <para>
        ///         Search for redistributable packages using the following format:
        ///         '%CurDir%\_CommonRedist\vcredist\2017\vcredist_x86.exe'
        ///     </para>
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redistributable packages will be
        ///     enabled or disabled.
        /// </param>
        public static void Handler(PortalizerActions option) =>
            Handler(option, GetFlags());
    }
}
