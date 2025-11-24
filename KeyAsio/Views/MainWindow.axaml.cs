using Avalonia.Controls;
using KeyAsio.ViewModels;

namespace KeyAsio.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = mainWindowViewModel;
    }
}