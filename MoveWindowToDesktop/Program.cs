using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MoveWindowToDesktop;

static class Program
{
    public const string Name = "MoveWindowToDesktop";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\" + Name, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show($"{Name} is already running (look in the notification area).", Name,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

/// <summary>Tray icon plus the hidden hotkey window. The icon exists mainly so the app is discoverable.</summary>
sealed class TrayContext : ApplicationContext
{
    readonly NotifyIcon _icon;
    readonly HotkeyWindow _hotkeys;

    public TrayContext()
    {
        _icon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = Program.Name };

        var menu = new ContextMenuStrip();
        foreach (var binding in Hotkeys.All)
            menu.Items.Add($"{binding.Label}   {binding.Description}").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _icon.ContextMenuStrip = menu;

        _hotkeys = new HotkeyWindow(Warn);
    }

    void Warn(string message) =>
        _icon.ShowBalloonTip(4000, Program.Name, message, ToolTipIcon.Warning);

    protected override void ExitThreadCore()
    {
        _hotkeys.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        base.ExitThreadCore();
    }
}

record Hotkey(int Id, uint Modifiers, uint Key, string Label, string Description, int Delta, bool Follow);

static class Hotkeys
{
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;
    const uint VK_LEFT = 0x25, VK_RIGHT = 0x27;

    // Win+Alt+Arrow is owned by Windows 11 itself (snap layouts), so we sit next to Windows' own
    // Win+Ctrl+Arrow desktop switch: add Alt to take the window with you.
    public static readonly Hotkey[] All =
    [
        new(1, MOD_WIN | MOD_CONTROL | MOD_ALT,             VK_LEFT,  "Win+Ctrl+Alt+Left",        "move window left and follow",  -1, true),
        new(2, MOD_WIN | MOD_CONTROL | MOD_ALT,             VK_RIGHT, "Win+Ctrl+Alt+Right",       "move window right and follow", +1, true),
        new(3, MOD_WIN | MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_LEFT,  "Win+Ctrl+Alt+Shift+Left",  "move window left, stay here",  -1, false),
        new(4, MOD_WIN | MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_RIGHT, "Win+Ctrl+Alt+Shift+Right", "move window right, stay here", +1, false),
    ];
}

/// <summary>Message-only window that owns the RegisterHotKey registrations.</summary>
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;
    readonly Action<string> _warn;

    public HotkeyWindow(Action<string> warn)
    {
        _warn = warn;
        CreateHandle(new CreateParams());

        var failures = new List<string>();
        foreach (var hk in Hotkeys.All)
        {
            if (!Native.RegisterHotKey(Handle, hk.Id, hk.Modifiers | Native.MOD_NOREPEAT, hk.Key))
            {
                var err = Marshal.GetLastWin32Error();
                failures.Add($"{hk.Label}: {new Win32Exception(err).Message} ({err})");
            }
        }
        if (failures.Count > 0)
        {
            MessageBox.Show(
                "Could not register these hotkeys, another program already owns them:\n\n" + string.Join("\n", failures),
                Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            var id = (int)m.WParam;
            var hk = Hotkeys.All.FirstOrDefault(h => h.Id == id);
            if (hk is not null) Mover.MoveForegroundWindow(hk.Delta, hk.Follow, _warn);
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var hk in Hotkeys.All) Native.UnregisterHotKey(Handle, hk.Id);
        DestroyHandle();
    }
}

static class Mover
{
    public static void MoveForegroundWindow(int delta, bool follow, Action<string> warn)
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == 0) return;
        var owner = Native.GetAncestor(hwnd, Native.GA_ROOTOWNER);
        if (owner != 0) hwnd = owner;

        int count = Vda.GetDesktopCount();
        int current = Vda.GetCurrentDesktopNumber();
        if (count <= 0 || current < 0)
        {
            warn("Could not read the virtual desktops. VirtualDesktopAccessor.dll probably needs updating for this Windows build.");
            return;
        }

        int target = current + delta;
        if (target < 0 || target >= count) return; // no desktop that way: do nothing rather than surprise

        if (Vda.MoveWindowToDesktopNumber(hwnd, target) < 0)
        {
            warn("Windows refused to move that window. If it is an elevated window, run this app elevated too.");
            return;
        }

        if (follow)
        {
            Vda.GoToDesktopNumber(target);
            Native.SetForegroundWindow(hwnd);
        }
    }
}

static class Vda
{
    const string Dll = "VirtualDesktopAccessor.dll";
    [DllImport(Dll)] public static extern int GetDesktopCount();
    [DllImport(Dll)] public static extern int GetCurrentDesktopNumber();
    [DllImport(Dll)] public static extern int GoToDesktopNumber(int number);
    [DllImport(Dll)] public static extern int MoveWindowToDesktopNumber(nint hwnd, int number);
}

static class Native
{
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint GA_ROOTOWNER = 3;
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint hWnd, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hWnd);
}
