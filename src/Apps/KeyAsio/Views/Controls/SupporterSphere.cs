using System.Buffers;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using KeyAsio.ViewModels;
using SkiaSharp;

namespace KeyAsio.Views.Controls;

public class SupporterSphere : UserControl
{
    private static readonly Comparison<SphereTag> ZOrderComparer = (a, b) => a.Z.CompareTo(b.Z);

    // SKResources (Cached for measurement)
    private static readonly SKTypeface DefaultTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
    private static readonly SKFont MeasureFont = new() { Typeface = DefaultTypeface, };

    private static readonly SKPaint TagBgPaint = new()
    {
        Color = new SKColor(16, 16, 16, 48), // 半透明黑色背景
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    private readonly List<SphereTag> _tags = new();
    private TopLevel? _cachedTopLevel;

    // Rotation state
    private double _currentVelocityX;
    private double _currentVelocityY;

    // Configuration
    private double _radius = 150;
    private const double Friction = 0.95;
    private const double AutoRotationSpeed = 0.02;
    private const double Sensitivity = 0.005;

    // Interaction state
    private bool _isDragging;
    private Point _lastMousePosition;
    private long _lastMoveTicks;

    private TimeSpan _lastFrameTime = TimeSpan.Zero;
    private bool _isRunning;

    public static readonly StyledProperty<System.Collections.IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SupporterSphere, System.Collections.IEnumerable?>(nameof(ItemsSource));

    public System.Collections.IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public SupporterSphere()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Ensure we receive hit test events
        Background = Brushes.Transparent;

        // 开启 ClipToBounds 可以避免绘制到控件外部，提升合成器效率
        ClipToBounds = true;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cachedTopLevel = TopLevel.GetTopLevel(this);

        _lastFrameTime = TimeSpan.Zero;
        _isRunning = true;

        // 启动循环
        _cachedTopLevel?.RequestAnimationFrame(OnFrame);
        UpdateTags();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isRunning = false;
        _cachedTopLevel = null;
        _tags.Clear();
    }

    private void OnFrame(TimeSpan timestamp)
    {
        if (!_isRunning) return;

        if (_lastFrameTime != TimeSpan.Zero)
        {
            double deltaMs = (timestamp - _lastFrameTime).TotalMilliseconds;

            double scale = deltaMs / 16.666; // Target 60fps => ~16.66ms
            scale = Math.Clamp(scale, 0.1, 4.0); // Clamp scale to avoid huge jumps if lag spike

            if (!_isDragging)
            {
                ApplyInertia(scale);
            }
        }

        _lastFrameTime = timestamp;
        _cachedTopLevel?.RequestAnimationFrame(OnFrame);
    }

    private void ApplyInertia(double scale)
    {
        // 1. 应用摩擦力
        double frictionFactor = Math.Pow(Friction, scale);
        _currentVelocityX *= frictionFactor;
        _currentVelocityY *= frictionFactor;

        // 2. 速度极低时切换到自动旋转
        if (Math.Abs(_currentVelocityX) < 0.0001 && Math.Abs(_currentVelocityY) < 0.0001)
        {
            _currentVelocityY = AutoRotationSpeed; // 默认给一点 Y 轴旋转
            _currentVelocityX = 0;
        }

        // 3. 计算本帧的旋转量
        double rotX = _currentVelocityX * scale;
        double rotY = _currentVelocityY * scale;

        // 4. 应用旋转
        PerformRotation(rotX, rotY);

        // 5. 触发重绘
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnCollectionChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnCollectionChanged;
            }

