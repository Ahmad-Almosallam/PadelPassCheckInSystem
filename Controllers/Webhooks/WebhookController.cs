using Microsoft.AspNetCore.Mvc;
using PadelPassCheckInSystem.Integration.Rekaz.Models;
using PadelPassCheckInSystem.Services;

namespace PadelPassCheckInSystem.Controllers.Webhooks;

public class WebhookController(IEndUserSubscriptionService endUserSubscriptionService) : Controller
{
    [HttpPost("rekaz-webhooks")]
    public async Task<IActionResult> Index([FromBody] WebhookEvent model)
    {
        var res = await endUserSubscriptionService.ProcessWebhookEvent(model);
        return Ok(res);
    }
}