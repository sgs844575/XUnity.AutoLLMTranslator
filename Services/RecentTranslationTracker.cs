using System;
using System.Collections.Generic;
using System.Linq;

public class RecentTranslationTracker
{
    private readonly List<TranslationEntry> _recentTranslations = new List<TranslationEntry>();
    private readonly object _lock = new object();
    private readonly int _capacity;

    // Scene boundary detection keywords
    private static readonly HashSet<string> SceneBoundaryKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chapter", "stage", "scene", "prologue", "epilogue",
        "\u7b2c", "\u7ae0", "\u8bdd", "\u56de", "\u573a\u666f", "\u7bc7",
        "act", "part", "section", "level"
    };

    public RecentTranslationTracker(int capacity = 10)
    {
        _capacity = Math.Max(capacity, 3);
    }

    public void Add(string original, string translated)
    {
        lock (_lock)
        {
            var entry = new TranslationEntry
            {
                Original = original,
                Translated = translated,
                Timestamp = DateTime.Now,
                IsSceneBoundary = DetectSceneBoundary(original)
            };

            _recentTranslations.Add(entry);

            // Remove oldest entries beyond capacity
            while (_recentTranslations.Count > _capacity)
            {
                _recentTranslations.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Returns recent translations in chronological order (oldest first).
    /// Respects scene boundaries: if a scene boundary is detected, only returns
    /// translations after the most recent boundary.
    /// </summary>
    public IEnumerable<string> GetRecentTranslations()
    {
        lock (_lock)
        {
            var entries = GetEntriesSinceLastBoundary();
            return entries.Select(e => $"{StringUtils.EscapeSpecialCharacters(e.Original)} === {StringUtils.EscapeSpecialCharacters(e.Translated)}");
        }
    }

    /// <summary>
    /// Returns the most recent N translations regardless of scene boundaries.
    /// </summary>
    public IEnumerable<string> GetRawRecentTranslations(int count)
    {
        lock (_lock)
        {
            return _recentTranslations
                .Skip(Math.Max(0, _recentTranslations.Count - Math.Min(count, _recentTranslations.Count)))
                .Select(e => $"{StringUtils.EscapeSpecialCharacters(e.Original)} === {StringUtils.EscapeSpecialCharacters(e.Translated)}");
        }
    }

    /// <summary>
    /// Gets entries since the last scene boundary, or all if no boundary exists.
    /// </summary>
    private List<TranslationEntry> GetEntriesSinceLastBoundary()
    {
        int lastBoundaryIndex = -1;
        for (int i = _recentTranslations.Count - 1; i >= 0; i--)
        {
            if (_recentTranslations[i].IsSceneBoundary)
            {
                lastBoundaryIndex = i;
                break;
            }
        }

        int startIndex = lastBoundaryIndex >= 0 ? lastBoundaryIndex : 0;
        var result = new List<TranslationEntry>();
        for (int i = startIndex; i < _recentTranslations.Count; i++)
        {
            result.Add(_recentTranslations[i]);
        }
        return result;
    }

    public int Count
    {
        get
        {
            lock (_lock) { return _recentTranslations.Count; }
        }
    }

    /// <summary>
    /// Returns all stored entries for persistence.
    /// </summary>
    public List<RecentEntry> GetAllEntries()
    {
        lock (_lock)
        {
            return _recentTranslations.Select(e => new RecentEntry
            {
                Original = e.Original,
                Translated = e.Translated,
                Timestamp = e.Timestamp,
                IsSceneBoundary = e.IsSceneBoundary
            }).ToList();
        }
    }

    /// <summary>
    /// Loads entries from a previous session. Clears existing data.
    /// </summary>
    public void LoadEntries(List<RecentEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        lock (_lock)
        {
            _recentTranslations.Clear();
            int skipCount = Math.Max(0, entries.Count - _capacity);
            foreach (var entry in entries.Skip(skipCount))
            {
                _recentTranslations.Add(new TranslationEntry
                {
                    Original = entry.Original,
                    Translated = entry.Translated,
                    Timestamp = entry.Timestamp,
                    IsSceneBoundary = entry.IsSceneBoundary
                });
            }
        }
    }

    private static bool DetectSceneBoundary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lowerText = text.ToLowerInvariant();
        foreach (var keyword in SceneBoundaryKeywords)
        {
            if (lowerText.Contains(keyword))
                return true;
        }

        // Detect common chapter/scene patterns like "1-1", "Stage 1", "Chapter 1"
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(stage|chapter|scene|act|level)\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return true;

        // Detect Japanese chapter markers like "\u7b2c\u4e00\u7ae0" (\u7b2c\u4e00\u7ae0)
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\u7b2c[\u4e00-\u9fff\d]+[\u7ae0\u8bdd\u56de\u7bc7]"))
            return true;

        return false;
    }

    private class TranslationEntry
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsSceneBoundary { get; set; }
    }
}
