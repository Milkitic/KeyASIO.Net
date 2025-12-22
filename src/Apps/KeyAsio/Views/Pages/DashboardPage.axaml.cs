using Avalonia.Controls;
using Avalonia.Media;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            UpdateSwitchColor(vm.IsMixModeTagPro);
            vm.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsMixModeTagPro))
                {
                    UpdateSwitchColor(vm.IsMixModeTagPro);
                }
            };
        }
    }

    private void UpdateSwitchColor(bool isPro)
    {
        if (isPro)
        {
            MixSwitch.Resources["SukiPrimaryColor"] = Color.Parse("#FFD700");
        }
        else
        {
            MixSwitch.Resources.Remove("SukiPrimaryColor");
        }
    }
}