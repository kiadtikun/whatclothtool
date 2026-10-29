using System.Text;
using mjxClothTool.Worker;
using Xunit;

namespace mjxClothTool.Worker.Tests;

public sealed class ProjectSnapshotReaderTests
{
    [Fact]
    public void Read_keeps_duplicate_drawables_in_source_order_and_original_texture_name()
    {
        using var fixture = new SaveFixture("autosave.json", """
            {"ProjectName":"Tale","IsExternalProject":false,"Groups":["jbib"],"Tags":[],"Addons":[
              {"Name":"Addon 1","Drawables":[
                {"Id":"11111111-1111-1111-1111-111111111111","Name":"jbib_075_u","Number":75,"Order":102,"Sex":0,"TypeNumeric":11,"IsProp":false,"Group":"jbib","Textures":[{"Id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","FilePath":"assets/a.ytd","OriginalFileName":"shirt.ytd","DisplayName":"jbib_diff_075_a_uni","TxtNumber":0,"TxtDetails":{"Width":1024,"Height":1024,"MipMapCount":1,"Compression":"D3DFMT_DXT5","Name":"shirt"}}]},
                {"Id":"22222222-2222-2222-2222-222222222222","Name":"jbib_075_u","Number":75,"Order":75,"Sex":0,"TypeNumeric":11,"IsProp":false,"Group":"jbib","Textures":[]}
              ]}
            ]}
            """);

        var snapshot = ProjectSnapshotReader.Read(fixture.Path);

        Assert.Equal("Tale", snapshot.ProjectName);
        Assert.Equal(new[] { 102, 75 }, snapshot.Addons[0].Drawables.Select(x => x.Order));
        Assert.Equal("shirt.ytd", snapshot.Addons[0].Drawables[0].Textures[0].OriginalFileName);
        Assert.Equal("shirt", snapshot.Addons[0].Drawables[0].Textures[0].TxtDetails?.Name);
    }

    [Fact]
    public void Read_external_project_keeps_external_asset_path()
    {
        using var fixture = new SaveFixture("autosave.external.json", """
            {"ProjectName":"External","IsExternalProject":true,"Groups":[],"Tags":[],"Addons":[{"Name":"Addon 1","Drawables":[{"Name":"hair_000_u","FilePath":"D:\\clothes\\hair_000_u.ydd","Textures":[]}]}]}
            """);

        var snapshot = ProjectSnapshotReader.Read(fixture.Path);

        Assert.True(snapshot.IsExternalProject);
        Assert.Equal("D:\\clothes\\hair_000_u.ydd", snapshot.Addons[0].Drawables[0].FilePath);
    }

    [Fact]
    public void Read_never_rewrites_unknown_properties()
    {
        using var fixture = new SaveFixture("autosave.json", """
            { "ProjectName": "Tale", "Addons": [], "FutureFormat": { "keep": [1, 2, 3] } }
            """);
        var before = File.ReadAllBytes(fixture.Path);

        _ = ProjectSnapshotReader.Read(fixture.Path);

        Assert.Equal(before, File.ReadAllBytes(fixture.Path));
    }

    private sealed class SaveFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mjx-worker-tests-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }

        public SaveFixture(string filename, string json)
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, filename);
            File.WriteAllText(Path, json, new UTF8Encoding(false));
        }

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
