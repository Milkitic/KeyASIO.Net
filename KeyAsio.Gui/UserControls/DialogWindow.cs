using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;
using KeyAsio.Gui.Utils;

namespace KeyAsio.Gui.UserControls;

public class DialogWindow : FrameWindow
{
    private readonly DialogFrame _frame;

    public static readonly DependencyProperty ToolBarContentProperty = DependencyProperty.Register(nameof(ToolBarContent),
        typeof(object),
        typeof(DialogWindow),
        new PropertyMetadata(default(object), (d, e) =>
        {
            if (d is DialogWindow w && w.Frame != null)
            {
                w.Frame.ToolBarContent = e.NewValue;
            }
        }));

    public object ToolBarContent
    {
        get => GetValue(ToolBarContentProperty);
        set => SetValue(ToolBarContentProperty, value);
    }

    // ReSharper disable IdentifierTypo
    // ReSharper disable InconsistentNaming
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo

    public DialogWindow() : base(new DialogFrame())
    {
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 31,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(1),
            ResizeBorderThickness = new Thickness(4),
            NonClientFrameEdges = NonClientFrameEdges.None,
            UseAeroCaptionButtons = false
        });
        StateChanged += DialogWindow_StateChanged;
        _frame = (DialogFrame)Frame!;
        //SourceInitialized += DialogWindow_SourceInitialized;
    }

    private void DialogWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            _frame.BaseGrid.Margin = new Thickness(7);
        }
        else
        {
            _frame.BaseGrid.Margin = new Thickness(0);
        }
    }

    private void DialogWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        source?.AddHook(WndProc);
    }

    //https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowpos
    //https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-windowposchanging
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Owner is not null && msg == WM_WINDOWPOSCHANGING)
        {
            var wp = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            //Console.WriteLine(JsonConvert.SerializeObject(wp));
            if (wp.x < Owner.Left)
            {
                wp.x = (int)Owner.Left;
            }
            else if (wp.x > Owner.Left + Owner.ActualWidth - ActualWidth)
            {
                wp.x = (int)(Owner.Left + Owner.ActualWidth - ActualWidth);
            }

            if (wp.y < Owner.Top)
            {
                wp.y = (int)Owner.Top;
            }
            else if (wp.y > Owner.Top + Owner.ActualHeight - ActualHeight)
            {
                wp.y = (int)(Owner.Top + Owner.ActualHeight - ActualHeight);
            }
            //wp.flags |= SWP_NOMOVE | SWP_NOSIZE;
            Marshal.StructureToPtr(wp, lParam, false);
        }

        return IntPtr.Zero;
    }
}