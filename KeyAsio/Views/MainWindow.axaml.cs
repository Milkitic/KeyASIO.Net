using Avalonia.Controls;
using Avalonia.Interactivity;
using KeyAsio.Services;
using KeyAsio.ViewModels;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;
using SukiUI.Toasts;

namespace KeyAsio.Views;

public partial class MainWindow : SukiWindow
{
    private readonly UpdateService _updateService;
    private readonly MainWindowViewModel _viewModel;

    private readonly ISukiToastManager _mainWindowManager = new SukiToastManager();

    public MainWindow(MainWindowViewModel mainWindowViewModel, UpdateService updateService)
    {
        _updateService = updateService;
        DataContext = _viewModel = mainWindowViewModel;
        InitializeComponent();
        ToastHost.Manager = _mainWindowManager;
    }

    #region For Designer

    public MainWindow()
    {
        if (!Design.IsDesignMode)
            throw new NotSupportedException();
        InitializeComponent();
    }

    #endregion

    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode) return;

        var theme = SukiTheme.GetInstance();
        var result = await _updateService.CheckUpdateAsync();
        if (result == true)
        {
            _mainWindowManager.CreateToast()
                .WithTitle("Update Available")
                .WithContent($"Update {_updateService.NewVersion} is Now Available.")
                .WithActionButton("Later", _ => { }, true, SukiButtonStyles.Basic)
                .WithActionButton("Update", _ => _updateService.OpenLastReleasePage(), true)
                .Queue();
        }
    }
}