using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class TranslationProcessor
{
    private readonly LLMClient _llmClient;
    private readonly TranslationDatabase _translationDatabase;
    private readonly PromptManager _promptManager;
    private readonly TextPostProcessor _textPostProcessor;
    private readonly ContextManager _contextManager;
    private readonly TranslationConfiguration _configuration;
    private readonly TranslationValidator _validator;

    public TranslationProcessor(
        LLMClient llmClient,
        TranslationDatabase translationDatabase,
        PromptManager promptManager,
        TextPostProcessor textPostProcessor,
        ContextManager contextManager,
        TranslationConfiguration configuration)
    {
        _llmClient = llmClient;
        _translationDatabase = translationDatabase;
        _promptManager = promptManager;
        _textPostProcessor = textPostProcessor;
        _contextManager = contextManager;
        _configuration = configuration;
        _validator = new TranslationValidator(configuration);
    }

    public async Task ProcessTaskBatch(List<TaskData> tasks)
    {
        int hashkey = tasks.GetHashCode();

        try
        {
            // Handle SkipLatinOnly: skip tasks where all texts contain only ASCII characters
            if (_configuration.SkipLatinOnly)
            {
                var skippedTasks = new List<TaskData>();
                var remainingTasks = new List<TaskData>();
                foreach (var task in tasks)
                {
                    if (task.texts.Length > 0 && Array.TrueForAll(task.texts, IsLatinOnly))
                    {
                        skippedTasks.Add(task);
                    }
                    else
                    {
                        remainingTasks.Add(task);
                    }
                }

                foreach (var task in skippedTasks)
                {
                    Logger.Debug("Processor", $"{hashkey} 跳过纯ASCII文本: {task.texts[0]}");
                    task.result = task.texts;
                    task.state = TaskState.Completed;
                    _contextManager.RecordTranslation(task.texts[0], task.result[0]);
                }

                tasks = remainingTasks;
                if (tasks.Count == 0)
                    return;
            }

            foreach (var task in tasks)
            {
                Logger.Info("Processor", $"{hashkey} 翻译开始: {task.texts[0]}");
            }

            List<string> texts = new List<string>();
            var offsetTasks = new List<TaskData>();
            var offsetIndices = new List<int>();
            foreach (var task in tasks)
            {
                offsetTasks.Add(task);
                offsetIndices.Add(texts.Count);
                texts.AddRange(task.texts);
            }

            int totalTexts = texts.Count;

            var basePrompt = _translationDatabase.GetPrompt();
            var context = _contextManager.BuildContext(tasks);

            var systemPrompt = _promptManager.BuildSystemPrompt(
                basePrompt,
                _configuration,
                context);

            var userPrompt = BuildUserPrompt(systemPrompt, texts);

            var fullContent = await _llmClient.CallLLMAsync(systemPrompt, userPrompt);

            var translations = TranslationResponseParser.Parse(fullContent);
            Logger.Debug("Processor", $"{hashkey} LLM返回 {translations.Count}/{totalTexts} 条翻译结果");

            // 严格检查返回结果与原文编号是否一一对应
            var completenessError = ValidateCompleteness(translations, totalTexts);
            if (completenessError != null)
            {
                Logger.Error("Processor", $"{hashkey} 返回结果完整性检查失败: {completenessError}");
                throw new Exception($"LLM返回结果不完整: {completenessError}");
            }

            int successCount = 0;
            int retryCount = 0;
            int fallbackCount = 0;

            for (int i = 0; i < offsetTasks.Count; i++)
            {
                var task = offsetTasks[i];
                int startIndex = offsetIndices[i];
                int textCount = task.texts.Length;

                var taskResults = new string[textCount];
                bool allFound = true;

                for (int j = 0; j < textCount; j++)
                {
                    int globalNumber = startIndex + j + 1;
                    if (translations.TryGetValue(globalNumber, out string rawTranslated))
                    {
                        var translatedText = _textPostProcessor.Process(rawTranslated);
                        var terminologyResult = _translationDatabase.FindTerminology(task.texts[j]);
                        taskResults[j] = terminologyResult ?? translatedText;
                    }
                    else
                    {
                        allFound = false;
                        break;
                    }
                }

                if (!allFound)
                {
                    task.retryCount++;
                    if (task.retryCount < _configuration.MaxRetry)
                    {
                        task.state = TaskState.Waiting;
                        task.result = null;
                        retryCount++;
                        Logger.Warning("Processor", $"{hashkey} [{i + 1}] 未匹配到全部结果，进入重试 (retry={task.retryCount}/{_configuration.MaxRetry})");
                    }
                    else
                    {
                        task.result = task.texts;
                        task.state = TaskState.Completed;
                        fallbackCount++;
                        Logger.Warning("Processor", $"{hashkey} [{i + 1}] 超过最大重试次数，返回原文");
                    }
                    continue;
                }

                // Validate all translation results
                bool allValid = true;
                string firstError = "";
                for (int j = 0; j < textCount; j++)
                {
                    var validation = _validator.Validate(task.texts[j], taskResults[j]);
                    if (!validation.IsValid)
                    {
                        allValid = false;
                        firstError = validation.GetErrorMessage();
                        break;
                    }
                }

                if (!allValid)
                {
                    task.retryCount++;
                    if (task.retryCount < _configuration.MaxRetry)
                    {
                        task.state = TaskState.Waiting;
                        task.result = null;
                        retryCount++;
                        Logger.Warning("Processor", $"{hashkey} [{i + 1}] 翻译验证失败，进入重试 (retry={task.retryCount}/{_configuration.MaxRetry}): {firstError}");
                    }
                    else
                    {
                        task.result = taskResults;
                        task.state = TaskState.Completed;
                        fallbackCount++;
                        Logger.Warning("Processor", $"{hashkey} [{i + 1}] 翻译验证失败但超过最大重试次数，返回译文: {firstError}");
                    }
                    continue;
                }

                task.result = taskResults;
                task.state = TaskState.Completed;
                successCount++;

                for (int j = 0; j < textCount; j++)
                {
                    Logger.Debug("Processor", $"{hashkey} [{i + 1}.{j + 1}] 翻译成功: '{task.texts[j]}' -> '{taskResults[j]}'");
                    _contextManager.RecordTranslation(task.texts[j], taskResults[j]);
                    _translationDatabase.AddData(task.texts[j], taskResults[j]);
                }
            }

            if (retryCount > 0 || fallbackCount > 0)
            {
                Logger.Warning("Processor", $"{hashkey} 批次完成: 成功={successCount}, 重试={retryCount}, fallback={fallbackCount}");
            }
            else
            {
                Logger.Info("Processor", $"{hashkey} 批次翻译完成: {successCount}/{offsetTasks.Count} (共 {totalTexts} 条文本)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Processor", ex, $"Batch翻译异常 (hash={hashkey})");

            int retryCount = 0;
            int fallbackCount = 0;
            foreach (var task in tasks)
            {
                task.retryCount++;
                if (task.retryCount < _configuration.MaxRetry)
                {
                    task.state = TaskState.Waiting;
                    task.result = null;
                    retryCount++;
                }
                else
                {
                    // 超过最大重试次数，返回原文作为 fallback
                    task.result = task.texts;
                    task.state = TaskState.Completed;
                    fallbackCount++;
                    Logger.Warning("Processor", $"{hashkey} 异常后超过最大重试次数，返回原文: '{task.texts[0]}'");
                }
            }

            if (retryCount > 0)
                Logger.Info("Processor", $"{hashkey} 异常后重试: {retryCount} 个任务, fallback: {fallbackCount} 个任务");
        }
    }

    private static string BuildUserPrompt(string systemPrompt, List<string> texts)
    {
        var sb = new StringBuilder();
        int index = 1;
        foreach (var data in texts)
        {
            var t = StringUtils.EscapeSpecialCharacters(data);
            sb.AppendLine($"[{index}]={t}");
            index++;
        }

        if (systemPrompt.Contains("/no_think") || systemPrompt.Contains("/nothink"))
        {
            sb.AppendLine("/no_think");
        }

        return sb.ToString();
    }

    private static bool IsLatinOnly(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        foreach (char c in text)
        {
            if (c > 127)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 验证 LLM 返回的翻译结果编号是否与原文一一对应。
    /// 返回 null 表示验证通过，否则返回错误描述。
    /// </summary>
    private static string? ValidateCompleteness(Dictionary<int, string> translations, int expectedCount)
    {
        if (expectedCount <= 0)
            return null;

        if (translations.Count != expectedCount)
        {
            var missing = new List<int>();
            var extra = new List<int>();

            for (int i = 1; i <= expectedCount; i++)
            {
                if (!translations.ContainsKey(i))
                    missing.Add(i);
            }

            foreach (var key in translations.Keys)
            {
                if (key < 1 || key > expectedCount)
                    extra.Add(key);
            }

            var parts = new List<string>();
            parts.Add($"期望 {expectedCount} 条，实际返回 {translations.Count} 条");
            if (missing.Count > 0)
                parts.Add($"缺失编号: [{string.Join(", ", missing)}]");
            if (extra.Count > 0)
                parts.Add($"多余编号: [{string.Join(", ", extra)}]");

            return string.Join("; ", parts);
        }

        for (int i = 1; i <= expectedCount; i++)
        {
            if (!translations.ContainsKey(i))
                return $"缺失编号 [{i}]";
        }

        return null;
    }
}
