using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Central manager for all translation context sources.
/// Handles deduplication, prioritization, and length limiting.
///
/// Context priority (highest to lowest):
/// 1. Terminology (must-use terms)
/// 2. Dictionary matches (game-specific terms)
/// 3. Speaker styles (character voice consistency)
/// 4. Recent scene translations (current conversation flow)
/// 5. Timeline history (broader chronological context)
/// 6. Fuzzy historical matches (style reference)
/// </summary>
public class ContextManager
{
    private readonly TranslationDatabase _translationDatabase;
    private readonly DictionaryManager _dictionaryManager;
    private readonly TerminologyManager _terminologyManager;
    private readonly RecentTranslationTracker _recentTracker;
    private readonly TranslationHistoryTracker _historyTracker;
    private readonly SpeakerDetector _speakerDetector;
    private readonly TranslationConfiguration _configuration;
    private readonly ContextPersistence _persistence;

    public ContextManager(
        TranslationDatabase translationDatabase,
        DictionaryManager dictionaryManager,
        TerminologyManager terminologyManager,
        RecentTranslationTracker recentTracker,
        TranslationHistoryTracker historyTracker,
        SpeakerDetector speakerDetector,
        TranslationConfiguration configuration,
        ContextPersistence persistence)
    {
        _translationDatabase = translationDatabase;
        _dictionaryManager = dictionaryManager;
        _terminologyManager = terminologyManager;
        _recentTracker = recentTracker;
        _historyTracker = historyTracker;
        _speakerDetector = speakerDetector;
        _configuration = configuration;
        _persistence = persistence;
    }

    /// <summary>
    /// Builds comprehensive context for a batch of translation tasks.
    /// Returns a ContextData object containing separate sections for the prompt builder.
    /// </summary>
    public ContextData BuildContext(List<TaskData> tasks)
    {
        var texts = tasks.SelectMany(t => t.texts).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (texts.Count == 0)
            return new ContextData();

        var contextData = new ContextData();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int currentLength = 0;
        int maxLength = _configuration.MaxContextLength;

        // Priority 1: Terminology (exact matches, highest priority)
        var terminologyEntries = CollectTerminology(texts, usedKeys);
        contextData.Terminology = TrimToLength(terminologyEntries, ref currentLength, maxLength, 500);

        // Priority 2: Dictionary matches
        var dictionaryEntries = _dictionaryManager.GetTranslateData(texts);
        var filteredDictionary = FilterAndTrack(dictionaryEntries, usedKeys);
        contextData.Dictionary = TrimToLength(filteredDictionary, ref currentLength, maxLength, 800);

        // Priority 3: Speaker context (if enabled)
        if (_configuration.EnableSpeakerContext)
        {
            var speakerContext = _speakerDetector.GetSpeakerContext(texts).ToList();
            contextData.SpeakerContext = TrimToLength(speakerContext, ref currentLength, maxLength, 600);
            Logger.Debug("ContextManager", $"SpeakerContext: {contextData.SpeakerContext.Count} 条 (原始 {speakerContext.Count} 条, 预算 {maxLength - currentLength} 字符)");
        }
        else
        {
            Logger.Debug("ContextManager", "SpeakerContext: 已禁用");
        }

        // Priority 4: Recent scene translations
        var recentEntries = _recentTracker.GetRecentTranslations().ToList();
        var filteredRecent = FilterAndTrack(recentEntries, usedKeys);
        contextData.RecentTranslations = TrimToLength(filteredRecent, ref currentLength, maxLength, 1000);

        // Priority 5: Timeline history (broader context)
        if (currentLength < maxLength)
        {
            var timelineEntries = _historyTracker.GetRecentTimeline(_configuration.HistoryContextSize).ToList();
            var filteredTimeline = FilterAndTrack(timelineEntries, usedKeys);
            contextData.Timeline = TrimToLength(filteredTimeline, ref currentLength, maxLength, 800);
        }

        // Priority 6: Related history via fuzzy search and keyword search
        if (currentLength < maxLength)
        {
            var relatedFromHistory = _historyTracker.FindRelatedContext(string.Join(" ", texts), 10).ToList();
            var filteredRelated = FilterAndTrack(relatedFromHistory, usedKeys);
            contextData.RelatedHistory = TrimToLength(filteredRelated, ref currentLength, maxLength, 600);
        }

        if (currentLength < maxLength)
        {
            var fuzzyHistory = _translationDatabase.Search(texts, maxLength - currentLength);
            var filteredFuzzy = FilterAndTrack(fuzzyHistory, usedKeys);
            contextData.FuzzyHistory = TrimToLength(filteredFuzzy, ref currentLength, maxLength, maxLength - currentLength);
        }

        Logger.Debug("ContextManager", $"上下文汇总: Terminology={contextData.Terminology.Count}, Dictionary={contextData.Dictionary.Count}, Speaker={contextData.SpeakerContext.Count}, Recent={contextData.RecentTranslations.Count}, Timeline={contextData.Timeline.Count}, Related={contextData.RelatedHistory.Count}, Fuzzy={contextData.FuzzyHistory.Count}, 总长度={currentLength}/{maxLength}");

        return contextData;
    }

