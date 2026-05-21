using System.IO;
using System.Threading.Tasks;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

// ReSharper disable All

public class TranslatorTask
{
    private TranslationScheduler? _translationScheduler;
    private HttpServer? _httpServer;
    private TaskManager? _taskManager;
    public TranslationConfiguration? Config { get; private set; }

    public void Init(IInitializationContext context)
    {
        var configuration = new TranslationConfiguration(context);
        Config = configuration;

        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string language = context.DestinationLanguage ?? "zh_cn";

        string[] requiredFolders = new[]
        {
            Path.Combine(appDirectory, "BepInEx", "Translation", language, "Dictionary"),
            Path.Combine(appDirectory, "BepInEx", "Translation", language, "Prompt"),
            Path.Combine(appDirectory, "BepInEx", "Translation", language, "Context"),
            Path.Combine(appDirectory, "BepInEx", "Translation", language, "Text")
        };

        foreach (var folder in requiredFolders)
        {
            PathUtils.EnsureFolderExists(folder);
        }

        var fileManager = new FileManager();
        var terminologyManager = new TerminologyManager();
        var dictionaryManager = new DictionaryManager(fileManager)
        {
            Language = context.DestinationLanguage ?? "zh_cn"
        };
        var promptManager = new PromptManager(fileManager)
        {
            Language = context.DestinationLanguage ?? "zh_cn"
        };

        var translationDatabase = new TranslationDatabase(
            fileManager,
            terminologyManager,
            dictionaryManager,
            promptManager);

        translationDatabase.Init(context, configuration.Terminology);

        var llmClient = new LLMClient(configuration);

        var textPostProcessor = new TextPostProcessor(configuration.HalfWidth);
        var recentTranslationTracker = new RecentTranslationTracker(configuration.RecentContextSize);
        var historyTracker = new TranslationHistoryTracker(200);
        var speakerDetector = new SpeakerDetector(5);

        var persistence = new ContextPersistence(
            configuration.DestinationLanguage ?? "zh_cn",
            recentTranslationTracker,
            historyTracker,
            speakerDetector);
        persistence.Load();

        var contextManager = new ContextManager(
            translationDatabase,
            dictionaryManager,
            terminologyManager,
            recentTranslationTracker,
            historyTracker,
            speakerDetector,
            configuration,
            persistence);

        var translationProcessor = new TranslationProcessor(
            llmClient,
            translationDatabase,
            promptManager,
            textPostProcessor,
            contextManager,
            configuration);

        _taskManager = new TaskManager();

        _httpServer = new HttpServer(async (texts) =>
        {
            var task = await _taskManager.AddTask(texts);
            return task.result ?? new string[0];
        });

        _translationScheduler = new TranslationScheduler(
            _taskManager,
            translationProcessor,
            configuration);

        _httpServer.Start();
        _translationScheduler.Start();
    }
}
