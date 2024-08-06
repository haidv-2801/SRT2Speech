using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Cache;
using SRT2Speech.Core.Extensions;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class FptModule : CarterModule
    {
        private readonly IDowloadService _dowloadService;
        private readonly IMemCacheService _memCacheService;
        public FptModule(IDowloadService dowloadService, IMemCacheService memCacheService) : base("/api/fpt")
        {
            WithTags("Webhook");
            IncludeInOpenApi();
            _dowloadService = dowloadService;
            _memCacheService = memCacheService;
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/listen", async ([FromBody] object body) =>
            {
                return await ListenDowload(body);
            })
              .WithName("FptListenResponse")
              .WithOpenApi();
        }
      

        private async Task<IResult> ListenDowload([FromBody] object body)
        {
            var obj = JObject.Parse(body.ToString()!);

            bool success = obj.GetSafeValue<bool>("success");
            if (success)
            {
                string message = obj.GetSafeValue<string>("message");
                if (!string.IsNullOrEmpty(message))
                {
                    string fileName = Path.GetFileName(message);
                    string requestId = obj.GetSafeValue<string>("requestid");
                    string originalFileName = _memCacheService.Get<string>(requestId);
                    if (string.IsNullOrEmpty(originalFileName))
                    {
                        originalFileName = fileName;
                    }
                    string filePath = Path.Combine("Files/FPT", originalFileName);
                    success = await _dowloadService.DownloadMp3Async(message, filePath);
                }
            }
            return TypedResults.Ok(success);
        }
    }
}
