using System.Text.Json;
using mjxClothTool.Helpers;
using mjxClothTool.Models.Texture;

namespace mjxClothTool.UnitTests.Views;

public class TextureTagsTests
{
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
