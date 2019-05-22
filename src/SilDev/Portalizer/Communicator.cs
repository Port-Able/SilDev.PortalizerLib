namespace SilDev.Portalizer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Win32;

    internal enum CommunicatorDataTypes
    {
        Directory,
        File,
        RegKey
    }

    internal static class Communicator
    {
        private const string BaseKey = "HKCU\\Software\\Portable Apps Suite";
        private static readonly string CurKey = $"{BaseKey}\\{ProcessEx.CurrentName}";

        internal static bool EntryExists(string entry) =>
            Reg.EntryExists(CurKey, entry);

        internal static TValue GetEntry<TValue>(string entry, TValue defValue = default(TValue)) =>
            Reg.Read(CurKey, entry, default(TValue));

        internal static bool AddEntry<TValue>(string entry, TValue value, RegistryValueKind registryValueKind) =>
            Reg.Write(CurKey, entry, value, registryValueKind);

        internal static Dictionary<string, TValue> GetEntries<TValue>(string entry, TValue defValue = default(TValue))
        {
            var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
            var keys = Reg.GetSubKeys(BaseKey)?.ToArray();
            if (keys == null || keys.Length <= 1)
                return result;
            foreach (var key in keys)
            {
                var name = Path.GetFileName(key);
                if (string.IsNullOrWhiteSpace(name) || name.EqualsEx(ProcessEx.CurrentName))
                    continue;
                var path = $"{BaseKey}\\{name}";
                var value = Reg.Read(path, entry, default(TValue));
                if (value == null)
                    continue;
                result.Add(name, value);
            }
            return result;
        }

        internal static List<string> GetMultipleEntries(CommunicatorDataTypes dataType)
        {
            var result = new List<string>();
            var current = GetEntry("", default(string[]));
            if (current == null)
                return result;
            var entries = GetEntries("", default(string[]));
            try
            {
                result.AddRange(from entry in current from value in entries.Values from item in value where entry.EqualsEx(item) select item);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
            return result;
        }
    }
}
