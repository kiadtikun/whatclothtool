using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using mjxClothTool.Worker;
using Xunit;

namespace mjxClothTool.Worker.Tests;

public sealed class WorkerSmokeTests
{
    [Fact]
    public async Task Process_opens_save_without_changing_its_bytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mjx-worker-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var savePath = Path.Combine(directory, "autosave.json");
        await File.WriteAllTextAsync(savePath, """{"ProjectName":"Smoke","Addons":[{"Name":"Addon 1","Drawables":[]}]}""", new UTF8Encoding(false));
        var before = SHA256.HashData(await File.ReadAllBytesAsync(savePath));

        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(typeof(ProjectSnapshotReader).Assembly.Location);
            using var process = Process.Start(start)!;
            var request = JsonSerializer.Serialize(new { version = 1, id = "smoke", method = "openProject", savePath });
            await process.StandardInput.WriteLineAsync(request);
            await process.StandardInput.FlushAsync();
            var responseLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(responseLine);
            using var response = JsonDocument.Parse(responseLine);
            Assert.Equal("smoke", response.RootElement.GetProperty("id").GetString());
            Assert.Equal("Smoke", response.RootElement.GetProperty("result").GetProperty("projectName").GetString());
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(savePath)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
