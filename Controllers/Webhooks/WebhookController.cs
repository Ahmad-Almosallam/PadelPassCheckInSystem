using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Integration.Rekaz.Models;

namespace PadelPassCheckInSystem.Controllers.Webhooks;

public class WebhookController : Controller
{
    
    
    
    [HttpPost("rekaz-webhooks")]
    public async Task<IActionResult> Index([FromBody] WebhookModel model)
    {
        return Ok();
    }
}