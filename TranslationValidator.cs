using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TranslationValidator
{
    private readonly TranslationConfiguration _configuration;

    // Chinese CJK unified ideographs
    private static readonly Regex ChineseCharRegex = new Regex(
        @"[\u4E00-\u9FFF]", RegexOptions.Compiled);

    // Tags: <...>, [...], %s, %d, %f, etc.
    private static readonly Regex TagRegex = new Regex(
        @"<[^>]+>|\[[^\]]+\]|%[sdifcouxXeEfFgGpnt]", RegexOptions.Compiled);

    public TranslationValidator(TranslationConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidationResult Validate(string originalText, string translatedText)
    {
        var result = new ValidationResult { IsValid = true };

        if (_configuration.CheckOriginalReturn)
        {
            var check = CheckOriginalReturn(originalText, translatedText);
            if (!check.IsValid)
            {
                result.IsValid = false;
                result.FailedChecks.Add(check);
            }
        }

        if (_configuration.CheckTranslationResidue)
        {
            var check = CheckTranslationResidue(originalText, translatedText);
            if (!check.IsValid)
            {
                result.IsValid = false;
                result.FailedChecks.Add(check);
            }
        }

        if (_configuration.CheckTagCount)
        {
            var check = CheckTagCount(originalText, translatedText);
            if (!check.IsValid)
            {
                result.IsValid = false;
                result.FailedChecks.Add(check);
            }
        }

        if (_configuration.CheckTargetLanguage)
        {
            var check = CheckTargetLanguage(originalText, translatedText);
            if (!check.IsValid)
            {
                result.IsValid = false;
                result.FailedChecks.Add(check);
            }
        }

        return result;
    }

    private static CheckResult CheckOriginalReturn(string original, string translated)
    {
        // Ignore whitespace differences for comparison
        var origTrimmed = original?.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "") ?? "";
        var transTrimmed = translated?.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "") ?? "";

        if (origTrimmed.Length > 0 && origTrimmed.Equals(transTrimmed, StringComparison.Ordinal))
        {
            // 如果原文不含任何日文假名（纯汉字词汇如"狐火"、中文词汇），允许译文相同
            if (original == null || !ContainsJapaneseKana(original))
            {
                return new CheckResult { Name = "原文返回检查", IsValid = true };
            }

            return new CheckResult
            {
                Name = "原文返回检查",
                IsValid = false,
                Message = "译文与原文完全相同"
            };
        }

        return new CheckResult { Name = "原文返回检查", IsValid = true };
    }

    private static bool ContainsJapaneseKana(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (char c in text)
        {
            // Hiragana: U+3040-U+309F
            if (c >= '\u3040' && c <= '\u309F')
                return true;
            // Katakana letters: U+30A1-U+30FA, U+30FC-U+30FF (excludes ・ U+30FB)
            if ((c >= '\u30A1' && c <= '\u30FA') || (c >= '\u30FC' && c <= '\u30FF'))
                return true;
        }
        return false;
    }

    private CheckResult CheckTranslationResidue(string original, string translated)
    {
        var sourceLang = _configuration.SourceLanguage?.ToLowerInvariant() ?? "";

        bool hasResidue = false;
        string residueType = "";

        if (sourceLang.Contains("ja") || sourceLang.Contains("jp"))
        {
            if (ContainsJapaneseKana(translated))
            {
                hasResidue = true;
                residueType = "日文假名";
            }
        }

        if (hasResidue)
        {
            return new CheckResult
            {
                Name = "翻译残留检查",
                IsValid = false,
                Message = $"译文残留源语言字符: {residueType}"
            };
        }

        return new CheckResult { Name = "翻译残留检查", IsValid = true };
    }

    private static CheckResult CheckTagCount(string original, string translated)
    {
        var origTags = TagRegex.Matches(original ?? "").Count;
        var transTags = TagRegex.Matches(translated ?? "").Count;

        if (origTags != transTags)
        {
            return new CheckResult
            {
                Name = "标签总数检查",
                IsValid = false,
                Message = $"标签数量不一致: 原文 {origTags} 个, 译文 {transTags} 个"
            };
        }

        return new CheckResult { Name = "标签总数检查", IsValid = true };
    }

    private CheckResult CheckTargetLanguage(string original, string translated)
    {
        var destLang = _configuration.DestinationLanguage?.ToLowerInvariant() ?? "";

        Logger.Debug("Validator", $"目标语言检查: destLang={destLang}, translated='{translated}'");

        // Skip check for very short texts, pure symbols, numbers, or empty strings
        if (string.IsNullOrWhiteSpace(translated) || translated.Length <= 1)
        {
            return new CheckResult { Name = "译文目标语言检查", IsValid = true };
        }

        // Check if the text contains only symbols/numbers/punctuation (no letters)
        bool hasLetters = false;
        foreach (char c in translated)
        {
            if (char.IsLetter(c))
            {
                hasLetters = true;
                break;
            }
        }
        if (!hasLetters)
        {
            return new CheckResult { Name = "译文目标语言检查", IsValid = true };
        }

        bool hasTargetLangChars = false;
        bool isChineseTarget = destLang.Contains("zh") || destLang.Contains("cn") || destLang.Contains("chinese");

        if (isChineseTarget)
        {
            hasTargetLangChars = ChineseCharRegex.IsMatch(translated);

            // Extra check: if translation has no Chinese AND is mostly Latin letters, it's likely untranslated English
            if (hasTargetLangChars)
            {
                // Has Chinese chars, pass
            }
            else
            {
                // No Chinese chars - check if it's mostly English
                int latinCount = 0;
                int totalLetters = 0;
                foreach (char c in translated)
                {
                    if (char.IsLetter(c))
                    {
                        totalLetters++;
                        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                            latinCount++;
                    }
                }
                // If > 80% of letters are Latin, consider it untranslated English
                if (totalLetters > 0 && (double)latinCount / totalLetters > 0.8)
                {
                    return new CheckResult
                    {
                        Name = "译文目标语言检查",
                        IsValid = false,
                        Message = $"译文为纯英文文本，未翻译为目标语言 ({_configuration.DestinationLanguage})"
                    };
                }
            }
        }
        else if (destLang.Contains("en") || destLang.Contains("english"))
        {
            // For English target, check for Latin letters
            foreach (char c in translated)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    hasTargetLangChars = true;
                    break;
                }
            }
        }
        else if (destLang.Contains("ja") || destLang.Contains("jp"))
        {
            hasTargetLangChars = ContainsJapaneseKana(translated);
        }
        else
        {
            // For unknown target languages, assume valid if it has any letters
            hasTargetLangChars = true;
        }

        if (!hasTargetLangChars)
        {
            return new CheckResult
            {
                Name = "译文目标语言检查",
                IsValid = false,
                Message = $"译文未包含目标语言字符 ({_configuration.DestinationLanguage})"
            };
        }

        return new CheckResult { Name = "译文目标语言检查", IsValid = true };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<CheckResult> FailedChecks { get; set; } = new List<CheckResult>();

    public string GetErrorMessage()
    {
        if (IsValid || FailedChecks.Count == 0)
            return "";

        var messages = new List<string>();
        foreach (var check in FailedChecks)
        {
            messages.Add($"[{check.Name}] {check.Message}");
        }
        return string.Join("; ", messages);
    }
}

public class CheckResult
{
    public string Name { get; set; } = "";
    public bool IsValid { get; set; }
    public string Message { get; set; } = "";
}
