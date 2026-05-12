using System.Text.Json;

internal static partial class Program
{
    private static async Task WriteTextAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.tmp";
        await File.WriteAllTextAsync(temp, text);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    private static bool TryParseJson(string path, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonText(string json, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
