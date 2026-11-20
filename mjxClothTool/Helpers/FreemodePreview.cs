using CodeWalker;
using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace mjxClothTool.Helpers;

internal sealed record FreemodePreview(Model3DGroup Body, Skeleton Rig)
{
    internal static string FindGame()
    {
        try
        {
            var folder = GTAFolder.AutoDetectFolder();
            if (folder != null) return folder;
        }
        catch (System.Security.SecurityException) { }
        catch (UnauthorizedAccessException) { }
        var steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        var libraries = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraries))
            foreach (Match match in Regex.Matches(File.ReadAllText(libraries), @"""path""\s+""([^""]+)"""))
            {
                var root = match.Groups[1].Value.Replace(@"\\", @"\");
                var candidate = Path.Combine(root, "steamapps", "common", "Grand Theft Auto V");
                if (File.Exists(Path.Combine(candidate, "x64v.rpf"))) return candidate;
            }
        return null;
    }

    private static RpfFile OpenArchive(string folder)
    {
        if (folder == null || !File.Exists(Path.Combine(folder, "x64v.rpf")))
            throw new FileNotFoundException("Choose your GTA V Legacy folder to show the character.");
        var rpf = new RpfFile(Path.Combine(folder, "x64v.rpf"), "x64v.rpf");
        GTA5Keys.LoadFromPath(folder, CodeWalker.Properties.Settings.Default.Key);
        rpf.ScanStructure(null, _ => { });
        return rpf.Children?.FirstOrDefault(a => a.Name.Equals("streamedpeds_mp.rpf", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Cannot read freemode characters from this installation. Choose GTA V Legacy.");
    }

    internal static Skeleton LoadRig(string folder, string pedName) => ReadRig(OpenArchive(folder), pedName);

    private static Skeleton ReadRig(RpfFile archive, string pedName) =>
        RpfFile.GetFile<YftFile>(archive.AllEntries.FirstOrDefault(e => e.Name == pedName + ".yft"))?.Fragment?.Drawable?.Skeleton
            ?? throw new InvalidDataException("The character skeleton could not be loaded.");

    internal static FreemodePreview Load(string folder, string pedName)
    {
        var archive = OpenArchive(folder);
        var entries = archive.AllEntries;
        var rig = ReadRig(archive, pedName);
        var components = entries.Where(e => e.Parent?.Name == pedName).ToArray();
        var body = new Model3DGroup();
        foreach (string part in new[] { "head", "uppr", "lowr", "feet", "jbib" })
        {
            var entry = components.FirstOrDefault(e => e.Name == part + "_000_r.ydd" || e.Name == part + "_000_u.ydd");
            var model = RpfFile.GetFile<YddFile>(entry) ?? throw new InvalidDataException($"Missing character component {part}.");
            var tex = components.FirstOrDefault(e => e.Name.StartsWith(part + "_diff_000_a_", StringComparison.Ordinal) && e.Name.EndsWith(".ytd"));
            body.Children.Add(DrawableImportPreview.Create(model, RpfFile.GetFile<YtdFile>(tex)?.TextureDict, default, false));
        }
        body.Freeze();
        return new(body, rig);
    }

    internal MatrixTransform3D Anchor(Enums.PropNumbers slot)
    {
        Matrix m = Rig.BonesMap[RigidPropConverter.AnchorTag(slot)].AbsTransform;
        return new(new Matrix3D(m.M11,m.M12,m.M13,0, m.M21,m.M22,m.M23,0,
            m.M31,m.M32,m.M33,0, m.M41,m.M42,m.M43,1));
    }
}
