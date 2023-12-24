using System.Windows;

namespace KeyAsio.Shared;

public static class UiDispatcher
{
    public static void Invoke(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(action);
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
}