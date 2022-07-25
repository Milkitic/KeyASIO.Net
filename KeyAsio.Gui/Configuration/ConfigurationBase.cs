using KeyAsio.Gui.Models;

namespace KeyAsio.Gui.Configuration;

public abstract class ConfigurationBase : ViewModelBase
{
    public void Save()
    {
        ConfigurationFactory.Save(this);
    }
}