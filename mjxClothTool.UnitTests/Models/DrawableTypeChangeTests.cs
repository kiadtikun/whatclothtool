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
    public void ChangingGenderAndTypeTogetherReservesOnlyTheOriginalSlot()
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
            var drawable = new GDrawable(Guid.NewGuid(), temp.FilePath("teef_000_u.ydd"),
                SexType.male, false, (int)ComponentNumbers.teef, 0, false, []) { Group = "Accessories" };
            addon.Drawables.Add(drawable);

            var edited = manager.ChangeDrawableProperties(addon, [(drawable, SexType.female, false, (int)ComponentNumbers.accs)]);

            Assert.Same(drawable, Assert.Single(edited));
            Assert.Equal(2, addon.Drawables.Count);
            var reserved = Assert.Single(addon.Drawables.Where(item => item.IsReserved));
            Assert.Equal(SexType.male, reserved.Sex);
            Assert.Equal("teef", reserved.TypeName);
            Assert.Equal("Accessories", reserved.Group);
            Assert.Equal(0, reserved.Number);
            Assert.Equal(SexType.female, drawable.Sex);
            Assert.Equal("female", drawable.SexName);
            Assert.Equal("accs", drawable.TypeName);
            Assert.Equal("accs", drawable.Group);
        }
        finally
        {
            reservedPath.Invoke(null, [previousPath]);
        }
    }

    [Fact]
    public void ChangingGenderKeepsTheDrawableAndReservedSlotInTheOriginalGroup()
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
            var drawable = new GDrawable(Guid.NewGuid(), temp.FilePath("teef_001_u.ydd"),
                SexType.male, false, (int)ComponentNumbers.teef, 1, false, [])
            {
                Group = "Custom accessories",
                Order = 4
            };
            addon.Drawables.Add(drawable);
            Assert.Equal("male", drawable.SexName);

            Assert.True(manager.ChangeDrawableSex(addon, drawable, SexType.female));

            var reserved = Assert.Single(addon.Drawables.Where(item => item.IsReserved));
            Assert.Equal(SexType.male, reserved.Sex);
            Assert.Equal(1, reserved.Number);
            Assert.Equal(4, reserved.Order);
            Assert.Equal("Custom accessories", reserved.Group);
            Assert.Contains(drawable, addon.Drawables);
            Assert.Equal(SexType.female, drawable.Sex);
            Assert.Equal("female", drawable.SexName);
            Assert.Equal("Custom accessories", drawable.Group);
            Assert.False(manager.ChangeDrawableSex(addon, drawable, SexType.female));
            Assert.Equal(2, addon.Drawables.Count);
        }
        finally
        {
            reservedPath.Invoke(null, [previousPath]);
        }
    }

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
