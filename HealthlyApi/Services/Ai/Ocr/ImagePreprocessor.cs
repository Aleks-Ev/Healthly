using SkiaSharp;

namespace HealthlyApi.Services.Ai.Ocr;

// Подготовка фото бланка к OCR (SkiaSharp). Качество распознавания больше всего зависит от этих шагов:
//   1. поворот по EXIF — телефон часто сохраняет снимок «на боку» и только помечает, как его повернуть;
//   2. оттенки серого — Tesseract всё равно работает с яркостью, цвет только мешает;
//   3. масштаб — у мелкого шрифта на маленьком снимке слишком мало пикселей, огромный снимок только замедляет OCR.
// На выходе — PNG без EXIF (заодно пропадают GPS-координаты и модель телефона из метаданных).
public static class ImagePreprocessor
{
    private const int MinWidth = 2000;          // меньше — увеличиваем (но не больше чем в MaxUpscale раз)
    private const int MaxWidth = 4000;          // больше — уменьшаем
    private const float MaxUpscale = 3f;
    private const long MaxPixels = 50_000_000;  // защита от «бомбы распаковки»: маленький файл, гигантская картинка

    // null — это не картинка, которую умеем читать
    public static byte[]? PrepareForOcr(byte[] image)
    {
        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data);
        if (codec == null || (long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return null;
        }

        using var source = SKBitmap.Decode(codec, codec.Info.WithColorType(SKColorType.Rgba8888));
        if (source == null)
        {
            return null;
        }

        // После поворота на 90°/270° ширина и высота меняются местами
        var rotation = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0,
        };
        var (width, height) = rotation is 90 or 270 ? (source.Height, source.Width) : (source.Width, source.Height);

        var scale = width < MinWidth ? Math.Min((float)MinWidth / width, MaxUpscale)
            : width > MaxWidth ? (float)MaxWidth / width
            : 1f;
        var targetWidth = (int)Math.Round(width * scale);
        var targetHeight = (int)Math.Round(height * scale);

        using var surface = SKSurface.Create(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White); // прозрачный фон PNG → белый, а не чёрный
        canvas.Translate(targetWidth / 2f, targetHeight / 2f);
        canvas.RotateDegrees(rotation);
        canvas.Scale(scale);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);

        using var paint = new SKPaint { ColorFilter = Grayscale() };
        using var sourceImage = SKImage.FromBitmap(source);
        canvas.DrawImage(sourceImage, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);

        using var result = surface.Snapshot();
        using var png = result.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    public static byte[] ToPng(SKBitmap bitmap)
    {
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    // Яркость по формуле Rec. 601: 0.299 R + 0.587 G + 0.114 B
    private static SKColorFilter Grayscale() => SKColorFilter.CreateColorMatrix(
    [
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0, 0, 0, 1, 0,
    ]);
}
