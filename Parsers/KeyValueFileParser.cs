using System;
using System.Collections.Generic;
using System.IO;

public static class KeyValueFileParser
{
    public static IEnumerable<KeyValuePair<string, string>> ParseFile(string filePath, int maxKeyLength = 100)
    {
        if (!File.Exists(filePath))
            yield break;

        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var pair = ParseLine(line, maxKeyLength);
            if (pair.HasValue)
                yield return pair.Value;
        }
    }

    public static KeyValuePair<string, string>? ParseLine(string line, int maxKeyLength = 100)
    {
        int equalIndex = -1;
        bool foundUnescaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '=' && (i == 0 || line[i - 1] != '\\'))
            {
                equalIndex = i;
                foundUnescaped = true;
                break;
            }
        }

        if (!foundUnescaped)
            return null;

        string key = line.Substring(0, equalIndex);
        string value = line.Substring(equalIndex + 1);

        if (key.Length > maxKeyLength)
            return null;

        return new KeyValuePair<string, string>(key, value);
    }
}
