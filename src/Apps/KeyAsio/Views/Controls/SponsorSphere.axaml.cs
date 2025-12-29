using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using KeyAsio.ViewModels;

namespace KeyAsio.Views.Controls;

public partial class SponsorSphere : UserControl
{
    private readonly List<SphereTag> _tags = new();

    // Rotation state
    private double _angleX;
    private double _angleY;
    private double _currentVelocityX;
    private double _currentVelocityY;

    // Configuration
    private double _radius = 150;
    private const double Friction = 0.95;
    private const double AutoRotationSpeed = 0.002;

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

        // Mouse interaction
        ParticleCanvas.PointerPressed += OnPointerPressed;
        ParticleCanvas.PointerMoved += OnPointerMoved;
        ParticleCanvas.PointerReleased += OnPointerReleased;
        // ParticleCanvas.PointerWheelChanged += OnPointerWheelChanged; // Optional zoom
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _lastFrameTime = TimeSpan.Zero;
        _isRunning = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        UpdateTags();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isRunning = false;
    }

    private void OnFrame(TimeSpan timestamp)
    {
        if (!_isRunning) return;

        if (_lastFrameTime != TimeSpan.Zero)
        {
            double deltaMs = (timestamp - _lastFrameTime).TotalMilliseconds;
            // Target 60fps => ~16.66ms
            double scale = deltaMs / 16.666;

            // Clamp scale to avoid huge jumps if lag spike
            scale = Math.Clamp(scale, 0.1, 4.0);

            UpdatePositions(scale);
        }
        else
        {
            // First frame, just update with scale 1 or 0? 
            // Better to skip movement or assume 1.0
            UpdatePositions(1.0);
        }

        _lastFrameTime = timestamp;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
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
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTags();
    }

    private void UpdateTags()
    {
        ParticleCanvas.Children.Clear();
        _tags.Clear();

        if (ItemsSource == null) return;

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
            var control = CreateTagControl(item);

            var scaleTransform = new ScaleTransform();
            var translateTransform = new TranslateTransform();
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            control.RenderTransform = transformGroup;

            var tag = new SphereTag
            {
                Control = control,
                X = x,
                Y = y,
                Z = z,
                ScaleTransform = scaleTransform,
                TranslateTransform = translateTransform
            };

            tag.UpdateSize();
            control.Tag = tag;
            control.SizeChanged += OnTagSizeChanged;
            _tags.Add(tag);

            ParticleCanvas.Children.Add(control);
        }
    }

    private Control CreateTagControl(object item)
    {
        if (item is SponsorItem sponsor)
        {
            var textBlock = new TextBlock
            {
                Text = sponsor.Name,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White, // Will be styled later
                FontSize = 14
            };

            // Add some tier-based styling if needed
            if (sponsor.Tier.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Foreground = Brushes.Gold;
                textBlock.FontSize = 16;
            }
            else if (sponsor.Tier.Contains("Silver", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Foreground = Brushes.Silver;
            }

            var border = new Border
            {
                Child = textBlock,
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Color.Parse("#20000000")),
                CornerRadius = new CornerRadius(4)
            };

            return border;
        }

        return new TextBlock { Text = item.ToString() };
    }

    private void UpdatePositions(double scale)
    {
        if (_isDragging)
        {
            // Velocity is calculated in PointerMoved
        }
        else
        {
            // Apply friction
            double frictionFactor = Math.Pow(Friction, scale);
            _currentVelocityX *= frictionFactor;
            _currentVelocityY *= frictionFactor;

            // Auto rotation if velocity is very low
            if (Math.Abs(_currentVelocityX) < 0.0001 && Math.Abs(_currentVelocityY) < 0.0001)
            {
                _currentVelocityY = AutoRotationSpeed;
            }
        }

        // --- Trackball / Incremental Rotation Logic ---
        // Instead of accumulating global angles, we rotate the current coordinates
        // by the current velocity (delta angle).

        double rotX = _currentVelocityX * scale;
        double rotY = _currentVelocityY * scale;

        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;

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
            // Corresponds to rotation around screen's vertical axis
            double x1 = x * cosY - z * sinY;
            double z1 = z * cosY + x * sinY;

            // 2. Rotate around X axis (Vertical drag affects Y and Z)
            // Corresponds to rotation around screen's horizontal axis
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

            // --- Rendering ---

            // Perspective Scale
            double tagScale = (_radius * 2 + tag.Z) / (_radius * 3);

            // Opacity
            double alpha = (tag.Z + _radius) / (2 * _radius);
            tag.Control.Opacity = Math.Clamp(alpha + 0.2, 0.2, 1.0);

            // Scale transform
            tag.ScaleTransform.ScaleX = tagScale;
            tag.ScaleTransform.ScaleY = tagScale;

            // Position
            double left = cx + tag.X - tag.HalfWidth;
            double top = cy + tag.Y - tag.HalfHeight;

            tag.TranslateTransform.X = left;
            tag.TranslateTransform.Y = top;

            // ZIndex
            int newZIndex = (int)tag.Z;
            if (tag.Control.ZIndex != newZIndex)
            {
                tag.Control.ZIndex = newZIndex;
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        e.Pointer.Capture(ParticleCanvas);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _lastMousePosition.X;
            double deltaY = currentPos.Y - _lastMousePosition.Y;

            _currentVelocityY = -deltaX * 0.005;
            _currentVelocityX = -deltaY * 0.005;

            _lastMousePosition = currentPos;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void OnTagSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Control { Tag: SphereTag tag })
        {
            tag.UpdateSize();
        }
    }

    private class SphereTag
    {
        public Control Control { get; set; } = null!;
        public ScaleTransform ScaleTransform { get; set; } = null!;
        public TranslateTransform TranslateTransform { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double HalfWidth { get; private set; }
        public double HalfHeight { get; private set; }

        public void UpdateSize()
        {
            var width = Control.Bounds.Width > 0 ? Control.Bounds.Width : Control.DesiredSize.Width;
            var height = Control.Bounds.Height > 0 ? Control.Bounds.Height : Control.DesiredSize.Height;

            HalfWidth = width / 2;
            HalfHeight = height / 2;
        }
    }
}