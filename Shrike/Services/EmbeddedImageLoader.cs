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
}
