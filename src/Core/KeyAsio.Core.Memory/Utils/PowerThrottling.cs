using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace KeyAsio.Core.Memory.Utils;

public static class PowerThrottling
{
    [SupportedOSPlatform("windows8.0")]
    public static unsafe void DisableThrottling()
    {
        var throttlingState = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            // 控制 ExecutionSpeed 和 TimerResolution 的行为
            ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED |
                          PInvoke.PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
            // 设置为 OFF（不节流，不忽略）
            StateMask = 0
        };
        var currentProcess = Process.GetCurrentProcess();
        try
        {
            currentProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            Console.WriteLine("Process priority set to AboveNormal.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to set priority: " + ex.Message);
        }

        var hProcess = new HANDLE(currentProcess.Handle);

        int size = Marshal.SizeOf(throttlingState);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(throttlingState, ptr, false);
            bool result = PInvoke.SetProcessInformation(hProcess,
                PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, ptr.ToPointer(), (uint)size);

            var resultString = result ? "Success" : "Failed";
            Console.WriteLine($"Power Throttling Disabled: {resultString}");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}