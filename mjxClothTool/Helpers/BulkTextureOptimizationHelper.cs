using mjxClothTool.Models.Texture;
using System;

namespace mjxClothTool.Helpers;

public static class BulkTextureOptimizationHelper
{
    private const int MaxTextureDimension = 2048;

    public static bool TryCreateDetails(GTextureDetails source, bool downscale, bool changeCompression,
        string mipmapChoice, out GTextureDetails result)
    {
        result = null;
        if (source == null || source.Width <= 0 || source.Height <= 0) return false;

        var is2048Square = source.Width == MaxTextureDimension && source.Height == MaxTextureDimension;
        var needsDownscale = downscale &&
            (source.Width > MaxTextureDimension || source.Height > MaxTextureDimension || is2048Square);
        if (!needsDownscale && !changeCompression && mipmapChoice == "Keep original") return false;

        var (width, height) = ImgHelper.CheckPowerOfTwo(source.Width, source.Height);
        if (needsDownscale)
        {
            var newWidth = width;
            var newHeight = height;
            while (newWidth > MaxTextureDimension || newHeight > MaxTextureDimension)
            {
                newWidth /= 2;
                newHeight /= 2;
            }
            if (is2048Square)
            {
                newWidth /= 2;
                newHeight /= 2;
            }
            if (newWidth >= 4 && newHeight >= 4)
            {
                width = newWidth;
                height = newHeight;
            }
            else if (!changeCompression && mipmapChoice == "Keep original") return false;
        }

        var maximumMipmaps = Math.Max(1, ImgHelper.GetCorrectMipMapAmount(width, height));
        var mipmapCount = mipmapChoice switch
        {
            "Keep original" when width == source.Width && height == source.Height => source.MipMapCount,
            "Keep original" => Math.Min(Math.Max(1, source.MipMapCount), maximumMipmaps),
            "Auto" => maximumMipmaps,
            _ => Math.Min(int.Parse(mipmapChoice), maximumMipmaps)
        };

        var compression = !changeCompression && source.Compression is
            "D3DFMT_DXT1" or "D3DFMT_DXT3" or "D3DFMT_DXT5" or "D3DFMT_A8R8G8B8"
            ? source.Compression : "D3DFMT_DXT5";

        // A matching source already has the requested output. Re-encoding a lossy
        // DXT texture only creates another file and can degrade its image quality.
        if (width == source.Width && height == source.Height &&
            mipmapCount == source.MipMapCount &&
            string.Equals(compression, source.Compression, StringComparison.OrdinalIgnoreCase))
            return false;

        result = new GTextureDetails
        {
            Name = source.Name,
            Width = width,
            Height = height,
            Compression = compression,
            MipMapCount = mipmapCount,
            IsOptimizeNeeded = false
        };
        return true;
    }
}
