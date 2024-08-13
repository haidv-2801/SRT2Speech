using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Socket.Methods;
using SRT2Speech.Core.Models;
using System.Net;
using SRT2Speech.Socket.Models;

namespace SRT2Speech.AppAPI.Modules
{
    public class VbeeModule : CarterModule
    {
        private readonly IDowloadService _dowloadService;
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly IMemCacheService _memCacheService;
        private readonly IMicrosoftCacheService _microsoftCacheService;

        public VbeeModule(IDowloadService dowloadService, IHubContext<MessageHub> hubContext, IMicrosoftCacheService microsoftCacheService) : base("/api/vbee")
        {
            WithTags("Webhook");
            IncludeInOpenApi();
            _dowloadService = dowloadService;
            _hubContext = hubContext;

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
            var obj = JObject.Parse(body.ToString()!);
            Console.WriteLine($"[Vbee] response = {body.ToString()}");

            string success = obj.GetSafeValue<string>("status");
            if (success == "SUCCESS")
            {
                string requestId = obj.GetSafeValue<string>("request_id");
                if (!string.IsNullOrEmpty(requestId))
                {
                    string fileName = Path.GetFileName(requestId);
                    string originalFileName = fileName;
                    string filePath = Path.Combine("Files/Vbee", originalFileName + ".mp3");
                    var res = await _dowloadService.DownloadMp3Async(obj.GetSafeValue<string>("audio_link"), filePath);

                    var signalItem = new SignalItem()
                    {
                        Id = requestId,
                        TimeStamp = DateTime.Now,
                        Data = new
                        {
                            success = res,
                            message = res ? $"[SUCCESS] dowload Files/Vbee/{originalFileName}" : $"[FAILED] dowload Files/Vbee/{originalFileName}"
                        }
                    };
                    await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG_VBEE, signalItem);
                    return TypedResults.Ok(success);
                }
            }
            return TypedResults.Ok(false);
        }
    }
}
