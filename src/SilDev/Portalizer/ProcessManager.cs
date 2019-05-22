namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;

    /// <summary>
    ///     Provides functionality to handle process instances.
    /// </summary>
    public static class ProcessManager
    {
        private static List<string> _instanceDirs;

        private static void ApplyConfig()
        {
            if (!ConfigManager.IniFormat.Launcher.HideInTaskbar && ConfigManager.IniFormat.Launcher.WindowState == WinApi.ShowWindowFlags.ShowNormal)
                return;
            try
            {
                var title = string.Empty;
                var count = 0;
                var name = Path.GetFileNameWithoutExtension(Attributes.AppPath);
                var hWnd = IntPtr.Zero;
                while (string.IsNullOrWhiteSpace(title) || ++count > 64)
                {
                    Thread.Sleep(250);
                    var processes = Process.GetProcessesByName(name);
                    if (!processes.Any())
                        break;
                    foreach (var p in processes)
                    {
                        if (p?.HasExited != false || string.IsNullOrWhiteSpace(p.MainWindowTitle) || !string.IsNullOrEmpty(ConfigManager.IniFormat.Launcher.WindowTitle) && !ConfigManager.IniFormat.Launcher.WindowTitle.EqualsEx(p.MainWindowTitle))
                            continue;
                        title = p.MainWindowTitle;
                        break;
                    }
                }
                if (!string.IsNullOrWhiteSpace(title))
                    hWnd = WinApi.NativeHelper.FindWindowByCaption(title);
                if (hWnd == IntPtr.Zero)
                    throw new ArgumentNullException(nameof(hWnd));
                if (ConfigManager.IniFormat.Launcher.HideInTaskbar)
                    TaskBar.DeleteTab(hWnd);
                if (ConfigManager.IniFormat.Launcher.WindowState != WinApi.ShowWindowFlags.ShowNormal)
                    WinApi.NativeHelper.ShowWindowAsync(hWnd, ConfigManager.IniFormat.Launcher.WindowState);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
        }

        private static IEnumerable<Process> GetInstances()
        {
            if (Attributes.AppWaitFull < 0)
                return ProcessEx.GetInstances(Attributes.AppPath, true);

            if (_instanceDirs == default(List<string>))
            {
                _instanceDirs = new List<string>();
                switch (Attributes.AppWaitFull)
                {
                    case 0:
                    case 1:
                        _instanceDirs.Add(Attributes.AppDir);
                        _instanceDirs.Add(Attributes.DataDir);
                        break;
                    default:
                        _instanceDirs.Add(PathEx.LocalDir);
                        if (Attributes.DirMap?.Any() == true)
                        {
                            var fromKeys = Attributes.DirMap.Keys.Where(x => !x.StartsWithEx(PathEx.LocalDir)).ToArray();
                            if (fromKeys.Any())
                                _instanceDirs.AddRange(fromKeys);
                            var fromValues = Attributes.DirMap.Values.Where(x => !x.StartsWithEx(PathEx.LocalDir)).ToArray();
                            if (fromValues.Any())
                                _instanceDirs.AddRange(fromValues);
                        }
                        break;
                }
                if (Attributes.AppWaitDirs?.Any() == true)
                    _instanceDirs.AddRange(Attributes.AppWaitDirs);
                _instanceDirs = _instanceDirs.Select(PathEx.Combine).Distinct().ToList();
            }

            var files = new List<string>();
            foreach (var dir in _instanceDirs.Where(Directory.Exists))
                files.AddRange(DirectoryEx.GetFiles(dir, "*.exe", SearchOption.AllDirectories).Where(x => !x.EqualsEx(PathEx.LocalPath)));
            files = (Attributes.IgnoredProcesses?.Any() == true ? files.Distinct().Where(x => !Attributes.IgnoredProcesses.ContainsEx(Path.GetFileName(x), Path.GetFileNameWithoutExtension(x))) : files.Distinct()).ToList();

            return files.SelectMany(x => ProcessEx.GetInstances(Attributes.AppWaitFull == 0 ? Path.GetFileName(x) : x, Attributes.AppWaitFull >= 2)).Distinct().Where(x => x.Handle != ProcessEx.CurrentHandle);
        }

        private static void WaitForExit()
        {
            Check:
            var wasRunning = false;
            bool isRunning;
            do
            {
                var instances = GetInstances()?.ToList();
                isRunning = instances?.Any() == true;
                if (isRunning)
                {
                    ApplyConfig();
                    foreach (var instance in instances)
                    {
                        if (instance?.HasExited != false)
                            continue;
                        instance.WaitForExit();
                    }
                }
                else
                {
                    Thread.Sleep(200);
                    continue;
                }
                if (Log.DebugMode > 0)
                    Log.Write($"Instances: '{instances.Select(x => x.ProcessName).Join("'; '")}'");
                if (!wasRunning)
                    wasRunning = true;
            }
            while (isRunning);
            if (!wasRunning)
                return;
            Thread.Sleep(200);
            goto Check;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Process"/> class from the specified
        ///     parameters and starts the process component and waits for its exit.
        /// </summary>
        /// <param name="fileName">
        ///     The application to start.
        /// </param>
        /// <param name="workingDirectory">
        ///     The working directory for the process to be started.
        /// </param>
        /// <param name="arguments">
        ///     The command-line arguments to use when starting the application.
        /// </param>
        /// <param name="processWindowStyle">
        ///     The window state to use when the process is started.
        /// </param>
        public static void Start(string fileName, string workingDirectory, string arguments, ProcessWindowStyle processWindowStyle = ProcessWindowStyle.Normal)
        {
            var path = PathEx.Combine(fileName);
            if (!File.Exists(path))
                return;
            var startInfo = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = path,
                WorkingDirectory = workingDirectory,
                WindowStyle = processWindowStyle
            };
            if (Elevation.IsAdministrator && ConfigManager.IniFormat.Launcher.ForceNonAdmin)
                startInfo.Verb = "RunNotAs";
            using (var p = ProcessEx.Start(startInfo, false))
                if (p?.HasExited == false)
                {
                    ApplyConfig();
                    if (!p.HasExited)
                        p.WaitForExit();
                }
            WaitForExit();
        }
    }
}
