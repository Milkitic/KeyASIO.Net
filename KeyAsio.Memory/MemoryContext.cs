using System.Reflection;
using System.Runtime.CompilerServices;
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
            // 1. 加上偏移量 (定位到存放指针的地址)
            currentPtr += offset;

            // 2. 读取指针 (读取该地址内存中的值，作为新的地址)
            // 相当于 C++ 中的: currentPtr = *(int*)(currentPtr);
            currentPtr = MemoryReadHelper.GetPointer(_sigScan, currentPtr);

            // 如果读出来是空指针，说明链断了，停止
            if (currentPtr == IntPtr.Zero) return IntPtr.Zero;
        }

        return currentPtr;
    }

    public bool TryGetValue<T>(string valueName, out T result)
    {
        result = default!;

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
            return false; // Base not found
        }

        if (basePtr == IntPtr.Zero) return false;

        var finalAddr = basePtr + def.Offset;

        try
        {
            // Fast path for supported value types to avoid boxing using Unsafe.As
            if (typeof(T) == typeof(int))
            {
                var val = MemoryReadHelper.GetValue<int>(_sigScan, finalAddr);
                result = Unsafe.As<int, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(float))
            {
                var val = MemoryReadHelper.GetValue<float>(_sigScan, finalAddr);
                result = Unsafe.As<float, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(double))
            {
                var val = MemoryReadHelper.GetValue<double>(_sigScan, finalAddr);
                result = Unsafe.As<double, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(bool))
            {
                var val = MemoryReadHelper.GetValue<bool>(_sigScan, finalAddr);
                result = Unsafe.As<bool, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(short))
            {
                var val = MemoryReadHelper.GetValue<short>(_sigScan, finalAddr);
                result = Unsafe.As<short, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(ushort))
            {
                var val = MemoryReadHelper.GetValue<ushort>(_sigScan, finalAddr);
                result = Unsafe.As<ushort, T>(ref val);
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                var val = MemoryReadHelper.GetManagedString(_sigScan, finalAddr);
                result = Unsafe.As<string, T>(ref val);
                return true;
            }

            // Fallback (boxing)
            var valObj = GetValue(valueName);
            if (valObj is T typedVal)
            {
                result = typedVal;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
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
        if (TryGetValue<T>(valueName, out var result))
        {
            return result;
        }

        return null;
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
                // Try optimized path first for common types
                if (prop.PropertyType == typeof(int))
                {
                    if (TryGetValue<int>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(float))
                {
                    if (TryGetValue<float>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(double))
                {
                    if (TryGetValue<double>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(bool))
                {
                    if (TryGetValue<bool>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(short))
                {
                    if (TryGetValue<short>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(ushort))
                {
                    if (TryGetValue<ushort>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                if (prop.PropertyType == typeof(string))
                {
                    if (TryGetValue<string>(prop.Name, out var val)) prop.SetValue(target, val);
                    continue;
                }

                // Fallback to boxing for other types or conversions
                var valObj = GetValue(prop.Name);
                if (valObj != null)
                {
                    if (prop.PropertyType.IsInstanceOfType(valObj))
                    {
                        prop.SetValue(target, valObj);
                    }
                    else
                    {
                        var converted = Convert.ChangeType(valObj, prop.PropertyType);
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