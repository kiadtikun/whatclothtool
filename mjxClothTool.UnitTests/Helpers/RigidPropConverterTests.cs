using CodeWalker.GameFiles;
using mjxClothTool.Helpers;
using SharpDX;

namespace mjxClothTool.UnitTests.Helpers;

public class RigidPropConverterTests
{
    // Triangle in ped space, one metre above the head's rest origin.
    private const string Model = """
        <Item><HasSkin value="1"/><Geometries><Item><BoneIDs>0</BoneIDs><VertexBuffer>
        <Layout type="GTAV1"><Position/><BlendWeights/><BlendIndices/><Normal/><TexCoord0/></Layout>
        <Data>
        0 0 3  255 0 0 0  0 0 0 0  0 0 1  0 1
        1 0 3  255 0 0 0  0 0 0 0  0 0 1  1 0
        0 1 3  255 0 0 0  0 0 0 0  0 0 1  1 1
        </Data></VertexBuffer><IndexBuffer><Data>0 1 2</Data></IndexBuffer></Item></Geometries></Item>
        """;

    private static string WriteSource(TestTempDirectory dir, string model)
    {
        string xml = $"""
            <DrawableDictionary><Item><Name>teef_012_u</Name><Skeleton><Bones><Item>
            <Name>SKEL_Head</Name><Tag value="31086"/><Index value="0"/><ParentIndex value="-1"/><SiblingIndex value="-1"/>
            <Translation x="0" y="0" z="2"/><Rotation x="0" y="0" z="0" w="1"/><Scale x="1" y="1" z="1"/>
            </Item></Bones></Skeleton><DrawableModelsHigh>{model}</DrawableModelsHigh>
            <DrawableModelsMedium>{model}</DrawableModelsMedium></Item></DrawableDictionary>
            """;
        var path = dir.FilePath("teef_012_u.ydd");
        File.WriteAllBytes(path, XmlYdd.GetYdd(xml).Save());
        return path;
    }

