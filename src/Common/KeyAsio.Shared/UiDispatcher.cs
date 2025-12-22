namespace KeyAsio.Shared;

public static class UiDispatcher
{
    private static SynchronizationContext? _uiContext;

    /// <summary>
    /// Should be called on UI thread
    /// </summary>
    public static void SetUiSynchronizationContext(SynchronizationContext? synchronizationContext = null)
    {
        if (synchronizationContext != null)
        {
            _uiContext = synchronizationContext;
            return;
        }

        _uiContext ??= SynchronizationContext.Current;

        if (_uiContext != null) return;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var fileName = Path.GetFileName(assembly.Location);
            if (fileName == "System.Windows.Forms.dll")
            {
                var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("WindowsFormsSynchronizationContext"));
                _uiContext = Activator.CreateInstance(type) as SynchronizationContext;
                break;
            }
            else if (fileName == "WindowsBase.dll")
            {
                var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("DispatcherSynchronizationContext"));
                _uiContext = Activator.CreateInstance(type) as SynchronizationContext;
                break;
            }
        }
    }

    public static void Invoke(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (_uiContext != null)
        {
            _uiContext.Send(obj => { action?.Invoke(); }, null);
        }
        else
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine("UiContext execute error: " + ex.Message);
            }
        }
    }

    public static async Task InvokeAsync(Action action)
    {
        var tcs = new TaskCompletionSource();

        _uiContext.Post(_ =>
        {
            action.Invoke();
            tcs.TrySetResult();
        }, null);
        await tcs.Task;

    }

    public static async Task InvokeAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        Task? task = null;
        _uiContext.Post(_ =>
        {
            task = action.Invoke();
            tcs.TrySetResult();
        }, null);

        await tcs.Task;
        await task!;
    }
}