using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

/// <summary>
/// Persists and restores translation context across game sessions.
/// Saves RecentTranslationTracker, TranslationHistoryTracker, and SpeakerDetector
/// data to JSON files in BepInEx/Translation/<lang>/Context/
/// </summary>
public class ContextPersistence
{
    private readonly string _contextDirectory;
    private readonly RecentTranslationTracker _recentTracker;
    private readonly TranslationHistoryTracker _historyTracker;
    private readonly SpeakerDetector _speakerDetector;

    private readonly string _recentFile;
    private readonly string _historyFile;
    private readonly string _speakersFile;

    private volatile int _pendingSaveCount;
    private readonly int _autoSaveThreshold;
    private readonly Timer _autoSaveTimer;
    private readonly object _saveLock = new object();

    public ContextPersistence(
        string language,
        RecentTranslationTracker recentTracker,
        TranslationHistoryTracker historyTracker,
        SpeakerDetector speakerDetector,
        int autoSaveThreshold = 20)
    {
        _recentTracker = recentTracker;
        _historyTracker = historyTracker;
        _speakerDetector = speakerDetector;
        _autoSaveThreshold = Math.Max(autoSaveThreshold, 5);

        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _contextDirectory = Path.Combine(TranslationPathHelper.GetTranslationPath(appDirectory, language), "Context");
        PathUtils.EnsureFolderExists(_contextDirectory);

        _recentFile = Path.Combine(_contextDirectory, "recent.json");
        _historyFile = Path.Combine(_contextDirectory, "history.json");
        _speakersFile = Path.Combine(_contextDirectory, "speakers.json");

        // Auto-save every 30 seconds as a safety net
        _autoSaveTimer = new Timer(OnAutoSaveTimer, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // Ensure save on process exit
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <summary>
    /// Loads all persisted context data into the trackers.
    /// Call once during initialization.
    /// </summary>
    public void Load()
    {
        try
        {
            int loadedRecent = LoadRecent();
            int loadedHistory = LoadHistory();
            int loadedSpeakers = LoadSpeakers();

            Logger.Info("ContextPersistence", $"Loaded {loadedRecent} recent, {loadedHistory} history, {loadedSpeakers} speaker entries.");
        }
        catch (Exception ex)
        {
            Logger.Error("ContextPersistence", $"Failed to load context: {ex.Message}");
        }
    }

    /// <summary>
    /// Signals that new data was added and triggers auto-save if threshold reached.
    /// </summary>
    public void NotifyDataAdded()
    {
        int count = Interlocked.Increment(ref _pendingSaveCount);
        if (count >= _autoSaveThreshold)
        {
            Save();
        }
    }

    /// <summary>
    /// Forces an immediate save of all context data.
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                SaveRecent();
                SaveHistory();
                SaveSpeakers();
                Interlocked.Exchange(ref _pendingSaveCount, 0);
            }
            catch (Exception ex)
            {
                Logger.Error("ContextPersistence", $"Save failed: {ex.Message}");
            }
        }
    }

    private void OnAutoSaveTimer(object? state)
    {
        if (_pendingSaveCount > 0)
        {
            Save();
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        Logger.Info("ContextPersistence", "Process exiting, saving context...");
        Save();
        _autoSaveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _autoSaveTimer?.Dispose();
    }

    #region Recent

    private int LoadRecent()
    {
        if (!File.Exists(_recentFile))
            return 0;

        var json = File.ReadAllText(_recentFile);
        var data = JsonConvert.DeserializeObject<RecentContextDto>(json);
        if (data?.Entries == null || data.Entries.Count == 0)
            return 0;

        _recentTracker.LoadEntries(data.Entries.Select(e => new RecentEntry
        {
            Original = e.Original,
            Translated = e.Translated,
            Timestamp = e.Timestamp,
            IsSceneBoundary = e.IsSceneBoundary
        }).ToList());

        return data.Entries.Count;
    }

    private void SaveRecent()
    {
        var entries = _recentTracker.GetAllEntries();
        var dto = new RecentContextDto
        {
            Entries = entries.Select(e => new RecentEntryDto
            {
                Original = e.Original,
                Translated = e.Translated,
                Timestamp = e.Timestamp,
                IsSceneBoundary = e.IsSceneBoundary
            }).ToList()
        };

        File.WriteAllText(_recentFile, JsonConvert.SerializeObject(dto, Formatting.Indented));
    }

    #endregion

    #region History

    private int LoadHistory()
    {
        if (!File.Exists(_historyFile))
            return 0;

        var json = File.ReadAllText(_historyFile);
        var data = JsonConvert.DeserializeObject<HistoryContextDto>(json);
        if (data?.Entries == null || data.Entries.Count == 0)
            return 0;

        _historyTracker.LoadEntries(data.Entries.Select(e => new HistoryEntry
        {
            Original = e.Original,
            Translated = e.Translated,
            Timestamp = e.Timestamp,
            WordCount = e.WordCount
        }).ToList());

        return data.Entries.Count;
    }

    private void SaveHistory()
    {
        var entries = _historyTracker.GetAllEntries();
        var dto = new HistoryContextDto
        {
            Entries = entries.Select(e => new HistoryEntryDto
            {
                Original = e.Original,
                Translated = e.Translated,
                Timestamp = e.Timestamp,
                WordCount = e.WordCount
            }).ToList()
        };

        File.WriteAllText(_historyFile, JsonConvert.SerializeObject(dto, Formatting.Indented));
    }

    #endregion

    #region Speakers

    private int LoadSpeakers()
    {
        if (!File.Exists(_speakersFile))
            return 0;

        var json = File.ReadAllText(_speakersFile);
        var data = JsonConvert.DeserializeObject<SpeakersContextDto>(json);
        if (data?.Speakers == null || data.Speakers.Count == 0)
            return 0;

        var speakerData = data.Speakers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(s => new SpeakerSample
            {
                Original = s.Original,
                Translated = s.Translated,
                Timestamp = s.Timestamp
            }).ToList()
        );

        _speakerDetector.LoadSpeakerData(speakerData);

        return data.Speakers.Values.Sum(v => v.Count);
    }

    private void SaveSpeakers()
    {
        var data = _speakerDetector.GetAllSpeakerData();
        var dto = new SpeakersContextDto
        {
            Speakers = data.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(s => new SpeakerSampleDto
                {
                    Original = s.Original,
                    Translated = s.Translated,
                    Timestamp = s.Timestamp
                }).ToList()
            )
        };

        File.WriteAllText(_speakersFile, JsonConvert.SerializeObject(dto, Formatting.Indented));
    }

    #endregion

    #region DTOs

    private class RecentContextDto
    {
        public List<RecentEntryDto> Entries { get; set; } = new List<RecentEntryDto>();
    }

    private class RecentEntryDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsSceneBoundary { get; set; }
    }

    private class HistoryContextDto
    {
        public List<HistoryEntryDto> Entries { get; set; } = new List<HistoryEntryDto>();
    }

    private class HistoryEntryDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int WordCount { get; set; }
    }

    private class SpeakersContextDto
    {
        public Dictionary<string, List<SpeakerSampleDto>> Speakers { get; set; } = new Dictionary<string, List<SpeakerSampleDto>>();
    }

    private class SpeakerSampleDto
    {
        public string Original { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    #endregion
}

/// <summary>
/// Public DTO for loading recent entries.
/// </summary>
public class RecentEntry
{
    public string Original { get; set; } = string.Empty;
    public string Translated { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsSceneBoundary { get; set; }
}

/// <summary>
/// Public DTO for loading history entries.
/// </summary>
public class HistoryEntry
{
    public string Original { get; set; } = string.Empty;
    public string Translated { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int WordCount { get; set; }
}

/// <summary>
/// Public DTO for loading speaker samples.
/// </summary>
public class SpeakerSample
{
    public string Original { get; set; } = string.Empty;
    public string Translated { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
