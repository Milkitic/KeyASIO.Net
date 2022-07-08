using System;
using System.Management;

namespace KeyAsio.Gui.Utils;

public static class HardwareInformationHelper
{
    public static string GetPhysicalMemory()
    {
        var managementScope = new ManagementScope();
        var objectQuery = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
        using var managementObjectSearcher = new ManagementObjectSearcher(managementScope, objectQuery);
        using var managementObjectCollection = managementObjectSearcher.Get();

        long memSize = 0;

        // In case more than one Memory sticks are installed
        foreach (var obj in managementObjectCollection)
        {
            var capacity = Convert.ToInt64(obj["Capacity"]);
            memSize += capacity;
            obj.Dispose();
        }

        memSize = memSize / 1024 / 1024;
        return memSize + " MB";
    }

    public static string GetProcessorInformation()
    {
        using var managementClass = new ManagementClass("win32_processor");
        using var managementObjectCollection = managementClass.GetInstances();
        foreach (var managementObject in managementObjectCollection)
        {
            var name = (string)managementObject["Name"];
            return $"{name}, {managementObject["Caption"]}, {managementObject["SocketDesignation"]}";
        }

        return "unknown";
    }

    public static string GetOsInformation()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        foreach (var wmi in searcher.Get())
        {
            try
            {
                return $"{((string)wmi["Caption"]).Trim()}, {wmi["Version"]}, {wmi["OSArchitecture"]}";
            }
            catch { }
        }

        return "unknown";
    }
}