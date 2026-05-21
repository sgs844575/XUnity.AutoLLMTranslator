using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Detects speakers/characters in game text and tracks their speaking styles.
/// Helps maintain translation consistency for recurring characters.
/// </summary>
public class SpeakerDetector
{
    // Speaker name -> speaking style samples
    private readonly Dictionary<string, SpeakerProfile> _speakers = new Dictionary<string, SpeakerProfile>(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new object();
    private readonly int _maxSamplesPerSpeaker;

    public SpeakerDetector(int maxSamplesPerSpeaker = 5)
    {
        _maxSamplesPerSpeaker = Math.Max(maxSamplesPerSpeaker, 3);
    }

    /// <summary>
    /// Analyzes text for speaker information and extracts speaking style samples.
    /// </summary>
    public void AnalyzeText(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(original))
            return;

        var speakerName = ExtractSpeakerName(original);
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            Logger.Debug("SpeakerDetector", $"未检测到说话人: {original.Substring(0, Math.Min(50, original.Length))}");
            return;
        }

        var dialogue = ExtractDialogue(original, translated);
        if (string.IsNullOrWhiteSpace(dialogue.Original))
        {
            Logger.Debug("SpeakerDetector", $"检测到说话人 '{speakerName}' 但无有效对话");
            return;
        }

