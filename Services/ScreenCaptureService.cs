using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace Student.Services;

public static class ScreenCaptureService
{
    public static string CaptureScreenAsBase64Jpeg(int targetWidth = 480, long quality = 50L)
    {
        try
        {
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                screenWidth = 1920;
                screenHeight = 1080;
            }

            using var bitmap = new Bitmap(screenWidth, screenHeight);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
            }

            // Proportsional kichraytirish
            int targetHeight = (int)((float)screenHeight / screenWidth * targetWidth);
            using var resized = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
            }

            using var ms = new MemoryStream();
            var encoder = GetEncoder(ImageFormat.Jpeg);
            if (encoder != null)
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                resized.Save(ms, encoder, encoderParams);
            }
            else
            {
                resized.Save(ms, ImageFormat.Jpeg);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
                return codec;
        }
        return null;
    }
}
