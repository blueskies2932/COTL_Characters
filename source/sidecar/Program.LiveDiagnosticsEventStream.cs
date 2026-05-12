using System.Text.Json;

internal static partial class Program
{
    private static List<GameEvent> ReadTailLines(string path, int maxLines)
    {
        try
        {
            var queue = new Queue<string>();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                queue.Enqueue(reader.ReadLine() ?? string.Empty);
                while (queue.Count > maxLines)
                    queue.Dequeue();
            }
            return queue.Select(ParseGameEvent).Where(item => item != null).Cast<GameEvent>().ToList();
        }
        catch
        {
            return new List<GameEvent>();
        }
    }

    private static GameEvent? ParseGameEvent(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            return new GameEvent(
                GetString(root, "time"),
                GetString(root, "scope"),
                GetString(root, "background"),
                GetString(root, "paused"),
                GetString(root, "special_event"),
                GetString(root, "followers"),
                GetString(root, "diagnostics"));
        }
        catch
        {
            return null;
        }
    }

    private sealed record GameEvent(string Time, string Scope, string Background, string Paused, string SpecialEvent, string Followers, string Diagnostics);
}
