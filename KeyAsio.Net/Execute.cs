using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace KeyAsio.Net
{
    public static class Execute
    {
        private static SynchronizationContext _uiContext;

        public static void SetMainThreadContext()
        {
            if (_uiContext != null) Console.WriteLine("Current SynchronizationContext may be replaced.");

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                var fileName = Path.GetFileName(assembly.Location);
                if (fileName == "System.Windows.Forms.dll")
                {
                    var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("WindowsFormsSynchronizationContext"));
                    _uiContext = (SynchronizationContext)Activator.CreateInstance(type);
                    break;
                }
                else if (fileName == "WindowsBase.dll")
                {
                    var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("DispatcherSynchronizationContext"));
                    _uiContext = (SynchronizationContext)Activator.CreateInstance(type);
                    break;
                }
            }

            if (_uiContext == null) _uiContext = SynchronizationContext.Current;
        }

        public static void OnUiThread(this Action action)
        {
            if (_uiContext == null)
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
            else
            {
                _uiContext.Send(obj => { action?.Invoke(); }, null);
            }
        }

        public static void ToUiThread(this Action action)
        {
            if (_uiContext == null)
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
            else
            {
                _uiContext.Post(obj => { action?.Invoke(); }, null);
            }
        }

        public static bool CheckDispatcherAccess() => Thread.CurrentThread.ManagedThreadId == 1;
    }
}
