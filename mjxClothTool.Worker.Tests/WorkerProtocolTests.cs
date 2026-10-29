using System.Text.Json;
using mjxClothTool.Worker;
using Xunit;

namespace mjxClothTool.Worker.Tests;

public sealed class WorkerProtocolTests
{
    [Fact]
    public async Task OpenProject_returns_snapshot_with_matching_id()
    {
        using var fixture = new SaveFixture();
        var responses = await ExchangeAsync(Request("one", fixture.Path));
        Assert.Equal("one", responses[0].GetProperty("id").GetString());
        Assert.Equal(1, responses[0].GetProperty("version").GetInt32());
        Assert.Equal("Fixture", responses[0].GetProperty("result").GetProperty("projectName").GetString());
    }

    [Fact]
    public async Task Unsupported_version_and_method_return_distinct_errors()
    {
        var responses = await ExchangeAsync(
            """{"version":2,"id":"v","method":"openProject","savePath":"C:\\missing\\autosave.json"}""",
            """{"version":1,"id":"m","method":"deleteProject","savePath":"C:\\missing\\autosave.json"}"""
        );
        Assert.Equal("unsupported_version", responses[0].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("unknown_method", responses[1].GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Malformed_json_returns_error_and_keeps_processing()
    {
        using var fixture = new SaveFixture();
        var responses = await ExchangeAsync("{bad", Request("next", fixture.Path));
        Assert.Equal("invalid_request", responses[0].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Fixture", responses[1].GetProperty("result").GetProperty("projectName").GetString());
    }

    [Fact]
    public async Task Missing_and_corrupt_save_return_errors_then_valid_save_works()
    {
        using var fixture = new SaveFixture();
        var missing = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fixture.Path)!, "missing", "autosave.json");
        var corrupt = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fixture.Path)!, "corrupt", "autosave.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(corrupt)!);
        File.WriteAllText(corrupt, "{");
        var responses = await ExchangeAsync(Request("missing", missing), Request("corrupt", corrupt), Request("valid", fixture.Path));
        Assert.Equal("project_error", responses[0].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("project_error", responses[1].GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("valid", responses[2].GetProperty("id").GetString());
        Assert.Equal("Fixture", responses[2].GetProperty("result").GetProperty("projectName").GetString());
    }

    [Fact]
    public async Task Two_requests_receive_two_responses_in_order()
    {
        using var fixture = new SaveFixture();
        var responses = await ExchangeAsync(Request("first", fixture.Path), Request("second", fixture.Path));
        Assert.Equal(new[] { "first", "second" }, responses.Select(x => x.GetProperty("id").GetString()));
    }

    private static string Request(string id, string path) => JsonSerializer.Serialize(new { version = 1, id, method = "openProject", savePath = path });

    private static async Task<JsonElement[]> ExchangeAsync(params string[] requests)
    {
        using var input = new StringReader(string.Join(Environment.NewLine, requests));
        using var output = new StringWriter();
        using var errors = new StringWriter();
        await WorkerProtocol.RunAsync(input, output, errors, CancellationToken.None);
        Assert.Equal(string.Empty, errors.ToString());
        return output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    }

    private sealed class SaveFixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mjx-worker-protocol-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public SaveFixture()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "autosave.json");
            File.WriteAllText(Path, """{"ProjectName":"Fixture","Addons":[]}""");
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
