using mjxClothTool.Helpers;
using mjxClothTool.Models.Texture;

namespace mjxClothTool.UnitTests.Helpers;

public class BulkTextureOptimizationHelperTests
{
    [Theory]
    [InlineData(1024, 1024, "D3DFMT_DXT5", 1, false, true, "1", false)]
    [InlineData(1024, 1024, "D3DFMT_DXT5", 1, false, true, "Auto", true)]
    [InlineData(1024, 1024, "D3DFMT_DXT5", 1, false, false, "1", false)]
    [InlineData(1024, 1024, "D3DFMT_DXT1", 1, false, true, "1", true)]
    [InlineData(1024, 1024, "D3DFMT_DXT5", 2, false, true, "1", true)]
    [InlineData(2048, 2048, "D3DFMT_DXT5", 1, true, true, "1", true)]
    [InlineData(1024, 1024, "D3DFMT_DXT5", 1, true, true, "1", false)]
    public void TryCreateDetails_OnlyProcessesTexturesThatNeedChanges(
        int width, int height, string compression, int mipmaps,
        bool downscale, bool changeCompression, string mipmapChoice, bool expected)
    {
        var source = new GTextureDetails
        {
            Width = width,
            Height = height,
            Compression = compression,
            MipMapCount = mipmaps
        };

        var changed = BulkTextureOptimizationHelper.TryCreateDetails(
            source, downscale, changeCompression, mipmapChoice, out var output);

        Assert.Equal(expected, changed);
        if (!changed) Assert.Null(output);
    }

    [Fact]
    public void TryCreateDetails_Downscales2048SquareEvenWhenFormatAndMipmapsAlreadyMatch()
    {
        var source = new GTextureDetails
        {
            Width = 2048,
            Height = 2048,
            Compression = "D3DFMT_DXT5",
            MipMapCount = 1
        };

        Assert.True(BulkTextureOptimizationHelper.TryCreateDetails(
            source, true, true, "1", out var output));
        Assert.Equal(1024, output.Width);
        Assert.Equal(1024, output.Height);
        Assert.Equal(1, output.MipMapCount);
    }
}
