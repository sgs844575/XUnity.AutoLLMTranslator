using System;

/// <summary>
/// 提供基于字符类型的简单 token 估算。
/// 针对 CJK（中文、日文、韩文）和拉丁字符使用不同的估算系数，
/// 用于控制翻译批次的最大 token 数量。
/// </summary>
public static class TokenEstimator
{
    /// <summary>
    /// 估算文本的近似 token 数量。
    /// 返回值乘以 10 表示内部累加值，最终除以 10。
    /// CJK 字符约 1.5 tokens/字，拉丁字符约 0.3 tokens/字。
    /// </summary>
    public static int Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int tokenCount = 0;
        foreach (char c in text)
        {
            if (IsCjkCharacter(c))
            {
                tokenCount += 15; // ~1.5 tokens per CJK char
            }
            else if (char.IsWhiteSpace(c))
            {
                tokenCount += 1;
            }
            else
            {
                tokenCount += 3; // ~0.3 tokens per ASCII char
            }
        }

        return Math.Max(1, tokenCount / 10);
    }

    private static bool IsCjkCharacter(char c)
    {
        // CJK Unified Ideographs
        if (c >= '\u4e00' && c <= '\u9fff')
            return true;
        // CJK Unified Ideographs Extension A
        if (c >= '\u3400' && c <= '\u4dbf')
            return true;
        // Hiragana
        if (c >= '\u3040' && c <= '\u309f')
            return true;
        // Katakana
        if (c >= '\u30a0' && c <= '\u30ff')
            return true;
        // CJK Symbols and Punctuation
        if (c >= '\u3000' && c <= '\u303f')
            return true;
        // Halfwidth and Fullwidth Forms
        if (c >= '\uff00' && c <= '\uffef')
            return true;
        // Hangul Syllables
        if (c >= '\uac00' && c <= '\ud7af')
            return true;
        // Hangul Jamo
        if (c >= '\u1100' && c <= '\u11ff')
            return true;
        return false;
    }
}
