using KeyAsio.Application.Models;
using KeyAsio.Configuration;
using KeyAsio.Configuration.Models;

namespace KeyAsio.Application.Services;

internal sealed class SkinSelectionPreferences(AppSettingsPaths paths)
{
    private bool _isApplyingProgrammaticSelection;

    public string? Get(GameClientType clientType)
    {
        return clientType == GameClientType.Lazer
            ? paths.SelectedSkinNameLazer
            : paths.SelectedSkinNameStable;
    }

    public void OnSelectionChanged(GameClientType clientType, SkinDescription? skin)
    {
        if (_isApplyingProgrammaticSelection)
        {
            return;
        }

        if (clientType == GameClientType.Lazer)
        {
            // Lazer skin names are not guaranteed to be unique; its synthetic
            // folder is derived from the stable realm ID and is safe to persist.
            paths.SelectedSkinNameLazer = skin?.Folder;
        }
        else
        {
            paths.SelectedSkinNameStable = skin?.FolderName;
        }
    }

    public void ApplyProgrammaticSelection(Action apply)
    {
        var wasApplyingProgrammaticSelection = _isApplyingProgrammaticSelection;
        _isApplyingProgrammaticSelection = true;

        try
        {
            apply();
        }
        finally
        {
            _isApplyingProgrammaticSelection = wasApplyingProgrammaticSelection;
        }
    }
}
