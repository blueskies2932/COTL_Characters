using System.Linq;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiTextNormalize
    {
        internal static string CompactLettersAndDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant);
            return new string(chars.ToArray());
        }
    }
}
