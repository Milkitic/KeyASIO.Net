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

    public void OnSelectionChanged(GameClientType clientType, string? folderName)
    {
        if (_isApplyingProgrammaticSelection)
        {
            return;
        }

        if (clientType == GameClientType.Lazer)
        {
            paths.SelectedSkinNameLazer = folderName;
        }
        else
        {
            paths.SelectedSkinNameStable = folderName;
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
