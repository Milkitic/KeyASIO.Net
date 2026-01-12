﻿﻿﻿using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeyAsio.Views.Dialogs;

public partial class PresetSelectionDialog : UserControl
{
    public PresetSelectionDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
