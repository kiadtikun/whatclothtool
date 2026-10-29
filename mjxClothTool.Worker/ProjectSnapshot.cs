namespace mjxClothTool.Worker;

public sealed record ProjectSnapshot(
    string ProjectName,
    bool IsExternalProject,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AddonSnapshot> Addons);

public sealed record AddonSnapshot(string Name, IReadOnlyList<DrawableSnapshot> Drawables);

public sealed record DrawableSnapshot(
    Guid Id,
    string Name,
    string? DisplayName,
    string? FilePath,
    int TypeNumeric,
    int Sex,
    bool IsProp,
    int Number,
    int Order,
    string? Group,
    IReadOnlyList<string> Tags,
    IReadOnlyList<TextureSnapshot> Textures);

public sealed record TextureSnapshot(
    Guid Id,
    string? FilePath,
    string? OriginalFileName,
    string? DisplayName,
    int TxtNumber,
    TextureDetailsSnapshot? TxtDetails);

public sealed record TextureDetailsSnapshot(
    int Width,
    int Height,
    int MipMapCount,
    string? Compression,
    string? Name);
