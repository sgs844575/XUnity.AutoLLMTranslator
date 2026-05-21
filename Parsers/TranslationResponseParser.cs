using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class TranslationResponseParser
{
    public static Dictionary<int, string> Parse(string responseContent)
    {
        var translations = new Dictionary<int, string>();

        var matches = Regex.Matches(responseContent, @"\[(\d+)\]=<textarea>([\s\S]*?)</textarea>");
        foreach (Match match in matches)
        {
            int num = int.Parse(match.Groups[1].Value);
            string translatedText = match.Groups[2].Value;
            translations[num] = translatedText;
        }

        return translations;
    }
}
