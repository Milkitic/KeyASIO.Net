using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeyAsio.Utils;

[SuppressUnmanagedCodeSecurity]
[SupportedOSPlatform("windows5.0")]
public static class ConsoleManager
{
    private static PHANDLER_ROUTINE? _handler;

    private static readonly IntPtr OriginalConsoleWindow;
    private static readonly bool IsOriginalConsoleApp;

    static ConsoleManager()
    {
        OriginalConsoleWindow = PInvoke.GetConsoleWindow();
        IsOriginalConsoleApp = OriginalConsoleWindow != IntPtr.Zero;
    }

    public static string? Title { get; set; } = Assembly.GetExecutingAssembly().GetName().Name + " Debugging Console";
    public static ConsoleColor? PromptForeground { get; set; } = ConsoleColor.DarkYellow;
    public static ConsoleColor? PromptBackground { get; set; }
    public static string OpenPrompt { get; set; } = "Note: Closing this window will lead to program exiting.";
    public static string ClosePrompt { get; set; } = "User manually closes debug window. Program will now exit.";

    public static bool HasConsole
    {
        get
        {
            if (IsOriginalConsoleApp)
            {
                return PInvoke.IsWindowVisible((HWND)OriginalConsoleWindow);
            }

            var consoleWindow = PInvoke.GetConsoleWindow();
            return consoleWindow != IntPtr.Zero;
        }
    }

    public static void Show()
    {
        if (HasConsole) return;
        if (IsOriginalConsoleApp)
        {
            PInvoke.ShowWindow((HWND)OriginalConsoleWindow, SHOW_WINDOW_CMD.SW_SHOW);
        }
        else
        {
            CheckSuccess(PInvoke.AllocConsole(), "Failed to alloc console.");
        }

        RebindConsoleStreams();

        if (Title != null) Console.Title = Title;
        if (PromptForeground is { } foreground)
            Console.ForegroundColor = foreground;
        if (PromptBackground is { } background)
            Console.BackgroundColor = background;
        Console.WriteLine(OpenPrompt);
        Console.ResetColor();
        PInvoke.DeleteMenu(
            PInvoke.GetSystemMenu(PInvoke.GetConsoleWindow(), false),
            PInvoke.SC_CLOSE,
            MENU_ITEM_FLAGS.MF_BYCOMMAND);
    }

    public static void Hide()
    {
        if (!HasConsole) return;

        SetOutAndErrorNull();

        if (IsOriginalConsoleApp)
        {
            PInvoke.ShowWindow((HWND)OriginalConsoleWindow, SHOW_WINDOW_CMD.SW_HIDE);
        }
        else
        {
            CheckSuccess(PInvoke.FreeConsole(), "Failed to destroy console.");
        }
    }

    public static void Toggle()
    {
        if (HasConsole)
            Hide();
        else
            Show();
    }

    public static void BindExitAction(Action? exitAction)
    {
        if (exitAction == null || _handler != null) return;
        _handler = dwCtrlType =>
        {
            if (dwCtrlType is not (PInvoke.CTRL_C_EVENT or PInvoke.CTRL_BREAK_EVENT or PInvoke.CTRL_CLOSE_EVENT))
                return false;
            if (PromptForeground is { } foreground)
                Console.ForegroundColor = foreground;
            if (PromptBackground is { } background)
                Console.BackgroundColor = background;
            Console.WriteLine(ClosePrompt);
            Console.ResetColor();
            exitAction();
            return true;
        };
        CheckSuccess(PInvoke.SetConsoleCtrlHandler(_handler, true),
            "Failed to set console control handler.");
    }

    private static void RebindConsoleStreams()
    {
        var stdOut = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding)
        {
            AutoFlush = true
        };
        Console.SetOut(new FilteredTextWriter(stdOut));

        var stdErr = new StreamWriter(Console.OpenStandardError(), Console.OutputEncoding)
        {
            AutoFlush = true
        };
        Console.SetError(stdErr);
    }

    private static void SetOutAndErrorNull()
    {
        Console.Out.Dispose();
        Console.Error.Dispose();

        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
    }

    private static void CheckSuccess(BOOL success, string error)
    {
        if (success) return;
        var errorCode = Marshal.GetLastWin32Error();
        if (errorCode != 0) throw new InvalidOperationException(error, new Win32Exception(errorCode));
    }

    private class FilteredTextWriter : TextWriter
    {
        private readonly TextWriter _inner;

        public FilteredTextWriter(TextWriter inner)
        {
            _inner = inner;
        }

        public override System.Text.Encoding Encoding => _inner.Encoding;

        public override void Write(char value) => _inner.Write(value);

        public override void Write(string? value)
        {
            if (value is "Unsub") return;
            _inner.Write(value);
        }

        public override void WriteLine(string? value)
        {
            if (value is "Unsub") return;
            _inner.WriteLine(value);
        }
    }
}