    [Fact]
    public void ConvertedLodsRoundTripInAnchorSpaceAndExportWithoutOverwriting()
    {
        using var dir = new TestTempDirectory();
        var path = WriteSource(dir, Model);
        var source = File.ReadAllBytes(path);
        var result = RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 7, Vector3.UnitX, Vector3.Zero, 2);
        var ydd = new YddFile();
        ydd.Load(result.Data);
        var drawable = Assert.Single(ydd.Drawables);
        Assert.Null(drawable.Skeleton);
        Assert.Equal("p_eyes_007", drawable.Name);
        Assert.Equal(2, drawable.AllModels.Length);
        foreach (var model in drawable.AllModels)
        {
            Assert.Equal(0, model.HasSkin);
            var geo = Assert.Single(model.Geometries);
            Assert.Equal(0u, geo.VertexData.Info.Flags & 6u);
            Assert.Equal(new Vector3(1, 0, 2), geo.VertexData.GetVector3(0, 0));
            Assert.Equal(new Vector2(1, 0), geo.VertexData.GetVector2(1, (int)VertexSemantics.TexCoord0));
        }
        var texture = Path.Combine(Path.GetDirectoryName(typeof(RigidPropConverterTests).Assembly.Location)!, "TestData", "reservedTexture.ytd");
        var folder = RigidPropConverter.Export(result, [texture], dir.Path);
        Assert.Equal(result.Data, File.ReadAllBytes(Path.Combine(folder, "p_eyes_007.ydd")));
        Assert.Equal(File.ReadAllBytes(texture), File.ReadAllBytes(Path.Combine(folder, "p_eyes_diff_007_a.ytd")));
        Assert.Throws<IOException>(() => RigidPropConverter.Export(result, [], dir.Path));
        Assert.Equal(source, File.ReadAllBytes(path));
    }

    [Fact]
    public void ConvertedPropIsAlwaysDrawableAndNotAProxy()
    {
        using var dir = new TestTempDirectory();
        var path = WriteSource(dir, Model);
        var result = RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 3, Vector3.Zero, Vector3.Zero, 1);
        var ydd = new YddFile();
        ydd.Load(result.Data);
        var drawable = Assert.Single(ydd.Drawables);
        // The game culls a prop whose draw distance is zero; accessories must draw at any range.
        Assert.Equal(9998f, drawable.LodDistHigh);
        Assert.Equal(9998f, drawable.LodDistMed);
        Assert.Equal(9998f, drawable.LodDistLow);
        Assert.Equal(9998f, drawable.LodDistVlow);
        // Without this bit the game treats each model as a proxy and never draws it on its own.
        Assert.All(drawable.AllModels, model => Assert.Equal(1, model.RenderMask & 1));
        Assert.Equal(1, drawable.RenderMaskHigh & 1);
        Assert.Equal(1, drawable.RenderMaskMed & 1);
    }

    [Theory]
    [InlineData(3176173383u)] // ped_default_cutout.sps
    [InlineData(999874878u)] // ped_default.sps
    public void ClothingGeometryBecomesACompleteRigidPedPropGeometry(uint sourceShaderFile)
    {
        using var dir = new TestTempDirectory();
        var modelWithColors = Model.Replace("<Normal/><TexCoord0/>", "<Normal/><Colour0/><Colour1/><TexCoord0/>")
            .Replace("0 0 1  ", "0 0 1  1 2 3 255  4 5 6 255  ");
        var path = WriteSource(dir, modelWithColors);
        var source = new YddFile();
        source.Load(File.ReadAllBytes(path));
        var geometry = source.Drawables[0].DrawableModels.High[0].Geometries[0];
        geometry.Shader = new ShaderFX
        {
            Name = new MetaHash(695944937),
            FileName = new MetaHash(sourceShaderFile)
        };
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        RigidPropConverter.RewriteVertices(geometry, Matrix.Identity, ref min, ref max);
        var convertedShader = Assert.IsType<ShaderFX>(geometry.Shader);

        Assert.Equal(3, geometry.VertexData.VertexCount);
        Assert.Equal(0x40F9u, geometry.VertexData.Info.Flags);
        Assert.Equal(0u, geometry.VertexData.Info.Flags & 6u);
        Assert.NotEqual(0u, geometry.VertexData.Info.Flags & (1u << (int)VertexSemantics.TexCoord1));
        Assert.NotEqual(0u, geometry.VertexData.Info.Flags & (1u << (int)VertexSemantics.Tangent));
        Assert.Equal(540746503u, (uint)convertedShader.FileName);
        Assert.Equal(886638433u, (uint)convertedShader.Name);
        for (var vertex = 0; vertex < geometry.VertexData.VertexCount; vertex++)
        {
            Assert.Equal(new Color(255, 128, 0, 255),
                geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour0));
            Assert.Equal(new Color(0, 0, 0, 255),
                geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour1));
        }
    }

    [Fact]
    public void PropCompatibleGeometryAlsoGetsTheFixedColors()
    {
        using var dir = new TestTempDirectory();
        var modelWithColors = Model.Replace("<Normal/><TexCoord0/>", "<Normal/><Colour0/><Colour1/><TexCoord0/>")
            .Replace("0 0 1  ", "0 0 1  1 2 3 255  4 5 6 255  ");
        var source = new YddFile();
        source.Load(File.ReadAllBytes(WriteSource(dir, modelWithColors)));
        var geometry = source.Drawables[0].DrawableModels.High[0].Geometries[0];
        geometry.Shader = new ShaderFX { FileName = new MetaHash(659901003u) };
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        RigidPropConverter.RewriteVertices(geometry, Matrix.Identity, ref min, ref max);

        for (var vertex = 0; vertex < geometry.VertexData.VertexCount; vertex++)
        {
            Assert.Equal(new Color(255, 128, 0, 255),
                geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour0));
            Assert.Equal(new Color(0, 0, 0, 255),
                geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour1));
        }
    }

    [Fact]
    public void EveryGeometryOfEveryLodGetsTheFixedColors()
    {
        using var dir = new TestTempDirectory();
        const string colours = "1 2 3 255  4 5 6 255";
        string Geometry(string x) => $@"<Item><BoneIDs>0</BoneIDs><VertexBuffer>
            <Layout type=""GTAV1""><Position/><BlendWeights/><BlendIndices/><Normal/><Colour0/><Colour1/><TexCoord0/></Layout>
            <Data>
            {x} 0 3  255 0 0 0  0 0 0 0  0 0 1  {colours}  0 1
            {x} 0 3  255 0 0 0  0 0 0 0  0 0 1  {colours}  1 0
            {x} 0 3  255 0 0 0  0 0 0 0  0 0 1  {colours}  1 1
            </Data></VertexBuffer><IndexBuffer><Data>0 1 2</Data></IndexBuffer></Item>";
        string model = $"<Item><HasSkin value=\"1\"/><Geometries>{Geometry("0")}{Geometry("1")}</Geometries></Item>";
        var path = WriteSource(dir, model);
        var result = RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 5,
            Vector3.Zero, Vector3.Zero, 1);
        var ydd = new YddFile();
        ydd.Load(result.Data);
        var drawable = Assert.Single(ydd.Drawables);
        Assert.Equal(2, drawable.AllModels.Length);
        foreach (var geometry in drawable.AllModels.SelectMany(m => m.Geometries))
        {
            Assert.Equal(3, geometry.VertexData.VertexCount);
            for (var vertex = 0; vertex < geometry.VertexData.VertexCount; vertex++)
            {
                Assert.Equal(new Color(255, 128, 0, 255),
                    geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour0));
                Assert.Equal(new Color(0, 0, 0, 255),
                    geometry.VertexData.GetColour(vertex, (int)VertexSemantics.Colour1));
            }
        }
    }

    [Fact]
    public void MixedWeightsRequireSeparatingTheAccessory()
    {
        using var dir = new TestTempDirectory();
        string model = Model.Replace("<BoneIDs>0</BoneIDs>", "<BoneIDs>0,1</BoneIDs>")
            .Replace("255 0 0 0  0 0 0 0", "200 55 0 0  0 1 0 0");
        var path = WriteSource(dir, model);
        var error = Assert.Throws<InvalidDataException>(() => RigidPropConverter.Convert(path, null,
            Enums.PropNumbers.p_eyes, 0, Vector3.Zero, Vector3.Zero, 1));
        Assert.Contains("several bones", error.Message);
    }

    [Fact]
    public void ExtractsOnlySelectedIslandAndRigidlyAttachesMixedWeights()
    {
        using var dir = new TestTempDirectory();
        var model = Model.Replace("<BoneIDs>0</BoneIDs>", "<BoneIDs>0,1</BoneIDs>")
            .Replace("255 0 0 0  0 0 0 0", "200 55 0 0  0 1 0 0")
            .Replace("</Data></VertexBuffer>", "10 0 3  255 0 0 0  0 0 0 0  0 0 1  0 1\n11 0 3  255 0 0 0  0 0 0 0  0 0 1  1 0\n10 1 3  255 0 0 0  0 0 0 0  0 0 1  1 1\n</Data></VertexBuffer>")
            .Replace(">0 1 2<", ">0 1 2 3 4 5<");
        var path = WriteSource(dir, model);
        var parts = PropMeshParts.List(path);
        Assert.Equal(2, parts.Length);
        var result = RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 7,
            Vector3.Zero, Vector3.Zero, 1, pieces: [parts[0]]);
        var ydd = new YddFile();
        ydd.Load(result.Data);
        var drawable = Assert.Single(ydd.Drawables);
        var output = Assert.Single(drawable.AllModels);
        var geometry = Assert.Single(output.Geometries);
        Assert.Equal(3, geometry.VertexData.VertexCount);
        Assert.Equal(new ushort[] { 0, 1, 2 }, geometry.IndexBuffer.Indices);
        Assert.Equal(new Vector3(0, 0, 1), geometry.VertexData.GetVector3(0, 0));
        Assert.Null(drawable.Skeleton);
        Assert.Equal(0u, geometry.VertexData.Info.Flags & 6u);
    }

    [Fact]
    public void AutomaticSelectionKeepsHeadPiecesAndRejectsDistantOutfit()
    {
        using var dir = new TestTempDirectory();
        string mixed = Model.Replace("255 0 0 0  0 0 0 0", "200 55 0 0  0 1 0 0")
            .Replace("<BoneIDs>0</BoneIDs>", "<BoneIDs>0,1</BoneIDs>");
        string face = mixed.Replace("0 0 3  ", "0 0 2  ").Replace("1 0 3  ", "0.05 0 2  ").Replace("0 1 3  ", "0 0.05 2  ");
        string path = WriteSource(dir, face + mixed);
        RigidPropConverter.Result Convert(PropMeshParts.Part[] manual = null) =>
            RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 19,
                Vector3.Zero, Vector3.Zero, 1, pieces: manual, autoSelect: true);
        var whole = RigidPropConverter.Convert(path, null, Enums.PropNumbers.p_eyes, 19,
            Vector3.Zero, Vector3.Zero, 1, pieces: [PropMeshParts.List(path)[0]], autoSelect: true, allGlassesPieces: true);
        var saved = new YddFile();
        saved.Load(whole.Data);
        var full = Assert.Single(saved.Drawables);
        Assert.Null(full.Skeleton);
        foreach (var lod in new[] { full.DrawableModels.High, full.DrawableModels.Med })
        {
            Assert.Equal(2, lod.Length);
            Assert.Equal(new Vector3(0, 0, 1), Assert.Single(lod[1].Geometries).VertexData.GetVector3(0, 0));
            Assert.All(lod, m => Assert.Equal(0, m.HasSkin));
        }
        var result = Convert();
        var ydd = new YddFile();
        ydd.Load(result.Data);
        var model = Assert.Single(Assert.Single(ydd.Drawables).AllModels);
        var geometry = Assert.Single(model.Geometries);
        Assert.Equal(Vector3.Zero, geometry.VertexData.GetVector3(0, 0));
        Assert.Equal(3, geometry.VertexData.VertexCount);
        Assert.Contains("Auto-selected 1", result.SourceBinding);
        var manual = Convert([PropMeshParts.List(path)[1]]);
        Assert.Contains("Selected pieces", manual.SourceBinding);
        path = WriteSource(dir, mixed);
        Assert.Equal(RigidPropConverter.NeedsPieces, Assert.Throws<InvalidDataException>(() => Convert()).Message);
    }

    [Theory]
    [InlineData(Enums.PropNumbers.p_lwrist)]
    [InlineData(Enums.PropNumbers.p_rwrist)]
    public void ObjectOriginUsesEnteredRotationAndOffsetForWrist(Enums.PropNumbers slot)
    {
        using var dir = new TestTempDirectory();
        var ydd = new YddFile();
        ydd.Load(File.ReadAllBytes(WriteSource(dir, Model)));
        var drawable = ydd.Drawables[0];
        drawable.Skeleton = null;
        foreach (var model in drawable.AllModels) model.SkeletonBinding = 0;
        var ydr = new YdrFile { Drawable = drawable };
        var path = dir.FilePath("accessory.ydr");
        File.WriteAllBytes(path, ydr.Save());
        var prop = RigidPropConverter.Convert(path, null, slot, 0, Vector3.UnitX, new Vector3(0, 0, 90), 1);
        ydd.Load(prop.Data);
        var point = ydd.Drawables[0].AllModels[0].Geometries[0].VertexData.GetVector3(1, 0);
        Assert.True(Vector3.Distance(point, new Vector3(1, 1, 3)) < 0.0001f);
        Assert.Equal($"{slot}_000", ydd.Drawables[0].Name);
        Assert.Throws<FileNotFoundException>(() => RigidPropConverter.Export(prop, [dir.FilePath("missing.ytd")], dir.Path));
        Assert.Empty(Directory.GetDirectories(dir.Path));
    }
}
