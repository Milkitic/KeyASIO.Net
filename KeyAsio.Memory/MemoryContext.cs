using System.Reflection;
using KeyAsio.Memory.Configuration;

namespace KeyAsio.Memory;

public class MemoryContext
{
    private readonly SigScan _sigScan;
    private readonly MemoryProfile _profile;
    private readonly Dictionary<string, IntPtr> _signatureCache = new();

    public MemoryContext(SigScan sigScan, MemoryProfile profile)
    {
        _sigScan = sigScan;
        _profile = profile;
    }

    public void Scan()
    {
        _signatureCache.Clear();
        foreach (var kvp in _profile.Signatures)
        {
            var ptr = _sigScan.FindPattern(kvp.Value);
            if (ptr != IntPtr.Zero)
            {
                _signatureCache[kvp.Key] = ptr;
                // Console.WriteLine($"Found signature {kvp.Key} at {ptr:X}");
            }
        }
    }

    public IntPtr ResolvePointer(string pointerName)
    {
        if (!_profile.Pointers.TryGetValue(pointerName, out var def))
            throw new ArgumentException($"Pointer '{pointerName}' not defined.");

        // Resolve Base
        IntPtr currentPtr;
        if (_signatureCache.TryGetValue(def.Base, out var sigPtr))
        {
            currentPtr = sigPtr;
        }
        else if (_profile.Pointers.ContainsKey(def.Base))
        {
            currentPtr = ResolvePointer(def.Base); // Recursive resolution
        }
        else
        {
            return IntPtr.Zero; // Base not found
        }

        if (currentPtr == IntPtr.Zero) return IntPtr.Zero;

        // Apply Offsets
        foreach (var offset in def.Offsets)
        {
            currentPtr = currentPtr + offset;
            currentPtr = MemoryReadHelper.GetPointer(_sigScan, currentPtr);
            if (currentPtr == IntPtr.Zero) return IntPtr.Zero;
        }

        return currentPtr;
    }

    public object? GetValue(string valueName)
    {
        if (!_profile.Values.TryGetValue(valueName, out var def))
            throw new ArgumentException($"Value '{valueName}' not defined.");

        IntPtr basePtr;

        // Resolve Base (can be Signature or Pointer)
        if (_signatureCache.TryGetValue(def.Base, out var value))
        {
            basePtr = value;
        }
        else if (_profile.Pointers.ContainsKey(def.Base))
        {
            basePtr = ResolvePointer(def.Base);
        }
        else
        {
            return null; // Base not found
        }

        if (basePtr == IntPtr.Zero) return null;

        var finalAddr = basePtr + def.Offset;

        return def.Type.ToLower() switch
        {
            "int" or "int32" => MemoryReadHelper.GetValue<int>(_sigScan, finalAddr),
            "float" or "single" => MemoryReadHelper.GetValue<float>(_sigScan, finalAddr),
            "double" => MemoryReadHelper.GetValue<double>(_sigScan, finalAddr),
            "bool" or "boolean" => MemoryReadHelper.GetValue<bool>(_sigScan, finalAddr),
            "short" or "int16" => MemoryReadHelper.GetValue<short>(_sigScan, finalAddr),
            "ushort" or "uint16" => MemoryReadHelper.GetValue<ushort>(_sigScan, finalAddr),
            "managed_string" => MemoryReadHelper.GetManagedString(_sigScan, finalAddr),
            _ => throw new NotSupportedException($"Type {def.Type} not supported.")
        };
    }

    // Generic helper for strongly typed access
    public T? GetValue<T>(string valueName) where T : struct
    {
        var result = GetValue(valueName);
        return result is T typedResult ? typedResult : null;
    }

    public string? GetString(string valueName)
    {
        var result = GetValue(valueName);
        return result as string;
    }

    // Auto-populate a POCO based on property names matching Value definitions
    public void Populate<T>(T target) where T : class
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (!_profile.Values.ContainsKey(prop.Name)) continue;

            try
            {
                var val = GetValue(prop.Name);
                if (val != null)
                {
                    // Handle type conversion if necessary
                    if (prop.PropertyType.IsInstanceOfType(val))
                    {
                        prop.SetValue(target, val);
                    }
                    else
                    {
                        // Try Convert
                        var converted = Convert.ChangeType(val, prop.PropertyType);
                        prop.SetValue(target, converted);
                    }
                }
            }
            catch
            {
                throw;
                // Ignore read errors for individual properties
            }
        }
    }
}