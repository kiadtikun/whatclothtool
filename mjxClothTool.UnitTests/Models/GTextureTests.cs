using mjxClothTool.Models.Texture;
using mjxClothTool.Helpers;

namespace mjxClothTool.UnitTests.Models;

public class GTextureTests
{
    [Fact]
    public void SourceFileName_PreservesOriginalNameAcrossRenumberingAndSaveLoad()
    {
        var texture = CreateTexture(Guid.NewGuid(), 11, 0, 0, false, false);
        texture.OriginalFileName = "mp_f_freemode_01^jbib_diff_042_b_uni.ytd";
        texture.Number = 12;
        texture.TxtNumber = 2;

        var json = System.Text.Json.JsonSerializer.Serialize(texture);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<GTexture>(json)!;

        Assert.Equal(texture.OriginalFileName, loaded.SourceFileName);
        Assert.Equal("jbib_diff_012_c_uni", loaded.GetBuildName());
    }

    [Fact]
    public void SourceFileName_OldGuidAssetFallsBackToDisplayName()
    {
        var texture = CreateTexture(Guid.NewGuid(), 11, 0, 0, false, false);
        Assert.Equal(texture.DisplayName, texture.SourceFileName);
    }

    [Fact]
    public void SourceFileName_OldExternalAssetUsesFileName()
    {
        var texture = CreateTexture(Guid.NewGuid(), 11, 0, 0, false, false);
        texture.FilePath = @"C:\textures\original_texture.ytd";
        Assert.Equal("original_texture.ytd", texture.SourceFileName);
    }

    [Fact]
    public void ReplacementHistory_SurvivesSaveAndReload()
    {
        var original = CreateTexture(Guid.NewGuid(), 11, 0, 0, false, false);
        original.OriginalFileName = "original.ytd";
        var firstReplacement = CreateTexture(original.Id, 11, 0, 0, false, false);
        firstReplacement.PreviousReplacement = original;
        var latestReplacement = CreateTexture(original.Id, 11, 0, 0, false, false);
        latestReplacement.PreviousReplacement = firstReplacement;

        Assert.True(latestReplacement.CanUndoReplacement);

        var json = System.Text.Json.JsonSerializer.Serialize(latestReplacement);
        Assert.Contains("PreviousReplacement", json);
        Assert.DoesNotContain("CanUndoReplacement", json);

        var loaded = System.Text.Json.JsonSerializer.Deserialize<GTexture>(json)!;
        Assert.True(loaded.CanUndoReplacement);
        Assert.Equal(firstReplacement.FilePath, loaded.PreviousReplacement!.FilePath);
        Assert.True(loaded.PreviousReplacement.CanUndoReplacement);
        Assert.Equal(original.FilePath, loaded.PreviousReplacement.PreviousReplacement!.FilePath);
        Assert.Equal(original.OriginalFileName, loaded.PreviousReplacement.PreviousReplacement.OriginalFileName);
        Assert.Null(loaded.PreviousReplacement.PreviousReplacement.PreviousReplacement);
    }

    [Fact]
    public void Constructor_AssignsIdWhenEmptyGuidProvided()
    {
        var texture = CreateTexture(Guid.Empty, typeNumeric: 11, number: 0, txtNumber: 0, hasSkin: false, isProp: false);

        Assert.NotEqual(Guid.Empty, texture.Id);
    }

    [Theory]
    [InlineData(11, 5, 0, false, false, "jbib_diff_005_a_uni")]
    [InlineData(11, 5, 1, true, false, "jbib_diff_005_b_whi")]
    [InlineData(0, 3, 2, false, true, "p_head_diff_003_c")]
    public void GetBuildName_UsesTypeNumberTextureLetterSkinAndPropState(
        int typeNumeric,
        int number,
        int txtNumber,
        bool hasSkin,
        bool isProp,
        string expected)
    {
        var texture = CreateTexture(Guid.NewGuid(), typeNumeric, number, txtNumber, hasSkin, isProp);

        Assert.Equal(expected, texture.GetBuildName());
        Assert.Equal(expected, texture.DisplayName);
    }

    [Fact]
    public void NumberAndTextureNumber_UpdateDisplayName()
    {
        var texture = CreateTexture(Guid.NewGuid(), typeNumeric: 11, number: 0, txtNumber: 0, hasSkin: false, isProp: false);

        texture.Number = 12;
        texture.TxtNumber = 2;

        Assert.Equal('c', texture.TxtLetter);
        Assert.Equal("jbib_diff_012_c_uni", texture.DisplayName);
    }

    [Fact]
    public async Task DetailsLoaded_UsesActualMetadataForUppercaseYtd()
    {
        using var temp = new TestTempDirectory();
        var path = temp.FilePath("texture.YTD");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "reservedTexture.ytd"), path);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            FilePath = path,
            TypeNumeric = 11,
            IsLoading = false,
            TxtDetails = new GTextureDetails { Width = -1 }
        });

        var texture = System.Text.Json.JsonSerializer.Deserialize<GTexture>(json)!;
        var metadata = await texture.DetailsLoaded;

        Assert.NotNull(metadata);
        Assert.True(metadata.Width > 0);
        Assert.False(texture.IsLoading);

        using var image = ImgHelper.GetImage(path);
        Assert.Equal((uint)metadata.Width, image.Width);
        Assert.Equal((uint)metadata.Height, image.Height);
    }

    [Fact]
    public async Task DetailsLoaded_UsesOptimizedMetadataOnTheNextPass()
    {
        var texture = CreateTexture(Guid.NewGuid(), 11, 0, 0, false, false);
        await texture.DetailsLoaded;
        var replacement = new GTextureDetails { Width = 1024, Height = 1024 };

        texture.UseOptimizedFile("optimized.ytd", replacement);

        Assert.Same(replacement, await texture.DetailsLoaded);
    }

    private static GTexture CreateTexture(Guid id, int typeNumeric, int number, int txtNumber, bool hasSkin, bool isProp)
    {
        return new GTexture(
            id,
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ytd"),
            typeNumeric,
            number,
            txtNumber,
            hasSkin,
            isProp);
    }
}
