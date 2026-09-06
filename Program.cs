using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyboardClick;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        bool createdNew;
        using (var mutex = new Mutex(true, @"Local\KeyboardClick_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                return; // 既に常駐中
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            using var context = new AppContext();
            Application.Run(context);
        }
    }
}

internal sealed class AppContext : ApplicationContext
{
    private const string HeartbeatFileName = "keyboard-click.heartbeat";
    private const string LogFileName = "keyboard-click.log";

    private readonly NotifyIcon _notify;
    private readonly string _heartbeatPath;
    private readonly string _logPath;
    private readonly ThreadingTimer _heartbeatTimer;
    private readonly ThreadingTimer _trayNudge;
    private readonly Native.LowLevelKeyboardProc _hookDelegate;
    private readonly Icon _iconOk;
    private readonly Icon _iconBad;
    private readonly MainForm? _form;

    private IntPtr _hook = IntPtr.Zero;
    private bool _hookOk;
    private bool _rCtrlDown;
    private bool _rShiftDown;
    private bool _shuttingDown;

    public bool RemapEnabled { get; private set; } = true;

    public AppContext()
    {
        string temp = Path.GetTempPath();
        _heartbeatPath = Path.Combine(temp, HeartbeatFileName);
        _logPath = Path.Combine(temp, LogFileName);

        _iconOk = CreateIcon(Color.FromArgb(0x1B, 0x6E, 0xE8));  // 青 = 稼働中
        _iconBad = CreateIcon(Color.FromArgb(0xE8, 0x11, 0x23)); // 赤 = 停止中

        _notify = new NotifyIcon
        {
            Icon = _iconOk,
            Text = TrayText(),
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("フック再装着", null, (_, _) => ReinstallHook());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => {
            _shuttingDown = true;
            Application.Exit();
        });
        _notify.ContextMenuStrip = menu;
        _notify.DoubleClick += (_, _) => ReinstallHook();

        _hookDelegate = HookCallback;
        ReinstallHook();

        // watchdog 用ハートビート: 15秒毎にタイムスタンプを更新
        _heartbeatTimer = new ThreadingTimer(_ => WriteHeartbeat(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));

        // トレイアイコンの自己修復: シェルに消されたり幽霊化するのを防ぐため30秒毎に再登録
        _trayNudge = new ThreadingTimer(_ => NudgeTray(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _form = new MainForm(this);
        _form.Show();

        WriteLog("起動しました (PID=" + Environment.ProcessId + ")");
    }

    public void ToggleRemap()
    {
        RemapEnabled = !RemapEnabled;
        if (!RemapEnabled && _rCtrlDown)
        {
            _rCtrlDown = false;
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP);
        }
        if (RemapEnabled && _hook == IntPtr.Zero)
        {
            ReinstallHook();
        }
        _notify.Icon = _hookOk ? _iconOk : _iconBad;
        _notify.Text = TrayText();
        _form?.RefreshState();
        WriteLog("リマップ(" + (RemapEnabled ? "ON" : "OFF") + ")");
    }

    public void RequestExit()
    {
        _shuttingDown = true;
        Application.Exit();
    }

    private void NudgeTray()
    {
        if (_shuttingDown)
        {
            return;
        }
        try
        {
            _notify.Visible = false;
            _notify.Visible = true;
        }
        catch { }
    }

    private void ReinstallHook()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        if (_shuttingDown)
        {
            return;
        }

        using (var proc = Process.GetCurrentProcess())
        {
            string? moduleName = null;
            try { moduleName = proc.MainModule?.ModuleName; } catch { }
            _hook = Native.SetWindowsHookEx(
                Native.WH_KEYBOARD_LL,
                _hookDelegate,
                Native.GetModuleHandle(moduleName),
                0);
        }

        if (_hook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            WriteLog("キーボードフック失敗 (err=" + err + ")");
        }
        else
        {
            WriteLog("キーボードフック装着成功");
        }

        _hookOk = _hook != IntPtr.Zero;
        _notify.Icon = _hookOk ? _iconOk : _iconBad;
        _notify.Text = TrayText();
        _form?.RefreshState();

    }

    public void ReinstallHookNow()
    {
        ReinstallHook();
        _form?.RefreshState();
    }

