using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;

namespace KeyAsio;

[SupportedOSPlatform("windows6.0")]
internal static class MsgDialogWin32
{
    public const int IDABORT = 3;
    public const int IDCANCEL = 2;
    public const int IDCONTINUE = 11;
    public const int IDIGNORE = 5;
    public const int IDNO = 7;
    public const int IDOK = 1;
    public const int IDRETRY = 4;
    public const int IDTRYAGAIN = 10;
    public const int IDYES = 6;

    // ----------------------------------------------------------
    // Public API (保持原有签名，对外无感知)
    // ----------------------------------------------------------

    public static bool Question(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON,
            PInvoke.TD_INFORMATION_ICON
        );
        return result == IDYES;
    }

    public static void Info(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_INFORMATION_ICON
        );
    }

    public static void Warn(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
    }

    public static bool WarnYesNo(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
        return result == IDYES;
    }

    public static bool WarnOkCancel(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
        return result == IDOK;
    }

    public static void Error(string content, string? instruction = null, string? title = null, string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_ERROR_ICON
        );
    }

    // ----------------------------------------------------------
    // Private Implementation (CsWin32 Core)
    // ----------------------------------------------------------

    /// <summary>
    /// 核心方法：构建配置结构体并调用 user32 API
    /// </summary>
    private static unsafe int ShowNativeDialog(
        Window? window,
        string? title,
        string? instruction,
        string? content,
        string? footer,
        string? detail,
        TASKDIALOG_COMMON_BUTTON_FLAGS buttons,
        PCWSTR mainIcon)
    {
        // 1. 获取窗口句柄和标题
        GetWindowInfo(window, title, out var hwnd, out var actualTitle);

        // 2. 使用 fixed 语句固定字符串内存，防止 GC 移动，并转换为 Win32 需要的 PCWSTR
        fixed (char* pTitle = actualTitle)
        fixed (char* pInstruction = instruction)
        fixed (char* pContent = content)
        fixed (char* pFooter = footer)
        fixed (char* pDetail = detail)
        {
            // 3. 配置 TASKDIALOGCONFIG
            var config = new TASKDIALOGCONFIG
            {
                cbSize = (uint)Unsafe.SizeOf<TASKDIALOGCONFIG>(),
                hwndParent = new HWND(hwnd),
                hInstance = HMODULE.Null, // 使用系统资源
                dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW,
                dwCommonButtons = buttons,
                pszWindowTitle = pTitle,
                pszMainInstruction = pInstruction,
                pszContent = pContent,
            };

            config.Anonymous1.pszMainIcon = mainIcon; // 设置主图标

            // 处理 Footer
            if (footer != null)
            {
                config.pszFooter = pFooter;
                // 保持和你原代码一致的逻辑：Footer 图标设为 Info
                config.Anonymous2.pszFooterIcon = PInvoke.TD_INFORMATION_ICON;
            }

            // 处理 Details (展开内容)
            if (detail != null)
            {
                config.pszExpandedInformation = pDetail;
                // 启用展开功能，并且默认将展开信息放在 Footer 区域下方 (这是标准做法，如果需要原先的行为可调整 Flags)
                config.dwFlags |= TASKDIALOG_FLAGS.TDF_EXPAND_FOOTER_AREA;
            }

            // 4. 调用 API
            int buttonPressed = 0;
            // 注意：TaskDialogIndirect 可能会失败（例如在极旧的系统上，虽然 Avalonia 11 不再支持 Win7 以下），这里暂不做复杂的 HRESULT 检查
            PInvoke.TaskDialogIndirect(config, &buttonPressed, null, null);

            return buttonPressed;
        }
    }

    private static void GetWindowInfo(Window? window, string? title, out IntPtr actualHwnd, out string actualTitle)
    {
        actualHwnd = IntPtr.Zero;
        if (window == null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                window = mainWindow;
            }
        }

        if (window != null)
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is { HandleDescriptor: "HWND" })
            {
                actualHwnd = handle.Handle;
            }
        }

        if (title != null)
        {
            actualTitle = title;
        }
        else
        {
            // 注意：Assembly 获取在某些裁剪发布(AOT)场景下可能为空，建议保留默认值
            actualTitle = (Assembly.GetEntryAssembly()?.GetName().Name ?? "Application") + " - 系统提示";

            if (window?.Title != null)
            {
                actualTitle = window.Title;
            }
        }
    }
}