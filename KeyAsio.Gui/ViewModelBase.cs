﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using Milki.Extensions.MixPlayer.Annotations;

namespace KeyAsio.Gui;

/// <summary>
/// ViewModel基础类
/// </summary>
public abstract class ViewModelBase : IInvokableVm, INotifyPropertyChanged, INotifyPropertyChanging
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// 通知UI更新操作
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void IInvokableVm.RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void IInvokableVm.RaisePropertyChanging(string propertyName)
    {
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
    }
}

public interface IInvokableVm
{
    internal void RaisePropertyChanged(string propertyName);
    internal void RaisePropertyChanging(string propertyName);
}