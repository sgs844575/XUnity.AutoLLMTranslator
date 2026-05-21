using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class LLMClient
{
    private readonly TranslationConfiguration _configuration;

    public LLMClient(TranslationConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> CallLLMAsync(string systemPrompt, string userPrompt)
    {
        var requestJson = LLMRequestBuilder.BuildRequestJson(_configuration, systemPrompt, userPrompt);
        Logger.Debug("LLMClient", $"请求URL: {_configuration.URL}, Model: {_configuration.Model}");
        Logger.Debug("LLMClient", $"UserPrompt ({userPrompt.Length} chars): {userPrompt.Substring(0, Math.Min(userPrompt.Length, 500))}");

        using (var client = new WebClient())
        {
            DateTime start = DateTime.Now;
            client.Headers[HttpRequestHeader.Authorization] = $"Bearer {_configuration.APIKey}";
            client.Headers[HttpRequestHeader.ContentType] = "application/json";

            var responseBytes = await client.UploadDataTaskAsync(_configuration.URL, "POST", Encoding.UTF8.GetBytes(requestJson));
            var responseString = Encoding.UTF8.GetString(responseBytes);

            var jsonResponse = JObject.Parse(responseString);
            var fullContent = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(fullContent))
            {
                Logger.Error("LLMClient", "API返回空内容");
                throw new Exception("API返回空内容");
            }

            TimeSpan duration = DateTime.Now - start;
            Logger.Info("LLMClient", $"请求完成, 耗时: {duration.TotalSeconds:F2}s, 响应长度: {fullContent.Length} chars");
            Logger.Info("LLMClient", $"LLM原始响应:\n{fullContent}");

            return fullContent;
        }
    }
}
