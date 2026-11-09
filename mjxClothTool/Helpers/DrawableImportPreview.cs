using CodeWalker.GameFiles;
using CodeWalker.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace mjxClothTool.Helpers;

internal static class DrawableImportPreview
{
    public static Model3DGroup Load(string filePath, bool isProp, CancellationToken cancellationToken)
    {
        var ydd = new YddFile();
        ydd.Load(File.ReadAllBytes(filePath));
        cancellationToken.ThrowIfCancellationRequested();

        var drawable = ydd.Drawables?.FirstOrDefault();
        var lod = drawable?.DrawableModels;
        var models = new[] { lod?.High, lod?.Med, lod?.Low, lod?.VLow }
            .FirstOrDefault(level => level?.Length > 0);
        if (models == null)
        {
            throw new InvalidDataException("The drawable contains no models.");
        }

        var externalTextures = LoadMatchingTextures(filePath, isProp);
        var embeddedTextures = drawable.ShaderGroup?.TextureDictionary;
        var materials = new Dictionary<Texture, DiffuseMaterial>();
        var fallbackMaterial = new DiffuseMaterial(Brushes.LightSlateGray);
        var preview = new Model3DGroup();

        foreach (var model in models)
        {
            foreach (var geometry in model.Geometries ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mesh = CreateMesh(geometry);
                if (mesh == null)
                {
                    continue;
                }

                var material = fallbackMaterial;
                if (mesh.TextureCoordinates.Count > 0)
                {
                    var texture = FindDiffuseTexture(geometry, embeddedTextures, externalTextures);
                    if (texture != null && !materials.TryGetValue(texture, out material))
                    {
                        material = CreateMaterial(texture) ?? fallbackMaterial;
                        materials.Add(texture, material);
                    }
                }

                preview.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
            }
        }

        var bounds = preview.Bounds;
        var diagonal = new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length;
        if (bounds.IsEmpty || !double.IsFinite(diagonal) || diagonal <= 0)
        {
            throw new InvalidDataException("The drawable contains no previewable geometry.");
        }

        // Fit the mesh into a unit sphere so it stays in view at every rotation.
        var transform = Matrix3D.Identity;
        transform.Translate(new Vector3D(
            -bounds.X - bounds.SizeX / 2,
            -bounds.Y - bounds.SizeY / 2,
            -bounds.Z - bounds.SizeZ / 2));
        var scale = 2 / diagonal;
        transform.Scale(new Vector3D(scale, scale, scale));
        preview.Transform = new MatrixTransform3D(transform);
        preview.Freeze();
        return preview;
    }

    private static MeshGeometry3D CreateMesh(DrawableGeometry geometry)
    {
        const int position = (int)VertexSemantics.Position;
        const int texCoord = (int)VertexSemantics.TexCoord0;

        var vertices = geometry.VertexData;
        var indices = geometry.IndexBuffer?.Indices;
        if (vertices?.Info == null || vertices.VertexCount == 0 ||
            (vertices.Info.Flags & (1 << position)) == 0 ||
            indices == null || indices.Length < 3 || indices.Length % 3 != 0 ||
            indices.Any(index => index >= vertices.VertexCount))
        {
            return null;
        }

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(vertices.VertexCount),
            TriangleIndices = new Int32Collection(indices.Select(index => (int)index))
        };
        var uvType = (vertices.Info.Flags & (1 << texCoord)) != 0
            ? vertices.Info.GetComponentType(texCoord)
            : VertexComponentType.Nothing;

        for (var i = 0; i < vertices.VertexCount; i++)
        {
            var point = vertices.GetVector3(i, position);
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            {
                return null;
            }

            mesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
            switch (uvType)
            {
                case VertexComponentType.Float2:
                    var uv = vertices.GetVector2(i, texCoord);
                    mesh.TextureCoordinates.Add(new Point(uv.X, uv.Y));
                    break;
                case VertexComponentType.Half2:
                    var halfUv = vertices.GetHalf2(i, texCoord);
                    mesh.TextureCoordinates.Add(new Point((float)halfUv.X, (float)halfUv.Y));
                    break;
            }
        }

        return mesh;
    }

    private static TextureDictionary LoadMatchingTextures(string filePath, bool isProp)
    {
        try
        {
            var texturePath = FileHelper.FindMatchingTextures(filePath, null, isProp).FirstOrDefault();
            return texturePath == null ? null : CWHelper.GetYtdFile(texturePath).TextureDict;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to read textures for '{filePath}': {ex.Message}");
            return null;
        }
    }

    private static Texture FindDiffuseTexture(
        DrawableGeometry geometry, TextureDictionary embeddedTextures, TextureDictionary externalTextures)
    {
        var parameters = geometry.Shader?.ParametersList;
        if (parameters?.Hashes == null || parameters.Parameters == null)
        {
            return null;
        }

        var index = Array.IndexOf(parameters.Hashes, (MetaName)ShaderParamNames.DiffuseSampler);
        if (index < 0 || index >= parameters.Parameters.Length || parameters.Parameters[index].Data is not TextureBase diffuse)
        {
            return null;
        }

        return externalTextures?.Lookup(diffuse.NameHash)
            ?? diffuse as Texture
            ?? embeddedTextures?.Lookup(diffuse.NameHash)
            ?? externalTextures?.Textures?.data_items?.FirstOrDefault();
    }

    private static DiffuseMaterial CreateMaterial(Texture texture)
    {
        try
        {
            var pixels = DDSIO.GetPixels(texture, 0);
            if (pixels == null)
            {
                return null;
            }

            var image = BitmapSource.Create(texture.Width, texture.Height, 96, 96,
                PixelFormats.Bgra32, null, pixels, texture.Width * 4);
            return new DiffuseMaterial(new ImageBrush(image) { TileMode = TileMode.Tile });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to decode texture '{texture.Name}': {ex.Message}");
            return null;
        }
    }
}
