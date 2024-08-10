using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Cache;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Models;
using SRT2Speech.Socket.Methods;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class FptModule : CarterModule
    {
        private readonly IDowloadService _dowloadService;
        private readonly IMemCacheService _memCacheService;
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly IMicrosoftCacheService _microsoftCacheService;
        public FptModule(IDowloadService dowloadService, IMemCacheService memCacheService, IHubContext<MessageHub> hubContext, IMicrosoftCacheService microsoftCacheService) : base("/api/fpt")
        {
            WithTags("Webhook");
            IncludeInOpenApi();
            _dowloadService = dowloadService;
            _memCacheService = memCacheService;
            _hubContext = hubContext;
            _microsoftCacheService = microsoftCacheService;
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/listen", async ([FromBody] object body) =>
            {
                return await ListenDowload(body);
            })
              .WithName("FptListenResponse")
              .WithOpenApi();

            app.MapPost("/send", async ([FromQuery] string msg) =>
            {
                await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, msg);
            })
             .WithName("SendSignalR")
             .WithOpenApi();
        }

        private async Task<IResult> ListenDowload([FromBody] object body)
        {
            await Task.Delay(1000);
            var obj = JObject.Parse(body.ToString()!);

            bool success = obj.GetSafeValue<bool>("success");
            if (success)
            {
                string message = obj.GetSafeValue<string>("message");
                if (!string.IsNullOrEmpty(message))
                {
                    string fileName = Path.GetFileName(message);
                    string requestId = obj.GetSafeValue<string>("requestid");
                    string originalFileName = JObject.Parse(_microsoftCacheService.Get<object>(requestId).ToString()!)
                                                          .GetSafeValue<string>("FileName");
                    if (string.IsNullOrEmpty(originalFileName))
                    {
                        originalFileName = fileName;
                    }
                    string filePath = Path.Combine("Files/FPT", originalFileName);
                    success = await _dowloadService.DownloadMp3Async(message, filePath);
                    if (success)
                    {
                        await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, $"[SUCCESS] dowload Files/FPT/{originalFileName}");
                    }
                    else
                    {
                        await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, $"[FAILED] dowload Files/FPT/{originalFileName}");
                    }
                }
            }
            return TypedResults.Ok(success);
        }
    }
}
