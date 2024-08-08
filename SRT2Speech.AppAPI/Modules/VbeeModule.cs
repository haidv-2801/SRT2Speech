using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Socket.Methods;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class VbeeModule : CarterModule
    {
        private readonly IDowloadService _dowloadService;
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly IMemCacheService _memCacheService;

        public VbeeModule(IDowloadService dowloadService, IHubContext<MessageHub> hubContext, IMemCacheService memCacheService) : base("/api/vbee")
        {
            WithTags("Webhook");
            IncludeInOpenApi();
            _dowloadService = dowloadService;
            _hubContext = hubContext;
            _memCacheService = memCacheService;
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/listen", async ([FromBody] object body) =>
            {
                return await ListenDowload(body);
            })
              .WithName("VbeeListenResponse")
              .WithOpenApi();
        }


        private async Task<IResult> ListenDowload([FromBody] object body)
        {
            var obj = JObject.Parse(body.ToString()!);

            string success = obj.GetSafeValue<string>("status");
            if (success == "SUCCESS")
            {
                string message = obj.GetSafeValue<string>("request_id");
                if (!string.IsNullOrEmpty(message))
                {
                    string fileName = Path.GetFileName(message);
                    string originalFileName = _memCacheService.Get<string>(message);
                    if (string.IsNullOrEmpty(originalFileName))
                    {
                        originalFileName = fileName;
                    }
                    string filePath = Path.Combine("Files/Vbee", originalFileName + ".mp3");
                  
                    var dowloadSuccess = await _dowloadService.DownloadMp3Async(obj.GetSafeValue<string>("audio_link"), filePath);
                    if (dowloadSuccess)
                    {
                        await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG_VBEE, $"[SUCCESS] dowload Files/Vbee/{originalFileName}");
                    }
                    else
                    {
                        await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG_VBEE, $"[FAILED] dowload Files/Vbee/{originalFileName}");
                    }
                    return TypedResults.Ok(success);
                }
            }
            return TypedResults.Ok(false);
        }
    }
}
