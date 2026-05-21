using System.Collections.Generic;
using System.IO;
using System.Linq;
using FuzzyString;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

public class TranslationDatabase
{
    private readonly FileManager _fileManager;
    private readonly TerminologyManager _terminologyManager;
    private readonly DictionaryManager _dictionaryManager;
    private readonly PromptManager _promptManager;

    private readonly Dictionary<string, string> _translateDatas = new Dictionary<string, string>();
    private readonly List<KeyValuePair<string, string>> _sortedTranslateDatas = new List<KeyValuePair<string, string>>();
    private readonly object _lock = new object();

    private readonly FuzzyStringComparisonOptions[] _options = new FuzzyStringComparisonOptions[]
    {
        FuzzyStringComparisonOptions.UseOverlapCoefficient,
        FuzzyStringComparisonOptions.UseLongestCommonSubsequence,
        FuzzyStringComparisonOptions.UseLongestCommonSubstring
    };

    public TranslationDatabase(
        FileManager fileManager,
        TerminologyManager terminologyManager,
        DictionaryManager dictionaryManager,
        PromptManager promptManager)
    {
        _fileManager = fileManager;
        _terminologyManager = terminologyManager;
        _dictionaryManager = dictionaryManager;
        _promptManager = promptManager;
    }

    public void Init(IInitializationContext context, string terminology)
    {
        InitDB(context, terminology);
    }

    public string? FindTerminology(string str)
    {
        return _terminologyManager.FindTerminology(str);
    }

    public bool AddData(string key, string value)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 100)
            return false;

        lock (_lock)
        {
            if (_translateDatas.ContainsKey(key))
                return false;

            _translateDatas.Add(key, value);
            _sortedTranslateDatas.Add(new KeyValuePair<string, string>(key, value));
            // 保持按长度降序排列
            _sortedTranslateDatas.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }
        return true;
    }

    private void InitDB(IInitializationContext context, string _terminology)
    {
        lock (_lock)
        {
            _translateDatas.Clear();
            _sortedTranslateDatas.Clear();

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var folderPath = Path.Combine(appDirectory, "BepInEx", "Translation", context.DestinationLanguage, "Text");
            PathUtils.EnsureFolderExists(folderPath);
            List<string> txtFiles = _fileManager.GetAllTxtFiles(folderPath);

            foreach (string txtFile in txtFiles)
            {
                foreach (var pair in KeyValueFileParser.ParseFile(txtFile, 100))
                {
                    if (!_translateDatas.ContainsKey(pair.Key))
                    {
                        _translateDatas.Add(pair.Key, pair.Value);
                        _sortedTranslateDatas.Add(pair);
                    }
                }
            }

            _terminologyManager.Init(_terminology);

            // Add terminology to translation data
            if (!string.IsNullOrEmpty(_terminology))
            {
                var txts = _terminology.Split('|');
                foreach (var txt in txts)
                {
                    var ts = txt.Split(new string[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                    if (ts.Length == 2)
                    {
                        if (!_translateDatas.ContainsKey(ts[0]))
                        {
                            _translateDatas.Add(ts[0], ts[1]);
                            _sortedTranslateDatas.Add(new KeyValuePair<string, string>(ts[0], ts[1]));
                        }
                    }
                }
            }

            // 按键长度降序排列，以便优先匹配更长的键
            _sortedTranslateDatas.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            Logger.Info("Database", $"初始化数据库完成, 字典数量共计: {_translateDatas.Count}");
        }

        _dictionaryManager.LoadDictionary();
    }

    public void SortData()
    {
        lock (_lock)
        {
            _sortedTranslateDatas.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }
    }

    public List<string> Search(List<string> keys, int Length = 2000)
    {
        var rs = new List<string>();
        if (keys == null || keys.Count == 0)
            return rs;

        int l = 0;
        HashSet<string> findkeys = new HashSet<string>();

        List<KeyValuePair<string, string>> snapshot;
        lock (_lock)
        {
            snapshot = new List<KeyValuePair<string, string>>(_sortedTranslateDatas);
        }

        foreach (var kvp in snapshot)
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                if (key.ApproximatelyEquals(kvp.Key, FuzzyStringComparisonTolerance.Strong, _options))
                {
                    if (!findkeys.Contains(kvp.Key))
                    {
                        l += kvp.Key.Length + kvp.Value.Length;
                        if (l > Length)
                            break;
                        findkeys.Add(kvp.Key);
                        rs.Add($"{kvp.Key} === {StringUtils.EscapeSpecialCharacters(kvp.Value)}");
                    }
                }
            }
        }

        return rs;
    }

    public string GetPrompt()
    {
        return _promptManager.GetPrompt();
    }

    public List<string> GetTranslateData(List<string> messages)
    {
        return _dictionaryManager.GetTranslateData(messages);
    }
}