        lock (_lock)
        {
            if (!_speakers.TryGetValue(speakerName!, out var profile))
            {
                profile = new SpeakerProfile { Name = speakerName! };
                _speakers[speakerName!] = profile;
                Logger.Info("SpeakerDetector", $"发现新说话人: {speakerName}");
            }

            profile.AddSample(dialogue.Original, dialogue.Translated);
            Logger.Debug("SpeakerDetector", $"记录样本 [{speakerName}]: {dialogue.Original.Substring(0, Math.Min(30, dialogue.Original.Length))} => {dialogue.Translated.Substring(0, Math.Min(30, dialogue.Translated.Length))}");
        }
    }

    /// <summary>
    /// Gets speaker style context for characters detected in the given texts.
    /// </summary>
    public IEnumerable<string> GetSpeakerContext(IEnumerable<string> texts)
    {
        if (texts == null)
            return Enumerable.Empty<string>();

        var speakerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var name = ExtractSpeakerName(text);
            if (!string.IsNullOrWhiteSpace(name))
                speakerNames.Add(name!);
        }

        if (speakerNames.Count == 0)
        {
            Logger.Debug("SpeakerDetector", "当前批次未检测到说话人");
            return Enumerable.Empty<string>();
        }

        Logger.Debug("SpeakerDetector", $"当前批次检测到说话人: {string.Join(", ", speakerNames)}");

        lock (_lock)
        {
            var result = new List<string>();
            foreach (var name in speakerNames)
            {
                if (_speakers.TryGetValue(name, out var profile))
                {
                    var samples = profile.GetRecentSamples(3);
                    if (samples.Count > 0)
                    {
                        result.Add($"[Speaker: {name}]");
                        foreach (var sample in samples)
                        {
                            result.Add($"  {StringUtils.EscapeSpecialCharacters(sample.Original)} => {StringUtils.EscapeSpecialCharacters(sample.Translated)}");
                        }
                        Logger.Debug("SpeakerDetector", $"为说话人 '{name}' 提供 {samples.Count} 条样本");
                    }
                    else
                    {
                        Logger.Debug("SpeakerDetector", $"说话人 '{name}' 存在但无样本");
                    }
                }
                else
                {
                    Logger.Debug("SpeakerDetector", $"说话人 '{name}' 无历史记录（首次出现）");
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Gets all known speakers.
    /// </summary>
    public IEnumerable<string> GetKnownSpeakers()
    {
        lock (_lock)
        {
            return _speakers.Keys.ToList();
        }
    }

    /// <summary>
    /// Extracts speaker name from text patterns like:
    /// - "CharacterName:「dialogue」"
    /// - "CharacterName「dialogue」"
    /// - "CharacterName: dialogue"
    /// - "【CharacterName】dialogue"
    /// - "CharacterName：dialogue"
    /// </summary>
    private static string? ExtractSpeakerName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Pattern: Name:「dialogue」 or Name「dialogue」
        var match = Regex.Match(text, @"^\s*([^\s「『""\[【:：]+)[\s]*[「『""\[【]");
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name))
                return name;
        }

        // Pattern: Name: dialogue or Name：dialogue
        match = Regex.Match(text, @"^\s*([^\s:：""「『\[【\n\r]{1,20})\s*[:：]\s*(?!//)");
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name))
                return name;
        }

        // Pattern: 【Name】dialogue
        match = Regex.Match(text, @"【\s*([^\s【】]{1,20})\s*】");
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name))
                return name;
        }

        // Pattern: [Name] dialogue
        match = Regex.Match(text, @"\[\s*([^\s\[\]]{1,20})\s*\]");
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name) && !name.StartsWith("color", StringComparison.OrdinalIgnoreCase))
                return name;
        }

        // Pattern: （Name）dialogue
        match = Regex.Match(text, @"（\s*([^\s（）]{1,20})\s*）");
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            if (IsValidName(name))
                return name;
        }

        // Pattern: Name\nDialogue (newline separated, no quotes, as fallback)
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length >= 2)
        {
            var firstLine = lines[0].Trim();
            if (firstLine.Length >= 1 && firstLine.Length <= 20 && IsValidName(firstLine))
            {
                var secondLine = lines[1].Trim();
                if (secondLine.Length >= 3 && !Regex.IsMatch(secondLine, @"^[\d\s\p{P}]+$"))
                    return firstLine;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the dialogue portion from text, given the original and translated versions.
    /// </summary>
    private static DialoguePair ExtractDialogue(string original, string translated)
    {
        // Extract content inside quotation marks
        var originalMatch = Regex.Match(original, @"[「『""'](.+?)[」』""']");
        if (originalMatch.Success)
        {
            var origDialogue = originalMatch.Groups[1].Value;
            // Try to find corresponding translated dialogue
            var transMatch = Regex.Match(translated, @"[「『""'](.+?)[」』""']");
            var transDialogue = transMatch.Success ? transMatch.Groups[1].Value : translated;
            return new DialoguePair { Original = origDialogue, Translated = transDialogue };
        }

        // If no quotes, return the full text (minus speaker prefix if detectable)
        var speakerName = ExtractSpeakerName(original);
        if (!string.IsNullOrWhiteSpace(speakerName))
        {
            var idx = original.IndexOf(speakerName, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var dialogueStart = idx + speakerName!.Length;
                // Skip delimiter
                while (dialogueStart < original.Length && ":： ".Contains(original[dialogueStart]))
                    dialogueStart++;
                if (dialogueStart < original.Length)
                {
                    return new DialoguePair { Original = original.Substring(dialogueStart).Trim(), Translated = translated.Trim() };
                }
            }
        }

        return new DialoguePair { Original = original.Trim(), Translated = translated.Trim() };
    }

    private static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Filter RPG Maker control characters and escape sequences
        if (name.StartsWith("\\") || name.StartsWith("\uff3c"))
            return false;

        // Filter out common false positives
        var lower = name.ToLowerInvariant();
        var falsePositives = new HashSet<string>
        {
            "ui", "hp", "mp", "sp", "exp", "lv", "level", "item", "skill",
            "quest", "map", "menu", "save", "load", "options", "config",
            "attack", "defense", "magic", "heal", "items", "equip", "status",
            "formation", "ability", "magic", "special", "limit", "break",
            "\u30c6\u30ad\u30b9\u30c8", "\u30c0\u30a4\u30a2\u30ed\u30b0", "\u30e1\u30c3\u30bb\u30fc\u30b8",
            "http", "https", "www", "com", "org", "net", "txt", "png", "jpg"
        };

        if (falsePositives.Contains(lower))
            return false;

        // Names should be reasonably short
        if (name.Length > 20)
            return false;

        // Names shouldn't be just numbers or symbols
        if (Regex.IsMatch(name, @"^[\d\s\p{P}]+$"))
            return false;

        return true;
    }

    /// <summary>
    /// Returns all speaker data for persistence.
    /// </summary>
    public Dictionary<string, List<SpeakerSample>> GetAllSpeakerData()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, List<SpeakerSample>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _speakers)
            {
                result[kvp.Key] = kvp.Value.GetAllSamples();
            }
            return result;
        }
    }

    /// <summary>
    /// Loads speaker data from a previous session.
    /// </summary>
    public void LoadSpeakerData(Dictionary<string, List<SpeakerSample>> speakerData)
    {
        if (speakerData == null || speakerData.Count == 0)
            return;

        lock (_lock)
        {
            _speakers.Clear();
            foreach (var kvp in speakerData)
            {
                var profile = new SpeakerProfile { Name = kvp.Key };
                int skipCount = Math.Max(0, kvp.Value.Count - _maxSamplesPerSpeaker);
                foreach (var sample in kvp.Value.Skip(skipCount))
                {
                    profile.AddSample(sample.Original, sample.Translated);
                }
                _speakers[kvp.Key] = profile;
            }
        }
    }

    private class SpeakerProfile
    {
        public string Name { get; set; } = string.Empty;
        private readonly List<DialogueSample> _samples = new List<DialogueSample>();
        private readonly int _maxSamples;

        public SpeakerProfile(int maxSamples = 5)
        {
            _maxSamples = maxSamples;
        }

        public void AddSample(string original, string translated)
        {
            _samples.Add(new DialogueSample
            {
                Original = original,
                Translated = translated,
                Timestamp = DateTime.Now
            });

            while (_samples.Count > _maxSamples)
            {
                _samples.RemoveAt(0);
            }
        }

        public List<DialogueSample> GetRecentSamples(int count)
        {
            int skipCount = Math.Max(0, _samples.Count - Math.Min(count, _samples.Count));
            return _samples.Skip(skipCount).Take(count).ToList();
        }

        public List<SpeakerSample> GetAllSamples()
        {
            return _samples.Select(s => new SpeakerSample
            {
                Original = s.Original,
                Translated = s.Translated,
                Timestamp = s.Timestamp
            }).ToList();
        }
    }

    private class DialogueSample
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    private class DialoguePair
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
    }
}
