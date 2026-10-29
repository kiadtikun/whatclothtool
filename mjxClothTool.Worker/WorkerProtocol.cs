using System.Text.Json;

namespace mjxClothTool.Worker;

public static class WorkerProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(TextReader input, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
                return;

            var response = ProcessLine(line);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            await output.FlushAsync(cancellationToken);
        }
    }

    private static object ProcessLine(string line)
    {
        string id = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(line);
            var request = document.RootElement;
            if (request.ValueKind != JsonValueKind.Object)
                return Failure(id, "invalid_request", "Request must be a JSON object.");

            if (request.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                id = idElement.GetString() ?? string.Empty;

            if (!request.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
                return Failure(id, "invalid_request", "Request version is required.");
            if (number != 1)
                return Failure(id, "unsupported_version", "Only protocol version 1 is supported.");

            if (!request.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String)
                return Failure(id, "invalid_request", "Request method is required.");
            if (method.GetString() != "openProject")
                return Failure(id, "unknown_method", "Unknown worker method.");

            if (!request.TryGetProperty("savePath", out var path) || path.ValueKind != JsonValueKind.String)
                return Failure(id, "invalid_request", "Project save path is required.");
            try
            {
                var snapshot = ProjectSnapshotReader.Read(path.GetString()!);
                return new { version = 1, id, result = snapshot };
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                return Failure(id, "project_error", ex.Message);
            }
        }
        catch (JsonException)
        {
            return Failure(id, "invalid_request", "Malformed JSON request.");
        }
    }

    private static object Failure(string id, string code, string message) =>
        new { version = 1, id, error = new { code, message } };
}
