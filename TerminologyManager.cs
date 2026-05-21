using System.Collections.Generic;

public class TerminologyManager
{
    private readonly Dictionary<string, string> _terminology = new Dictionary<string, string>();

    public void AddTerminology(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var normalizedKey = key.Trim();
        _terminology[normalizedKey] = value;
    }

    public string? FindTerminology(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return null;

        if (_terminology.TryGetValue(str.Trim(), out string value))
        {
            return value;
        }

        return null;
    }

    public void Init(string terminology)
    {
        _terminology.Clear();

        if (string.IsNullOrWhiteSpace(terminology))
            return;

        var txts = terminology.Split('|');
        foreach (var txt in txts)
        {
            var ts = txt.Split(new string[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
            if (ts.Length == 2)
            {
                AddTerminology(ts[0], ts[1]);
            }
            else
            {
                Logger.Warning("Terminology", $"格式错误: {txt}");
            }
        }
    }

    public IReadOnlyDictionary<string, string> GetAllTerminology()
    {
        return _terminology;
    }
}
