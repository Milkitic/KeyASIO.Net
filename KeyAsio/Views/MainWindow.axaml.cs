using Avalonia.Interactivity;
using KeyAsio.ViewModels;
using SukiUI;
using SukiUI.Controls;
using SukiUI.Enums;

namespace KeyAsio.Views;

public partial class MainWindow : SukiWindow
{
    private readonly MainWindowViewModel _viewModel;


    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = mainWindowViewModel;
    }

    #region For Designer

    public MainWindow()
    {
        InitializeComponent();
    }

    #endregion

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        var theme = SukiTheme.GetInstance();
        //theme.ChangeColorTheme(SukiColor.Green);
    }
}