    /// <summary>
    /// Records a completed translation into all context trackers.
    /// Call this after a translation is successfully processed.
    /// </summary>
    public void RecordTranslation(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(original))
            return;

        _recentTracker.Add(original, translated);
        _historyTracker.Add(original, translated);

        if (_configuration.EnableSpeakerContext)
        {
            _speakerDetector.AnalyzeText(original, translated);
        }

        _persistence?.NotifyDataAdded();
    }

    /// <summary>
    /// Collects terminology entries for the given texts.
    /// </summary>
    private List<string> CollectTerminology(List<string> texts, HashSet<string> usedKeys)
    {
        var result = new List<string>();

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var terminology = _terminologyManager.FindTerminology(text);
            if (!string.IsNullOrWhiteSpace(terminology))
            {
                var entry = $"{StringUtils.EscapeSpecialCharacters(text.Trim())} === {StringUtils.EscapeSpecialCharacters(terminology)}";
                if (!usedKeys.Contains(text.Trim()))
                {
                    usedKeys.Add(text.Trim());
                    result.Add(entry);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Filters out entries whose key has already been used.
    /// </summary>
    private List<string> FilterAndTrack(List<string> entries, HashSet<string> usedKeys)
    {
        var result = new List<string>();

        foreach (var entry in entries)
        {
            var key = ExtractKey(entry);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!usedKeys.Contains(key))
            {
                usedKeys.Add(key);
                result.Add(entry);
            }
        }

        return result;
    }

    /// <summary>
    /// Trims a list to fit within the remaining length budget.
    /// </summary>
    private List<string> TrimToLength(List<string> entries, ref int currentLength, int maxLength, int sectionBudget)
    {
        var result = new List<string>();
        int remaining = Math.Min(sectionBudget, maxLength - currentLength);

        foreach (var entry in entries)
        {
            int entryLength = entry.Length;
            if (currentLength + entryLength <= maxLength && entryLength <= remaining)
            {
                result.Add(entry);
                currentLength += entryLength;
                remaining -= entryLength;
            }
            else if (currentLength >= maxLength)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts the key (original text) from a context entry string.
    /// Expected format: "original === translated"
    /// </summary>
    private static string ExtractKey(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return string.Empty;

        var separatorIndex = entry.IndexOf(" === ");
        if (separatorIndex > 0)
        {
            return StringUtils.UnEscapeSpecialCharacters(entry.Substring(0, separatorIndex)).Trim();
        }

        // Fallback: try to find first unescaped equals
        for (int i = 0; i < entry.Length; i++)
        {
            if (entry[i] == '=' && (i == 0 || entry[i - 1] != '\\'))
            {
                return StringUtils.UnEscapeSpecialCharacters(entry.Substring(0, i)).Trim();
            }
        }

        return entry.Trim();
    }
}

/// <summary>
/// Holds all context sections for prompt building.
/// </summary>
public class ContextData
{
    public List<string> Terminology { get; set; } = new List<string>();
    public List<string> Dictionary { get; set; } = new List<string>();
    public List<string> SpeakerContext { get; set; } = new List<string>();
    public List<string> RecentTranslations { get; set; } = new List<string>();
    public List<string> Timeline { get; set; } = new List<string>();
    public List<string> RelatedHistory { get; set; } = new List<string>();
    public List<string> FuzzyHistory { get; set; } = new List<string>();

    public bool HasAnyContext =>
        Terminology.Count > 0 ||
        Dictionary.Count > 0 ||
        SpeakerContext.Count > 0 ||
        RecentTranslations.Count > 0 ||
        Timeline.Count > 0 ||
        RelatedHistory.Count > 0 ||
        FuzzyHistory.Count > 0;
}
