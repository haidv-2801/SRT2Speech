using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Socket.Methods;
using SRT2Speech.Core.Models;
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
            _microsoftCacheService = microsoftCacheService;
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
            await Task.Delay(1000);
            var obj = JObject.Parse(body.ToString()!);

            string success = obj.GetSafeValue<string>("status");
            if (success == "SUCCESS")
            {
                string requestId = obj.GetSafeValue<string>("request_id");
                if (!string.IsNullOrEmpty(requestId))
                {
                    string fileName = Path.GetFileName(requestId);
                    string originalFileName = JObject.Parse(_microsoftCacheService.Get<object>(requestId).ToString()!)
                                                           .GetSafeValue<string>("FileName");
                    if (string.IsNullOrEmpty(originalFileName))
                    {
                        originalFileName = fileName;
                    }
                    string filePath = Path.Combine("Files/Vbee", originalFileName + ".mp3");
                    var res = await _dowloadService.DownloadMp3Async(obj.GetSafeValue<string>("audio_link"), filePath);
                    return TypedResults.Ok(success);
                }
            }
            return TypedResults.Ok(false);
        }
    }
}
