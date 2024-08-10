
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using SRT2Speech.Core.Models;
using SRT2Speech.Socket.Methods;
using YamlDotNet.Core.Tokens;

namespace SRT2Speech.AppAPI.Modules
{
    public class CacheModule : CarterModule
    {
        private readonly IMicrosoftCacheService _microsoftCacheService;
        private readonly IHubContext<MessageHub> _hubContext;
        public CacheModule(IMicrosoftCacheService microsoftCacheService, IHubContext<MessageHub> hubContext) : base("/api/cache")
        {
            IncludeInOpenApi();
            _microsoftCacheService = microsoftCacheService;
            _hubContext = hubContext;
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/get-cache/{key}", async ([FromQuery] string key) =>
            {
                return await GetCacheByKey(key);
            })
           .WithName("FptGetCacheResponse")
           .WithOpenApi();

            app.MapPost("/set-cache", async ([FromBody] BaseRequestCache model) =>
            {
                return await SetCacheByKey(model);
            })
          .WithName("FptSetCacheResponse")
          .WithOpenApi();
        }

        private async Task<IResult> GetCacheByKey([FromQuery] string key)
        {
            try
            {
                var value = await _microsoftCacheService.GetFromCacheAsync<object>(key);
                return TypedResults.Ok(value);
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, $"[FAILED] Get cache : {key}");
                return TypedResults.BadRequest();
            }
        }

        private async Task<IResult> SetCacheByKey([FromBody] BaseRequestCache model)
        {
            try
            {
                await _microsoftCacheService.SetCacheAsync(model.Key, JsonConvert.DeserializeObject<object>(model.Value), TimeSpan.FromMinutes(10));
                return TypedResults.StatusCode(200);
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.All.SendAsync(SignalMethods.SIGNAL_LOG, $"[FAILED] Get cache : {JsonConvert.SerializeObject(model)}");
                return TypedResults.BadRequest();
            }
        }
    }
}
