using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mjxClothTool.Helpers;

internal static class RigidPropConverter
{
    internal const string NeedsPieces = "This model uses several bones. Select the mesh pieces to export.";
    private const uint PedShaderName = 886638433;
    private const uint PedShaderFile = 540746503;
    private const uint PedDefaultCutoutShaderFile = 3176173383;
    private const uint RigidPedVertexFlags = 0x40F9;
    internal record Result(string Name, byte[] Data, string SourceBinding);

    internal static string Export(Result prop, string[] textures, string parent)
    {
        var destination = Path.Combine(parent, prop.Name);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"'{prop.Name}' already exists. Choose another folder or drawable number.");
        if (textures.Length > 26) throw new ArgumentException("Too many texture variations.");
        var stage = Path.Combine(parent, $".prop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        try
        {
            var modelData = prop.Data;
            var split = prop.Name.LastIndexOf('_');
            var texturePrefix = prop.Name.Insert(split, "_diff");
            uint originalDiffuse = 0;
            var references = new List<TextureBase>();
            if (textures.Length > 0)
            {
                var ydd = new YddFile();
                ydd.Load(modelData);
                var drawable = ydd.Drawables.Single();
                var shaders = drawable.AllModels.SelectMany(m => m.Geometries).Select(g => g.Shader).OfType<ShaderFX>().Distinct();
                var missing = new List<ShaderParameter>();
                foreach (var shader in shaders)
                {
                    var parameters = shader.ParametersList;
                    if (parameters?.Hashes == null) continue;
                    int index = Array.IndexOf(parameters.Hashes, (MetaName)ShaderParamNames.DiffuseSampler);
                    if (index < 0) continue;
                    if (parameters.Parameters[index].Data == null) missing.Add(parameters.Parameters[index]);
                    else if (parameters.Parameters[index].Data is TextureBase texture &&
                        texture is not Texture && drawable.ShaderGroup.TextureDictionary?.Lookup(texture.NameHash) == null)
                        references.Add(texture);
                }
                // A clothing variation replaces one diffuse map. Preserve independent material maps.
                if (references.Select(t => t.NameHash).Distinct().Count() <= 1 && (references.Count > 0 || missing.Count > 0))
                {
                    originalDiffuse = references.FirstOrDefault()?.NameHash ?? 0;
                    foreach (var parameter in missing)
                    {
                        var replacement = new TextureBase { Unknown_32h = 2 };
                        parameter.Data = replacement;
                        references.Add(replacement);
                    }
                    foreach (var texture in references.Distinct())
                    {
                        texture.Name = texturePrefix + "_a";
                        texture.NameHash = JenkHash.GenHash(texture.Name);
                    }
                    modelData = ydd.Save();
                }
                else references.Clear();
            }
            File.WriteAllBytes(Path.Combine(stage, prop.Name + ".ydd"), modelData);
            for (int i = 0; i < textures.Length; i++)
            {
                var textureName = $"{texturePrefix}_{(char)('a' + i)}";
                var output = Path.Combine(stage, textureName + ".ytd");
                if (references.Count == 0) File.Copy(textures[i], output);
                else
                {
                    var ytd = new YtdFile();
                    ytd.Load(File.ReadAllBytes(textures[i]));
                    var entries = ytd.TextureDict?.Textures?.data_items;
                    if (entries == null || entries.Length == 0) throw new InvalidDataException("Empty texture variation.");
                    var candidates = entries.Where(t => t.NameHash == originalDiffuse || string.Equals(t.Name, textureName, StringComparison.OrdinalIgnoreCase)).ToArray();
                    var diffuse = entries.Length == 1 ? entries[0] : candidates.Length == 1 ? candidates[0] :
                        throw new InvalidDataException($"Cannot identify the diffuse map in {Path.GetFileName(textures[i])}.");
                    diffuse.Name = textureName;
                    diffuse.NameHash = JenkHash.GenHash(textureName);
                    ytd.TextureDict.BuildFromTextureList(entries.ToList());
                    File.WriteAllBytes(output, ytd.Save());
                }
            }
            Directory.Move(stage, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }

    internal static ushort AnchorTag(Enums.PropNumbers slot) => slot switch
        {
            Enums.PropNumbers.p_eyes => 31086,
            Enums.PropNumbers.p_lwrist => 61007,
            Enums.PropNumbers.p_rwrist => 43810,
            _ => throw new ArgumentException("Choose glasses or a wrist slot.")
        };

    internal static Result Convert(string path, string rigPath, Enums.PropNumbers slot, int number,
        Vector3 offset, Vector3 degrees, float scale, Skeleton pedRig = null, string projectPropType = null, PropMeshParts.Part[] pieces = null, bool autoSelect = false, bool allGlassesPieces = false)
    {
        if (number < 0 || number > 999 || !float.IsFinite(scale) || scale <= 0 || !Finite(offset) || !Finite(degrees))
            throw new ArgumentException("Enter a number from 0 to 999, finite adjustments and a positive scale.");
        ushort tag = AnchorTag(slot);

        bool objectLocal = Path.GetExtension(path).Equals(".ydr", StringComparison.OrdinalIgnoreCase);
        Drawable drawable;
        var bytes = File.ReadAllBytes(path);
        if (objectLocal)
        {
            var source = new YdrFile();
            source.Load(bytes);
            drawable = source.Drawable ?? throw new InvalidDataException("Cannot read this YDR.");
        }
        else
        {
            var source = new YddFile();
            source.Load(bytes);
            if (source.Drawables?.Length != 1)
                throw new InvalidDataException("Select a YDD containing one accessory drawable.");
            drawable = source.Drawables[0];
        }
        bool wholeGlasses = allGlassesPieces && slot == Enums.PropNumbers.p_eyes;
        bool extracting = !wholeGlasses && pieces is { Length: > 0 };
        if (extracting) PropMeshParts.Extract(drawable, pieces);
        var models = drawable.AllModels;
        if (models == null || models.Length == 0)
            throw new InvalidDataException("The accessory has no drawable models.");
        var sourceName = Path.GetFileNameWithoutExtension(path).Split('^').Last();
        if (!sourceName.StartsWith("p_", StringComparison.OrdinalIgnoreCase))
            sourceName = drawable.Name?.Split('^').Last() ?? "";
        // Project assets may have GUID filenames and no embedded prop name.
        if (!sourceName.StartsWith("p_", StringComparison.OrdinalIgnoreCase) && projectPropType != null &&
            drawable.Skeleton == null && models.All(m => m.HasSkin == 0 && m.BoneIndex == 0))
            sourceName = projectPropType + "_";
        if (!objectLocal && sourceName.StartsWith("p_", StringComparison.OrdinalIgnoreCase))
        {
            if (!sourceName.StartsWith(slot + "_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("To adjust this existing prop, choose its original slot.");
            objectLocal = true; // Already expressed in this anchor's local coordinates.
        }
        if (objectLocal && (drawable.Skeleton != null || models.Any(m => m.HasSkin != 0 || m.BoneIndex != 0)))
            throw new InvalidDataException("Use a rigid object YDR or a clothing YDD.");

        var rig = pedRig ?? drawable.Skeleton;
        if (!objectLocal && rigPath != null)
        {
            var yft = new YftFile();
            yft.Load(File.ReadAllBytes(rigPath));
            rig = yft.Fragment?.Drawable?.Skeleton;
        }
        var targetInverse = Matrix.Identity;
        if (!objectLocal)
        {
            if (rig?.BonesMap == null || !rig.BonesMap.TryGetValue(tag, out var target))
                throw new InvalidDataException($"Choose the matching freemode ped YFT containing the {slot} anchor.");
            targetInverse = Matrix.Invert(target.AbsTransform);
        }
        var sourceRig = drawable.Skeleton ?? rig;
        bool automatic = false;
        if (!wholeGlasses && !extracting && !objectLocal && autoSelect && UsesSeveralBones(models))
        {
            pieces = PropMeshParts.NearAnchor(drawable, sourceRig, rig, tag);
            if (pieces.Length == 0) throw new InvalidDataException(NeedsPieces);
            PropMeshParts.Extract(drawable, pieces);
            models = drawable.AllModels;
            automatic = extracting = true;
        }
        var radians = degrees * (float)(Math.PI / 180);
        var adjust = Matrix.Scaling(scale) * Matrix.RotationX(radians.X) * Matrix.RotationY(radians.Y)
            * Matrix.RotationZ(radians.Z) * Matrix.Translation(offset);
        var bindings = new HashSet<int>();
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var model in models.Distinct())
        {
            var transform = targetInverse * adjust;
            if (!objectLocal && model.HasSkin == 0)
            {
                var bone = BoneAt(sourceRig, model.BoneIndex);
                bindings.Add(model.BoneIndex);
                transform = bone.AbsTransform * transform;
            }
            if (model.Geometries == null || model.Geometries.Length == 0)
                throw new InvalidDataException("An accessory model has no geometry.");
            foreach (var geom in model.Geometries)
            {
                if (!wholeGlasses && !extracting && model.HasSkin != 0)
                    ReadBinding(geom, bindings);
                if (!wholeGlasses && !extracting && bindings.Count > 1)
                    throw new InvalidDataException(NeedsPieces);
                RewriteVertices(geom, transform, ref min, ref max);
            }
            model.SkeletonBinding = 0;
            // A model without the smallest render mask bit is treated as a proxy and is never
            // drawn on its own, so the accessory only shows up in tools that ignore the mask.
            model.RenderMask |= 1;
            model.RebuildBounds();
        }
        // Ped props are drawn at any distance and vanilla ped drawables use 9998. A zero
        // distance - common on YDR and third-party accessories - makes the game cull the prop.
        const float AlwaysDraw = 9998f;
        if (drawable.LodDistHigh <= 0) drawable.LodDistHigh = AlwaysDraw;
        if (drawable.LodDistMed <= 0) drawable.LodDistMed = AlwaysDraw;
        if (drawable.LodDistLow <= 0) drawable.LodDistLow = AlwaysDraw;
        if (drawable.LodDistVlow <= 0) drawable.LodDistVlow = AlwaysDraw;
        drawable.BuildRenderMasks();
        var binding = wholeGlasses ? "All pieces (glasses)" : automatic ? $"Auto-selected {pieces.Length} pieces (High LOD)" : extracting ? "Selected pieces (rigid)" : objectLocal ? "Object origin" : BoneAt(sourceRig, bindings.Single()).Name;
        var name = $"{slot}_{number:D3}";
        drawable.Name = name;
        drawable.Skeleton = null;
        drawable.Joints = null;
        drawable.Bound = null;
        drawable.BoundingBoxMin = min;
        drawable.BoundingBoxMax = max;
        drawable.BoundingCenter = (min + max) * 0.5f;
        drawable.BoundingSphereRadius = Vector3.Distance(min, drawable.BoundingCenter);
        drawable.BuildVertexDecls();
        var result = new YddFile { DrawableDict = new DrawableDictionary
        {
            Hashes = [JenkHash.GenHash(name)],
            Drawables = new ResourcePointerArray64<Drawable> { data_items = [drawable] }
        } };
        var data = result.Save();
        var check = new YddFile();
        check.Load(data);
        var saved = check.Drawables?.SingleOrDefault()?.DrawableModels;
        var lods = drawable.DrawableModels;
        if (saved == null || saved.High?.Length != lods.High?.Length || saved.Med?.Length != lods.Med?.Length ||
            saved.Low?.Length != lods.Low?.Length || saved.VLow?.Length != lods.VLow?.Length || saved.Extra?.Length != lods.Extra?.Length)
            throw new InvalidDataException("Could not preserve all levels of detail. Supply consecutive LODs starting at High.");
        return new Result(name, data, binding);
    }

    private static bool UsesSeveralBones(DrawableModel[] models)
    {
        var bindings = new HashSet<int>();
        foreach (var model in models)
        {
            if (model.HasSkin == 0) bindings.Add(model.BoneIndex);
            else foreach (var geometry in model.Geometries)
            {
                ReadBinding(geometry, bindings);
                if (bindings.Count > 1) return true;
            }
            if (bindings.Count > 1) return true;
        }
        return false;
    }

    private static Bone BoneAt(Skeleton rig, int index)
    {
        var bones = rig?.Bones?.Items;
        if (bones == null || index >= bones.Length)
            throw new InvalidDataException("The source bone is missing from the selected skeleton.");
        return bones[index];
    }

    private static void ReadBinding(DrawableGeometry geometry, HashSet<int> bindings)
    {
        var v = geometry.VertexData;
        const int weights = (int)VertexSemantics.BlendWeights, indices = (int)VertexSemantics.BlendIndices;
        if (v?.Info == null || (v.Info.Flags & 6) != 6 ||
            VertexComponentTypes.GetSizeInBytes(v.Info.GetComponentType(weights)) != 4 ||
            VertexComponentTypes.GetSizeInBytes(v.Info.GetComponentType(indices)) != 4)
            throw new InvalidDataException("Unsupported skin weight layout.");
        var wo = v.Info.GetComponentOffset(weights);
        var io = v.Info.GetComponentOffset(indices);
        for (int n = 0; n < v.VertexCount; n++)
        {
            int start = n * v.VertexStride;
            bool weighted = false;
            for (int j = 0; j < 4; j++)
            {
                if (v.VertexBytes[start + wo + j] == 0) continue;
                weighted = true;
                int index = v.VertexBytes[start + io + j];
                if (geometry.BoneIds is { Length: > 0 } palette)
                {
                    if (index >= palette.Length) throw new InvalidDataException("Invalid bone palette.");
                    index = palette[index];
                }
                bindings.Add(index);
            }
            if (!weighted) throw new InvalidDataException("The accessory contains unweighted vertices.");
            if (bindings.Count > 1) return;
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    internal static void RewriteVertices(DrawableGeometry geometry, Matrix transform, ref Vector3 min, ref Vector3 max)
    {
        var src = geometry.VertexData;
        var info = src?.Info;
        if (src == null || src.VertexCount == 0 || info?.Types != VertexDeclarationTypes.GTAV1 ||
            (info.Flags & 1) == 0 || info.Stride != src.VertexStride ||
            src.VertexBytes.Length != checked(src.VertexCount * src.VertexStride))
            throw new InvalidDataException("Unsupported vertex layout. Use a standard GTA V rigid accessory mesh.");
        var shader = geometry.Shader as ShaderFX;
        bool rigidPed = shader != null && (uint)shader.FileName == PedDefaultCutoutShaderFile;
        uint flags = info.Flags & ~6u;
        if (rigidPed)
        {
            flags = RigidPedVertexFlags;
            shader.Name = new MetaHash(PedShaderName);
            shader.FileName = new MetaHash(PedShaderFile);
        }
        var layout = new VertexDeclaration { Flags = flags, Types = info.Types };
        layout.UpdateCountAndStride();
        var dest = new VertexData { Info = layout, VertexCount = src.VertexCount, VertexStride = layout.Stride,
            VertexType = (VertexType)layout.Flags, VertexBytes = new byte[checked(src.VertexCount * layout.Stride)] };
        var normalMatrix = Matrix.Transpose(Matrix.Invert(transform));
        for (int n = 0; n < src.VertexCount; n++)
        {
            if (rigidPed)
            {
                foreach (int channel in new[] { (int)VertexSemantics.Colour0, (int)VertexSemantics.Colour1, (int)VertexSemantics.TexCoord0 })
                {
                    if ((info.Flags & (1u << channel)) == 0) continue;
                    int size = VertexComponentTypes.GetSizeInBytes(info.GetComponentType(channel));
                    Buffer.BlockCopy(src.VertexBytes, n * src.VertexStride + info.GetComponentOffset(channel),
                        dest.VertexBytes, n * dest.VertexStride + layout.GetComponentOffset(channel), size);
                }
                foreach (int channel in new[] { (int)VertexSemantics.Colour0, (int)VertexSemantics.Colour1 })
                    if ((info.Flags & (1u << channel)) == 0)
                        dest.SetColour(n, channel, new Color(255, 255, 255, 255));
                var uv = (info.Flags & (1u << (int)VertexSemantics.TexCoord0)) == 0
                    ? Vector2.Zero : src.GetVector2(n, (int)VertexSemantics.TexCoord0);
                dest.SetVector2(n, (int)VertexSemantics.TexCoord0, uv);
                dest.SetVector2(n, (int)VertexSemantics.TexCoord1, uv);
            }
            else
            {
                // Position is first, then the two optional skin fields. Preserve the remaining channels exactly.
                int tail = info.GetComponentOffset((int)VertexSemantics.Normal);
                Buffer.BlockCopy(src.VertexBytes, n * src.VertexStride + tail, dest.VertexBytes,
                    n * dest.VertexStride + 12, dest.VertexStride - 12);
            }
            var pos = Vector3.TransformCoordinate(src.GetVector3(n, 0), transform);
            if (!Finite(pos)) throw new InvalidDataException("Non-finite coordinates in converted model.");
            dest.SetVector3(n, 0, pos);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
            foreach (int channel in DirectionChannels)
            {
                if ((info.Flags & (1u << channel)) == 0) continue;
                var dir = Vector3.TransformNormal(src.GetVector3(n, channel),
                    channel == (int)VertexSemantics.Normal ? normalMatrix : transform);
                dir.Normalize();
                if (!Finite(dir)) throw new InvalidDataException("Invalid vertex direction.");
                dest.SetVector3(n, channel, dir);
            }
            if (rigidPed)
            {
                var normal = dest.GetVector3(n, (int)VertexSemantics.Normal);
                var axis = Math.Abs(normal.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
                var tangent = Vector3.Cross(axis, normal);
                tangent.Normalize();
                dest.SetVector4(n, (int)VertexSemantics.Tangent, new Vector4(tangent, 1));
            }
        }
        var buffer = geometry.VertexBuffer;
        if (buffer.Data1 != null && buffer.Data2 != null && buffer.Data1 != buffer.Data2)
            throw new InvalidDataException("Accessory uses two different vertex streams. Split the mesh first.");
        geometry.VertexBuffer = new VertexBuffer
        {
            VFT = buffer.VFT, Flags = buffer.Flags, Info = layout, VertexStride = layout.Stride,
            VertexCount = (uint)dest.VertexCount, Data1 = dest, Data2 = dest
        };
        geometry.VertexData = dest;
        geometry.BoneIds = null;
    }

    private static readonly int[] DirectionChannels = [(int)VertexSemantics.Normal, (int)VertexSemantics.Tangent, (int)VertexSemantics.Binormal];
}
