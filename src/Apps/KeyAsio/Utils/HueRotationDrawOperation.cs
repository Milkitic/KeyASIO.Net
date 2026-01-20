using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace KeyAsio.Utils;

public static class SkiaColorMatrixUtils
{
    public static SKColorFilter CreateHueFilter(float angleDegrees)
    {
        // 将角度转为弧度
        double angleRad = Math.PI * angleDegrees / 180.0;
        float cosA = (float)Math.Cos(angleRad);
        float sinA = (float)Math.Sin(angleRad);

        // 亮度系数 (Luma coefficients)
        const float lumR = 0.213f;
        const float lumG = 0.715f;
        const float lumB = 0.072f;

        // 构建 Hue 旋转矩阵
        float[] matrix =
        [
            lumR + cosA * (1 - lumR) + sinA * (-lumR),    lumG + cosA * (-lumG) + sinA * (-lumG),   lumB + cosA * (-lumB) + sinA * (1 - lumB), 0, 0,
            lumR + cosA * (-lumR) + sinA * 0.143f,        lumG + cosA * (1 - lumG) + sinA * 0.140f, lumB + cosA * (-lumB) + sinA * (-0.283f),  0, 0,
            lumR + cosA * (-lumR) + sinA * (-(1 - lumR)), lumG + cosA * (-lumG) + sinA * lumG,      lumB + cosA * (1 - lumB) + sinA * lumB,    0, 0,
            0, 0, 0, 1, 0
        ];

        return SKColorFilter.CreateColorMatrix(matrix);
    }
}

public class HueRotationDrawOperation : ICustomDrawOperation
{
    private readonly SKPicture _picture;
    private readonly float _hueAngle;
    private readonly Rect _bounds;

    public HueRotationDrawOperation(Rect bounds, SKPicture picture, float hueAngle)
    {
        _bounds = bounds;
        _picture = picture;
        _hueAngle = hueAngle;
    }

    public Rect Bounds => _bounds;

    public void Dispose() { /* 无需释放，SKPicture 由外部管理 */ }

    public bool HitTest(Point p) => _bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        int count = canvas.Save();


        try
        {
            using var paint = new SKPaint();
            paint.ColorFilter = SkiaColorMatrixUtils.CreateHueFilter(_hueAngle);
            //paint.IsAntialias = true;

            canvas.DrawPicture(_picture, paint);
        }
        finally
        {
            canvas.RestoreToCount(count);
        }
    }
}