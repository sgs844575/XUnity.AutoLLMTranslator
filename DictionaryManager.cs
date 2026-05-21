using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class DictionaryManager
{
    private readonly FileManager _fileManager;
    private readonly Dictionary<string, string> _dictMap = new Dictionary<string, string>();
    private readonly object _lock = new object();

    public string Language { get; set; } = "zh_cn";

    public DictionaryManager(FileManager fileManager)
    {
        _fileManager = fileManager;
    }

    public void LoadDictionary()
    {
        lock (_lock)
        {
            _dictMap.Clear();
            int index = 0;
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var folderPath = Path.Combine(TranslationPathHelper.GetTranslationPath(appDirectory, Language), "Dictionary");
            PathUtils.EnsureFolderExists(folderPath);
            List<string> txtFiles = _fileManager.GetAllTxtFiles(folderPath);

            foreach (string txtFile in txtFiles)
            {
                foreach (var pair in KeyValueFileParser.ParseFile(txtFile, 100))
                {
                    if (!_dictMap.ContainsKey(pair.Key))
                    {
                        _dictMap.Add(pair.Key, pair.Value);
                        index++;
                    }
                }
            }

            List<string> jsonFiles = _fileManager.GetAllJsonFiles(folderPath);
            foreach (string jsonFile in jsonFiles)
            {
                try
                {
                    string content = File.ReadAllText(jsonFile);
                    JArray array = JArray.Parse(content);
                    foreach (JToken item in array)
                    {
                        string? src = item["src"]?.ToString();
                        string? dst = item["dst"]?.ToString();
                        if (!string.IsNullOrEmpty(src) && !_dictMap.ContainsKey(src))
                        {
                            _dictMap.Add(src, dst ?? string.Empty);
                            index++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("Dictionary", $"加载 JSON 字典失败: {jsonFile}, 错误: {ex.Message}");
                }
            }

            Logger.Info("Dictionary", $"初始化字典库完成, 字典数量共计: {index}");
        }
    }

    public IReadOnlyDictionary<string, string> GetDictionary()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(_dictMap);
        }
    }

    public List<string> GetTranslateData(List<string> messages)
    {
        if (messages == null || messages.Count == 0)
            return new List<string>();

        var result = new List<string>();
        var matchedKeys = new HashSet<string>();

        // 预处理消息：移除 <br> 并缓存
        var processedMessages = new List<string>(messages.Count);
        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message))
                continue;
            processedMessages.Add(message.Replace("<br>", ""));
        }

        lock (_lock)
        {
            foreach (var pair in _dictMap)
            {
                foreach (var processedMessage in processedMessages)
                {
                    if (processedMessage.Contains(pair.Key) && matchedKeys.Add(pair.Key))
                    {
                        result.Add($"{pair.Key} === {StringUtils.EscapeSpecialCharacters(pair.Value)}");
                        break;
                    }
                }
            }
        }

        return result;
    }
}
