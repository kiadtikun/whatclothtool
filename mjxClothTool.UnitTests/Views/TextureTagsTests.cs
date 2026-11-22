using System.Collections.ObjectModel;
using System.Text.Json;
using mjxClothTool.Controls;
using mjxClothTool.Helpers;
using mjxClothTool.Models.Drawable;
using mjxClothTool.Models.Texture;

namespace mjxClothTool.UnitTests.Views;

public class TextureTagsTests
{
    [Fact]
    public void DrawableSearchMatchesTagOnTheCorrectTexture()
    {
        var other = new GTexture(Guid.NewGuid(), "other.ytd", 3, 13, 0, false, false);
        var matching = new GTexture(Guid.NewGuid(), "gang.ytd", 3, 13, 1, false, false);
        matching.Tags.Add("Gang 2");
        var drawable = new GDrawable(Guid.NewGuid(), "test.ydd", mjxClothTool.Enums.SexType.female,
            false, 3, 13, false, new ObservableCollection<GTexture>([other, matching]));

        Assert.Same(matching, DrawableList.GetMatchingTexture(drawable, "gang 2"));
        Assert.Null(DrawableList.GetMatchingTexture(drawable, "Gang 3"));
        Assert.Null(DrawableList.GetMatchingTexture(drawable, " "));
    }

    [Fact]
    public void TextureTagsSurviveProjectSerialization()
    {
        var texture = new GTexture(Guid.NewGuid(), "gang.ytd", 3, 13, 0, false, false);
        texture.Tags.Add("Gang 2");

        var json = JsonSerializer.Serialize(texture, SaveHelper.SerializerOptions);
        var restored = JsonSerializer.Deserialize<GTexture>(json, SaveHelper.SerializerOptions);

        Assert.NotNull(restored);
        Assert.Contains("Gang 2", restored.Tags);
    }
}
