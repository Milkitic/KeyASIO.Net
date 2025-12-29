using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Controls;

public partial class SponsorSphere : UserControl
{
    private static readonly Comparison<SphereTag> ZOrderComparer = (a, b) => a.Z.CompareTo(b.Z);

    // Visual Resources
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#20000000"));
    private static readonly Typeface DefaultTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private readonly List<SphereTag> _tags = new();
    private TopLevel? _cachedTopLevel;

    // Rotation state
    private double _currentVelocityX;
    private double _currentVelocityY;

    // Configuration
    private double _radius = 150;
    private const double Friction = 0.95;
    private const double AutoRotationSpeed = 0.002;
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
            //InvalidateVisual();
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
            var tag = CreateTag(item, DefaultTypeface);

            tag.X = x;
            tag.Y = y;
            tag.Z = z;

            _tags.Add(tag);
        }

        _tags.Sort(ZOrderComparer);
        InvalidateVisual();
    }

    private SphereTag CreateTag(object item, Typeface typeface)
    {
        string text = item.ToString() ?? "";    
        IBrush foreground = Brushes.White;
        double fontSize = 14;

        if (item is SponsorItem sponsor)
        {
            text = sponsor.Name;
            if (sponsor.Tier.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            {
                foreground = Brushes.Gold;
                fontSize = 16;
            }
            else if (sponsor.Tier.Contains("Silver", StringComparison.OrdinalIgnoreCase))
            {
                foreground = Brushes.Silver;
            }
        }

        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            foreground
        );

        return new SphereTag(formattedText);
    }

    private void PerformRotation(double rotX, double rotY)
    {
        double sinX = Math.Sin(rotX);
        double cosX = Math.Cos(rotX);
        double sinY = Math.Sin(rotY);
        double cosY = Math.Cos(rotY);

        foreach (var tag in _tags)
        {
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
        base.Render(context);

        if (_tags.Count == 0) return;

        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;
        double radius = _radius; // 提取局部变量

        for (var i = 0; i < _tags.Count; i++)
        {
            var tag = _tags[i];

            // Perspective Scale
            double tagScale = (radius * 2 + tag.Z) / (radius * 3);
            if (tagScale < 0.1) continue;

            // Opacity
            double alpha = (tag.Z + radius) / (2 * radius);
            if (alpha < 0.1) continue;

            double opacity = Math.Clamp(alpha + 0.2, 0.2, 1.0);

            // Position
            double x = cx + tag.X;
            double y = cy + tag.Y;

            var transform = new Matrix(tagScale, 0, 0, tagScale, x, y);

            // Scale then Translate
            using (context.PushOpacity(opacity))
            using (context.PushTransform(transform))
            {
                double halfW = tag.HalfWidth;
                double halfH = tag.HalfHeight;

                // Draw centered
                var rect = new Rect(-halfW - 5, -halfH - 5, tag.Width + 10, tag.Height + 10);

                context.DrawRectangle(BackgroundBrush, null, new RoundedRect(rect, 4));

                // Draw Text
                context.DrawText(tag.FormattedText, new Point(-halfW, -halfH));
            }
        }
    }

    private class SphereTag
    {
        public FormattedText FormattedText { get; }
        public double Width { get; }
        public double Height { get; }
        public double HalfWidth { get; }
        public double HalfHeight { get; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public SphereTag(FormattedText text)
        {
            FormattedText = text;
            Width = text.Width;
            Height = text.Height;
            HalfWidth = Width / 2;
            HalfHeight = Height / 2;
        }
    }
}