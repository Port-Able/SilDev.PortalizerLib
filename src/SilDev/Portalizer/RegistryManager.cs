namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Win32;

    /// <summary>
    ///     Provides functionality for the Windows registry database.
    /// </summary>
    public static class RegistryManager
    {
        private static void SecureOverrides(IReadOnlyDictionary<string, Dictionary<string, string>> regMap, bool elevated = false)
        {
            if (regMap?.Any() != true)
                return;
            var file = PathEx.Combine(Attributes.DataDir, $"Temp\\overwrite-{{{EnvironmentEx.MachineId}}}.reg");
            try
            {
                var dir = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(dir))
                    throw new ArgumentNullException(dir);
                DirectoryEx.Create(dir);
                FileEx.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
                return;
            }
            using (var sw = new StreamWriter(file, true, Encoding.GetEncoding(1252)))
            {
                sw.WriteLine("Windows Registry Editor Version 5.00");
                sw.WriteLine();
                foreach (var data in regMap)
                {
                    var section = data.Key;
                    if (!section.ContainsEx('\\'))
                        continue;
                    var levels = section.Split('\\');
                    var first = levels.FirstOrDefault();
                    switch (first?.TrimStart('[', '-'))
                    {
                        case "HKEY_CLASSES_ROOT":
                        case "HKEY_CURRENT_CONFIG":
                        case "HKEY_CURRENT_USER":
                        case "HKEY_LOCAL_MACHINE":
                        case "HKEY_PERFORMANCE_DATA":
                        case "HKEY_USERS":
                            break;
                        case "HKCR":
                            levels[0] = "HKEY_CLASSES_ROOT";
                            break;
                        case "HKCC":
                            levels[0] = "HKEY_CURRENT_CONFIG";
                            break;
                        case "HKCU":
                            levels[0] = "HKEY_CURRENT_USER";
                            break;
                        case "HKLM":
                            levels[0] = "HKEY_LOCAL_MACHINE";
                            break;
                        case "HKPD":
                            levels[0] = "HKEY_PERFORMANCE_DATA";
                            break;
                        case "HKU":
                            levels[0] = "HKEY_USERS";
                            break;
                        default:
                            continue;
                    }
                    if (!first.Equals(levels[0]))
                    {
                        if (first.StartsWithEx("[-", "-"))
                            levels[0] = $"-{levels[0]}";
                        section = levels.Join('\\');
                    }
                    if (!section.StartsWith("["))
                        section = $"[{section}";
                    if (!section.EndsWith("]"))
                        section = $"{section}]";
                    sw.WriteLine(section);
                    if (regMap[data.Key]?.Any() != true)
                    {
                        sw.WriteLine();
                        continue;
                    }
                    foreach (var pair in regMap[data.Key])
                    {
                        var key = !string.IsNullOrWhiteSpace(pair.Key) && !pair.Key.Equals("@") ? $"\"{pair.Key}\"" : "@";
                        var value = pair.Value;
                        if (string.IsNullOrWhiteSpace(value))
                            value = "-";
                        sw.WriteLine($"{key}={value}");
                    }
                    sw.WriteLine();
                }
                sw.WriteLine();
            }
            Reg.ImportFile(file, elevated || Elevation.IsAdministrator);
            ProcessEx.SendHelper.WaitThenDelete(file, 10, Elevation.IsAdministrator);
        }

        /// <summary>
        ///     Enables/disables the registry key redirection.
        /// </summary>
        /// <param name="option">
        ///     The option that determines whether the redirect will be enabled or disabled.
        /// </param>
        /// <param name="keys">
        ///     The registry keys for redirecting.
        /// </param>
        public static void KeyRedirection(PortalizerActions option, params string[] keys)
        {
            if (keys == null || keys.Length == 0)
                return;
            var backup = PathEx.Combine(Attributes.DataDir, $"Temp\\backup-{{{EnvironmentEx.MachineId}}}.reg");
            switch (option)
            {
                case PortalizerActions.Disable:
                    if (keys.Length > 0)
                    {
                        Reg.ExportKeys(Attributes.RegFilePath, keys);
                        foreach (var key in keys)
                            Reg.RemoveSubKey(key);
                        Reg.RemoveEntry(Attributes.RegPath, nameof(Attributes.RegKeys));
                    }
                    if (!File.Exists(backup))
                        return;
                    Reg.ImportFile(backup);
                    FileEx.TryDelete(backup);
                    break;
                default:
                    if (!Reg.SubKeyExists(Attributes.RegPath))
                        Reg.CreateNewSubKey(Attributes.RegPath);
                    if (!Reg.EntryExists(Attributes.RegPath, nameof(Attributes.RegKeys)) && keys.Any(Reg.SubKeyExists))
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(backup);
                            if (string.IsNullOrEmpty(dir))
                                throw new ArgumentNullException(dir);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                            FileEx.Delete(backup);
                        }
                        catch (Exception ex)
                        {
                            Log.Write(ex);
                        }
                        if (!File.Exists(backup))
                            Reg.ExportKeys(backup, keys);
                        foreach (var key in keys)
                            Reg.RemoveSubKey(key);
                    }
                    Reg.Write(Attributes.RegPath, nameof(Attributes.RegKeys), keys, RegistryValueKind.MultiString);
                    if (File.Exists(Attributes.RegFilePath))
                        Reg.ImportFile(Attributes.RegFilePath);
                    break;
            }
        }

        /// <summary>
        ///     Removes the specified registry keys.
        /// </summary>
        /// <param name="keys">
        ///     The keys to remove.
        /// </param>
        public static void RemoveKeys(params string[] keys)
        {
            var regSecureMap = new Dictionary<string, Dictionary<string, string>>();
            foreach (var key in keys)
                switch (key)
                {
                    case "HKEY_CLASSES_ROOT":
                    case "HKCR":
                    case "HKEY_CURRENT_CONFIG":
                    case "HKCC":
                    case "HKEY_CURRENT_USER":
                    case "HKCU":
                    case "HKEY_LOCAL_MACHINE":
                    case "HKLM":
                    case "HKEY_PERFORMANCE_DATA":
                    case "HKPD":
                    case "HKEY_USERS":
                    case "HKU":
                        continue;
                    default:
                        regSecureMap.Add($"-{key}", null);
                        break;
                }
            SecureOverrides(regSecureMap, Elevation.IsAdministrator);
        }

        /// <summary>
        ///     Sets registry values from an INI config or from a REG file.
        /// </summary>
        /// <param name="fileOrContent">
        ///     The path or content of an INI or REG file.
        /// </param>
        public static void SetConfig(string fileOrContent)
        {
            if (string.IsNullOrWhiteSpace(fileOrContent))
                return;
            var sections = Ini.GetSections(fileOrContent, false);
            try
            {
                if (sections.All(x => x.StartsWith("HKEY_")) && sections.All(x => Ini.GetKeys(x, fileOrContent, false).All(y => y.Equals("@") || y.StartsWith("\"") && y.EndsWith("\""))))
                {
                    try
                    {
                        var regex = new Regex("%(.+?)%");
                        var content = File.Exists(fileOrContent) ? File.ReadAllText(fileOrContent) : fileOrContent;
                        foreach (var variable in regex.Matches(content).Cast<Match>().Select(x => x.Value).Distinct())
                        {
                            var value = EnvironmentEx.GetVariableValue(variable);
                            if (!content.ContainsEx(variable))
                                continue;
                            content = content.Replace(variable, value);
                        }
                        var cArray = TextEx.FormatNewLine(content).SplitNewLine();
                        Reg.ImportFile(cArray);
                    }
                    catch (Exception ex)
                    {
                        Log.Write(ex);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }

            foreach (var section in Ini.GetSections(fileOrContent, false))
            {
                var key = Ini.Read(section, "Key", default(string), fileOrContent);
                if (string.IsNullOrEmpty(key))
                    continue;

                var entry = Ini.Read(section, "Entry", default(string), fileOrContent);
                var value = Ini.Read(section, "Value", default(string), fileOrContent);
                if (string.IsNullOrEmpty(value))
                    continue;
                if (!string.IsNullOrEmpty(entry))
                    entry = PathEx.Combine(entry);
                value = PathEx.Combine(value);

                var kind = Ini.Read(section, "Kind", default(string), fileOrContent);
                if (string.IsNullOrEmpty(kind))
                    continue;

                try
                {
                    Reg.Write(key, !string.IsNullOrWhiteSpace(entry) ? entry : null, value, (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kind));
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                    continue;
                }

                var temp = Ini.Read(section, "Temp", default(string), fileOrContent);
                if (!temp.EqualsEx("True", "Entry"))
                    continue;

                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    switch (temp.ToLower())
                    {
                        case "true":
                            Reg.RemoveSubKey(key);
                            break;
                        case "entry":
                            Reg.RemoveEntry(key, entry);
                            break;
                    }
                };
            }
        }
    }
}
