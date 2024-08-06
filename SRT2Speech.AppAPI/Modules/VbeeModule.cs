using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class VbeeModule : CarterModule
    {
        public VbeeModule() : base("/api/vbee")
        {
            WithTags("webhook");
            IncludeInOpenApi();
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/listen", ListenDowload)
              .WithName("VbeeListenResponse")
              .WithOpenApi();
        }

        private  IResult ListenDowload([FromBody] object body)
        {
            return TypedResults.Ok(true);
        }
    }
}
