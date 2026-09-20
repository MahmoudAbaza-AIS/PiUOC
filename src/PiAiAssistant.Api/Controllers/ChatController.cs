using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PiAiAssistant.Application.Chat;

namespace PiAiAssistant.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    [HttpGet("models")]
    public IActionResult GetModels([FromServices] IOptions<PiAiAssistant.Infrastructure.Options.OllamaOptions> options)
    {
        var ollama = options.Value;
        return Ok(new
        {
            enabled = ollama.Enabled,
            defaultModel = ollama.DefaultModel,
            models = ollama.ListModels()
        });
    }

    [HttpPost]
    public async Task<IActionResult> AskDecisionAssistant([FromBody] ChatAskRequest request, [FromServices] IDecisionAssistant assistant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "message is required" });
        }

        return Ok(await assistant.AskAsync(request, ct));
    }

    [HttpPost("tags")]
    public async Task<IActionResult> AskTagAssistant([FromBody] ChatAskRequest request, [FromServices] ITagAssistant assistant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "message is required" });
        }

        return Ok(await assistant.AskAsync(request, ct));
    }

    [HttpPost("ollama")]
    public async Task<IActionResult> ChatOllama([FromBody] ChatAskRequest request, [FromKeyedServices("ollama")] IRawChatService chatService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "message is required" });
        }

        if (!chatService.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            return Ok(await chatService.AskAsync(request, ct));
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("deepseek")]
    public async Task<IActionResult> ChatDeepSeek([FromBody] ChatAskRequest request, [FromKeyedServices("deepseek")] IRawChatService chatService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "message is required" });
        }

        if (!chatService.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            return Ok(await chatService.AskAsync(request, ct));
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("qwen-online")]
    public async Task<IActionResult> ChatQwenOnline([FromBody] ChatAskRequest request, [FromKeyedServices("qwen-online")] IRawChatService chatService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "message is required" });
        }

        if (!chatService.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            return Ok(await chatService.AskAsync(request, ct));
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
