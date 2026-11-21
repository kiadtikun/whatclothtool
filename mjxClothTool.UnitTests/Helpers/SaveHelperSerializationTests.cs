using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using mjxClothTool.Helpers;
using mjxClothTool.Models;
using mjxClothTool.Models.Drawable;
using mjxClothTool.Models.Texture;
using static mjxClothTool.Enums;

namespace mjxClothTool.UnitTests.Helpers;

[Collection(AddonManagerStaticStateCollection.Name)]
public class SaveHelperSerializationTests
{
    [Fact]
    public void SerializerOptions_RoundTripsAddonManagerGraph()
    {
        var manager = new AddonManager { ProjectName = "test-project" };
        var addon = new Addon("Addon 1");

        for (int i = 0; i < 3; i++)
        {
            var texture = new GTexture(
                Guid.NewGuid(),
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ytd"),
                typeNumeric: 11,
                number: i,
                txtNumber: 0,
                hasSkin: false,
                isProp: false);

            addon.Drawables.Add(new GDrawable(
                Guid.NewGuid(),
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ydd"),
                SexType.male,
                isProp: false,
                typeNumeric: 11,
                number: i,
                hasSkin: false,
                new ObservableCollection<GTexture> { texture }));
        }

        manager.Addons.Add(addon);

        // Serialize with the cached options used by autosave
        var json = JsonSerializer.Serialize(manager, SaveHelper.SerializerOptions);
        Assert.False(string.IsNullOrWhiteSpace(json));

        // The autosave validation step must accept the output
        using (var doc = JsonDocument.Parse(json))
        {
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        // And the load path must be able to restore the full graph
        var restored = JsonSerializer.Deserialize<AddonManager>(json, SaveHelper.SerializerOptions);
        Assert.NotNull(restored);
        Assert.Equal("test-project", restored.ProjectName);
        Assert.Single(restored.Addons);
        Assert.Equal(3, restored.Addons[0].Drawables.Count);
        Assert.Equal("jbib_000_u", restored.Addons[0].Drawables[0].Name);
        Assert.Single(restored.Addons[0].Drawables[0].Textures);
    }

    [Fact]
    public async Task DeserializingDetachedManagerDoesNotMutateActiveMoveMenuItems()
    {
        var field = typeof(MainWindow).GetField("_addonManager", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = field.GetValue(null);
        var active = new AddonManager();
        field.SetValue(null, active);

        try
        {
            active.Addons.Add(new Addon("Current Addon"));
            const string json = """
                {
                  "ProjectName": "loaded-project",
                  "Addons": [
                    { "Name": "Loaded Addon", "Drawables": [] }
                  ]
                }
                """;

            var detached = await Task.Run(() =>
                JsonSerializer.Deserialize<AddonManager>(json, SaveHelper.SerializerOptions));

            Assert.NotNull(detached);
            Assert.Equal(["Current Addon"], active.MoveMenuItems.Select(item => item.Header).ToArray());
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    [Fact]
    public void AddonManagerKeepsMoveMenuItemsSynchronized()
    {
        var manager = new AddonManager();
        var first = new Addon("Addon 1");
        var second = new Addon("Addon 2");

        manager.Addons.Add(first);
        manager.Addons.Add(second);
        Assert.Equal(["Addon 1", "Addon 2"], Headers(manager));

        second.Name = "Renamed Addon";
        Assert.Equal(["Addon 1", "Renamed Addon"], Headers(manager));

        manager.Addons.Remove(first);
        Assert.Equal(["Renamed Addon"], Headers(manager));

        manager.Addons.Clear();
        Assert.Equal([], Headers(manager));
    }

    [Fact]
    public void ReplacingAddonsDetachesTheOldCollection()
    {
        var manager = new AddonManager();
        var old = manager.Addons;
        old.Add(new Addon("Old Addon"));

        manager.Addons = new ObservableCollection<Addon> { new("Replacement Addon") };
        var menuChanges = 0;
        manager.MoveMenuItems.CollectionChanged += (_, _) => menuChanges++;
        old.Add(new Addon("Detached Addon"));

        Assert.Equal(["Replacement Addon"], Headers(manager));
        Assert.Equal(0, menuChanges);
    }

    [Fact]
    public void SelectedAddonRemainsDisabledWhenMoveMenuIsRebuilt()
    {
        var manager = new AddonManager();
        var first = new Addon("Addon 1");
        var second = new Addon("Addon 2");
        manager.Addons.Add(first);
        manager.Addons.Add(second);

        manager.SelectedAddon = second;
        manager.Addons.Add(new Addon("Addon 3"));

        Assert.True(manager.MoveMenuItems[0].IsEnabled);
        Assert.False(manager.MoveMenuItems[1].IsEnabled);
        Assert.True(manager.MoveMenuItems[2].IsEnabled);
    }

    private static string[] Headers(AddonManager manager) =>
        manager.MoveMenuItems.Select(item => item.Header).ToArray();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AddonManagerStaticStateCollection
{
    public const string Name = "AddonManager static state";
}
