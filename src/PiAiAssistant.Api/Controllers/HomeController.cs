using Microsoft.AspNetCore.Mvc;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Index() => Redirect("/app/index.html");
}