            UpdateTags();
        }
        else if (change.Property == BoundsProperty)
        {
            _radius = Math.Min(Bounds.Width, Bounds.Height) / 2 * 0.8;
            UpdateTags();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTags();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        _lastMoveTicks = DateTime.UtcNow.Ticks;
        e.Pointer.Capture(this);

        _currentVelocityX = 0;
        _currentVelocityY = 0;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            var currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _lastMousePosition.X;
            double deltaY = currentPos.Y - _lastMousePosition.Y;
            long nowTicks = DateTime.UtcNow.Ticks;

            // 1. 直接应用旋转 (Direct Manipulation)
            double rotY = -deltaX * Sensitivity;
            double rotX = -deltaY * Sensitivity;

            PerformRotation(rotX, rotY);
            InvalidateVisual();

            // 2. 计算基于时间的物理速度
            // 计算时间差 (ms)
            double dtMs = (nowTicks - _lastMoveTicks) / 10000.0;

            // 避免除以零或极小值带来的数值不稳定
            if (dtMs < 1.0) dtMs = 1.0;

            // 计算这一瞬间的"每16.66ms帧"的理论位移量
            // 速度 = (位移 / 时间) * 标准帧时间
            double instantVelY = (rotY / dtMs) * 16.666;
            double instantVelX = (rotX / dtMs) * 16.666;

            // 使用指数移动平均 (EMA) 平滑速度，避免抖动
            // Alpha 值越小越平滑，越大响应越快。0.3 是一个经验值。
            const double alpha = 0.3;
            _currentVelocityY = _currentVelocityY * (1 - alpha) + instantVelY * alpha;
            _currentVelocityX = _currentVelocityX * (1 - alpha) + instantVelX * alpha;

            _lastMousePosition = currentPos;
            _lastMoveTicks = nowTicks;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // 如果最后一次移动距离现在超过 50ms，则认为已经停止，消除惯性
        if ((DateTime.UtcNow.Ticks - _lastMoveTicks) / 10000.0 > 50)
        {
            _currentVelocityX = 0;
            _currentVelocityY = 0;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void UpdateTags()
    {
        var isDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        TagBgPaint.Color = isDarkMode
            ? new SKColor(16, 16, 16, 48)
            : new SKColor(242, 242, 242, 210);
        _tags.Clear();

        if (ItemsSource == null)
        {
            InvalidateVisual();
            return;
        }

        var items = ItemsSource.Cast<object>().ToList();
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            // Fibonacci Sphere Algorithm
            double phi = Math.Acos(-1.0 + (2.0 * i + 1.0) / count);
            double theta = Math.Sqrt(count * Math.PI) * phi;

            double x = _radius * Math.Cos(theta) * Math.Sin(phi);
            double y = _radius * Math.Sin(theta) * Math.Sin(phi);
            double z = _radius * Math.Cos(phi);

            var item = items[i];
            var tag = CreateTag(item, isDarkMode);

            tag.X = x;
            tag.Y = y;
            tag.Z = z;

            _tags.Add(tag);
        }

        _tags.Sort(ZOrderComparer);
        InvalidateVisual();
    }

    private SphereTag CreateTag(object item, bool isDarkMode)
    {
        string text = item.ToString() ?? "";
        SKColor color = isDarkMode
            ? new SKColor(0xE0C0C0C0)
            : new SKColor(30, 30, 30, 210);
        float fontSize = 12;

        if (item is SupporterItem supporter)
        {
            text = supporter.Name;
            if (supporter.Tier.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            {
                color = isDarkMode ? SKColors.Gold : SKColors.DarkGoldenrod;
                fontSize = 20;
            }
            else if (supporter.Tier.Contains("Silver", StringComparison.OrdinalIgnoreCase))
            {
                color = isDarkMode ? SKColors.Cornsilk : SKColors.SlateGray;
                fontSize = 16;
            }
        }

        MeasureFont.Size = fontSize;

        // 测量宽度
        float textWidth = MeasureFont.MeasureText(text);

        // 测量高度 (Ascent + Descent)
        MeasureFont.GetFontMetrics(out var metrics);
        // 通常高度取 descent - ascent (ascent 是负值)
        float textHeight = metrics.Descent - metrics.Ascent;

        float paddingH = 8; // 水平内边距
        float paddingV = 3; // 垂直内边距

        // 预渲染为图片
        int imgWidth = (int)Math.Ceiling(textWidth + paddingH * 2);
        int imgHeight = (int)Math.Ceiling(textHeight + paddingV * 2);

        using var surface = SKSurface.Create(new SKImageInfo(imgWidth, imgHeight));

        using var textPaint = new SKPaint();
        textPaint.Color = color;
        textPaint.IsAntialias = true;

        using var font = new SKFont();
        font.Size = fontSize;
        font.Typeface = DefaultTypeface;

        // Clear transparent
        surface.Canvas.Clear(SKColors.Transparent);

        var rect = new SKRect(0, 0, imgWidth, imgHeight);
        surface.Canvas.DrawRoundRect(rect, 6, 6, TagBgPaint); // 6是圆角半径

        float textX = paddingH;
        float textY = paddingV - metrics.Ascent;

        surface.Canvas.DrawText(text, textX, textY, font, textPaint); // +1 for padding

        var image = surface.Snapshot();

        return new SphereTag
        {
            HalfWidth = imgWidth / 2.0,
            HalfHeight = imgHeight / 2.0,
            CachedImage = image
        };
    }

    private void PerformRotation(double rotX, double rotY)
    {
        double sinX = Math.Sin(rotX);
        double cosX = Math.Cos(rotX);
        double sinY = Math.Sin(rotY);
        double cosY = Math.Cos(rotY);

        for (var i = 0; i < _tags.Count; i++)
        {
            var tag = _tags[i];
            double x = tag.X;
            double y = tag.Y;
            double z = tag.Z;

            // 1. Rotate around Y axis (Horizontal drag affects X and Z)
            double x1 = x * cosY - z * sinY;
            double z1 = z * cosY + x * sinY;

            // 2. Rotate around X axis (Vertical drag affects Y and Z)
            double y2 = y * cosX - z1 * sinX;
            double z2 = z1 * cosX + y * sinX;

            // Update mutable coordinates
            tag.X = x1;
            tag.Y = y2;
            tag.Z = z2;

            // 3. Normalize to radius (prevent floating point drift)
            double len = Math.Sqrt(tag.X * tag.X + tag.Y * tag.Y + tag.Z * tag.Z);
            if (len > 0)
            {
                // Snap back to current radius
                double s = _radius / len;
                tag.X *= s;
                tag.Y *= s;
                tag.Z *= s;
            }
        }

        _tags.Sort(ZOrderComparer);
    }

    public override void Render(DrawingContext context)
    {
        if (_tags.Count == 0) return;

        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;
        double radius = _radius;

        var tags = _tags;
        var count = tags.Count;

        var pool = ArrayPool<TagRenderData>.Shared;
        var dataArray = pool.Rent(count);

        int renderCount = 0;

        for (var i = 0; i < count; i++)
        {
            var tag = tags[i];

            // Perspective Scale
            double tagScale = (radius * 2 + tag.Z) / (radius * 3);
            // Opacity
            double alpha = (tag.Z + radius) / (2 * radius);

            if (count >= 1000)
            {
                if (tagScale < 0.45) continue;
                if (alpha < 0.45) continue;
            }
            else if (count >= 500)
            {
                if (tagScale < 0.3) continue;
                if (alpha < 0.3) continue;
            }
            else
            {
                if (tagScale < 0.1) continue;
                if (alpha < 0.0666666666) continue;
            }

            double opacity = Math.Clamp(alpha + 0.2, 0.2, 1.0);

            // Position
            float x = (float)(cx + tag.X);
            float y = (float)(cy + tag.Y);
            float s = (float)tagScale;

            dataArray[renderCount++] = new TagRenderData(
                tag.CachedImage!,
                x,
                y,
                s,
                (float)opacity
            );
        }

        var op = new SphereDrawOperation(Bounds, dataArray, renderCount);
        context.Custom(op);
    }

    private readonly struct TagRenderData
    {
        public readonly SKImage? Image;
        public readonly float X;
        public readonly float Y;
        public readonly float Scale;
        public readonly float Opacity;

        public TagRenderData(SKImage image, float x, float y, float scale, float opacity)
        {
            Image = image;
            X = x;
            Y = y;
            Scale = scale;
            Opacity = opacity;
        }
    }

    private class SphereDrawOperation : ICustomDrawOperation
    {
        private static readonly ThreadLocal<SKPaint> PaintCache = new(() => new SKPaint
        {
            IsAntialias = true
        });

        private readonly TagRenderData[] _rentedData;
        private readonly int _count;

        public Rect Bounds { get; }

        public SphereDrawOperation(Rect bounds, TagRenderData[] rentedData, int count)
        {
            Bounds = bounds;
            _rentedData = rentedData;
            _count = count;
        }

        public void Dispose()
        {
            ArrayPool<TagRenderData>.Shared.Return(_rentedData);
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            // Retrieve cached instances
            var paint = PaintCache.Value!;

            for (var i = 0; i < _count; i++)
            {
                // 使用 ref 避免结构体拷贝
                ref readonly var tag = ref _rentedData[i];
                if (tag.Image == null) continue;

                paint.Color = SKColors.White.WithAlpha((byte)(255 * tag.Opacity));

                var w = tag.Image.Width * tag.Scale;
                var h = tag.Image.Height * tag.Scale;

                var dest = SKRect.Create(
                    tag.X - w / 2.0f,
                    tag.Y - h / 2.0f,
                    w,
                    h);

                canvas.DrawImage(tag.Image, dest, new SKSamplingOptions(SKFilterMode.Linear), paint);
            }
        }
    }

    private class SphereTag
    {
        public SKImage? CachedImage { get; set; }

        public double HalfWidth { get; init; }
        public double HalfHeight { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}