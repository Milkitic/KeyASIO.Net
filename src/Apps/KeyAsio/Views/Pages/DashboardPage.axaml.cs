using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Pages;

public partial class DashboardPage : UserControl
{
    private MainWindowViewModel? _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            _viewModel = vm;
            UpdateSwitchColor(vm.PluginManager.IsMixModeTagPro);
            vm.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.PluginManager.IsMixModeTagPro))
                {
                    UpdateSwitchColor(vm.PluginManager.IsMixModeTagPro);
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

    private void CbControlStatus_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is CheckBox checkBox)
        {
            _viewModel.SyncSession.OsuStatus = checkBox.IsChecked == true
                ? OsuMemoryStatus.MainView
                : OsuMemoryStatus.NotRunning;
        }
    }
}