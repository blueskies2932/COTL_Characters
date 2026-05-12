using System.Diagnostics;

internal static partial class Program
{
    private static bool ParentProcessExited(int parentPid)
    {
        if (parentPid <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(parentPid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
