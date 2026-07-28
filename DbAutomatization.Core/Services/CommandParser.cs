using System.Text;

namespace DbAutomatization.Core.Services;

public static class CommandParser
{
    public static (string FileName, string[] Arguments) Parse(string command)
    {
        var parts = SplitCommandLine(command);
        if (parts.Length == 0)
            return (string.Empty, []);

        return (parts[0], parts[1..]);
    }

    private static string[] SplitCommandLine(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return [.. result];
    }
}
