﻿namespace KeyAsio.Gui.Models;

public interface IInvokableVm
{
    internal void RaisePropertyChanged(string propertyName);
    internal void RaisePropertyChanging(string propertyName);
}