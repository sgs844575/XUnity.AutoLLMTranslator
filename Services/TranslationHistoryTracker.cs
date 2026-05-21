using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Maintains a timeline of recent translations for chronological context.
/// Unlike RecentTranslationTracker (scene-focused), this provides a broader
/// historical view of the game's translation progress.
/// </summary>
public class TranslationHistoryTracker
{
    private readonly List<HistoryEntryInternal> _history = new List<HistoryEntryInternal>();
    private readonly object _lock = new object();
    private readonly int _maxCapacity;

    public TranslationHistoryTracker(int maxCapacity = 200)
    {
        _maxCapacity = Math.Max(maxCapacity, 50);
    }

    public void Add(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(original))
            return;

        lock (_lock)
        {
            _history.Add(new HistoryEntryInternal
            {
                Original = original.Trim(),
                Translated = translated.Trim(),
                Timestamp = DateTime.Now,
                WordCount = original.Length
            });

            // Trim old entries
            while (_history.Count > _maxCapacity)
            {
                _history.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Returns the most recent N translations in chronological order.
    /// </summary>
    public IEnumerable<string> GetRecentTimeline(int count)
    {
        lock (_lock)
        {
            var entries = _history
                .Skip(Math.Max(0, _history.Count - Math.Min(count, _history.Count)))
                .Select(e => $"{StringUtils.EscapeSpecialCharacters(e.Original)} === {StringUtils.EscapeSpecialCharacters(e.Translated)}");

            return entries.ToList();
        }
    }

    /// <summary>
    /// Searches history for entries containing the given keyword.
    /// Returns most recent matches first, limited to maxResults.
    /// </summary>
    public IEnumerable<string> SearchByKeyword(string keyword, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Enumerable.Empty<string>();

        lock (_lock)
        {
            var matches = _history
                .Where(e => e.Original.Contains(keyword) || e.Translated.Contains(keyword))
                .ToList();

            var result = matches
                .Skip(Math.Max(0, matches.Count - maxResults))
                .Select(e => $"{StringUtils.EscapeSpecialCharacters(e.Original)} === {StringUtils.EscapeSpecialCharacters(e.Translated)}");

            return result.ToList();
        }
    }

    /// <summary>
    /// Searches history for entries related to any of the given keywords.
    /// Useful for finding context around character names or terms.
    /// </summary>
    public IEnumerable<string> SearchByKeywords(IEnumerable<string> keywords, int maxResultsPerKeyword = 3)
    {
        if (keywords == null)
            return Enumerable.Empty<string>();

        var validKeywords = keywords.Where(k => !string.IsNullOrWhiteSpace(k) && k.Length >= 2).ToList();
        if (validKeywords.Count == 0)
            return Enumerable.Empty<string>();

        lock (_lock)
        {
            var result = new List<string>();
            var addedKeys = new HashSet<string>();

            foreach (var keyword in validKeywords)
            {
                var matches = _history
                    .Where(e => (e.Original.Contains(keyword) || e.Translated.Contains(keyword)) && !addedKeys.Contains(e.Original))
                    .ToList();

                var selectedMatches = matches
                    .Skip(Math.Max(0, matches.Count - maxResultsPerKeyword));

                foreach (var match in selectedMatches)
                {
                    addedKeys.Add(match.Original);
                    result.Add($"{StringUtils.EscapeSpecialCharacters(match.Original)} === {StringUtils.EscapeSpecialCharacters(match.Translated)}");
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Extracts potential keywords from text (names, terms) and searches history for them.
    /// </summary>
    public IEnumerable<string> FindRelatedContext(string text, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Empty<string>();

        var keywords = ExtractKeywords(text);
        if (keywords.Count == 0)
            return Enumerable.Empty<string>();

        return SearchByKeywords(keywords, maxResults / Math.Max(keywords.Count, 1));
    }

    /// <summary>
    /// Extracts likely proper nouns and terms from text.
    /// For Japanese: katakana words, kanji sequences.
    /// For English: capitalized words.
    /// </summary>
    private static List<string> ExtractKeywords(string text)
    {
        var keywords = new List<string>();

        // Extract katakana words (Japanese proper nouns, loanwords)
        var katakanaMatches = Regex.Matches(text, @"[\u30A0-\u30FF]{2,}");
        foreach (Match match in katakanaMatches)
        {
            if (match.Value.Length >= 2)
                keywords.Add(match.Value);
        }

        // Extract kanji sequences (likely names/terms)
        var kanjiMatches = Regex.Matches(text, @"[\u4E00-\u9FFF]{2,4}");
        foreach (Match match in kanjiMatches)
        {
            if (match.Value.Length >= 2 && match.Value.Length <= 6)
                keywords.Add(match.Value);
        }

        // Extract capitalized English words (names)
        var englishMatches = Regex.Matches(text, @"\b[A-Z][a-zA-Z]{1,15}\b");
        foreach (Match match in englishMatches)
        {
            if (match.Value.Length >= 2)
                keywords.Add(match.Value);
        }

        return keywords.Distinct().ToList();
    }

    public int Count
    {
        get
        {
            lock (_lock) { return _history.Count; }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }

    /// <summary>
    /// Returns all stored entries for persistence.
    /// </summary>
    public List<HistoryEntry> GetAllEntries()
    {
        lock (_lock)
        {
            return _history.Select(e => new HistoryEntry
            {
                Original = e.Original,
                Translated = e.Translated,
                Timestamp = e.Timestamp,
                WordCount = e.WordCount
            }).ToList();
        }
    }

    /// <summary>
    /// Loads entries from a previous session. Clears existing data.
    /// </summary>
    public void LoadEntries(List<HistoryEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        lock (_lock)
        {
            _history.Clear();
            int skipCount = Math.Max(0, entries.Count - _maxCapacity);
            foreach (var entry in entries.Skip(skipCount))
            {
                _history.Add(new HistoryEntryInternal
                {
                    Original = entry.Original,
                    Translated = entry.Translated,
                    Timestamp = entry.Timestamp,
                    WordCount = entry.WordCount
                });
            }
        }
    }

    private class HistoryEntryInternal
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int WordCount { get; set; }
    }
}
