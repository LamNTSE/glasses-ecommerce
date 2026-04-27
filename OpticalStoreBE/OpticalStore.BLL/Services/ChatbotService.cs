using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpticalStore.BLL.Configuration;
using OpticalStore.BLL.DTOs.Chatbot;
using OpticalStore.BLL.DTOs.Products;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.BLL.Services;

public sealed class ChatbotService : IChatbotService
{
    private const int MaxProductLines = 30;
    private const int MaxVariantPageSize = 40;
    private const int MaxVariantLinesPerProduct = 20;
    private const int MaxStoreContextChars = 28_000;

    private static readonly string SystemPromptBase = """
Bạn là chuyên gia tư vấn kính mắt tại cửa hàng **OptiCare** (cửa hàng mắt kính chuyên nghiệp, uy tín).

⚠️ QUY TẮC BẮT BUỘC - TUYỆT ĐỐI TUÂN THỦ:
1. CHỈ được gợi ý, giới thiệu hoặc recommend sản phẩm **CÓ TRONG DANH SÁCH SẢN PHẨM CỦA CỬA HÀNG** (phần dữ liệu bên dưới, sau phần "DANH SÁCH SẢN PHẨM" và "DANH SÁCH TRÒNG KÍNH").
2. TUYỆT ĐỐI KHÔNG tự bịa tên, thương hiệu, mã sản phẩm, giá hoặc mô tả sản phẩm **không có** trong dữ liệu được cung cấp.
3. Nếu không có sản phẩm phù hợp, hãy nói rõ cửa hàng chưa có sản phẩm lý tưởng theo mô tả đó, và gợi ý **một vài lựa chọn gần nhất** có trong kho.
4. Khi nêu tên sản phẩm, PHẢI dùng **đúng tên, thương hiệu, khoảng giá, biến thể (màu, size, số đo) có trong danh sách** — trích đúng từ bảng dữ liệu, không tự làm tròn hay đổi tên.
5. Có thể tư vấn kiểu dáng, khuôn mặt, công dụng tròng (ví dụ chống ánh sáng xanh) ở mức tổng quát, nhưng mọi **cụ thể sản phẩm, giá, tồn** chỉ từ danh sách bên dưới.
6. Không cung cấp chẩn đoán y khoa; với cận, loạn, tật khúc xạ, luôn nhắc thăm bác sĩ nhãn khoa.

PHONG CÁCH TƯ VẤN (CHUYÊN NGHIỆP):
- Tiếng Việt tự nhiên, thân thiện, lịch sự, xưng hô ngắn gọn.
- Cấu trúc câu rõ: có thể dùng gạch đầu dòng, **in đậm** tên sản phẩm, phân tách ý bằng xuống dòng.
- Hỏi nhu cầu (ngân sách, công dụng, sở thích) khi còn mơ hồ; tóm tắt 2–3 gợi ý hợp lý, không dài dòng.
- Không cạnh tranh so sánh bất lợi thương hiệu ngoài danh sách; không bảo đảm kết quả thị lực.

""";

    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<ChatbotService> _logger;
    private readonly IProductService _productService;
    private readonly IProductVariantService _productVariantService;
    private readonly ILensService _lensService;

