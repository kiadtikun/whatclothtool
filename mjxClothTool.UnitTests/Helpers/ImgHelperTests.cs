using mjxClothTool.Helpers;
using mjxClothTool.Models.Texture;
using ImageMagick;
using CodeWalker.Utils;

namespace mjxClothTool.UnitTests.Helpers;

public class ImgHelperTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Optimize_WritesRequestedTotalMipMapLevels(int requestedLevels)
    {
        using var source = new MagickImage(MagickColors.White, 64, 64);
        var details = new GTextureDetails
        {
            Width = 64,
            Height = 64,
            Compression = "D3DFMT_DXT5",
            MipMapCount = requestedLevels
        };

        var dds = await ImgHelper.Optimize(source.ToByteArray(MagickFormat.Png), details);

        Assert.NotNull(dds);
        Assert.Equal(requestedLevels, DDSIO.GetTexture(dds).Levels);
    }

    [Theory]
    [InlineData(2048, 2048, 10)]
    [InlineData(1024, 512, 8)]
    [InlineData(128, 256, 6)]
    public void GetCorrectMipMapAmount_UsesSmallestDimension(int width, int height, int expected)
    {
        Assert.Equal(expected, ImgHelper.GetCorrectMipMapAmount(width, height));
    }

    [Theory]
    [InlineData(512, 512, 512, 512)]
    [InlineData(513, 512, 1024, 512)]
    [InlineData(300, 700, 512, 1024)]
    public void CheckPowerOfTwo_ReturnsNextPowerOfTwoWhenNeeded(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ImgHelper.CheckPowerOfTwo(width, height));
    }
}
