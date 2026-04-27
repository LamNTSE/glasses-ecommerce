using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Chatbot;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Chatbot;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("chatbot")]
[Tags("00. AI Chatbot")]
public sealed class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    /// <summary>
    /// Tư vấn kính mắt (OpenAI) — dùng dữ liệu sản phẩm/tròng từ hệ thống. Không cần đăng nhập.
    /// </summary>
    [HttpPost("chat")]
    [AllowAnonymous]
    [Produces("application/json")]
    public async Task<ActionResult<ApiResponse<ChatbotReplyDto>>> Chat(
        [FromBody] ChatbotChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Messages is not { Count: > 0 })
        {
            return Ok(new ApiResponse<ChatbotReplyDto>
            {
                Code = 1000,
                Result = new ChatbotReplyDto
                {
                    Reply = "Bạn hãy gửi ít nhất một câu hỏi (messages không được rỗng)."
                }
            });
        }

        var list = request.Messages
            .Where(m => m is not null)
            .Select(m => new ChatbotMessageDto
            {
                Role = string.IsNullOrWhiteSpace(m!.Role) ? "user" : m.Role!.Trim(),
                Content = m.Content ?? string.Empty
            })
            .ToList();

        var reply = await _chatbotService.ChatAsync(list, cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResponse<ChatbotReplyDto> { Code = 1000, Result = new ChatbotReplyDto { Reply = reply } });
    }
}
