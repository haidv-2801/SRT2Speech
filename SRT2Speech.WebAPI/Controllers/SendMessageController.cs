using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using SRT2Speech.Core.Models;
using SRT2Speech.WebAPI.Hubs;

namespace SRT2Speech.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SendMessageController : ControllerBase
    {
        private readonly ILogger<SendMessageController> _logger;
        private readonly IHubContext<MessageHub> _hubContext;

        public SendMessageController(ILogger<SendMessageController> logger, IHubContext<MessageHub> hubContext)
        {
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpPost("PostMessage")]
        public async Task<IActionResult> PostMessage([FromQuery] string msg)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", msg);
            return Ok(true);
        }

        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok(true);
        }

        [HttpPost("PostKeyEvents")]
        public async Task<IActionResult> PostKeyEvents([FromBody] List<string> keyEvents)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveKeyEvents", keyEvents);
            _logger.LogInformation("PostKeyEvents Message");
            return Ok(true);
        }

        [HttpPost("PostAIResult")]
        public async Task<IActionResult> PostAIResult([FromBody] List<MessageModel> messages)
        {
            
            await _hubContext.Clients.All.SendAsync("ReceiveAIResult", messages);
            return Ok(true);
        }

        [HttpGet("ResetIndex")]
        public async Task<IActionResult> ResetIndex()
        {

            await _hubContext.Clients.All.SendAsync("ResetIndex", null);
            return Ok(true);
        }
    }
}