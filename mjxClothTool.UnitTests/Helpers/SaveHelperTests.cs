using mjxClothTool.Helpers;
using mjxClothTool.Models;
using System.Text.Json;

namespace mjxClothTool.UnitTests.Helpers;

public class SaveHelperTests
{
    [Fact]
    public async Task WritingProjectSavePreservesCustomGroupsInMemoryAndOnDisk()
    {
        using var temp = new TestTempDirectory();
        var path = temp.FilePath(SaveHelper.AutoSaveFileName);
        var project = new AddonManager { ProjectName = "Group test" };
        project.Groups.Add("Streetwear");
        project.Groups.Add("Accessories");

        var save = SaveHelper.WriteSaveFileAtomicallyAsync(project, path);
        project.Groups.Add("Edited during save");
        await save;

        Assert.Equal(["Streetwear", "Accessories", "Edited during save"], project.Groups);
        using var saved = JsonDocument.Parse(File.ReadAllBytes(path));
        var savedGroups = saved.RootElement.GetProperty(nameof(AddonManager.Groups));
        Assert.Equal(["Streetwear", "Accessories"], savedGroups.EnumerateArray().Select(group => group.GetString()));
    }

    [Theory]
    [InlineData(false, "autosave.json")]
    [InlineData(true, "autosave.external.json")]
    public void GetSaveFileName_SelectsProjectSaveFileName(bool isExternalProject, string expected)
    {
        Assert.Equal(expected, SaveHelper.GetSaveFileName(isExternalProject));
    }

    [Fact]
    public void ProjectExists_ReturnsFalseForMissingInputs()
    {
        Assert.False(SaveHelper.ProjectExists("", "Project", out var isExternal));
        Assert.False(isExternal);

        Assert.False(SaveHelper.ProjectExists("C:\\does-not-matter", "", out isExternal));
        Assert.False(isExternal);
    }

    [Fact]
    public void ProjectExists_FindsInternalProjectSave()
    {
        using var temp = new TestTempDirectory();
        var projectFolder = temp.FilePath("MyProject");
        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(Path.Combine(projectFolder, SaveHelper.AutoSaveFileName), "{}");

        var exists = SaveHelper.ProjectExists(temp.Path, " MyProject ", out var isExternal);

        Assert.True(exists);
        Assert.False(isExternal);
    }

    [Fact]
    public void ProjectExists_FindsExternalProjectSave()
    {
        using var temp = new TestTempDirectory();
        var projectFolder = temp.FilePath("ExternalProject");
        Directory.CreateDirectory(projectFolder);
        File.WriteAllText(Path.Combine(projectFolder, SaveHelper.AutoSaveExternalFileName), "{}");

        var exists = SaveHelper.ProjectExists(temp.Path, "ExternalProject", out var isExternal);

        Assert.True(exists);
        Assert.True(isExternal);
    }

    [Fact]
    public void GetBackupSaveFiles_ReturnsBackupsNewestFirst()
    {
        using var temp = new TestTempDirectory();
        var saveFilePath = temp.FilePath("Project", SaveHelper.AutoSaveFileName);
        var backupFolder = Path.Combine(Path.GetDirectoryName(saveFilePath)!, "save-backups");
        Directory.CreateDirectory(backupFolder);

        var olderBackup = Path.Combine(backupFolder, "autosave.20260101-120000-000.json");
        var newerBackup = Path.Combine(backupFolder, "autosave.20260102-120000-000.json");
        File.WriteAllText(olderBackup, "{}");
        File.WriteAllText(newerBackup, "{}");
        File.SetCreationTime(olderBackup, new DateTime(2026, 1, 1, 12, 0, 0));
        File.SetCreationTime(newerBackup, new DateTime(2026, 1, 2, 12, 0, 0));
        File.WriteAllText(Path.Combine(backupFolder, "other.20260103-120000-000.json"), "{}");

        var backups = SaveHelper.GetBackupSaveFiles(saveFilePath);

        Assert.Equal([newerBackup, olderBackup], backups.Select(backup => backup.FilePath).ToList());
    }

    [Fact]
    public void GetBackupSaveFiles_ReturnsEmptyListWhenBackupFolderDoesNotExist()
    {
        using var temp = new TestTempDirectory();
        var saveFilePath = temp.FilePath("Project", SaveHelper.AutoSaveFileName);

        var backups = SaveHelper.GetBackupSaveFiles(saveFilePath);

        Assert.Empty(backups);
    }

    [Theory]
    [InlineData(false, "autosave.20260101-120000-000.json")]
    [InlineData(true, "autosave.external.20260101-120000-000.json")]
    public void GetBackupSaveFiles_FiltersByRequestedSaveName(bool isExternal, string expectedFileName)
    {
        using var temp = new TestTempDirectory();
        var projectFolder = temp.FilePath("Project");
        var backupFolder = Path.Combine(projectFolder, "save-backups");
        Directory.CreateDirectory(backupFolder);
        var internalBackup = Path.Combine(backupFolder, "autosave.20260101-120000-000.json");
        var externalBackup = Path.Combine(backupFolder, "autosave.external.20260101-120000-000.json");
        File.WriteAllText(internalBackup, "{}");
        File.WriteAllText(externalBackup, "{}");

        var backups = SaveHelper.GetBackupSaveFiles(Path.Combine(projectFolder, SaveHelper.GetSaveFileName(isExternal)));

        var backupPath = Assert.Single(backups).FilePath;
        Assert.Equal(Path.Combine(backupFolder, expectedFileName), backupPath);
    }
}
