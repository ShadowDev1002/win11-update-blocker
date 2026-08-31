namespace Win11UpdateBlocker.Core.Updates;

public static class AppVersion
{
    public static string Normalize(string version) =>
        version.Trim().TrimStart('v', 'V');

    public static bool IsNewer(string candidate, string current)
    {
        var currentParts = Parse(Normalize(current));
        var candidateParts = Parse(Normalize(candidate));
        var length = Math.Max(currentParts.Count, candidateParts.Count);

        for (var index = 0; index < length; index++)
        {
            var currentValue = index < currentParts.Count ? currentParts[index] : 0;
            var candidateValue = index < candidateParts.Count ? candidateParts[index] : 0;

            if (candidateValue > currentValue)
            {
                return true;
            }

            if (candidateValue < currentValue)
            {
                return false;
            }
        }

        return false;
    }

    private static List<int> Parse(string version)
    {
        var parts = new List<int>();

        foreach (var segment in version.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var digits = new string(segment.TakeWhile(char.IsDigit).ToArray());
            parts.Add(int.TryParse(digits, out var value) ? value : 0);
        }

        return parts;
    }
}
