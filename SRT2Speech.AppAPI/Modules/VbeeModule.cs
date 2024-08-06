using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class VbeeModule : CarterModule
    {
        private readonly IDowloadService _dowloadService;
        public VbeeModule(IDowloadService dowloadService) : base("/api/vbee")
        {
            WithTags("Webhook");
            IncludeInOpenApi();
            _dowloadService = dowloadService;
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
                    string filePath = Path.Combine("Files/Vbee", fileName + ".mp3");
                    var res = await _dowloadService.DownloadMp3Async(obj.GetSafeValue<string>("audio_link"), filePath);
                    return TypedResults.Ok(success);
                }
            }
            return TypedResults.Ok(false);
        }
    }
}
