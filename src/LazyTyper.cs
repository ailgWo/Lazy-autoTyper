using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LazyTyper
{
    internal static class Native
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint LLKHF_INJECTED = 0x00000010;

        public const int VK_ESCAPE = 0x1B;
        public const int VK_RETURN = 0x0D;
        public const int VK_TAB = 0x09;
        public const int VK_F8 = 0x77;
        public const int VK_F9 = 0x78;

        public static readonly UIntPtr InjectionMarker = new UIntPtr(0x4C545950); // LTYP

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static bool SendUnicode(char c)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = 1;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = (ushort)c;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].U.ki.dwExtraInfo = InjectionMarker;
            inputs[1].type = 1;
            inputs[1].U.ki.wVk = 0;
            inputs[1].U.ki.wScan = (ushort)c;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].U.ki.dwExtraInfo = InjectionMarker;
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }

        public static bool SendVirtualKey(ushort vk)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = 1;
            inputs[0].U.ki.wVk = vk;
            inputs[0].U.ki.wScan = 0;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.dwExtraInfo = InjectionMarker;
            inputs[1].type = 1;
            inputs[1].U.ki.wVk = vk;
            inputs[1].U.ki.wScan = 0;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].U.ki.dwExtraInfo = InjectionMarker;
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }
    }

    internal sealed class HudForm : Form
    {
        private enum RunMode
        {
            Idle,
            PerKey,
            Auto,
            Done
        }

        private const int HotkeyPerKey = 1;
        private const int HotkeyAuto = 2;
        private const int HotkeyHud = 3;
        private const int WM_HOTKEY = 0x0312;

        private static readonly Color BgTop = Color.FromArgb(18, 21, 15);
        private static readonly Color BgBottom = Color.FromArgb(10, 13, 9);
        private static readonly Color BorderColor = Color.FromArgb(55, 64, 47);
        private static readonly Color Ink = Color.FromArgb(238, 241, 231);
        private static readonly Color Muted = Color.FromArgb(144, 154, 134);
        private static readonly Color Dim = Color.FromArgb(91, 101, 86);
        private static readonly Color Lime = Color.FromArgb(215, 255, 87);
        private static readonly Color Amber = Color.FromArgb(255, 196, 107);
        private static readonly Color ErrorColor = Color.FromArgb(255, 137, 118);

        private RunMode _mode = RunMode.Idle;
        private string _text = "";
        private int _index = 0;
        private string _status = "复制文字，选中目标，按 F8 开始乱打。";
        private bool _statusIsError = false;
        private IntPtr _targetWindow = IntPtr.Zero;
        private int _autoWarmup = 0;

        private readonly System.Windows.Forms.Timer _autoTimer;
        private NotifyIcon _tray;
        private Bitmap _trayBitmap;
        private Icon _trayIcon;
        private IntPtr _trayIconHandle = IntPtr.Zero;

        private IntPtr _hook = IntPtr.Zero;
        private Native.LowLevelKeyboardProc _hookProc;
        private bool _hookReady = false;
        private readonly HashSet<int> _suppressedKeys = new HashSet<int>();
        private bool _injecting = false;

        private bool _perKeyHotkey = false;
        private bool _autoHotkey = false;
        private bool _hudHotkey = false;
        private string _perKeyHotkeyText = "F8";
        private string _autoHotkeyText = "F9";
        private string _hudHotkeyText = "Ctrl+Alt+H";

        private bool _startHidden;
        private bool _hideTipShown;
        private ToolStripMenuItem _hudMenuItem;

        private bool _dragging = false;
        private Point _dragPointerOrigin;
        private Point _dragFormOrigin;
        private bool _hoverClose = false;
        private bool _closePressed = false;
        private Rectangle _closeRect;

        private readonly Font _titleFont;
        private readonly Font _monoSmall;
        private readonly Font _statusFont;
        private readonly Font _bodyFont;
        private readonly Font _charFont;

        public HudForm(bool startHidden)
        {
            _startHidden = startHidden;
            Text = "摸鱼打字机";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = BgBottom;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(420, 158);
            if (startHidden) Opacity = 0;

            _titleFont = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold, GraphicsUnit.Point);
            _monoSmall = new Font("Consolas", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _statusFont = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point);
            _bodyFont = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Regular, GraphicsUnit.Point);
            _charFont = new Font("Consolas", 9.5f, FontStyle.Bold, GraphicsUnit.Point);

            _autoTimer = new System.Windows.Forms.Timer();
            _autoTimer.Interval = 22;
            _autoTimer.Tick += AutoTimerTick;

            SetStartLocation();
            _closeRect = new Rectangle(Width - 36, 13, 24, 24);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private void SetStartLocation()
        {
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = work.Right - Width - 24;
            int y = work.Bottom - Height - 24;
            if (x < work.Left) x = work.Left + 8;
            if (y < work.Top) y = work.Top + 8;
            Location = new Point(x, y);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InstallKeyboardHook();
            SetupTrayIcon();
            UpdateTrayStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_startHidden)
            {
                _startHidden = false;
                Hide();
                Opacity = 1;
                UpdateHudMenuItem();
                UpdateTrayStatus();
            }
            else
            {
                BringHudToFront();
                UpdateHudMenuItem();
                UpdateTrayStatus();
            }
        }

        private void BringHudToFront()
        {
            if (Handle == IntPtr.Zero || !Visible) return;
            // HWND_TOPMOST, keep position/size and keyboard focus.
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                0x0001 | 0x0002 | 0x0010);
        }

        private void HideHud(bool showTip)
        {
            if (!Visible) return;
            Hide();
            UpdateHudMenuItem();
            UpdateTrayStatus();
            if (showTip && !_hideTipShown && _tray != null)
            {
                _hideTipShown = true;
                _tray.ShowBalloonTip(1800, "摸鱼打字机",
                    "已藏到托盘。Ctrl+Alt+H 召回，托盘菜单可退出。", ToolTipIcon.None);
            }
        }

        private void ShowHud()
        {
            if (!Visible)
            {
                Show();
            }
            BringHudToFront();
            UpdateHudMenuItem();
            UpdateTrayStatus();
            Invalidate();
        }

        private void ToggleHud()
        {
            if (Visible) HideHud(true);
            else ShowHud();
        }

        private void UpdateHudMenuItem()
        {
            if (_hudMenuItem != null)
            {
                _hudMenuItem.Text = (Visible ? "隐藏 HUD 到托盘" : "显示 HUD") +
                    (_hudHotkey ? " (" + _hudHotkeyText + ")" : "");
            }
        }

        private void UpdateTrayStatus()
        {
            if (_tray == null) return;
            string status;
            if (_mode == RunMode.Idle)
            {
                status = "待机";
            }
            else if (_mode == RunMode.Done)
            {
                status = "完成";
            }
            else
            {
                status = (_mode == RunMode.PerKey ? "乱打" : "自动") + " " + _index + "/" + _text.Length;
            }
            string text = "摸鱼打字机 · " + status;
            if (_mode == RunMode.PerKey || _mode == RunMode.Auto)
            {
                text += " · Esc 停";
            }
            else if (!Visible)
            {
                text += " · Ctrl+Alt+H 显示";
            }
            if (text.Length > 63) text = text.Substring(0, 63);
            _tray.Text = text;
        }

        private void ShowHiddenNotice(string message)
        {
            if (!Visible && _tray != null)
            {
                try
                {
                    _tray.ShowBalloonTip(1800, "摸鱼打字机", message, ToolTipIcon.Warning);
                }
                catch (Exception)
                {
                }
            }
        }

        private void RegisterHotkeys()
        {
            _perKeyHotkey = Native.RegisterHotKey(Handle, HotkeyPerKey, Native.MOD_NOREPEAT, Native.VK_F8);
            if (!_perKeyHotkey)
            {
                _perKeyHotkey = Native.RegisterHotKey(Handle, HotkeyPerKey,
                    Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.L);
                _perKeyHotkeyText = _perKeyHotkey ? "Ctrl+Alt+L" : "托盘菜单";
            }

            _autoHotkey = Native.RegisterHotKey(Handle, HotkeyAuto, Native.MOD_NOREPEAT, Native.VK_F9);
            if (!_autoHotkey)
            {
                _autoHotkey = Native.RegisterHotKey(Handle, HotkeyAuto,
                    Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.A);
                _autoHotkeyText = _autoHotkey ? "Ctrl+Alt+A" : "托盘菜单";
            }

            _hudHotkey = Native.RegisterHotKey(Handle, HotkeyHud,
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.H);
            if (!_hudHotkey) _hudHotkeyText = "托盘菜单";

            if (!_perKeyHotkey && !_autoHotkey)
            {
                _status = "F8 / F9 都被占用了，请从右下角托盘菜单启动。";
                _statusIsError = true;
            }
            else if (!_statusIsError && _mode == RunMode.Idle)
            {
                _status = "复制文字，选中目标，按 " + _perKeyHotkeyText + " 开始乱打。";
            }
        }

        private void SetupTrayIcon()
        {
            _trayIcon = BuildTrayIcon();
            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "摸鱼打字机 · " + _perKeyHotkeyText + " 乱打 / " + _autoHotkeyText + " 自动";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            menu.Items.Add("开始 / 停止乱打 (" + _perKeyHotkeyText + ")", null, delegate { TogglePerKey(); });
            menu.Items.Add("开始 / 停止自动 (" + _autoHotkeyText + ")", null, delegate { ToggleAuto(); });
            menu.Items.Add("停止 (Esc)", null, delegate { StopActive("已停止。"); });
            menu.Items.Add(new ToolStripSeparator());
            _hudMenuItem = new ToolStripMenuItem("隐藏 HUD 到托盘 (" + _hudHotkeyText + ")", null,
                delegate { ToggleHud(); });
            menu.Items.Add(_hudMenuItem);
            menu.Items.Add("重新读取剪贴板", null, delegate { ReloadClipboardForIdle(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出摸鱼打字机", null, delegate { Close(); });
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ToggleHud();
            };
            UpdateHudMenuItem();
        }

        private Icon BuildTrayIcon()
        {
            _trayBitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(_trayBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Lime))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }
                using (Font font = new Font("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("{}", font, Brushes.Black, new RectangleF(0, 0, 32, 32), sf);
                }
            }
            _trayIconHandle = _trayBitmap.GetHicon();
            return Icon.FromHandle(_trayIconHandle);
        }

        private void InstallKeyboardHook()
        {
            _hookProc = new Native.LowLevelKeyboardProc(KeyboardProc);
            _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc,
                Native.GetModuleHandle(null), 0);
            _hookReady = _hook != IntPtr.Zero;
            if (!_hookReady)
            {
                _status = "键盘拦截启动失败。仍可尝试 F9 自动模式。";
                _statusIsError = true;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyPerKey)
                {
                    TogglePerKey();
                    return;
                }
                if (id == HotkeyAuto)
                {
                    ToggleAuto();
                    return;
                }
                if (id == HotkeyHud)
                {
                    ToggleHud();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private bool ReadClipboard()
        {
            string text = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    }
                    else if (Clipboard.ContainsText())
                    {
                        text = Clipboard.GetText();
                    }
                    break;
                }
                catch (Exception)
                {
                    System.Threading.Thread.Sleep(35);
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                _status = "剪贴板是空的。先复制一段文字再来摸鱼。";
                _statusIsError = true;
                Invalidate();
                UpdateTrayStatus();
                ShowHiddenNotice("剪贴板是空的，先复制一段文字。");
                return false;
            }

            _text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            _index = 0;
            return true;
        }

        private void StartPerKey()
        {
            if (!_hookReady)
            {
                _status = "键盘拦截没有启动，乱打模式不可用。试试 F9 自动模式。";
                _statusIsError = true;
                Invalidate();
                UpdateTrayStatus();
                ShowHiddenNotice("键盘拦截没有启动，乱打模式不可用。");
                return;
            }
            if (!ReadClipboard()) return;

            IntPtr target = Native.GetForegroundWindow();
            if (target == IntPtr.Zero)
            {
                _status = "找不到当前窗口。先点一下要输入的地方。";
                _statusIsError = true;
                Invalidate();
                UpdateTrayStatus();
                ShowHiddenNotice("找不到当前窗口，先点一下目标输入框。");
                return;
            }

            _targetWindow = target;
            _mode = RunMode.PerKey;
            _autoTimer.Stop();
            _status = "乱打模式 ON · 随便按，每次一个字。";
            _statusIsError = false;
            _suppressedKeys.Clear();
            BringHudToFront();
            UpdateTrayStatus();
            Invalidate();
        }

        private void StartAuto()
        {
            if (!ReadClipboard()) return;

            IntPtr target = Native.GetForegroundWindow();
            if (target == IntPtr.Zero)
            {
                _status = "找不到当前窗口。先点一下要输入的地方。";
                _statusIsError = true;
                Invalidate();
                UpdateTrayStatus();
                ShowHiddenNotice("找不到当前窗口，先点一下目标输入框。");
                return;
            }

            _targetWindow = target;
            _mode = RunMode.Auto;
            _autoWarmup = 10;
            _status = "自动模式 ON · 逐字写入中，Esc 停止。";
            _statusIsError = false;
            _suppressedKeys.Clear();
            _autoTimer.Start();
            BringHudToFront();
            UpdateTrayStatus();
            Invalidate();
        }

        private void TogglePerKey()
        {
            if (_mode == RunMode.PerKey)
            {
                StopActive("已停止。");
                return;
            }
            if (_mode == RunMode.Auto)
            {
                StopActive("已停止自动模式。");
            }
            StartPerKey();
        }

        private void ToggleAuto()
        {
            if (_mode == RunMode.Auto)
            {
                StopActive("已停止。");
                return;
            }
            if (_mode == RunMode.PerKey)
            {
                StopActive("已停止乱打模式。");
            }
            StartAuto();
        }

        private void StopActive(string message)
        {
            _mode = RunMode.Idle;
            _autoTimer.Stop();
            _targetWindow = IntPtr.Zero;
            _status = message;
            _statusIsError = false;
            _suppressedKeys.Clear();
            UpdateTrayStatus();
            Invalidate();
        }

        private void ReloadClipboardForIdle()
        {
            if (_mode != RunMode.Idle)
            {
                StopActive("已重新读取剪贴板。按 F8 / F9 开始。");
            }
            if (ReadClipboard())
            {
                _status = "剪贴板已重读，共 " + _text.Length + " 个字符。";
                _statusIsError = false;
                UpdateTrayStatus();
                Invalidate();
            }
        }

        private void AutoTimerTick(object sender, EventArgs e)
        {
            if (_mode != RunMode.Auto)
            {
                _autoTimer.Stop();
                return;
            }
            if (_autoWarmup > 0)
            {
                _autoWarmup--;
                return;
            }
            AdvanceOne();
        }

        private bool IsTargetStillFocused()
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground != IntPtr.Zero && foreground != _targetWindow)
            {
                StopActive("目标窗口已切换，已自动停止。");
                return false;
            }
            return true;
        }

        private void AdvanceOne()
        {
            if (_mode != RunMode.PerKey && _mode != RunMode.Auto) return;
            if (!IsTargetStillFocused()) return;
            if (_index >= _text.Length)
            {
                CompleteRun();
                return;
            }

            char c = _text[_index];
            _index++;
            bool sent;
            _injecting = true;
            try
            {
                if (c == '\n') sent = Native.SendVirtualKey((ushort)Native.VK_RETURN);
                else if (c == '\t') sent = Native.SendVirtualKey((ushort)Native.VK_TAB);
                else sent = Native.SendUnicode(c);
            }
            catch (Exception)
            {
                sent = false;
            }
            finally
            {
                _injecting = false;
            }

            if (!sent)
            {
                StopActive("系统拒绝了输入。目标可能以管理员权限运行。");
                _statusIsError = true;
                Invalidate();
                return;
            }

            if (_index >= _text.Length)
            {
                CompleteRun();
                return;
            }

            UpdateTrayStatus();
            Invalidate();
        }

        private void CompleteRun()
        {
            _autoTimer.Stop();
            _mode = RunMode.Done;
            _targetWindow = IntPtr.Zero;
            _status = "完成 · 按 Esc 交还键盘。";
            _statusIsError = false;
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception)
            {
            }
            UpdateTrayStatus();
            Invalidate();
        }

        private bool IsPassThroughKey(int vk)
        {
            // Modifiers, lock keys and function keys stay native. This keeps
            // the F8/F9 toggle hotkeys reachable and lets users escape.
            if (vk == 0x10 || vk == 0x11 || vk == 0x12 ||
                vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3 ||
                vk == 0xA4 || vk == 0xA5 || vk == 0x5B || vk == 0x5C ||
                vk == 0x14 || vk == 0x90 || vk == 0x91 ||
                vk == 0x2C || vk == 0x13)
            {
                return true;
            }
            if (vk >= 0x70 && vk <= 0x87) return true; // F1 - F24

            bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool alt = (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool win = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
                       (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
            return ctrl || alt || win;
        }

        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    Native.KBDLLHOOKSTRUCT data = (Native.KBDLLHOOKSTRUCT)
                        Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    uint msg = (uint)wParam.ToInt32();
                    bool isDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                    bool isUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

                    if (data.dwExtraInfo.ToUInt64() == Native.InjectionMarker.ToUInt64())
                    {
                        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                    }
                    if (_injecting)
                    {
                        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                    }

                    int vk = (int)data.vkCode;

                    if (_suppressedKeys.Contains(vk) && isUp)
                    {
                        _suppressedKeys.Remove(vk);
                        return (IntPtr)1;
                    }

                    if (_mode != RunMode.Idle)
                    {
                        if (vk == Native.VK_ESCAPE)
                        {
                            if (isDown) StopActive("已停止。");
                            return (IntPtr)1;
                        }

                        if (isDown)
                        {
                            if (IsPassThroughKey(vk))
                            {
                                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                            }

                            _suppressedKeys.Add(vk);
                            if (_mode == RunMode.PerKey)
                            {
                                AdvanceOne();
                            }
                            return (IntPtr)1;
                        }

                        if (isUp)
                        {
                            if (IsPassThroughKey(vk))
                            {
                                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
                            }
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void LogError(Exception ex)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lazy-typer.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle full = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(full, 14))
            using (LinearGradientBrush fill = new LinearGradientBrush(full, BgTop, BgBottom, 90f))
            using (Pen border = new Pen(BorderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Color accent = _mode == RunMode.PerKey ? Lime : (_mode == RunMode.Auto ? Amber : (_mode == RunMode.Done ? Lime : Color.FromArgb(98, 106, 92)));
            if (_statusIsError) accent = ErrorColor;

            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                g.FillEllipse(accentBrush, 23, 20, 7, 7);
            }

            using (SolidBrush inkBrush = new SolidBrush(Ink))
            using (SolidBrush mutedBrush = new SolidBrush(Muted))
            using (SolidBrush dimBrush = new SolidBrush(Dim))
            using (SolidBrush accentBrush = new SolidBrush(accent))
            {
                g.DrawString("摸鱼打字机", _titleFont, inkBrush, 37f, 12f);
                g.DrawString("LAZY TYPER", _monoSmall, dimBrush, Width - 124f, 18f);

                Rectangle statusRect = new Rectangle(24, 46, Width - 150, 22);
                using (StringFormat sf = new StringFormat())
                {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(_status, _statusFont, _statusIsError ? accentBrush : mutedBrush, statusRect, sf);
                }

                if (_mode != RunMode.Idle)
                {
                    string modeText = _mode == RunMode.PerKey ? "ON · 逐键" : (_mode == RunMode.Auto ? "ON · 自动" : "DONE");
                    using (SolidBrush bgPill = new SolidBrush(Color.FromArgb(28, 32, 22)))
                    using (Pen pillPen = new Pen(Color.FromArgb(70, accent), 1f))
                    using (StringFormat sf = new StringFormat())
                    {
                        SizeF size = g.MeasureString(modeText, _monoSmall);
                        RectangleF pill = new RectangleF(Width - size.Width - 48f, 49f, size.Width + 14f, 17f);
                        using (GraphicsPath pillPath = RoundedRect(Rectangle.Round(pill), 8))
                        {
                            g.FillPath(bgPill, pillPath);
                            g.DrawPath(pillPen, pillPath);
                        }
                        g.DrawString(modeText, _monoSmall, accentBrush, pill.X + 7f, pill.Y + 3f);
                    }
                }

                // progress
                RectangleF track = new RectangleF(24, 83, Width - 48, 4);
                using (GraphicsPath trackPath = RoundedRect(Rectangle.Round(track), 2))
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(36, 43, 30)))
                {
                    g.FillPath(trackBrush, trackPath);
                }

                float progress = _text.Length > 0 ? (float)_index / (float)_text.Length : 0f;
                if (progress > 0f)
                {
                    float fillWidth = Math.Max(4f, track.Width * Math.Min(1f, progress));
                    RectangleF fillRect = new RectangleF(track.X, track.Y, fillWidth, track.Height);
                    using (GraphicsPath fillPath = RoundedRect(Rectangle.Round(fillRect), 2))
                    using (SolidBrush fillBrush = new SolidBrush(accent))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }

                if (_text.Length > 0)
                {
                    g.DrawString(_index + " / " + _text.Length + " 字符", _monoSmall, dimBrush, 24f, 97f);
                    if (_mode != RunMode.Idle && _index < _text.Length)
                    {
                        char next = _text[_index];
                        string nextDisplay = next == '\n' ? "↵" : (next == '\t' ? "⇥" : (next == ' ' ? "␠" : next.ToString()));
                        SizeF labelSize = g.MeasureString("下一个", _bodyFont);
                        float boxW = 27f;
                        float boxH = 22f;
                        float boxX = Width - 24f - boxW;
                        float boxY = 88f;
                        g.DrawString("下一个", _bodyFont, dimBrush, boxX - labelSize.Width - 7f, boxY + 4f);

                        Rectangle boxRect = Rectangle.Round(new RectangleF(boxX, boxY, boxW, boxH));
                        using (GraphicsPath boxPath = RoundedRect(boxRect, 5))
                        using (SolidBrush boxFill = new SolidBrush(Color.FromArgb(27, 33, 21)))
                        using (Pen boxPen = new Pen(Color.FromArgb(90, accent), 1f))
                        {
                            g.FillPath(boxFill, boxPath);
                            g.DrawPath(boxPen, boxPath);
                        }
                        SizeF charSize = g.MeasureString(nextDisplay, _charFont);
                        g.DrawString(nextDisplay, _charFont, accentBrush,
                            boxX + (boxW - charSize.Width) / 2f,
                            boxY + (boxH - charSize.Height) / 2f - 1f);
                    }
                }
                else
                {
                    g.DrawString("剪贴板待命", _monoSmall, dimBrush, 24f, 97f);
                }

                using (Pen divider = new Pen(Color.FromArgb(36, 43, 32), 1f))
                {
                    g.DrawLine(divider, 24f, 121f, Width - 24f, 121f);
                }

                string hints = _perKeyHotkeyText + " 乱打   ·   " + _autoHotkeyText + " 自动   ·   Esc 停止";
                g.DrawString(hints, _bodyFont, dimBrush, 24f, 130f);
                string hideHint = _hudHotkey ? "Ctrl+Alt+H 藏起" : "点击托盘图标";
                SizeF hideSize = g.MeasureString(hideHint, _monoSmall);
                g.DrawString(hideHint, _monoSmall, dimBrush, Width - 24f - hideSize.Width, 132f);
            }

            if (_mode != RunMode.Idle)
            {
                using (Pen accentPen = new Pen(Color.FromArgb(95, accent), 2f))
                {
                    g.DrawLine(accentPen, 24f, Height - 1f, Width - 24f, Height - 1f);
                }
            }

            // hide-to-tray affordance
            Color hideColor = _hoverClose ? Amber : Dim;
            using (Pen hidePen = new Pen(hideColor, 1.8f))
            {
                int cx = _closeRect.X + 8;
                int cy = _closeRect.Y + 11;
                g.DrawLine(hidePen, cx, cy, cx + 8, cy);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 14))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
            _closeRect = new Rectangle(Width - 36, 13, 24, 24);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_closeRect.Contains(e.Location))
            {
                _closePressed = true;
                _hoverClose = true;
                Invalidate();
                return;
            }
            _dragging = true;
            _dragPointerOrigin = Cursor.Position;
            _dragFormOrigin = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point now = Cursor.Position;
                int x = _dragFormOrigin.X + now.X - _dragPointerOrigin.X;
                int y = _dragFormOrigin.Y + now.Y - _dragPointerOrigin.Y;
                Rectangle work = Screen.FromPoint(now).WorkingArea;
                int maxX = work.Right - Width;
                int maxY = work.Bottom - Height;
                if (x < work.Left) x = work.Left;
                if (x > maxX) x = maxX;
                if (y < work.Top) y = work.Top;
                if (y > maxY) y = maxY;
                Location = new Point(x, y);
            }

            bool hover = _closeRect.Contains(e.Location);
            if (hover != _hoverClose)
            {
                _hoverClose = hover;
                Invalidate(_closeRect);
            }
            Cursor = hover ? Cursors.Hand : Cursors.SizeAll;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_closePressed)
            {
                bool shouldHide = _closeRect.Contains(e.Location);
                _closePressed = false;
                if (shouldHide)
                {
                    HideHud(true);
                    return;
                }
                Invalidate();
            }
            _dragging = false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _autoTimer.Stop();
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            if (_perKeyHotkey)
            {
                Native.UnregisterHotKey(Handle, HotkeyPerKey);
                _perKeyHotkey = false;
            }
            if (_autoHotkey)
            {
                Native.UnregisterHotKey(Handle, HotkeyAuto);
                _autoHotkey = false;
            }
            if (_hudHotkey)
            {
                Native.UnregisterHotKey(Handle, HotkeyHud);
                _hudHotkey = false;
            }
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            if (_trayIconHandle != IntPtr.Zero)
            {
                Native.DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }
            if (_trayBitmap != null)
            {
                _trayBitmap.Dispose();
                _trayBitmap = null;
            }
            _titleFont.Dispose();
            _monoSmall.Dispose();
            _statusFont.Dispose();
            _bodyFont.Dispose();
            _charFont.Dispose();
            base.OnFormClosing(e);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startHidden = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--hidden", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--tray", StringComparison.OrdinalIgnoreCase))
                    {
                        startHidden = true;
                    }
                }
            }

            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "LazyTyper.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("摸鱼打字机已经在运行了。", "摸鱼打字机",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    try
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lazy-typer.log");
                        File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.Exception + Environment.NewLine);
                    }
                    catch (Exception)
                    {
                    }
                    MessageBox.Show("摸鱼打字机出了一点问题：" + Environment.NewLine + e.Exception.Message,
                        "摸鱼打字机", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };

                Application.Run(new HudForm(startHidden));
            }
        }
    }
}
