using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FastExpressionCompiler;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue<T>(string valueName, out T result)
    {
        if (!TryGetProfile(valueName, out var def))
        {
            result = default!;
            return false;
        }

        return TryGetValueDef(def, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetProfile(string valueName, [NotNullWhen(true)] out ValueDefinition? def)
    {
        return _profile.Values.TryGetValue(valueName, out def);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetValue(string valueName)
    {
        if (!_profile.Values.TryGetValue(valueName, out var def))
            throw new ArgumentException($"Value '{valueName}' not defined.");

        var basePtr = ResolveBaseAddress(def);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetString(string valueName)
    {
        var result = GetValue(valueName);
        return result as string;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr ResolveBaseAddress(ValueDefinition def)
    {
        if (def.ParentPointer != null)
        {
            return ResolvePointerInternal(def.ParentPointer);
        }

        if (_signatureCache.TryGetValue(def.Base, out var value))
        {
            return value;
        }

        if (_profile.Pointers.TryGetValue(def.Base, out var ptrDef))
        {
            return ResolvePointerInternal(ptrDef);
        }

        return IntPtr.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBlock(IntPtr basePtr, int offset, byte[] buffer, int length)
    {
        if (basePtr == IntPtr.Zero) return false;
        return _sigScan.ReadMemory(basePtr + offset, buffer, length, out _);
    }

    public bool TryGetValueDef<T>(ValueDefinition? def, out T result)
    {
        result = default!;
        if (def == null) return false;

        var basePtr = ResolveBaseAddress(def);

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

        //if (IsUnmanaged<T>())
        //{
        //    return TryGetValueUnmanaged<T>(_sigScan, finalAddr, out result);
        //}
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnmanaged<T>()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TryGetValueUnmanaged<T>(SigScan sigScan, IntPtr address, out T result)
    {
        result = default;
        if (!MemoryReadHelper.TryGetValue<T>(sigScan, address, out var val)) return false;

        result = Unsafe.ReadUnaligned<T>(Unsafe.AsPointer(ref val));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr ResolvePointer(string pointerName)
    {
        if (!_profile.Pointers.TryGetValue(pointerName, out var def))
            return IntPtr.Zero;

        return ResolvePointerInternal(def);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    private byte[] _sharedBuffer = Array.Empty<byte>();

    public MemoryContext(SigScan sigScan, MemoryProfile profile) : base(sigScan, profile)
    {
        _populateAction = BuildPopulateAction(profile);
    }

    public void Populate(T target)
    {
        _populateAction(target);
    }

    private record BlockItem(PropertyInfo Property, ValueDefinition Definition);

    private Action<T> BuildPopulateAction(MemoryProfile profile)
    {
        var targetParam = Expression.Parameter(typeof(T), "target");
        var expressions = new List<Expression>();
        var contextConstant = Expression.Constant(this, typeof(MemoryContext));
        var tryGetValueInternalMethod = typeof(MemoryContext).GetMethod(nameof(TryGetValueDef),
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
        var readBlockMethod = typeof(MemoryContext).GetMethod(nameof(ReadBlock),
            BindingFlags.Instance | BindingFlags.Public);
        var resolveBaseMethod = typeof(MemoryContext).GetMethod(nameof(ResolveBaseAddress),
            BindingFlags.Instance | BindingFlags.Public);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetSetMethod() != null)
            .Select(p => new BlockItem(p, profile.Values.TryGetValue(p.Name, out var d) ? d : null!))
            .Where(x => x.Definition != null)
            .ToList();

        // Group by Base
        var groups = properties.GroupBy(x => x.Definition.Base);
        int maxBufferSize = 0;

        foreach (var group in groups)
        {
            var candidates = group.Where(x => IsPrimitive(x.Definition.Type)).OrderBy(x => x.Definition.Offset)
                .ToList();
            var others = group.Except(candidates).ToList();

            foreach (var item in others)
            {
                AddIndividualRead(expressions, targetParam, contextConstant, tryGetValueInternalMethod!, item.Property,
                    item.Definition);
            }

            if (candidates.Count == 0) continue;

            var blocks = CreateBlocks(candidates);

            foreach (var block in blocks)
            {
                if (block.Count == 1)
                {
                    AddIndividualRead(expressions, targetParam, contextConstant, tryGetValueInternalMethod!,
                        block[0].Property, block[0].Definition);
                }
                else
                {
                    int startOffset = block.First().Definition.Offset;
                    int endOffset = block.Last().Definition.Offset + GetSize(block.Last().Definition.Type);
                    int blockSize = endOffset - startOffset;
                    if (blockSize > maxBufferSize) maxBufferSize = blockSize;

                    AddBlockRead(expressions, targetParam, contextConstant, readBlockMethod!, resolveBaseMethod!,
                        block);
                }
            }
        }

        if (maxBufferSize > 0)
        {
            _sharedBuffer = new byte[maxBufferSize];
        }

        if (expressions.Count == 0) return _ => { };

        var finalBlock = Expression.Block(expressions);
        return Expression.Lambda<Action<T>>(finalBlock, targetParam).CompileFast();
    }

    private static bool IsPrimitive(string type)
    {
        return type.ToLower() switch
        {
            "int" or "int32" or "float" or "single" or "double" or "bool" or "boolean" or "short" or "int16" or "ushort"
                or "uint16" => true,
            _ => false
        };
    }

    private static List<List<BlockItem>> CreateBlocks(List<BlockItem> candidates)
    {
        var blocks = new List<List<BlockItem>>();
        if (candidates.Count == 0) return blocks;

        var currentBlock = new List<BlockItem> { candidates[0] };
        blocks.Add(currentBlock);

        for (int i = 1; i < candidates.Count; i++)
        {
            var prev = currentBlock.Last();
            var curr = candidates[i];

            int prevEnd = prev.Definition.Offset + GetSize(prev.Definition.Type);
            int currStart = curr.Definition.Offset;

            if (currStart - prevEnd < 512)
            {
                currentBlock.Add(curr);
            }
            else
            {
                currentBlock = [curr];
                blocks.Add(currentBlock);
            }
        }

        return blocks;
    }

    private static int GetSize(string type)
    {
        return type.ToLower() switch
        {
            "int" or "int32" or "float" or "single" => 4,
            "double" => 8,
            "bool" or "boolean" => 1,
            "short" or "int16" or "ushort" or "uint16" => 2,
            _ => 4
        };
    }

    private static void AddIndividualRead(List<Expression> expressions, ParameterExpression targetParam,
        ConstantExpression contextConstant, MethodInfo tryGetValueMethod, PropertyInfo prop, ValueDefinition valDef)
    {
        var valVar = Expression.Variable(prop.PropertyType, $"val_{prop.Name}");
        var genericMethod = tryGetValueMethod.MakeGenericMethod(prop.PropertyType);
        var defConstant = Expression.Constant(valDef, typeof(ValueDefinition));

        var call = Expression.Call(contextConstant, genericMethod, defConstant, valVar);
        var assign = Expression.Assign(Expression.Property(targetParam, prop), valVar);
        var check = Expression.IfThen(call, assign);

        expressions.Add(Expression.Block([valVar], check));
    }

    private void AddBlockRead(List<Expression> expressions, ParameterExpression targetParam,
        ConstantExpression contextConstant, MethodInfo readBlockMethod, MethodInfo resolveBaseMethod,
        List<BlockItem> block)
    {
        // 相当于生成以下 C# 代码块：
        // {
        //     IntPtr basePtr = ResolveBaseAddress(def);
        //     if (basePtr != IntPtr.Zero)
        //     {
        //         byte[] buffer = this._sharedBuffer;
        //         if (ReadBlock(basePtr, startOffset, buffer, blockSize))
        //         {
        //             // 批量读取
        //             target.Prop1 = ReadValue<int>(buffer, offset1);
        //             target.Prop2 = ReadValue<float>(buffer, offset2);
        //             ...
        //         }
        //     }
        // }

        int startOffset = block.First().Definition.Offset;
        int endOffset = block.Last().Definition.Offset + GetSize(block.Last().Definition.Type);
        int blockSize = endOffset - startOffset;

        // IntPtr basePtr;
        var basePtrVar = Expression.Variable(typeof(IntPtr), "basePtr");
        // basePtr = ResolveBaseAddress(def);
        var defConstant = Expression.Constant(block.First().Definition, typeof(ValueDefinition));
        var resolveCall = Expression.Call(contextConstant, resolveBaseMethod, defConstant);

        // byte[] buffer;
        var bufferVar = Expression.Variable(typeof(byte[]), "buffer");
        // buffer = this._sharedBuffer;
        var bufferAssign = Expression.Assign(bufferVar, Expression.Field(Expression.Constant(this), "_sharedBuffer"));

        // ReadBlock(basePtr, startOffset, buffer, blockSize)
        var readCall = Expression.Call(contextConstant, readBlockMethod, basePtrVar, Expression.Constant(startOffset),
            bufferVar, Expression.Constant(blockSize));

        var assignments = new List<Expression>();
        foreach (var item in block)
        {
            var prop = item.Property;
            var def = item.Definition;
            int relOffset = def.Offset - startOffset;

            // target.Prop = Unsafe.ReadUnaligned...
            Expression valueExpr = GetUnsafeReadExpression(bufferVar, relOffset, def.Type);

            if (prop.PropertyType != valueExpr.Type)
            {
                valueExpr = Expression.Convert(valueExpr, prop.PropertyType);
            }

            assignments.Add(Expression.Assign(Expression.Property(targetParam, prop), valueExpr));
        }

        // if (ReadBlock(...)) { assignments... }
        var readAndAssign = Expression.IfThen(readCall, Expression.Block(assignments));

        // Block Body
        var body = Expression.Block(
            [basePtrVar, bufferVar],
            Expression.Assign(basePtrVar, resolveCall),
            Expression.IfThen(
                Expression.NotEqual(basePtrVar, Expression.Constant(IntPtr.Zero)),
                Expression.Block(
                    bufferAssign,
                    readAndAssign
                )
            )
        );

        expressions.Add(body);
    }

    private static Expression GetUnsafeReadExpression(ParameterExpression buffer, int offset, string type)
    {
        Type targetType = type.ToLower() switch
        {
            "int" or "int32" => typeof(int),
            "float" or "single" => typeof(float),
            "double" => typeof(double),
            "bool" or "boolean" => typeof(bool),
            "short" or "int16" => typeof(short),
            "ushort" or "uint16" => typeof(ushort),
            _ => typeof(int)
        };

        var readMethod = typeof(MemoryContext<T>).GetMethod("ReadValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(targetType);

        return Expression.Call(readMethod, buffer, Expression.Constant(offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TVal ReadValue<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields |
                                    DynamicallyAccessedMemberTypes.NonPublicFields)]
    TVal>(byte[] buffer, int offset) where TVal : struct
    {
        return MemoryMarshal.Read<TVal>(buffer.AsSpan(offset));
    }
}