    private string TrayText()
    {
        if (!RemapEnabled)
        {
            return "KeyboardClick — リマップ停止中 (OFF)";
        }
        return _hookOk
            ? "KeyboardClick — 稼働中 (右Ctrl=左クリック / 右Shift=右クリック)"
            : "KeyboardClick — 停止中 (フック未装着)";
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && RemapEnabled)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            // 他のツールが注入したキーは無視してループ防止
            if ((data.flags & Native.LLKHF_INJECTED) == 0)
            {
                long msg = wParam.ToInt64();
                bool isDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                bool isUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

                if (isDown && data.vkCode == Native.VK_RCONTROL && !_rCtrlDown)
                {
                    _rCtrlDown = true;
                    WriteLog("RCtrl Down -> LEFTDOWN");
                    Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN);
                }
                else if (isUp && data.vkCode == Native.VK_RCONTROL && _rCtrlDown)
                {
                    _rCtrlDown = false;
                    WriteLog("RCtrl Up -> LEFTUP");
                    Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP);
                }
                else if (data.vkCode is Native.VK_RCONTROL or Native.VK_RSHIFT)
                {
                    // 対象キーは下で消費する判定
                }

                if (data.vkCode == Native.VK_RSHIFT)
                {
                    if (isDown && !_rShiftDown)
                    {
                        // 長押しの自動リピートでは再発火しない (AHK と同じ挙動)
                        _rShiftDown = true;
                        WriteLog("RShift -> RIGHTCLICK");
                        Native.MouseEvent(Native.MOUSEEVENTF_RIGHTDOWN);
                        Native.MouseEvent(Native.MOUSEEVENTF_RIGHTUP);
                    }
                    else if (isUp)
                    {
                        _rShiftDown = false;
                    }
                    return new IntPtr(1); // 消費してアプリ側に渡さない
                }

                if (data.vkCode == Native.VK_RCONTROL)
                {
                    return new IntPtr(1); // 消費
                }
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void WriteHeartbeat()
    {
        try
        {
            File.WriteAllText(_heartbeatPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }
        catch { }
    }

    private void WriteLog(string message)
    {
        try
        {
            File.AppendAllText(_logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        catch { }
    }

    private static Icon CreateIcon(Color main)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var body = new SolidBrush(main);
            using var wire = new Pen(Color.White, 1.3f);
            g.FillEllipse(body, 2, 1, 12, 13);
            g.DrawLine(wire, 8, 3, 8, 12);
            g.DrawArc(wire, 4, 6, 8, 6, 150, 220);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        _shuttingDown = true;

        if (_rCtrlDown)
        {
            _rCtrlDown = false;
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP);
        }

        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _heartbeatTimer?.Dispose();
        _trayNudge.Dispose();
        _form?.Dispose();
        _notify?.Dispose();
        if (_iconOk != null) { Native.DestroyIcon(_iconOk.Handle); _iconOk.Dispose(); }
        if (_iconBad != null) { Native.DestroyIcon(_iconBad.Handle); _iconBad.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class ThreadingTimer : IDisposable
    {
        private readonly System.Threading.Timer _timer;

        public ThreadingTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period)
        {
            _timer = new System.Threading.Timer(callback, state, due, period);
        }

        public void Dispose() => _timer.Dispose();
    }
}

internal sealed class MainForm : Form
{
    private readonly AppContext _owner;
    private readonly Label _statusLabel;
    private readonly Button _toggleButton;
    private readonly Button _reinstallButton;
    private readonly Button _exitButton;

    public MainForm(AppContext owner)
    {
        _owner = owner;

        Text = "KeyboardClick";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ShowInTaskbar = true;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(300, 180);

        _statusLabel = new Label
        {
            Location = new Point(12, 10),
            Size = new Size(276, 50)
        };

        _toggleButton = new Button
        {
            Location = new Point(12, 64),
            Size = new Size(276, 32)
        };
        _toggleButton.Click += (_, _) => _owner.ToggleRemap();

        _reinstallButton = new Button
        {
            Location = new Point(12, 102),
            Size = new Size(276, 32),
            Text = "フック再装着"
        };
        _reinstallButton.Click += (_, _) => _owner.ReinstallHookNow();

        _exitButton = new Button
        {
            Location = new Point(12, 140),
            Size = new Size(276, 32),
            Text = "終了"
        };
        _exitButton.Click += (_, _) => _owner.RequestExit();

        Controls.Add(_statusLabel);
        Controls.Add(_toggleButton);
        Controls.Add(_reinstallButton);
        Controls.Add(_exitButton);

        // プライマリ画面の右下へ
        var wa = Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty;
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);

        RefreshState();
    }

    public void RefreshState()
    {
        bool on = _owner.RemapEnabled;
        _statusLabel.Text = on
            ? "右Ctrl = 左クリック / ドラッグ\n右Shift = 右クリック"
            : "リマップ停止中 — 右Ctrl/右Shift は通常キーとして動作";
        _toggleButton.Text = on ? "停止する (OFF)" : "開始する (ON)";
        _toggleButton.BackColor = on ? Color.FromArgb(0x48, 0xC3, 0x7C) : Color.FromArgb(0xF4, 0x9E, 0x9E);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 窓を閉じたらアプリ終了 (トレイの奥に隠れさせない)
        _owner.RequestExit();
    }
}

internal static class Native
{
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    public const uint VK_RCONTROL = 0xA3;
    public const uint VK_RSHIFT = 0xA1;

    public const uint LLKHF_INJECTED = 0x10;

    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public static void MouseEvent(uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT { dwFlags = flags }
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}