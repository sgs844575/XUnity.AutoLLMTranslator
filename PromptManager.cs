using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PromptManager
{
    private readonly FileManager _fileManager;

    public string Language { get; set; } = "zh_cn";

    public PromptManager(FileManager fileManager)
    {
        _fileManager = fileManager;
    }

    public string GetPrompt()
    {
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var txtFile = $"{appDirectory}\\BepInEx\\Translation\\{Language}\\Prompt\\Prompt.txt";
        PathUtils.EnsureFileExists(txtFile, Config.prompt_modify);
        Logger.Info("PromptManager", "提示词读取成功");
        return _fileManager.ReadFile(txtFile);
    }

    public string BuildSystemPrompt(
        string basePrompt,
        TranslationConfiguration config,
        ContextData context)
    {
        var result = basePrompt
            .Replace("{{GAMENAME}}", config.GameName)
            .Replace("{{GAMEDESC}}", config.GameDesc)
            .Replace("{{OTHER}}", config.Requirement)
            .Replace("{{TARGET_LAN}}", config.DestinationLanguage ?? "Simplified Chinese")
            .Replace("{{SOURCE_LAN}}", config.SourceLanguage ?? "Japanese");

        // Replace context placeholders with their content, or remove the section if empty
        result = ReplaceContextSection(result, "{{TERMINOLOGY}}", context.Terminology,
            "#Terminology\nThese terms MUST be translated exactly as specified:");
        result = ReplaceContextSection(result, "{{DICTIONARY}}", context.Dictionary,
            "#Dictionary\nKnown game terms:");
        result = ReplaceContextSection(result, "{{SPEAKER_CONTEXT}}", context.SpeakerContext,
            "#Character Speaking Styles\nMaintain consistent voice for each character:");
        result = ReplaceContextSection(result, "{{RECENT}}", context.RecentTranslations,
            "#Recent Translations\nCurrent scene context (most recent first):");
        result = ReplaceContextSection(result, "{{TIMELINE}}", context.Timeline,
            "#Translation Timeline\nBroader story context:");
        result = ReplaceContextSection(result, "{{RELATED_HISTORY}}", context.RelatedHistory,
            "#Related Past Translations\nRelevant historical translations:");
        result = ReplaceContextSection(result, "{{HISTORY}}", context.FuzzyHistory,
            "#Historical Translations\nPast translations for style reference:");

        // For backward compatibility: also replace old-style placeholders
        result = result
            .Replace("{{HISTORY}}", FormatContextList(context.FuzzyHistory))
            .Replace("{{RECENT}}", FormatContextList(context.RecentTranslations))
            .Replace("{{DICTIONARY}}", FormatContextList(context.Dictionary));

        // Fallback: if prompt file lacks the speaker placeholder but we have speaker data, append it
        if (!basePrompt.Contains("{{SPEAKER_CONTEXT}}") && context.SpeakerContext != null && context.SpeakerContext.Count > 0)
        {
            var speakerSection = "\n#Character Speaking Styles\nMaintain consistent voice for each character:\n```\n" + string.Join("\n", context.SpeakerContext) + "\n```\n";
            result += speakerSection;
            Logger.Debug("PromptManager", "旧版提示词无 {{SPEAKER_CONTEXT}} 占位符，已追加 Speaker 上下文到末尾");
        }

        Logger.Debug("PromptManager", $"提示词构建完成: 包含Speaker={result.Contains("[Speaker:")}, 包含Terminology={result.Contains("#Terminology")}, 包含Dictionary={result.Contains("#Dictionary")}");

        return result;
    }

    private static string ReplaceContextSection(string prompt, string placeholder, List<string> entries, string sectionHeader)
    {
        if (entries == null || entries.Count == 0)
        {
            // Remove the placeholder and any associated section header pattern
            var escapedPlaceholder = placeholder.Replace("{", "\\{").Replace("}", "\\}");
            var pattern = $@"(^\s*#.*\n)?\s*{escapedPlaceholder}\s*\n?";
            return System.Text.RegularExpressions.Regex.Replace(prompt, pattern, "", System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        var content = $"{sectionHeader}\n```\n{string.Join("\n", entries)}\n```\n";
        return prompt.Replace(placeholder, content);
    }

    private static string FormatContextList(List<string> entries)
    {
        if (entries == null || entries.Count == 0)
            return string.Empty;
        return string.Join("\n", entries);
    }
}
