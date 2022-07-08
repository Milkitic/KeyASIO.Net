using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using KeyAsio.Gui.Models;
using NLog;
using NLog.Targets;

namespace KeyAsio.Gui;

[Target("RichTextBoxTarget")]
public sealed class RichTextBoxTarget : TargetWithLayout
{
    private Brush? _secondaryTextBrush;
    private Brush? _dangerBrush;
    private Brush? _warningBrush;

    protected override void Write(LogEventInfo logEvent)
    {
        if (!SharedViewModel.Instance.Debugging && logEvent.Level <= LogLevel.Debug)
        {
            return;
        }

        if (logEvent.Message.StartsWith("[DEBUGGING] "))
        {
            if (!SharedViewModel.Instance.Debugging)
            {
                return;
            }
            else
            {
                logEvent.Message = logEvent.Message[12..];
            }
        }

        string logMessage = Layout.Render(logEvent);
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var richTextBox = ((App)Application.Current).RichTextBox;
            var paragraph = new Paragraph
            {
                LineStackingStrategy = LineStackingStrategy.MaxHeight,
                LineHeight = 1
            };
            var run = new Run(logMessage);
            paragraph.Inlines.Add(run);
            if (logEvent.Level == LogLevel.Debug)
            {
                run.Foreground = _secondaryTextBrush ??= (Brush?)Application.Current.FindResource("SecondaryTextBrush");
            }
            else if (logEvent.Level == LogLevel.Error)
            {
                run.Foreground = _dangerBrush ??= (Brush?)Application.Current.FindResource("DangerBrush");
            }
            else if (logEvent.Level == LogLevel.Warn)
            {
                run.Foreground = _warningBrush ??= (Brush?)Application.Current.FindResource("WarningBrush");
            }

            richTextBox.Document.Blocks.Add(paragraph);
            if (richTextBox.Document.Blocks.Count > 500)
            {
                richTextBox.Document.Blocks.Remove(richTextBox.Document.Blocks.FirstBlock);
            }

            if (IsScrolledToEnd(richTextBox))
            {
                richTextBox.ScrollToEnd();
            }
        });
    }

    private static bool IsScrolledToEnd(TextBoxBase textBox)
    {
        return (textBox.VerticalOffset + textBox.ViewportHeight - textBox.ExtentHeight) >= -2;
    }
}