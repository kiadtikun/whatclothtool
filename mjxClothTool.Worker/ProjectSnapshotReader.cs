using System.Text.Json;

namespace mjxClothTool.Worker;

public static class ProjectSnapshotReader
{
    public static ProjectSnapshot Read(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath) || !Path.IsPathFullyQualified(savePath))
            throw new InvalidDataException("A full project save path is required.");

        var name = Path.GetFileName(savePath);
        if (!string.Equals(name, "autosave.json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(name, "autosave.external.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select autosave.json or autosave.external.json.");

        try
        {
            using var stream = File.OpenRead(savePath);
            using var document = JsonDocument.Parse(stream);
            var root = RequireObject(document.RootElement, "project");
            var addons = ReadArray(root, "Addons", ParseAddon);
            return new ProjectSnapshot(
                ReadString(root, "ProjectName") ?? string.Empty,
                ReadBool(root, "IsExternalProject", string.Equals(name, "autosave.external.json", StringComparison.OrdinalIgnoreCase)),
                ReadArray(root, "Groups", value => value.GetString() ?? string.Empty),
                ReadArray(root, "Tags", value => value.GetString() ?? string.Empty),
                addons);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Project save contains invalid JSON or an invalid field type.", ex);
        }
    }

    private static AddonSnapshot ParseAddon(JsonElement value)
    {
        value = RequireObject(value, "addon");
        return new AddonSnapshot(
            ReadString(value, "Name") ?? string.Empty,
            ReadArray(value, "Drawables", ParseDrawable));
    }

    private static DrawableSnapshot ParseDrawable(JsonElement value)
    {
        value = RequireObject(value, "drawable");
        var number = ReadInt(value, "Number");
        return new DrawableSnapshot(
            ReadGuid(value, "Id"),
            ReadString(value, "Name") ?? string.Empty,
            ReadString(value, "DisplayName"),
            ReadString(value, "FilePath"),
            ReadInt(value, "TypeNumeric"),
            ReadInt(value, "Sex"),
            ReadBool(value, "IsProp"),
            number,
            ReadInt(value, "Order", number),
            ReadString(value, "Group"),
            ReadArray(value, "Tags", item => item.GetString() ?? string.Empty),
            ReadArray(value, "Textures", ParseTexture));
    }

    private static TextureSnapshot ParseTexture(JsonElement value)
    {
        value = RequireObject(value, "texture");
        TextureDetailsSnapshot? details = null;
        if (value.TryGetProperty("TxtDetails", out var detailValue) && detailValue.ValueKind != JsonValueKind.Null)
        {
            detailValue = RequireObject(detailValue, "texture details");
            details = new TextureDetailsSnapshot(
                ReadInt(detailValue, "Width"),
                ReadInt(detailValue, "Height"),
                ReadInt(detailValue, "MipMapCount"),
                ReadString(detailValue, "Compression"),
                ReadString(detailValue, "Name"));
        }
        return new TextureSnapshot(
            ReadGuid(value, "Id"),
            ReadString(value, "FilePath"),
            ReadString(value, "OriginalFileName"),
            ReadString(value, "DisplayName"),
            ReadInt(value, "TxtNumber"),
            details);
    }

    private static JsonElement RequireObject(JsonElement value, string label) =>
        value.ValueKind == JsonValueKind.Object ? value : throw new InvalidDataException($"Expected {label} object.");

    private static IReadOnlyList<T> ReadArray<T>(JsonElement parent, string key, Func<JsonElement, T> parse)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return [];
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Expected {key} array.");
        return value.EnumerateArray().Select(parse).ToArray();
    }

    private static string? ReadString(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    private static int ReadInt(JsonElement parent, string key, int fallback = 0) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetInt32() : fallback;

    private static bool ReadBool(JsonElement parent, string key, bool fallback = false) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetBoolean() : fallback;

    private static Guid ReadGuid(JsonElement parent, string key) =>
        Guid.TryParse(ReadString(parent, key), out var id) ? id : Guid.Empty;
}
