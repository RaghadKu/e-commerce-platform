namespace ECommerce.Modules.Products.Application;

internal static class ImageSignature
{
    public const int HeaderLength = 12;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryDetect(ReadOnlySpan<byte> header, out string contentType, out string extension)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            (contentType, extension) = ("image/jpeg", ".jpg");
            return true;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(PngSignature))
        {
            (contentType, extension) = ("image/png", ".png");
            return true;
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            (contentType, extension) = ("image/webp", ".webp");
            return true;
        }

        (contentType, extension) = (string.Empty, string.Empty);
        return false;
    }

}
