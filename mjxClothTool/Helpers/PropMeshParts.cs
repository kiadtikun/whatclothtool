using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mjxClothTool.Helpers;

internal static class PropMeshParts
{
    internal sealed record Part(int Model, int Geometry, int Island, int Triangles)
    {
        public override string ToString() => $"Mesh {Model + 1}.{Geometry + 1} · piece {Island + 1} ({Triangles} triangles)";
    }

    internal static Part[] List(string path)
    {
        var source = new YddFile();
        source.Load(File.ReadAllBytes(path));
        var drawable = source.Drawables?.SingleOrDefault() ?? throw new InvalidDataException("Choose a single drawable YDD.");
        return Pieces(drawable).Select(p => p.Part).ToArray();
    }

    private static IEnumerable<(Part Part, int[] Faces)> Pieces(Drawable drawable)
    {
        var high = drawable.DrawableModels?.High ?? throw new InvalidDataException("No High LOD mesh.");
        for (int m = 0; m < high.Length; m++)
        for (int g = 0; g < high[m].Geometries.Length; g++)
        {
            var geometry = high[m].Geometries[g];
            var indices = geometry.IndexBuffer?.Indices;
            var parent = Enumerable.Range(0, geometry.VertexData.VertexCount).ToArray();
            if (indices == null || indices.Length % 3 != 0 || indices.Any(i => i >= parent.Length))
                throw new InvalidDataException("Invalid mesh triangle indices.");
            int Root(int v)
            {
                while (v != parent[v]) { parent[v] = parent[parent[v]]; v = parent[v]; }
                return v;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int root = Root(indices[i]);
                parent[Root(indices[i + 1])] = root;
                parent[Root(indices[i + 2])] = root;
            }
            int island = 0;
            foreach (var faces in Enumerable.Range(0, indices.Length / 3).GroupBy(i => Root(indices[3 * i])))
            {
                var triangles = faces.ToArray();
                yield return (new Part(m, g, island++, triangles.Length), triangles);
            }
        }
    }

    internal static Part[] NearAnchor(Drawable drawable, Skeleton sourceRig, Skeleton targetRig, ushort tag, bool glasses)
    {
        // Skinned YDD positions are already in ped rest space. Rigid models store bone-local positions.
        var center = targetRig.BonesMap[tag].AbsTransform.TranslationVector;
        var limits = new Vector3(0.10f);
        if (glasses)
        {
            var bones = targetRig.Bones.Items;
            var left = bones.FirstOrDefault(b => string.Equals(b.Name, "FB_L_Eye_000", StringComparison.OrdinalIgnoreCase));
            var right = bones.FirstOrDefault(b => string.Equals(b.Name, "FB_R_Eye_000", StringComparison.OrdinalIgnoreCase));
            bool haveEyes = left != null && right != null;
            if (haveEyes) center = (left.AbsTransform.TranslationVector + right.AbsTransform.TranslationVector) * 0.5f;
            limits = new Vector3(0.13f, 0.20f, haveEyes ? 0.065f : 0.17f);
        }

        var selected = new List<Part>();
        foreach (var (part, faces) in Pieces(drawable))
        {
            var model = drawable.DrawableModels.High[part.Model];
            var geometry = model.Geometries[part.Geometry];
            var pose = Matrix.Identity;
            if (model.HasSkin == 0)
            {
                var bones = sourceRig?.Bones?.Items;
                if (bones == null || model.BoneIndex >= bones.Length) continue;
                pose = bones[model.BoneIndex].AbsTransform;
            }
            bool Inside(int index)
            {
                var point = geometry.VertexData.GetVector3(geometry.IndexBuffer.Indices[index], 0);
                var delta = Vector3.TransformCoordinate(point, pose) - center;
                return Math.Abs(delta.X) <= limits.X && Math.Abs(delta.Y) <= limits.Y && Math.Abs(delta.Z) <= limits.Z;
            }
            if (faces.All(i => Inside(3*i) && Inside(3*i+1) && Inside(3*i+2))) selected.Add(part);
        }
        return selected.ToArray();
    }

    internal static void Extract(Drawable drawable, Part[] selection)
    {
        var chosen = selection.ToHashSet();
        var pieces = Pieces(drawable).Where(p => chosen.Contains(p.Part)).ToArray();
        if (pieces.Length != chosen.Count || pieces.Length == 0)
            throw new InvalidDataException("Source mesh changed. Select its pieces again.");
        var lods = drawable.DrawableModels;
        var models = new List<DrawableModel>();
        foreach (var group in pieces.GroupBy(p => p.Part.Model))
        {
            var model = lods.High[group.Key];
            var geometries = new List<DrawableGeometry>();
            foreach (var mesh in group.GroupBy(p => p.Part.Geometry))
            {
                var geom = model.Geometries[mesh.Key];
                var src = geom.VertexData;
                var indices = geom.IndexBuffer.Indices;
                var faces = mesh.SelectMany(p => p.Faces).OrderBy(i => i).ToArray();
                var used = new ushort[faces.Length * 3];
                for (int i = 0; i < faces.Length; i++)
                    Array.Copy(indices, faces[i] * 3, used, i * 3, 3);
                var vertices = used.Distinct().ToArray();
                var remap = new ushort[src.VertexCount];
                var bytes = new byte[checked(vertices.Length * src.VertexStride)];
                for (int i = 0; i < vertices.Length; i++)
                {
                    remap[vertices[i]] = checked((ushort)i);
                    Buffer.BlockCopy(src.VertexBytes, vertices[i] * src.VertexStride, bytes, i * src.VertexStride, src.VertexStride);
                }
                geom.VertexData = new VertexData { Info = src.Info, VertexType = src.VertexType,
                    VertexStride = src.VertexStride, VertexCount = vertices.Length, VertexBytes = bytes };
                geom.IndexBuffer = new IndexBuffer { Indices = used.Select(i => remap[i]).ToArray(), IndicesCount = (uint)used.Length };
                geometries.Add(geom);
            }
            model.Geometries = geometries.ToArray();
            model.GeometriesCount1 = model.GeometriesCount2 = model.GeometriesCount3 = checked((ushort)geometries.Count);
            model.ShaderMapping = geometries.Select(g => g.ShaderID).ToArray();
            models.Add(model);
        }
        lods.High = models.ToArray();
        lods.Med = lods.Low = lods.VLow = lods.Extra = null;
        drawable.BuildAllModels();
        drawable.BuildRenderMasks();
    }
}
