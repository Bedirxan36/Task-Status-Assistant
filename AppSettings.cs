using Microsoft.Win32;

namespace TrayStatusHelper
{
    internal static class AppSettings
    {
        private const string RootKeyPath = @"Software\TrayStatusHelper";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Task Status Assistant";
        private const string LegacyRunValueName = "TrayStatusHelper";

        public static bool NotificationsEnabled
        {
            get => ReadBool("NotificationsEnabled", true);
            set => WriteBool("NotificationsEnabled", value);
        }

        public static bool FanSimEnabled
        {
            get => ReadBool("FanSimEnabled", false);
            set => WriteBool("FanSimEnabled", value);
        }

        // Fan durumunu (veya fan boost modunu) klavyeden toggle etmek icin:
        // Fn+1 gibi OEM kisayollar cogu cihazda Windows'a "ayri bir tus" olarak gelir (veya hic gelmez).
        // Bu deger, uygulamanin yakaladigi Virtual-Key kodunu tutar (0 = devre disi).
        public static int FanHotkeyVKey
        {
            get => ReadInt("FanHotkeyVKey", 0);
            set => WriteInt("FanHotkeyVKey", value);
        }

        public static bool AutoStartEnabled
        {
            get
            {
                using var rk = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var v = rk?.GetValue(RunValueName) as string;
                if (!string.IsNullOrWhiteSpace(v)) return true;
                v = rk?.GetValue(LegacyRunValueName) as string;
                return !string.IsNullOrWhiteSpace(v);
            }
            set
            {
                using var rk = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (rk == null) return;

                if (value)
                {
                    // Uygulamanın exe yolunu yaz
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    rk.SetValue(RunValueName, $"\"{exePath}\"");
                    rk.DeleteValue(LegacyRunValueName, false);
                }
                else
                {
                    rk.DeleteValue(RunValueName, false);
                    rk.DeleteValue(LegacyRunValueName, false);
                }
            }
        }

        public static void MigrateAutoStartIfNeeded()
        {
            using var rk = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (rk == null) return;

            var current = rk.GetValue(RunValueName) as string;
            var legacy = rk.GetValue(LegacyRunValueName) as string;

            // Legacy entry varsa yeni ada tasiyip, path'i bu calisan exe'ye guncelle.
            if (string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(legacy))
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrWhiteSpace(exePath))
                    rk.SetValue(RunValueName, $"\"{exePath}\"");

                rk.DeleteValue(LegacyRunValueName, false);
                return;
            }

            // Yeni entry varsa ama path farkliysa (exe tasinmis/yeniden adlandirilmis olabilir), guncelle.
            if (!string.IsNullOrWhiteSpace(current))
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var desired = string.IsNullOrWhiteSpace(exePath) ? "" : $"\"{exePath}\"";
                if (!string.IsNullOrWhiteSpace(desired) &&
                    !string.Equals(current.Trim(), desired, System.StringComparison.OrdinalIgnoreCase))
                {
                    rk.SetValue(RunValueName, desired);
                }
            }
        }

        private static bool ReadBool(string name, bool defaultValue)
        {
            using var k = Registry.CurrentUser.OpenSubKey(RootKeyPath, false);
            var v = k?.GetValue(name);
            if (v is int i) return i == 1;
            return defaultValue;
        }

        private static void WriteBool(string name, bool value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(RootKeyPath);
            k.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }

        private static int ReadInt(string name, int defaultValue)
        {
            using var k = Registry.CurrentUser.OpenSubKey(RootKeyPath, false);
            var v = k?.GetValue(name);
            if (v is int i) return i;
            return defaultValue;
        }

        private static void WriteInt(string name, int value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(RootKeyPath);
            k.SetValue(name, value, RegistryValueKind.DWord);
        }
    }
}
