using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KeyAsio.Views.Dialogs;
using Milki.Extensions.Configuration;
using Milki.Extensions.MouseKeyHook;
using SukiUI.Dialogs;

namespace KeyAsio.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<HookKeys> BoundKeys
    {
        get
        {
            if (field != null) return field;

            field = new ObservableCollection<HookKeys>(AppSettings.Input.Keys);
            field.CollectionChanged += (_, _) =>
            {
                AppSettings.Input.Keys = field.ToList();
                AppSettings.Save();
            };
            return field;
        }
    }

    [RelayCommand]
    public void AddKey()
    {
        DialogManager.DismissDialog();

        var vm = new KeyBindDialogViewModel(AppSettings.Input, key => { BoundKeys.Add(key); },
            () => { DialogManager.DismissDialog(); });

        DialogManager.CreateDialog()
            .WithContent(new KeyBindDialogView { DataContext = vm })
            .WithTitle("Bind Key")
            .WithActionButton("Cancel", _ => vm.Dispose(), true)
            .TryShow();
    }

    [RelayCommand]
    public void RemoveKey(HookKeys key)
    {
        BoundKeys.Remove(key);
    }
}