using System.Text.RegularExpressions;

public class TextPostProcessor
{
    private readonly bool _halfWidth;

    private static readonly Regex FullWidthRegex = new Regex(
        @"[！＂＃＄％＆＇（）＊＋，－．／０１２３４５６７８９：；＜＝＞？＠［＼］＾＿｀｛｜｝～]",
        RegexOptions.Compiled);

    public TextPostProcessor(bool halfWidth)
    {
        _halfWidth = halfWidth;
    }

    public string Process(string translatedText)
    {
        if (_halfWidth)
        {
            translatedText = FullWidthRegex.Replace(translatedText,
                m => ((char)(m.Value[0] - 0xFEE0)).ToString());
        }

        translatedText = StringUtils.UnEscapeSpecialCharacters(translatedText);
        return translatedText;
    }
}
