using KeyAsio.ViewModels;
using SukiUI.Controls;

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
}