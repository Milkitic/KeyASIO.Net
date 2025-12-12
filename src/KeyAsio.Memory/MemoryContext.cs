using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using KeyAsio.Memory.Configuration;
using KeyAsio.Memory.Utils;

namespace KeyAsio.Memory;

public class MemoryContext
{
    private readonly SigScan _sigScan;
    private readonly MemoryProfile _profile;
    private readonly Dictionary<string, IntPtr> _signatureCache = new();
    private readonly Dictionary<ValueDefinition, CachedStringReader> _stringCache = new();
    private long _currentTick;

    public MemoryContext(SigScan sigScan, MemoryProfile profile)
    {
        _sigScan = sigScan;
        _profile = profile;
    }

    public void BeginUpdate()
    {
        _currentTick = unchecked(_currentTick + 1);
    }

    public void Scan()
    {
        _signatureCache.Clear();
        _stringCache.Clear();
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

    public bool TryGetValue<T>(string valueName, out T result)
    {
        if (!_profile.Values.TryGetValue(valueName, out var def))
        {
            result = default!;
            return false; // Don't throw
        }

        return TryGetValueInternal(def, out result);
    }

    public object? GetValue(string valueName)
    {
        if (!_profile.Values.TryGetValue(valueName, out var def))
            throw new ArgumentException($"Value '{valueName}' not defined.");

        IntPtr basePtr;

        // Resolve Base (can be Signature or Pointer)
        if (def.ParentPointer != null)
        {
            basePtr = ResolvePointerInternal(def.ParentPointer);
        }
        else if (_signatureCache.TryGetValue(def.Base, out var value))
        {
            basePtr = value;
        }
        else if (_profile.Pointers.TryGetValue(def.Base, out var ptrDef))
        {
            basePtr = ResolvePointerInternal(ptrDef);
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
            "managed_string" => GetCachedStringReader(def).Get(_sigScan, finalAddr),
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

    protected bool TryGetValueInternal<T>(ValueDefinition def, out T result)
    {
        result = default!;

        IntPtr basePtr;

        // Resolve Base (can be Signature or Pointer)
        if (def.ParentPointer != null)
        {
            basePtr = ResolvePointerInternal(def.ParentPointer);
        }
        else if (_signatureCache.TryGetValue(def.Base, out var value))
        {
            basePtr = value;
        }
        else if (_profile.Pointers.TryGetValue(def.Base, out var ptrDef))
        {
            basePtr = ResolvePointerInternal(ptrDef);
        }
        else
        {
            return false; // Base not found
        }

        if (basePtr == IntPtr.Zero) return false;

        var finalAddr = basePtr + def.Offset;

        // Fast path for supported value types to avoid boxing using Unsafe.As
        if (typeof(T) == typeof(int))
        {
            if (MemoryReadHelper.TryGetValue<int>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<int, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(float))
        {
            if (MemoryReadHelper.TryGetValue<float>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<float, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(double))
        {
            if (MemoryReadHelper.TryGetValue<double>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<double, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(bool))
        {
            if (MemoryReadHelper.TryGetValue<bool>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<bool, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(short))
        {
            if (MemoryReadHelper.TryGetValue<short>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<short, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(ushort))
        {
            if (MemoryReadHelper.TryGetValue<ushort>(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<ushort, T>(ref val);
                return true;
            }

            return false;
        }

        if (typeof(T) == typeof(string))
        {
            if (GetCachedStringReader(def).TryGet(_sigScan, finalAddr, out var val))
            {
                result = Unsafe.As<string, T>(ref val);
                return true;
            }
        }

        return false;
    }

    private IntPtr ResolvePointer(string pointerName)
    {
        if (!_profile.Pointers.TryGetValue(pointerName, out var def))
            return IntPtr.Zero;

        return ResolvePointerInternal(def);
    }

    private CachedStringReader GetCachedStringReader(ValueDefinition def)
    {
        if (!_stringCache.TryGetValue(def, out var reader))
        {
            reader = new CachedStringReader();
            _stringCache[def] = reader;
        }

        return reader;
    }

    private IntPtr ResolvePointerInternal(PointerDefinition def)
    {
        // Check Cache
        if (def.CachedTick == _currentTick)
        {
            return def.CachedAddress;
        }

        // Resolve Base
        IntPtr currentPtr;
        if (def.ParentPointer != null)
        {
            currentPtr = ResolvePointerInternal(def.ParentPointer);
        }
        else if (_signatureCache.TryGetValue(def.Base, out var sigPtr))
        {
            currentPtr = sigPtr;
        }
        else if (_profile.Pointers.TryGetValue(def.Base, out var baseDef))
        {
            // Recursive resolution
            currentPtr = ResolvePointerInternal(baseDef);
        }
        else
        {
            return IntPtr.Zero; // Base not found
        }

        if (currentPtr == IntPtr.Zero) return IntPtr.Zero;

        // Apply Offsets
        foreach (var offset in def.Offsets)
        {
            currentPtr += offset;

            if (!MemoryReadHelper.TryGetPointer(_sigScan, currentPtr, out currentPtr))
            {
                return IntPtr.Zero;
            }

            if (currentPtr == IntPtr.Zero) return IntPtr.Zero;
        }

        // Update Cache
        def.CachedAddress = currentPtr;
        def.CachedTick = _currentTick;

        return currentPtr;
    }
}

public class MemoryContext<T> : MemoryContext where T : class
{
    private readonly Action<T> _populateAction;

    public MemoryContext(SigScan sigScan, MemoryProfile profile) : base(sigScan, profile)
    {
        _populateAction = BuildPopulateAction(profile);
    }

    public void Populate(T target)
    {
        _populateAction(target);
    }

    private Action<T> BuildPopulateAction(MemoryProfile profile)
    {
        var targetParam = Expression.Parameter(typeof(T), "target");
        var expressions = new List<Expression>();
        var contextConstant = Expression.Constant(this, typeof(MemoryContext));
        var tryGetValueInternalMethod = typeof(MemoryContext).GetMethod(nameof(TryGetValueInternal),
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!profile.Values.TryGetValue(prop.Name, out var valDef)) continue;
            if (prop.GetSetMethod() == null) continue;

            var valVar = Expression.Variable(prop.PropertyType, $"val_{prop.Name}");
            var genericMethod = tryGetValueInternalMethod!.MakeGenericMethod(prop.PropertyType);

            // Bake ValueDefinition as a constant
            var defConstant = Expression.Constant(valDef, typeof(ValueDefinition));

            var call = Expression.Call(
                contextConstant,
                genericMethod,
                defConstant,
                valVar
            );

            var assign = Expression.Assign(Expression.Property(targetParam, prop), valVar);
            var check = Expression.IfThen(call, assign);

            expressions.Add(Expression.Block([valVar], check));
        }

        if (expressions.Count == 0) return _ => { };

        var finalBlock = Expression.Block(expressions);
        return Expression.Lambda<Action<T>>(finalBlock, targetParam).Compile();
    }
}