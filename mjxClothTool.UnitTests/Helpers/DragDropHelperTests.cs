using mjxClothTool.Helpers;
using System.Text;
using System.Windows;

namespace mjxClothTool.UnitTests.Helpers;

public class DragDropHelperTests
{
    [Fact]
    public void CreateExtensionFilter_MatchesExtensionsCaseInsensitively()
    {
        var filter = DragDropHelper.CreateExtensionFilter(".ydd", ".ytd");

        Assert.True(filter("asset.YDD"));
        Assert.True(filter(@"C:\tmp\texture.ytd"));
        Assert.False(filter("image.png"));
    }

    [Fact]
    public void CreateExtensionFilter_ReturnsFalseWhenFileHasNoExtension()
    {
        var filter = DragDropHelper.CreateExtensionFilter(".ydd");

        Assert.False(filter("drawable"));
    }

    [Fact]
    public void ValidateFileAccess_SplitsExistingAndMissingFiles()
    {
        using var temp = new TestTempDirectory();
        var existing = temp.FilePath("existing.ydd");
        var missing = temp.FilePath("missing.ydd");
        File.WriteAllBytes(existing, []);

        var (accessible, inaccessible) = DragDropHelper.ValidateFileAccess([existing, missing]);

        Assert.Equal([existing], accessible);
        Assert.Equal([missing], inaccessible);
    }

    [Fact]
    public void ValidateFileAccess_ReturnsEmptyListsForEmptyInput()
    {
        var (accessible, inaccessible) = DragDropHelper.ValidateFileAccess([]);

        Assert.Empty(accessible);
        Assert.Empty(inaccessible);
    }

    [Theory]
    [InlineData(new[] { "texture.ytd" }, true)]
    [InlineData(new[] { "first.ytd", "second.ytd" }, false)]
    [InlineData(new[] { "readme.txt" }, false)]
    public void HasSingleFileInDescriptor_OnlyAcceptsOneMatchingFile(string[] names, bool expected)
    {
        var bytes = new byte[4 + names.Length * 592];
        BitConverter.GetBytes(names.Length).CopyTo(bytes, 0);
        for (var i = 0; i < names.Length; i++)
            Encoding.Unicode.GetBytes(names[i]).CopyTo(bytes, 4 + i * 592 + 72);

        using var stream = new MemoryStream(bytes);
        var data = new DataObject();
        data.SetData("FileGroupDescriptorW", stream);
        var filter = DragDropHelper.CreateExtensionFilter(".ytd", ".png", ".jpg", ".dds");

        Assert.Equal(expected, DragDropHelper.HasSingleFileInDescriptor(data, filter));
        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(DragDropEffects.Copy | DragDropEffects.Move, true, DragDropEffects.Copy)]
    [InlineData(DragDropEffects.Move, true, DragDropEffects.None)]
    [InlineData(DragDropEffects.Copy, false, DragDropEffects.None)]
    public void GetExternalFileEffect_NeverRequestsMove(
        DragDropEffects allowedEffects, bool hasSupportedFiles, DragDropEffects expected)
    {
        Assert.Equal(expected, DragDropHelper.GetExternalFileEffect(allowedEffects, hasSupportedFiles));
    }
}
