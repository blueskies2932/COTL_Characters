using System.Text.Json.Nodes;

internal static partial class Program
{
    private static void WriteReady(string root, int parentPid, bool openAiAvailable)
    {
        var readyPath = Path.Combine(root, "sidecar-ready.json");
        var ready = new JsonObject
        {
            ["schema_version"] = 1,
            ["pid"] = Environment.ProcessId,
            ["parent_pid"] = parentPid,
            ["heartbeat_utc"] = DateTime.UtcNow.ToString("O"),
            ["capabilities"] = BuildCapabilities(openAiAvailable),
            ["message"] = "COTL AI NPC sidecar ready"
        };
        File.WriteAllText(readyPath, ready.ToJsonString(JsonOptions));
    }

    private static JsonArray BuildCapabilities(bool openAiAvailable)
    {
        var capabilities = new JsonArray
        {
            "sidecar_prompt_compiler_v1",
            "request_parts_v2",
            "decision_response_files"
        };

        if (openAiAvailable)
            capabilities.Add("openai_decision");
        if (openAiAvailable && ActiveProvider?.SupportsNativeWebSearch == true)
            capabilities.Add("native_web_search");

        return capabilities;
    }
}
