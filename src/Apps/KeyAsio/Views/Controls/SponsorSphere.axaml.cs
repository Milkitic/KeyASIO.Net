using System.Buffers;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KeyAsio.ViewModels;
using SkiaSharp;

namespace KeyAsio.Views.Controls;

public partial class SponsorSphere : UserControl
{
    private static readonly Comparison<SphereTag> ZOrderComparer = (a, b) => a.Z.CompareTo(b.Z);

    // SKResources (Cached for measurement)
    private static readonly SKTypeface DefaultTypeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
    private static readonly SKFont MeasureFont = new() { Typeface = DefaultTypeface, };

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

    private TimeSpan _lastFrameTime = TimeSpan.Zero;
    private bool _isRunning;

    public static readonly StyledProperty<System.Collections.IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SponsorSphere, System.Collections.IEnumerable?>(nameof(ItemsSource));

    public System.Collections.IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public SponsorSphere()
    {
        InitializeComponent();

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

            // 1. 直接应用旋转 (Direct Manipulation)
            double rotY = -deltaX * Sensitivity;
            double rotX = -deltaY * Sensitivity;

            PerformRotation(rotX, rotY);
            InvalidateVisual();

            // 2. 记录当前作为"投掷"速度，供松开鼠标后的惯性使用
            _currentVelocityY = rotY;
            _currentVelocityX = rotX;

            _lastMousePosition = currentPos;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
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
            var tag = CreateTag(item);

            tag.X = x;
            tag.Y = y;
            tag.Z = z;

            _tags.Add(tag);
        }

        _tags.Sort(ZOrderComparer);
        InvalidateVisual();
    }

    private SphereTag CreateTag(object item)
    {
        string text = item.ToString() ?? "";
        SKColor color = SKColors.White;
        float fontSize = 14;

        if (item is SponsorItem sponsor)
        {
            text = sponsor.Name;
            if (sponsor.Tier.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            {
                color = SKColors.Gold;
                fontSize = 16;
            }
            else if (sponsor.Tier.Contains("Silver", StringComparison.OrdinalIgnoreCase))
            {
                color = SKColors.Silver;
            }
        }

        MeasureFont.Size = fontSize;

        // 测量宽度
        float width = MeasureFont.MeasureText(text);

        // 测量高度 (Ascent + Descent)
        MeasureFont.GetFontMetrics(out var metrics);
        // 通常高度取 descent - ascent (ascent 是负值)
        float height = metrics.Descent - metrics.Ascent;

        return new SphereTag
        {
            Text = text,
            Color = color,
            FontSize = fontSize,
            Width = width,
            Height = height,
            HalfWidth = width / 2.0,
            HalfHeight = height / 2.0,
            // 预计算 Baseline 偏移量，渲染时就不用重复计算了
            BaselineOffset = -metrics.Ascent - height / 2 // 将文字垂直居中的偏移量
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
            if (tagScale < 0.1) continue;

            // Opacity
            double alpha = (tag.Z + radius) / (2 * radius);
            if (alpha < 0.0666666666) continue;

            double opacity = Math.Clamp(alpha + 0.2, 0.2, 1.0);

            // Position
            float x = (float)(cx + tag.X);
            float y = (float)(cy + tag.Y);
            float s = (float)tagScale;

            // 构造 Matrix: Scale(s, s) + Translate(x, y)
            var transform = new SKMatrix(s, 0, x, 0, s, y, 0, 0, 1);

            double halfW = tag.HalfWidth;
            double halfH = tag.HalfHeight;

            // Background rect
            var rect = new SKRect((float)(-halfW - 5), (float)(-halfH - 5), (float)(halfW + 5), (float)(halfH + 5));

            dataArray[renderCount++] = new TagRenderData(
                tag.Text,
                tag.Color,
                tag.FontSize,
                transform,
                (float)opacity,
                rect,
                (float)tag.BaselineOffset
            );
        }

        var op = new SphereDrawOperation(Bounds, dataArray, renderCount, DefaultTypeface);
        context.Custom(op);
    }

    private readonly struct TagRenderData
    {
        public readonly string Text;
        public readonly SKColor Color;
        public readonly float FontSize;
        public readonly SKMatrix Transform;
        public readonly float Opacity;
        public readonly SKRect BackgroundRect;
        public readonly float BaselineOffset; // 预计算的偏移

        public TagRenderData(string text, SKColor color, float fontSize, SKMatrix transform, float opacity,
            SKRect backgroundRect, float baselineOffset)
        {
            Text = text;
            Color = color;
            FontSize = fontSize;
            Transform = transform;
            Opacity = opacity;
            BackgroundRect = backgroundRect;
            BaselineOffset = baselineOffset;
        }
    }

    private class SphereDrawOperation : ICustomDrawOperation
    {
        private readonly TagRenderData[] _data; // 这是一个从 ArrayPool 借来的数组
        private readonly int _count;
        private readonly SKTypeface _typeface;

        public Rect Bounds { get; }

        public SphereDrawOperation(Rect bounds, TagRenderData[] data, int count, SKTypeface typeface)
        {
            Bounds = bounds;
            _data = data;
            _count = count;
            _typeface = typeface;
        }

        public void Dispose()
        {
            ArrayPool<TagRenderData>.Shared.Return(_data);
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            // 局部创建 Paint，避免多实例干扰。SkiaSharp 的 Paint 创建非常快。
            using var bgPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var textPaint = new SKPaint
            {
                IsAntialias = true, // 记得开启抗锯齿，否则文字很难看
            };

            // 使用 SKFont 来控制字体大小和 Typeface
            using var font = new SKFont(_typeface)
            {
                Subpixel = true,
                Hinting = SKFontHinting.None,
                LinearMetrics = true,
                BaselineSnap = false,
                Edging = SKFontEdging.SubpixelAntialias
            };

            for (var i = 0; i < _count; i++)
            {
                // 使用 ref 避免结构体拷贝
                ref readonly var tag = ref _data[i];

                int save = canvas.Save();
                canvas.Concat(tag.Transform);

                // Draw Background
                byte bgAlpha = (byte)(32 * tag.Opacity);
                if (bgAlpha > 0)
                {
                    bgPaint.Color = new SKColor(0, 0, 0, bgAlpha);
                    canvas.DrawRoundRect(tag.BackgroundRect, 4, 4, bgPaint);
                }

                // Draw Text
                // 设置字体大小
                font.Size = tag.FontSize;

                // 设置颜色
                textPaint.Color = tag.Color.WithAlpha((byte)(255 * tag.Opacity));

                // 绘制
                canvas.DrawText(tag.Text, 0, tag.BaselineOffset, SKTextAlign.Center, font, textPaint);

                canvas.RestoreToCount(save);
            }
        }
    }

    private class SphereTag
    {
        public string Text { get; init; } = "";
        public SKColor Color { get; init; }
        public float FontSize { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double HalfWidth { get; init; }
        public double HalfHeight { get; init; }
        public double BaselineOffset { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}