    public ChatbotService(
        HttpClient http,
        IOptions<OpenAiOptions> options,
        ILogger<ChatbotService> logger,
        IProductService productService,
        IProductVariantService productVariantService,
        ILensService lensService)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _productService = productService;
        _productVariantService = productVariantService;
        _lensService = lensService;
    }

    public async Task<string> ChatAsync(IReadOnlyList<ChatbotMessageDto> messages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return "Hệ thống tư vấn AI chưa được cấu hình (thiếu OpenAI API key). Vui lòng liên hệ quản trị viên hoặc thử lại sau.";
        }

        var systemContent = SystemPromptBase + await BuildStoreContextAsync(cancellationToken).ConfigureAwait(false);
        if (systemContent.Length > MaxStoreContextChars)
        {
            systemContent = string.Concat(
                systemContent.AsSpan(0, MaxStoreContextChars),
                "\n\n[…Dữ liệu cửa hàng rút gọn vì dung lượng; chỉ còn một phần sản phẩm.]\n");
        }
        _logger.LogDebug("OpenAI system+context length: {Length}", systemContent.Length);

        var openAiMessages = new List<OpenAiChatMessage>
        {
            new() { Role = "system", Content = systemContent }
        };

        foreach (var m in messages)
        {
            if (string.IsNullOrWhiteSpace(m.Content))
            {
                continue;
            }

            var role = m.Role?.Trim().ToLowerInvariant() switch
            {
                "user" => "user",
                "assistant" => "assistant",
                "system" => "system",
                _ => "user"
            };
            openAiMessages.Add(new OpenAiChatMessage { Role = role, Content = m.Content });
        }

        if (openAiMessages.Count < 2)
        {
            return "Bạn hãy nhập câu hỏi để tôi tư vấn nhé.";
        }

        var requestBody = new OpenAiChatCompletionsRequest
        {
            Model = _options.Model,
            Messages = openAiMessages,
            Temperature = 0.6,
            MaxTokens = 900
        };

        var json = JsonSerializer.Serialize(
            requestBody,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return "Xin lỗi, hiện tại tôi không thể kết nối dịch vụ AI. Bạn thử lại sau nhé.";
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenAI chat HTTP {Status}: {Body}",
                (int)response.StatusCode,
                TruncateForLog(raw, 6000));
            return MapOpenAiErrorToUserMessage(response.StatusCode, raw);
        }

        var parsed = JsonSerializer.Deserialize<OpenAiChatCompletionsResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return !string.IsNullOrWhiteSpace(text) ? text.Trim() : "Xin lỗi, hiện tại tôi không thể trả lời.";
    }

    private async Task<string> BuildStoreContextAsync(CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var products = await _productService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var activeProducts = products
            .Where(p => p.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Take(MaxProductLines)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("===== DANH SÁCH SẢN PHẨM HIỆN CÓ =====");
        var count = 0;
        foreach (var p in activeProducts)
        {
            count++;
            sb.AppendLine();
            sb.Append("📦 ").Append(p.Name);
            sb.Append(" | Brand: ").Append(NullSafe(p.Brand));
            sb.Append(" | Category: ").Append(NullSafe(p.Category));
            sb.Append(" | Frame: ").Append(NullSafe(p.FrameType));
            sb.Append(" | Material: ").Append(NullSafe(p.FrameMaterial));
            sb.Append(" | Shape: ").Append(NullSafe(p.Shape));
            sb.Append(" | Gender: ").Append(NullSafe(p.Gender));
            sb.Append(" | Price: ").Append(PriceRange(p));
            sb.AppendLine();

            var page = await _productVariantService
                .GetByProductIdAsync(
                    p.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "ACTIVE",
                    0,
                    MaxVariantPageSize,
                    "price",
                    "asc",
                    cancellationToken)
                .ConfigureAwait(false);

            var variants = page.Items
                .Where(v => v.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (variants.Count == 0)
            {
                continue;
            }

            sb.AppendLine("  - Biến thể:");
            var variantLines = 0;
            foreach (var v in variants)
            {
                if (variantLines >= MaxVariantLinesPerProduct)
                {
                    sb.AppendLine("    […còn biến thể khác, đã lược bớt…]");
                    break;
                }
                variantLines++;
                sb.Append("    + ");
                sb.Append(NullSafe(v.ColorName));
                sb.Append(" | Finish: ").Append(NullSafe(v.FrameFinish));
                sb.Append(" | Size: ").Append(NullSafe(v.SizeLabel));
                sb.Append(" | Lens/Bridge/Temple: ")
                    .Append(Num(v.LensWidthMm))
                    .Append("/")
                    .Append(Num(v.BridgeWidthMm))
                    .Append("/")
                    .Append(Num(v.TempleLengthMm));
                sb.Append(" | Price: ").Append(v.Price?.ToString() ?? "Liên hệ");
                sb.AppendLine();
            }
        }

        if (count == 0)
        {
            sb.AppendLine("(Chưa có sản phẩm ACTIVE nào trong dữ liệu.)");
        }

        sb.AppendLine();
        sb.AppendLine("===== DANH SÁCH TRÒNG KÍNH =====");
        var lenses = await _lensService.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (lenses.Count == 0)
        {
            sb.AppendLine("(Chưa có tròng kính trong dữ liệu.)");
        }
        else
        {
            foreach (var l in lenses)
            {
                sb.Append("- ").Append(l.Name);
                sb.Append(" | Material: ").Append(NullSafe(l.Material));
                sb.Append(" | Price: ").Append(l.Price.ToString());
                sb.Append(" | Desc: ").Append(NullSafe(l.Description));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string PriceRange(ProductResponseDto p)
    {
        if (p.MinPrice is { } min && p.MaxPrice is { } max)
        {
            return min + " - " + max;
        }
        return "Liên hệ";
    }

    private static string NullSafe(string? s) => string.IsNullOrWhiteSpace(s) ? "N/A" : s;

    private static string Num(int? v) => v?.ToString() ?? "?";

    private static string TruncateForLog(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
        return string.Concat(s.AsSpan(0, max), "…(truncated)");
    }

    private static string MapOpenAiErrorToUserMessage(HttpStatusCode status, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    var code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                    var hasType = err.TryGetProperty("type", out var typeEl);
                    var typeName = hasType && typeEl.ValueKind is JsonValueKind.String ? typeEl.GetString() : null;
                    if (string.Equals(code, "invalid_api_key", StringComparison.Ordinal) ||
                        string.Equals(code, "account_deactivated", StringComparison.Ordinal) ||
                        (string.Equals(typeName, "invalid_request_error", StringComparison.Ordinal) && msg != null && msg.Contains("api key", StringComparison.OrdinalIgnoreCase)))
                    {
                        return "Cấu hình OpenAI API key không hợp lệ hoặc đã hết hạn. Kiểm tra OpenAI:ApiKey (hoặc biến môi trường) trên server.";
                    }
                    if (string.Equals(code, "context_length_exceeded", StringComparison.Ordinal) ||
                        (msg != null && (msg.Contains("context_length", StringComparison.OrdinalIgnoreCase) || msg.Contains("maximum context", StringComparison.OrdinalIgnoreCase))))
                    {
                        return "Dữ liệu tư vấn vượt giới hạn model. Bạn hãy hỏi ngắn hơn hoặc thử lại sau.";
                    }
                    if (string.Equals(code, "model_not_found", StringComparison.Ordinal) ||
                        (msg != null && msg.Contains("model", StringComparison.OrdinalIgnoreCase) && status == HttpStatusCode.NotFound))
                    {
                        return "Model OpenAI (OpenAI:Model) không tồn tại với tài khoản này. Đổi sang gpt-4o-mini hoặc model bạn đã bật.";
                    }
                    if (string.Equals(code, "rate_limit_exceeded", StringComparison.Ordinal) || status == (HttpStatusCode)429)
                    {
                        return "Dịch vụ AI tạm quá tải. Bạn hãy thử lại sau vài phút.";
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return "OpenAI từ chối yêu cầu (401/403). Kiểm tra API key trên server.";
        }
        if (status == (HttpStatusCode)429)
        {
            return "Dịch vụ AI tạm quá tải. Bạn hãy thử lại sau vài phút.";
        }
        if (status == HttpStatusCode.PaymentRequired)
        {
            return "Tài khoản OpenAI cần nạp credit hoặc bật billing. Kiểm tra tài khoản tại platform.openai.com.";
        }
        if (status == HttpStatusCode.BadRequest)
        {
            return "Yêu cầu tới OpenAI không hợp lệ (400). Xem log server (OpenAI:… trong body) hoặc kiểm tra model/định dạng JSON.";
        }

        return "Xin lỗi, dịch vụ AI tạm thời không phản hồi. Bạn thử lại sau.";
    }

    private sealed class OpenAiChatCompletionsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<OpenAiChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class OpenAiChatCompletionsResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessageNode? Message { get; set; }
    }

    private sealed class OpenAiMessageNode
    {
        public string? Content { get; set; }
    }
}
