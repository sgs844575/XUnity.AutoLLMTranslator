using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Www;

internal class LLMTranslatorEndpoint : WwwEndpoint
{
    #region Since all batching and concurrency are handled within TranslatorTask, please do not modify these two parameters.

    public override int MaxTranslationsPerRequest => 10;
    public override int MaxConcurrency => 100;

    #endregion

    public override string Id => "AutoLLMTranslate";
    public override string FriendlyName => "AutoLLM Translate";
    
    private readonly TranslatorTask _task = new TranslatorTask();

    public override void Initialize(IInitializationContext context)
    {
        context.SetTranslationDelay(0.1f);
        context.DisableSpamChecks();
        _task.Init(context);
        Logger.Info("Endpoint", $"AutoLLMTranslate initialized. Model={_task.Config?.Model}, URL={_task.Config?.URL}");
    }

    public override void OnCreateRequest(IWwwRequestCreationContext context)
    {
        Logger.Debug("Endpoint", $"翻译请求: hash={context.GetHashCode()}, texts=[{string.Join(", ", context.UntranslatedTexts)}]");
        var requestBody = new
        {
            texts = context.UntranslatedTexts
        };
        context.Complete(new WwwRequestInfo("http://127.0.0.1:20000/", JsonConvert.SerializeObject(requestBody)));
    }

    public override void OnExtractTranslation(IWwwTranslationExtractionContext context)
    {
        var data = context.ResponseData;
        var jsonResponse = JObject.Parse(data);
        var rs = jsonResponse["texts"]?.ToObject<string[]>() ?? null;

        if ((rs?.Length ?? 0) == 0)
        {
            Logger.Warning("Endpoint", "翻译结果为空");
            context.Fail("翻译结果为空");
        }
        else
        {
            Logger.Debug("Endpoint", $"翻译完成: [{string.Join(", ", rs)}]");
            context.Complete(rs);
        }
    }
}