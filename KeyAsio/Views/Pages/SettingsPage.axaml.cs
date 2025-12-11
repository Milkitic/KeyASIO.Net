using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnBrowseOsuFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select osu! Folder",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            viewModel.AppSettings.Paths.OsuFolderPath = path;
        }
    }
}
