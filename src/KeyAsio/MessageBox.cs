using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeyAsio;

[SupportedOSPlatform("windows")]
internal static class MessageBox
{
    // ----------------------------------------------------------
    // Public API (保持不变)
    // ----------------------------------------------------------

    public static bool Question(string content, string? instruction = null, string? title = null, string? footer = null,
        string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON,
            PInvoke.TD_INFORMATION_ICON
        );
        return result == MESSAGEBOX_RESULT.IDYES;
    }

    public static void Info(string content, string? instruction = null, string? title = null, string? footer = null,
        string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_INFORMATION_ICON
        );
    }

    public static void Warn(string content, string? instruction = null, string? title = null, string? footer = null,
        string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
    }

    public static bool WarnYesNo(string content, string? instruction = null, string? title = null,
        string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
        return result == MESSAGEBOX_RESULT.IDYES;
    }

    public static bool WarnOkCancel(string content, string? instruction = null, string? title = null,
        string? footer = null, string? detail = null, Window? windowHwnd = null)
    {
        var result = ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON | TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON,
            PInvoke.TD_WARNING_ICON
        );
        return result == MESSAGEBOX_RESULT.IDOK;
    }

    public static void Error(string content, string? instruction = null, string? title = null, string? footer = null,
        string? detail = null, Window? windowHwnd = null)
    {
        ShowNativeDialog(
            windowHwnd, title, instruction, content, footer, detail,
            TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
            PInvoke.TD_ERROR_ICON
        );
    }

    // ----------------------------------------------------------
    // Private Implementation
    // ----------------------------------------------------------

    private static unsafe MESSAGEBOX_RESULT ShowNativeDialog(
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

        // 2. 检查系统版本。如果是 Vista (6.0) 以下，直接降级到 MessageBox
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
            return ShowLegacyMessageBox(hwnd, actualTitle, instruction, content, footer, detail, buttons, mainIcon);
        }

        try
        {
            // 3. 尝试使用 TaskDialog (Modern)
            fixed (char* pTitle = actualTitle)
            fixed (char* pInstruction = instruction)
            fixed (char* pContent = content)
            fixed (char* pFooter = footer)
            fixed (char* pDetail = detail)
            {
                var config = new TASKDIALOGCONFIG
                {
                    cbSize = (uint)Unsafe.SizeOf<TASKDIALOGCONFIG>(),
                    hwndParent = hwnd,
                    hInstance = HMODULE.Null,
                    dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION |
                              TASKDIALOG_FLAGS.TDF_POSITION_RELATIVE_TO_WINDOW,
                    dwCommonButtons = buttons,
                    pszWindowTitle = pTitle,
                    pszMainInstruction = pInstruction,
                    pszContent = pContent,
                    Anonymous1 = { pszMainIcon = mainIcon }
                };

                if (footer != null)
                {
                    config.pszFooter = pFooter;
                    config.Anonymous2.pszFooterIcon = PInvoke.TD_INFORMATION_ICON;
                }

                if (detail != null)
                {
                    config.pszExpandedInformation = pDetail;
                    config.dwFlags |= TASKDIALOG_FLAGS.TDF_EXPAND_FOOTER_AREA;
                }

                var hResult = PInvoke.TaskDialogIndirect(config, out var buttonPressed, out _, out _);

                // 如果调用成功 (S_OK = 0)，直接返回
                if (hResult.Value == 0)
                    return (MESSAGEBOX_RESULT)buttonPressed;
            }
        }
        catch (EntryPointNotFoundException)
        {
            /* 忽略，降级 */
        }
        catch (DllNotFoundException)
        {
            /* 忽略，降级 */
        }

        // 4. 如果 TaskDialog 失败 (或者意外异常)，兜底降级到 MessageBox
        return ShowLegacyMessageBox(hwnd, actualTitle, instruction, content, footer, detail, buttons, mainIcon);
    }

    /// <summary>
    /// 兼容 XP/2000 的 MessageBox 实现
    /// </summary>
    private static MESSAGEBOX_RESULT ShowLegacyMessageBox(
        HWND hwnd,
        string title,
        string? instruction,
        string? content,
        string? footer,
        string? detail,
        TASKDIALOG_COMMON_BUTTON_FLAGS buttons,
        PCWSTR mainIcon)
    {
        // 1. 拼接文本：MessageBox 没有那么多花哨的区域，只能全拼在一起
        // 格式：
        // [Instruction]
        //
        // [Content]
        //
        // ----------------
        // [Detail / Footer]

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(instruction)) sb.AppendLine(instruction).AppendLine();
        if (!string.IsNullOrEmpty(content)) sb.Append(content);

        if (!string.IsNullOrEmpty(detail) || !string.IsNullOrEmpty(footer))
        {
            sb.AppendLine().AppendLine().Append("----------------").AppendLine();
            if (!string.IsNullOrEmpty(detail)) sb.AppendLine(detail);
            if (!string.IsNullOrEmpty(footer)) sb.AppendLine(footer);
        }

        string fullText = sb.ToString().Trim();

        // 2. 映射图标 (TaskDialog Icon -> MessageBox Icon)
        MESSAGEBOX_STYLE uType = 0;
        if (mainIcon.Equals(PInvoke.TD_ERROR_ICON)) uType |= MESSAGEBOX_STYLE.MB_ICONHAND; // Error
        else if (mainIcon.Equals(PInvoke.TD_WARNING_ICON)) uType |= MESSAGEBOX_STYLE.MB_ICONEXCLAMATION; // Warning
        else if (mainIcon.Equals(PInvoke.TD_INFORMATION_ICON)) uType |= MESSAGEBOX_STYLE.MB_ICONASTERISK; // Info
        else uType |= MESSAGEBOX_STYLE.MB_ICONASTERISK; // Default

        // 3. 映射按钮 (TaskDialog Flags -> MessageBox Flags)
        // 注意：TaskDialog 的 Flag 是位掩码，MessageBox 的是互斥枚举，需要转换
        if ((buttons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_YES_BUTTON) != 0 &&
            (buttons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_NO_BUTTON) != 0)
        {
            uType |= MESSAGEBOX_STYLE.MB_YESNO;
        }
        else if ((buttons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON) != 0 &&
                 (buttons & TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_CANCEL_BUTTON) != 0)
        {
            uType |= MESSAGEBOX_STYLE.MB_OKCANCEL;
        }
        else
        {
            uType |= MESSAGEBOX_STYLE.MB_OK; // 默认
        }

        // 4. 调用原生 API
        return PInvoke.MessageBox(hwnd, fullText, title, uType);
    }

    private static void GetWindowInfo(Window? window, string? title, out HWND actualHwnd, out string actualTitle)
    {
        actualHwnd = HWND.Null;

        // 尝试从 Avalonia 生命周期获取主窗口
        if (window == null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } mainWindow
                })
            {
                window = mainWindow;
            }
        }

        if (window != null)
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is { HandleDescriptor: "HWND" })
            {
                actualHwnd = (HWND)handle.Handle;
            }
        }

        // 标题处理
        if (title != null)
        {
            actualTitle = title;
        }
        else
        {
            if (window?.Title != null)
            {
                actualTitle = window.Title;
            }

            string appName;
            try
            {
                appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Application";
            }
            catch
            {
                appName = "Application";
            }

            actualTitle = appName;
        }
    }
}