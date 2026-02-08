using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TrayStatusHelper
{
    public class TrayAppContext : ApplicationContext
    {
        private const string AppName = "Task Status Assistant";

        private readonly Control _ui; // UI thread marshal icin
        private readonly NotifyIcon _tray;
        private readonly Timer _pollTimer;

        private bool _lastCaps;
        private bool _lastNum;
        private bool _fanSim; // Fn+1 ile toggle edilen "fan boost" durumu gibi dusun

        private Keys _fanHotkeyKey;
        private bool _fanHotkeyDown;
        private bool _learnFanHotkey;
        private bool _suppressFanMenuEvent;

        private ToolStripMenuItem? _itemFan;
        private ToolStripMenuItem? _itemFanHotkeyInfo;

        private StatusForm? _statusForm;

        private IntPtr _kbdHookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _kbdHookProc;

        public TrayAppContext()
        {
            _ui = new Control();
            _ui.CreateControl();
            _ = _ui.Handle; // handle'i olustur

            _fanSim = AppSettings.FanSimEnabled;
            _fanHotkeyKey = (Keys)AppSettings.FanHotkeyVKey;

            _tray = new NotifyIcon
            {
                Visible = true,
                Text = AppName,
                ContextMenuStrip = BuildMenu()
            };

            AppSettings.MigrateAutoStartIfNeeded();
            InstallKeyboardHook();

            // İlk durum al
            _lastCaps = Control.IsKeyLocked(Keys.CapsLock);
            _lastNum = Control.IsKeyLocked(Keys.NumLock);

            UpdateTrayIcon();

            // 250ms - hafif ama anlık his
            _pollTimer = new Timer { Interval = 250 };
            _pollTimer.Tick += (_, __) => Tick();
            _pollTimer.Start();
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var itemShow = new ToolStripMenuItem("Durumları Göster");
            itemShow.Click += (_, __) => ShowStatus();

            var itemNotif = new ToolStripMenuItem("Bildirimler")
            {
                Checked = AppSettings.NotificationsEnabled,
                CheckOnClick = true
            };
            itemNotif.CheckedChanged += (_, __) =>
            {
                AppSettings.NotificationsEnabled = itemNotif.Checked;
                ShowStatus(updateOnly: true);
            };

            _itemFan = new ToolStripMenuItem("Fan Aç/Kapat (Fn+1)")
            {
                Checked = _fanSim,
                CheckOnClick = true
            };
            _itemFan.CheckedChanged += (_, __) =>
            {
                if (_suppressFanMenuEvent) return;
                SetFanState(_itemFan.Checked, notify: true);
            };

            var itemFanHotkeyMenu = new ToolStripMenuItem("Fan Kısayolu (Fn+1)");

            var itemFanHotkeySet = new ToolStripMenuItem("Ayarla / Öğren");
            itemFanHotkeySet.Click += (_, __) => BeginLearnFanHotkey();

            var itemFanHotkeyClear = new ToolStripMenuItem("Temizle");
            itemFanHotkeyClear.Click += (_, __) => ClearFanHotkey();

            _itemFanHotkeyInfo = new ToolStripMenuItem(GetFanHotkeyLabel()) { Enabled = false };

            itemFanHotkeyMenu.DropDownItems.Add(itemFanHotkeySet);
            itemFanHotkeyMenu.DropDownItems.Add(itemFanHotkeyClear);
            itemFanHotkeyMenu.DropDownItems.Add(new ToolStripSeparator());
            itemFanHotkeyMenu.DropDownItems.Add(_itemFanHotkeyInfo);

            var itemAutostart = new ToolStripMenuItem("Windows ile Başlat")
            {
                Checked = AppSettings.AutoStartEnabled,
                CheckOnClick = true
            };
            itemAutostart.CheckedChanged += (_, __) =>
            {
                AppSettings.AutoStartEnabled = itemAutostart.Checked;
                ShowStatus(updateOnly: true);
            };

            var sep1 = new ToolStripSeparator();

            var itemExit = new ToolStripMenuItem("Kapat");
            itemExit.Click += (_, __) => Exit();

            menu.Items.Add(itemShow);
            menu.Items.Add(sep1);
            menu.Items.Add(itemNotif);
            menu.Items.Add(_itemFan);
            menu.Items.Add(itemFanHotkeyMenu);
            menu.Items.Add(itemAutostart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemExit);

            return menu;
        }

        private void BeginLearnFanHotkey()
        {
            _learnFanHotkey = true;
            RefreshFanHotkeyInfo(waiting: true);

            MessageBox.Show(
                "Fan kısayolunu ayarlamak için şimdi Fn+1 tuşuna basın.\nİptal: Esc",
                "Fan Kısayolu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ClearFanHotkey()
        {
            _fanHotkeyKey = Keys.None;
            AppSettings.FanHotkeyVKey = 0;
            RefreshFanHotkeyInfo(waiting: false);
            Balloon("Fan Kısayolu", "Temizlendi.");
        }

        private void RefreshFanHotkeyInfo(bool waiting)
        {
            if (_itemFanHotkeyInfo == null) return;
            _itemFanHotkeyInfo.Text = waiting ? "Mevcut: (bekleniyor...)" : GetFanHotkeyLabel();
        }

        private string GetFanHotkeyLabel()
        {
            if (_fanHotkeyKey == Keys.None) return "Mevcut: (ayarlanmadı)";
            return $"Mevcut: {_fanHotkeyKey} (vk {(int)_fanHotkeyKey})";
        }

        private void SetFanState(bool enabled, bool notify)
        {
            if (_fanSim == enabled) return;

            _fanSim = enabled;
            AppSettings.FanSimEnabled = enabled;

            if (_itemFan != null)
            {
                _suppressFanMenuEvent = true;
                _itemFan.Checked = enabled;
                _suppressFanMenuEvent = false;
            }

            UpdateTrayIcon();
            ShowStatus(updateOnly: true);

            if (notify && AppSettings.NotificationsEnabled)
            {
                Balloon(
                    enabled ? "Fan AÇIK" : "Fan KAPALI",
                    enabled ? "Soğutucu fanlar açıldı." : "Soğutucu fanlar kapandı.");
            }
        }

        private void Tick()
        {
            bool caps = Control.IsKeyLocked(Keys.CapsLock);
            bool num = Control.IsKeyLocked(Keys.NumLock);

            bool changed = (caps != _lastCaps) || (num != _lastNum);

            if (changed)
            {
                // Tray ikonunu once guncelle, sonra balloon gonder.
                // Aksi halde surekli NIM_MODIFY (icon/tip) cagrilari balloon'u geciktirebiliyor.
                UpdateTrayIcon();

                // Uyarılar
                if (AppSettings.NotificationsEnabled)
                {
                    // Caps açıldıysa uyar
                    if (!_lastCaps && caps)
                        Balloon("Caps Lock AÇIK", "Yazarken istemeden açılmış olabilir.");

                    // Num kapandıysa uyar
                    if (_lastNum && !num)
                        Balloon("Num Lock KAPALI", "Sayısal tuşlar çalışmayabilir.");
                }

                _lastCaps = caps;
                _lastNum = num;
            }

            // Durum paneli aciksa canli guncelle
            if (_statusForm != null && !_statusForm.IsDisposed && _statusForm.Visible)
                ShowStatus(updateOnly: true);
        }

        private void OnUi(Action action)
        {
            if (_ui.IsHandleCreated && _ui.InvokeRequired)
            {
                _ui.BeginInvoke(action);
                return;
            }

            action();
        }

        private void InstallKeyboardHook()
        {
            _kbdHookProc = KeyboardHookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule == null) return;

            _kbdHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _kbdHookProc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private void UninstallKeyboardHook()
        {
            if (_kbdHookId == IntPtr.Zero) return;
            UnhookWindowsHookEx(_kbdHookId);
            _kbdHookId = IntPtr.Zero;
            _kbdHookProc = null;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var key = (Keys)info.vkCode;

                if (_learnFanHotkey && isKeyDown)
                {
                    if (key == Keys.Escape)
                    {
                        OnUi(() =>
                        {
                            _learnFanHotkey = false;
                            RefreshFanHotkeyInfo(waiting: false);
                            Balloon("Fan Kısayolu", "İptal edildi.");
                        });
                    }
                    else
                    {
                        OnUi(() =>
                        {
                            _learnFanHotkey = false;
                            _fanHotkeyKey = key;
                            AppSettings.FanHotkeyVKey = (int)info.vkCode;
                            RefreshFanHotkeyInfo(waiting: false);
                            Balloon("Fan Kısayolu Kaydedildi", $"{key} (vk {info.vkCode})");
                        });
                    }
                }
                else if (_fanHotkeyKey != Keys.None && key == _fanHotkeyKey)
                {
                    if (isKeyDown)
                    {
                        if (!_fanHotkeyDown)
                        {
                            _fanHotkeyDown = true;
                            OnUi(() => SetFanState(!_fanSim, notify: true));
                        }
                    }
                    else if (isKeyUp)
                    {
                        _fanHotkeyDown = false;
                    }
                }
            }

            return CallNextHookEx(_kbdHookId, nCode, wParam, lParam);
        }

        private void UpdateTrayIcon()
        {
            bool caps = Control.IsKeyLocked(Keys.CapsLock);
            bool num = Control.IsKeyLocked(Keys.NumLock);
            bool fan = _fanSim;

            // Önceki icon handle leak olmasın diye dispose et
            var old = _tray.Icon;
            _tray.Icon = IconFactory.Build(num, caps, fan);
            old?.Dispose();

            _tray.Text = $"{AppName} | C:{(caps ? "On" : "Off")} N:{(num ? "On" : "Off")} F:{(fan ? "On" : "Off")}";
        }

        private void Balloon(string title, string message)
        {
            // Windows 10+ NotifyIcon balloon sınırlı ama yeterli
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = message;
            _tray.ShowBalloonTip(1500);
        }

        private void ShowStatus(bool updateOnly = false)
        {
            bool caps = Control.IsKeyLocked(Keys.CapsLock);
            bool num = Control.IsKeyLocked(Keys.NumLock);

            if (updateOnly && (_statusForm == null || _statusForm.IsDisposed || !_statusForm.Visible))
                return;

            if (_statusForm == null || _statusForm.IsDisposed)
                _statusForm = new StatusForm();

            _statusForm.UpdateText(
                numOn: num,
                capsOn: caps,
                fanOn: _fanSim,
                notifOn: AppSettings.NotificationsEnabled,
                autostartOn: AppSettings.AutoStartEnabled
            );

            if (!updateOnly)
            {
                if (!_statusForm.Visible) _statusForm.Show();
                else _statusForm.BringToFront();
            }
        }

        private void Exit()
        {
            _pollTimer.Stop();
            UninstallKeyboardHook();
            _tray.Visible = false;
            _tray.Icon?.Dispose();
            _tray.Dispose();

            _statusForm?.Close();
            _statusForm?.Dispose();

            _ui.Dispose();

            Application.Exit();
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
