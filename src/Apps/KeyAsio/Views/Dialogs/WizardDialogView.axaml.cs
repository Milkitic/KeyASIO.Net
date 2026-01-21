using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeyAsio.Views.Dialogs;

public partial class WizardDialogView : UserControl
{
    public WizardDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}