namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;
    using Microsoft.Win32;

    public static class ProgramInstance
    {
        public static Action DoProgramStart { get; set; } = () => { };

        public static Func<bool> DoAppStart { get; set; } = () => true;

        public static Action DoExitRoutines { get; set; } = () => { };

        public static Action DoProgramExit { get; set; } = () => { };

        public static void Initialize()
        {
            using (new Mutex(true, ProcessEx.CurrentName, out var newInstance))
            {
#if x86
                if (Environment.Is64BitOperatingSystem)
                {
                    var curPath64 = PathEx.Combine(PathEx.LocalDir, $"{ProcessEx.CurrentName}64.exe");
                    if (File.Exists(curPath64))
                    {
                        ProcessEx.Start(curPath64, EnvironmentEx.CommandLine(false));
                        return;
                    }
                }
#endif

                if (!string.IsNullOrWhiteSpace(Attributes.DefIni) && !string.IsNullOrWhiteSpace(Attributes.ConfigPath) && !File.Exists(Attributes.ConfigPath))
                    try
                    {
                        File.WriteAllText(Attributes.ConfigPath, Attributes.DefIni);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Elevation.RestartAsAdministrator();
                    }
                    catch (Exception ex)
                    {
                        Log.Write(ex);
                        MessageBox.Show(ex.ToString(), AssemblyInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                if (!Elevation.IsAdministrator && ConfigManager.IniFormat.Launcher.ForceAdmin)
                    Elevation.RestartAsAdministrator();

                DoProgramStart();
                if (PathEx.IsValidPath(Attributes.UpdaterPath) && !File.Exists(Attributes.UpdaterPath))
                    return;

                if (!newInstance)
                {
                    double totalSeconds;
                    var stopwatch = Stopwatch.StartNew();
                    do
                    {
                        if (File.Exists(Attributes.InstanceHashPath))
                            break;
                        totalSeconds = Math.Abs(stopwatch.Elapsed.TotalSeconds);
                        Thread.Sleep(totalSeconds < 1 ? 600 : 100);
                    }
                    while (totalSeconds < 8);
                    stopwatch.Stop();
                    if (!File.Exists(Attributes.InstanceHashPath))
                        return;
                    if (!Directory.Exists(Attributes.AppDir) || !File.Exists(Attributes.AppPath) || Reg.ReadString(Attributes.RegPath, null).EqualsEx(Attributes.AppPath))
                        return;
                    var startInfo = new ProcessStartInfo
                    {
                        Arguments = Attributes.StartArguments,
                        FileName = Attributes.AppPath,
                        WorkingDirectory = Attributes.AppDir
                    };
                    if (Elevation.IsAdministrator && ConfigManager.IniFormat.Launcher.ForceNonAdmin)
                        startInfo.Verb = "RunNotAs";
                    ProcessEx.Start(startInfo);
                    return;
                }

                if (Attributes.ElevateFirst)
                    Elevation.RestartAsAdministrator();

                try
                {
                    var paths = new List<string>();
                    if (!string.IsNullOrEmpty(Attributes.AppPath))
                        paths.Add(Attributes.AppPath);
#if x86
                    var appPath = Attributes.GetValue(nameof(Attributes.AppPath));
#else
                    var appPath = Attributes.GetValue(nameof(Attributes.AppPath) + 64);
#endif
                    if (!string.IsNullOrEmpty(appPath) && !appPath.EqualsEx(appPath))
                        paths.Add(appPath);
                    if (!paths.Any() || paths.Any(x => !x.EndsWithEx("javaw.exe") && ProcessEx.IsRunning(x, true)) || paths.Any(x => x.EndsWithEx("javaw.exe") && ProcessEx.GetInstances(x, true).Any(y => y.GetCommandLine().ContainsEx(Attributes.StartArguments))))
                        throw new NotSupportedException();
                }
                catch (NotSupportedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                    return;
                }

                if (File.Exists(Attributes.UpdaterPath))
                {
                    if (ProcessEx.IsRunning(Attributes.UpdaterPath, true))
                        return;
                    ProcessManager.Start(Attributes.UpdaterPath, null, "/quiet");
                }

                if (!Directory.Exists(Attributes.AppDir) || !File.Exists(Attributes.AppPath))
                    return;

                Reg.RemoveSubKey(Attributes.RegPath);

                Redistributable.Handler(PortalizerActions.Enable);

                if (!Directory.Exists(Attributes.SettingsDir))
                {
                    Directory.CreateDirectory(Attributes.SettingsDir);
                    if (Directory.Exists(Attributes.DefSettingsDir))
                        DirectoryEx.Copy(Attributes.DefSettingsDir, Attributes.SettingsDir, true, true);
#if x86
                    if (Directory.Exists(Attributes.DefSettingsDir32))
                        DirectoryEx.Copy(Attributes.DefSettingsDir32, Attributes.SettingsDir, true, true);
#else
                    if (Directory.Exists(Attributes.DefSettingsDir64))
                        DirectoryEx.Copy(Attributes.DefSettingsDir64, Attributes.SettingsDir, true, true);
#endif
                }

                FileManager.DirRedirection(PortalizerActions.Enable, Attributes.DirMap);
                FileManager.FileSecureRedirection(PortalizerActions.Enable, Attributes.FileSecureMap);
                FileManager.FileRedirection(PortalizerActions.Enable, Attributes.FileMap, Attributes.FileMapSimple);

                RegistryManager.KeyRedirection(PortalizerActions.Enable, Attributes.RegKeys);

                if (!File.Exists(Attributes.RegFilePath))
                    RegistryManager.SetConfig(Attributes.DefReg);

                RegistryManager.SetConfig(Attributes.ForceReg);

                FileEx.Create(Attributes.InstanceHashPath);
                if (DoAppStart())
                    ProcessManager.Start(Attributes.AppPath, Attributes.AppDir, Attributes.StartArguments);

                DoExitRoutines();

                Reg.Write(Attributes.RegPath, null, Attributes.AppPath, RegistryValueKind.String);

                RegistryManager.KeyRedirection(PortalizerActions.Disable, Attributes.RegKeys);

                FileManager.FileRedirection(PortalizerActions.Disable, Attributes.FileMap, Attributes.FileMapSimple);
                FileManager.FileSecureRedirection(PortalizerActions.Disable, Attributes.FileSecureMap);
                FileManager.DirRedirection(PortalizerActions.Disable, Attributes.DirMap);
                FileManager.RemoveDirs(Attributes.RmDirs);

                RegistryManager.SetConfig(Attributes.RmReg);
                RegistryManager.RemoveKeys(Attributes.RmRegKeys);

                Redistributable.Handler(PortalizerActions.Disable);

                Reg.RemoveSubKey(Attributes.RegPath);

                DoProgramExit();

                FileEx.TryDelete(Attributes.InstanceHashPath);
            }
        }
    }
}
