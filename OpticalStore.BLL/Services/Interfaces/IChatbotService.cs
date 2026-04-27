using OpticalStore.BLL.DTOs.Chatbot;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IChatbotService
{
    Task<string> ChatAsync(IReadOnlyList<ChatbotMessageDto> messages, CancellationToken cancellationToken = default);
}
