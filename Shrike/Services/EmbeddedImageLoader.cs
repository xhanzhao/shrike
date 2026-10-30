using System.Reflection;
using Eto.Drawing;

namespace Shrike.Services;

public static class EmbeddedImageLoader
{
    public static Bitmap? LoadBitmap(
        string resourceName)
    {
        var assembly =
            Assembly.GetExecutingAssembly();

        using var stream =
            assembly.GetManifestResourceStream(
                resourceName);

        if (stream is null)
            return null;

        return new Bitmap(stream);
    }

    /// <summary>
    /// Loads an embedded image and repaints every pixel in one
    /// color, keeping its alpha. Used for single-color artwork
    /// that has to follow Rhino's light and dark themes.
    /// </summary>
    public static Bitmap? LoadTintedBitmap(
        string resourceName,
        Color color)
    {
        using var source =
            LoadBitmap(
                resourceName);

        if (source is null)
            return null;

        var tinted =
            new Bitmap(
                source.Width,
                source.Height,
                PixelFormat.Format32bppRgba);

        using (var from = source.Lock())
        using (var to = tinted.Lock())
        {
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    var alpha =
                        from.GetPixel(x, y).A;

                    to.SetPixel(
                        x,
                        y,
                        new Color(
                            color,
                            alpha));
                }
            }
        }

        return tinted;
    }
}
