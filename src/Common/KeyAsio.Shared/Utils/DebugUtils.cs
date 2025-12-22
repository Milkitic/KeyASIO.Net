using System.Runtime.CompilerServices;
using System.Text;
using KeyAsio.Core.Audio.Utils;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Utils;

public static class DebugUtils
{
    private const string Normal = "│ ";
    private const string Middle = "├─ ";
    private const string Last = "└─ ";

    public static string ToFullTypeMessage(this Exception exception)
    {
        return ExceptionToFullMessage(exception, new StringBuilder(), 0, true, true)!;
    }

    public static string ToSimpleTypeMessage(this Exception exception)
    {
        return ExceptionToFullMessage(exception, new StringBuilder(), 0, true, false)!;
    }

    public static string ToMessage(this Exception exception)
    {
        return ExceptionToFullMessage(exception, new StringBuilder(), 0, true, null)!;
    }

    private static string? ExceptionToFullMessage(Exception exception, StringBuilder stringBuilder, int deep,
        bool isLastItem, bool? includeFullType)
    {
        var hasChild = exception.InnerException != null;
        if (deep > 0)
        {
            for (int i = 0; i < deep; i++)
            {
                if (i == deep - 1)
                {
                    stringBuilder.Append((isLastItem && !hasChild) ? Last : Middle);
                }
                else
                {
                    stringBuilder.Append(Normal + " ");
                }
            }
        }

        var agg = exception as AggregateException;
        if (includeFullType == true)
        {
            var prefix = agg == null ? exception.GetType().ToString() : "!!AggregateException";
            stringBuilder.Append($"{prefix}: {GetTrueExceptionMessage(exception)}");
        }
        else if (includeFullType == false)
        {
            var prefix = exception.GetType().Name;
            stringBuilder.Append($"{prefix}: {GetTrueExceptionMessage(exception)}");
        }
        else
        {
            stringBuilder.Append(GetTrueExceptionMessage(exception));
        }

        stringBuilder.AppendLine();
        if (!hasChild)
        {
            return deep == 0 ? stringBuilder.ToString().Trim() : null;
        }

        if (agg != null)
        {
            for (int i = 0; i < agg.InnerExceptions.Count; i++)
            {
                ExceptionToFullMessage(agg.InnerExceptions[i], stringBuilder, deep + 1,
                    i == agg.InnerExceptions.Count - 1, includeFullType);
            }
        }
        else
        {
            ExceptionToFullMessage(exception.InnerException!, stringBuilder, deep + 1, true, includeFullType);
        }

        return deep == 0 ? stringBuilder.ToString().Trim() : null;

        static string GetTrueExceptionMessage(Exception ex)
        {
            if (ex is AggregateException { InnerException: { } } agg)
            {
                var complexMessage = agg.Message;
                var i = complexMessage.IndexOf(agg.InnerException.Message, StringComparison.Ordinal);
                if (i == -1)
                    return complexMessage;
                return complexMessage.Substring(0, i - 2);
            }

            return string.IsNullOrWhiteSpace(ex.Message) ? "{Empty Message}" : ex.Message;
        }
    }

    public static void InvokeAndPrint(Action method, string caller = "anonymous method")
    {
        var sw = HighPrecisionTimer.StartNew();
        method?.Invoke();
        Console.WriteLine($"[{caller}] Executed in {sw.Elapsed.TotalMilliseconds:#0.000} ms");
    }

    public static T InvokeAndPrint<T>(Func<T> method, string caller = "anonymous method")
    {
        var sw = HighPrecisionTimer.StartNew();
        var value = method.Invoke();
        Console.WriteLine($"[{caller}] Executed in {sw.Elapsed.TotalMilliseconds:#0.000} ms");
        return value;
    }

    public static IDisposable CreateTimer(string name, ILogger? logger = null)
    {
        return new TimerImpl(name, logger);
    }

    private class TimerImpl : IDisposable
    {
        private readonly string _name;
        private readonly ILogger? _logger;
        private readonly HighPrecisionTimer _sw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimerImpl(string name, ILogger? logger)
        {
            _name = name;
            _logger = logger;

            if (_logger == null)
            {
                Console.WriteLine($"[{_name}] executing");
            }
            else
            {
                _logger.LogTrace("[{Name}] executing", _name);
            }

            _sw = HighPrecisionTimer.StartNew();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_logger == null)
            {
                Console.WriteLine($"[{_name}] executed in {_sw.Elapsed.TotalMilliseconds:#0.000}ms");
            }
            else
            {
                _logger.LogDebug("[{Name}] executed in {Elapsed:#0.000}ms", _name, _sw.Elapsed.TotalMilliseconds);
            }
        }
    }
}
