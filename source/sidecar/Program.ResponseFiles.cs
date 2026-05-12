using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task WriteResponse(string responsePath, bool success, string message, string outputText, string model, IEnumerable<string> errors)
    {
        var response = new JsonObject
        {
            ["schema_version"] = 1,
            ["success"] = success,
            ["message"] = message,
            ["model"] = model,
            ["decision_json"] = outputText,
            ["completed_utc"] = DateTime.UtcNow.ToString("O"),
            ["errors"] = new JsonArray(errors.Select(error => JsonValue.Create(error)).ToArray())
        };

        await WriteTextAtomic(responsePath, response.ToJsonString(JsonOptions));
    }

    private static void Archive(string requestPath, string archiveDir)
    {
        try
        {
            Directory.CreateDirectory(archiveDir);
            var destination = Path.Combine(archiveDir, Path.GetFileName(requestPath));
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(requestPath, destination);
        }
        catch
        {
            // Best effort.
        }
    }
}
