using System.Reflection;
using mjxClothTool.Helpers;
using mjxClothTool.Models;
using mjxClothTool.Models.Drawable;
using mjxClothTool.Models.Texture;
using static mjxClothTool.Enums;

namespace mjxClothTool.UnitTests.Models;

public class DrawableTypeChangeTests
{
    [Fact]
    public void BatchReassignsSharedFileDrawablesAndPreservesTheirOriginalSlots()
    {
        using var temp = new TestTempDirectory();
        var reservedPath = typeof(FileHelper).GetProperty(nameof(FileHelper.ReservedAssetsPath))!.GetSetMethod(true)!;
        var previousPath = FileHelper.ReservedAssetsPath;
        reservedPath.Invoke(null, [temp.Path]);
        try
        {
            var manager = new AddonManager();
            var addon = new Addon("Addon 1");
            manager.Addons.Add(addon);
            var path = temp.FilePath("shared.ydd");
            var texture = new GTexture(Guid.NewGuid(), temp.FilePath("texture.ytd"), (int)ComponentNumbers.teef, 0, 0, false, false);
            var female = new GDrawable(Guid.NewGuid(), path, SexType.female, false, (int)ComponentNumbers.teef, 0, false, [texture]);
            var male = new GDrawable(Guid.NewGuid(), path, SexType.male, false, (int)ComponentNumbers.teef, 0, false, []);
            addon.Drawables.Add(female);
            addon.Drawables.Add(male);

            var changed = manager.ChangeDrawableTypes(addon, [(female, true, (int)PropNumbers.p_head), (male, false, (int)ComponentNumbers.accs)]);

            Assert.Equal(2, changed.Count);
            Assert.Equal(2, addon.Drawables.Count(item => item.IsReserved && item.TypeNumeric == (int)ComponentNumbers.teef && item.Number == 0));
            Assert.Contains(female, addon.Drawables);
            Assert.Contains(male, addon.Drawables);
            Assert.Equal("p_head", female.TypeName);
            Assert.Equal("accs", male.TypeName);
            Assert.True(texture.IsProp);
            Assert.Equal("p_head", texture.TypeName);
            Assert.Equal(path, female.FilePath);
            Assert.Equal(path, male.FilePath);
        }
        finally
        {
            reservedPath.Invoke(null, [previousPath]);
        }
    }
}
