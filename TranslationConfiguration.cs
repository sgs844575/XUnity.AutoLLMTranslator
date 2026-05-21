using System;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

public class TranslationConfiguration
{
    public string APIKey { get; }
    public string Model { get; }
    public string Requirement { get; }
    public string URL { get; }
    public string Terminology { get; }
    public string GameName { get; }
    public string GameDesc { get; }
    public int MaxWordCount { get; }
    public int MaxBatchTexts { get; }
    public int ParallelCount { get; }
    public int PollingInterval { get; }
    public bool HalfWidth { get; }
    public int MaxRetry { get; }
    public string ModelParams { get; }
    public bool SkipLatinOnly { get; }
    public LogLevel LogLevel { get; }
    public string DestinationLanguage { get; }
    public string SourceLanguage { get; }

    // Context configuration
    public int RecentContextSize { get; }
    public int HistoryContextSize { get; }
    public int MaxContextLength { get; }
    public bool EnableSpeakerContext { get; }

    // Validation checks
    public bool CheckOriginalReturn { get; }
    public bool CheckTranslationResidue { get; }
    public bool CheckTagCount { get; }
    public bool CheckTargetLanguage { get; }

    public TranslationConfiguration(IInitializationContext context)
    {
        APIKey = context.GetOrCreateSetting("AutoLLM", "APIKey", "");
        Model = context.GetOrCreateSetting("AutoLLM", "Model", "gpt-4o");
        Requirement = context.GetOrCreateSetting("AutoLLM", "Requirement", "");
        URL = context.GetOrCreateSetting("AutoLLM", "URL", "https://api.openai.com/v1/chat/completions");
        Terminology = context.GetOrCreateSetting("AutoLLM", "Terminology", "");
        GameName = context.GetOrCreateSetting("AutoLLM", "GameName", "A Game");
        GameDesc = context.GetOrCreateSetting("AutoLLM", "GameDesc", "");
        MaxWordCount = context.GetOrCreateSetting("AutoLLM", "MaxWordCount", 384);
        MaxBatchTexts = context.GetOrCreateSetting("AutoLLM", "MaxBatchTexts", 10);
        ParallelCount = context.GetOrCreateSetting("AutoLLM", "ParallelCount", 3);
        PollingInterval = context.GetOrCreateSetting("AutoLLM", "Interval", 200);
        HalfWidth = context.GetOrCreateSetting("AutoLLM", "HalfWidth", true);
        MaxRetry = context.GetOrCreateSetting("AutoLLM", "MaxRetry", 10);
        ModelParams = context.GetOrCreateSetting("AutoLLM", "ModelParams", "");
        SkipLatinOnly = context.GetOrCreateSetting("AutoLLM", "SkipLatinOnly", false);

        var logLevelStr = context.GetOrCreateSetting("AutoLLM", "LogLevel", "Info");
        if (!Enum.TryParse<LogLevel>(logLevelStr, true, out var parsedLevel))
            parsedLevel = LogLevel.Info;
        LogLevel = parsedLevel;
        Logger.SetLogLevel(LogLevel);

        // Context configuration
        RecentContextSize = context.GetOrCreateSetting("AutoLLM", "RecentContextSize", 10);
        HistoryContextSize = context.GetOrCreateSetting("AutoLLM", "HistoryContextSize", 20);
        MaxContextLength = context.GetOrCreateSetting("AutoLLM", "MaxContextLength", 3000);
        EnableSpeakerContext = context.GetOrCreateSetting("AutoLLM", "EnableSpeakerContext", true);

        // Validation checks
        CheckOriginalReturn = context.GetOrCreateSetting("AutoLLM", "CheckOriginalReturn", true);
        CheckTranslationResidue = context.GetOrCreateSetting("AutoLLM", "CheckTranslationResidue", true);
        CheckTagCount = context.GetOrCreateSetting("AutoLLM", "CheckTagCount", true);
        CheckTargetLanguage = context.GetOrCreateSetting("AutoLLM", "CheckTargetLanguage", true);

        if (context.GetOrCreateSetting("AutoLLM", "DisableSpamChecks", false))
        {
            context.DisableSpamChecks();
        }

        // Normalize URL
        if (URL.EndsWith("/v1"))
        {
            URL += "/chat/completions";
        }
        if (URL.EndsWith("/v1/"))
        {
            URL += "chat/completions";
        }

        DestinationLanguage = context.DestinationLanguage;
        SourceLanguage = context.SourceLanguage;

        // Validate API key for non-localhost URLs
        if (string.IsNullOrEmpty(APIKey) && !URL.Contains("localhost") && !URL.Contains("127.0.0.1") &&
            !URL.Contains("192.168."))
        {
            throw new Exception("The AutoLLM endpoint requires an API key which has not been provided.");
        }
    }
}
