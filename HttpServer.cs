using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class HttpServer
{
    private readonly HttpListener _listener = new HttpListener();
    private readonly Func<string[], Task<string[]>> _translationHandler;

    public HttpServer(Func<string[], Task<string[]>> translationHandler)
    {
        _translationHandler = translationHandler;
    }

    public void Start()
    {
        _listener.Prefixes.Add("http://127.0.0.1:20000/");
        _listener.Start();
        Logger.Info("HttpServer", "Listening for requests on http://127.0.0.1:20000/");

        Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("HttpServer", ex, "HTTP listener error");
            }
        });
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod == "POST")
            {
                await HandlePostAsync(context);
            }
            else if (context.Request.HttpMethod == "GET")
            {
                await HandleGetAsync(context);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("HttpServer", ex, "处理请求时发生错误");
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task HandlePostAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        using (Stream body = request.InputStream)
        using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
        {
            string requestBody = await reader.ReadToEndAsync();
            var requestData = JObject.Parse(requestBody);
            var texts = requestData["texts"]?.ToObject<string[]>() ?? new string[0];

            var result = await _translationHandler(texts);

            var rs = new { texts = result };
            await WriteJsonResponseAsync(response, rs);
        }
    }

    private async Task HandleGetAsync(HttpListenerContext context)
    {
        string responseString = "AutoLLMTranslator is running.";
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
    {
        string responseString = JsonConvert.SerializeObject(data);
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
}
