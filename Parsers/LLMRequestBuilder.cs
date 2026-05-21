using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class LLMRequestBuilder
{
    public static string BuildRequestJson(TranslationConfiguration configuration, string systemPrompt, string userPrompt)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

        var requestBody = new Dictionary<string, object>
        {
            { "model", configuration.Model },
            { "temperature", 0 },
            { "max_tokens", 8192 },
            { "top_p", 1 },
            { "frequency_penalty", 0 },
            { "presence_penalty", 0 },
            { "messages", messages }
        };

        if (!string.IsNullOrEmpty(configuration.ModelParams))
        {
            try
            {
                var modelParamsData = JsonConvert.DeserializeObject<JObject>(configuration.ModelParams);
                if (modelParamsData != null)
                {
                    foreach (var item in modelParamsData)
                    {
                        if (item.Value != null)
                        {
                            requestBody[item.Key] = item.Value;
                        }
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                Logger.Warning("LLMRequestBuilder", $"模型参数解析错误: {ex.Message}");
            }
        }

        return JsonConvert.SerializeObject(requestBody);
    }
}
