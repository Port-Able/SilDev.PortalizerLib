namespace SilDev.Portalizer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Languages;

    /// <summary>
    ///     Provides functionality to manage the JRE (Java Runtime Environment).
    /// </summary>
    public static class JavaHandler
    {
        /// <summary>
        ///     Gets or sets the JRE start parameter.
        /// </summary>
        public static string StartParameter { get; set; } = "-jar";

        private static string GetPath(string dir) =>
            Directory.EnumerateFiles(dir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();

        /// <summary>
        ///     Tries to find the portable JRE path.
        /// </summary>
        /// <param name="javaPath">
        ///     The JRE path.
        /// </param>
        [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
        public static void Find(out string javaPath)
        {
            // Read saved path from config.
            javaPath = null;
            if (File.Exists(Attributes.ConfigPath))
            {
                javaPath = Ini.ReadDirect("Java", "Path", Attributes.ConfigPath);
                if (!string.IsNullOrWhiteSpace(javaPath))
                    javaPath = PathEx.Combine(javaPath);
                if (File.Exists(javaPath))
                    goto Found;
            }

            // Try getting the default path from Portable Apps Suite JRE installation.
            try
            {
                var dir = EnvironmentEx.GetVariableValue("AppsSuiteDir");
                if (Directory.Exists(dir))
                {
#if x86
                    dir = Path.Combine(dir, "Apps", "CommonFiles", "Java");
#else
                    dir = Path.Combine(dir, "Apps", "CommonFiles", "Java64");
                    if (!Directory.Exists(dir))
                        Path.Combine(dir, "Apps", "CommonFiles", "Java");
#endif
                    if (Directory.Exists(dir))
                    {
                        javaPath = GetPath(dir);
                        if (File.Exists(javaPath))
                            goto Found;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }

            // Start searching for the portable JRE, starting in the current directory.
            try
            {
                var dir = PathEx.LocalDir;
                var drive = new DriveInfo(dir).RootDirectory.Root.Name;
                int count = 0, length = dir.Split(Path.DirectorySeparatorChar).Length;
                var subs = new[]
                {
#if x64
                    "_CommonFiles\\Java64",
                    "CommonFiles\\Java64",
#endif
                    "_CommonFiles\\Java",
                    "CommonFiles\\Java"
                };
                while (!drive.ContainsEx(dir) && ++count < length)
                {
                    foreach (var sub in subs)
                    {
                        var tmp = Path.Combine(dir, sub);
                        if (!Directory.Exists(tmp))
                            continue;
                        javaPath = GetPath(tmp);
                        if (File.Exists(javaPath))
                            goto Found;
                    }
                    dir = PathEx.Combine(dir, "..").TrimEnd(Path.DirectorySeparatorChar);
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }

            // Try getting the default path from the global JRE installation.
            try
            {
                var dirs = new[]
                {
                    "%ProgramFiles%\\Java",
#if x64
                    "%ProgramFiles(x86)%\\Java",
#endif
                    "%ProgramData%\\Oracle\\Java\\javapath"
                };
                foreach (var dir in dirs.Select(PathEx.Combine))
                {
                    if (!Directory.Exists(dir))
                        continue;
                    javaPath = GetPath(dir);
                    if (File.Exists(javaPath))
                        goto Found;
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }

            // If JRE was not found.
            MessageBox.Show(Strings.JavaWarnMessage, AssemblyInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            Environment.Exit(Environment.ExitCode);

            // Else
            Found:
            var envPath = EnvironmentEx.GetVariablePathFull(javaPath);
            Ini.WriteDirect(nameof(JavaHandler), "Path", envPath, Attributes.ConfigPath);

            var usageDir = PathEx.Combine("%UserProfile%\\.oracle_jre_usage");
            try
            {
                if (!Directory.Exists(usageDir))
                    Directory.CreateDirectory(usageDir);
                DirectoryEx.SetAttributes(usageDir, FileAttributes.Hidden);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
            AppDomain.CurrentDomain.ProcessExit += (s, e) => DirectoryEx.TryDelete(usageDir);
        }
    }
}
