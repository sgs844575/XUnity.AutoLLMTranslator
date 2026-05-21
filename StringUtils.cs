using System.Text;

public static class StringUtils
{
    public static string EscapeSpecialCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Replace("\n", "<b_n>");
        text = text.Replace("\r", "<b_r>");
        text = text.Replace("　", "<b_q>");
        text = text.Replace("<br>", "<b_a>");
        text = text.Replace("\"", "<quote>");
        return text;
    }

    public static string UnEscapeSpecialCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Replace("<b_n>", "\n");
        text = text.Replace("，", "、");
        text = text.Replace("<b_r>", "\r");
        text = text.Replace("<b_q>", "　");
        text = text.Replace("<b_a>", "<br>");
        text = text.Replace("</b_n>", "");
        text = text.Replace("っ", "");
        text = text.Replace("゛", "");
        text = text.Replace("</b_r>", "");
        text = text.Replace("</b_q>", "");
        text = text.Replace("</b_a>", "");
        text = text.Replace("</b>", "");
        text = text.Replace("<quote>", "\"");
        return text;
    }
}
