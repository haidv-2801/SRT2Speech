using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SRT2Speech.Core.Extensions;
using System.Net;

namespace SRT2Speech.AppAPI.Modules
{
    public class FptModule : CarterModule
    {
        public FptModule() : base("/api/fpt")
        {
            WithTags("webhook");
            IncludeInOpenApi();
        }

        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/listen", ListenDowload)
              .WithName("FptListenResponse")
              .WithOpenApi();
        }
        private IResult ListenDowload([FromBody] object body)
        {
            var obj = JObject.Parse(body.ToString());
            
            bool success = obj.GetSafeValue<bool>("success");
            if (success)
            {
                string message = obj.GetSafeValue<string>("message");
                if (!string.IsNullOrEmpty(message))
                {
                    Console.WriteLine(message);
                    try
                    {
                        string fileName = Path.GetFileName(message);
                        string filePath = Path.Combine("Files", fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                        using (WebClient webClient = new WebClient())
                        {
                            webClient.DownloadFileAsync(new Uri(message), filePath);
                            Console.WriteLine($"File saved to: {filePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error downloading file: {ex.Message}");
                    }
                }
            }
            return TypedResults.Ok(success);
        }
    }
}
