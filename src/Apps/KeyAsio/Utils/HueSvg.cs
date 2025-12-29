using Avalonia;
using Avalonia.Media;

namespace KeyAsio.Utils;

public class HueSvg : Avalonia.Svg.Skia.Svg
{
    public static readonly StyledProperty<float> HueProperty =
        AvaloniaProperty.Register<HueSvg, float>(nameof(Hue), 0f);

    public float Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }
    static HueSvg()
    {
        AffectsRender<HueSvg>(HueProperty);
    }

    public HueSvg(Uri baseUri) : base(baseUri)
    {
    }

    public HueSvg(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override void Render(DrawingContext context)
    {
        var picture = this.Picture;

        if (picture is null)
        {
            return;
        }

        // Code from base class
        var viewPort = new Rect(Bounds.Size);
        var sourceSize = new Size(picture.CullRect.Width, picture.CullRect.Height);
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .Intersect(viewPort);
        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale));

        var bounds = picture.CullRect;
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X - bounds.Left,
            -sourceRect.Y + destRect.Y - bounds.Top);

        var userMatrix = Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(PanX, PanY);

        using (context.PushClip(destRect))
        using (context.PushTransform(scaleMatrix * translateMatrix * userMatrix))
        {
            context.Custom(new HueRotationDrawOperation(
                new Rect(0, 0, bounds.Width, bounds.Height),
                picture, // 传入 SKPicture
                Hue // 传入当前的 Hue 值
            ));
        }
    }
}