using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AutoFlow.Web.Services;

public sealed class XenditPaymentService
{
    private const string ApiVersion = "2024-11-11";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public XenditPaymentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri("https://api.xendit.co/");
    }

    public string? SecretKey =>
        _configuration["XENDIT_SECRET_KEY"] ??
        Environment.GetEnvironmentVariable("XENDIT_SECRET_KEY");

    public string? WebhookToken =>
        _configuration["XENDIT_WEBHOOK_TOKEN"] ??
        Environment.GetEnvironmentVariable("XENDIT_WEBHOOK_TOKEN");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);

    public async Task<XenditPaymentResult> CreateQrPaymentAsync(
        string referenceId,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return XenditPaymentResult.Failed("Xendit is not configured. Set XENDIT_SECRET_KEY first.");
        }

        var payload = new
        {
            reference_id = referenceId,
            type = "PAY",
            country = "PH",
            currency = "PHP",
            request_amount = amount,
            capture_method = "AUTOMATIC",
            channel_code = "QRPH",
            description,
            metadata = new
            {
                autoflow_reference = referenceId
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/payment_requests");
        AddHeaders(request);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return XenditPaymentResult.Failed(
                TryGetErrorMessage(body) ?? $"Xendit returned HTTP {(int)response.StatusCode}.");
        }

        return ParsePaymentResponse(body);
    }

    public async Task<XenditPaymentResult> GetPaymentRequestAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return XenditPaymentResult.Failed("Xendit is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}");
        AddHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return XenditPaymentResult.Failed(
                TryGetErrorMessage(body) ?? $"Xendit returned HTTP {(int)response.StatusCode}.");
        }

        return ParsePaymentResponse(body);
    }

    public async Task<bool> SimulatePaymentAsync(
        string paymentRequestId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var payload = new { amount };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}/simulate");
        AddHeaders(request);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SecretKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        request.Headers.TryAddWithoutValidation("api-version", ApiVersion);
    }

    private static XenditPaymentResult ParsePaymentResponse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var paymentRequestId = GetString(root, "payment_request_id");
            var referenceId = GetString(root, "reference_id");
            var status = GetString(root, "status") ?? "UNKNOWN";
            var amount = GetDecimal(root, "request_amount");
            string? qrString = null;
            string? checkoutUrl = null;

            if (root.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
            {
                foreach (var action in actions.EnumerateArray())
                {
                    var descriptor = GetString(action, "descriptor");
                    var value = GetString(action, "value");

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (string.Equals(descriptor, "QR_STRING", StringComparison.OrdinalIgnoreCase))
                    {
                        qrString = value;
                    }
                    else if (string.Equals(descriptor, "WEB_URL", StringComparison.OrdinalIgnoreCase))
                    {
                        checkoutUrl = value;
                    }
                }
            }

            return new XenditPaymentResult(
                true,
                null,
                paymentRequestId,
                referenceId,
                status,
                amount,
                qrString,
                checkoutUrl);
        }
        catch (JsonException)
        {
            return XenditPaymentResult.Failed("Xendit returned an unreadable response.");
        }
    }

    private static string? TryGetErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return GetString(root, "message") ?? GetString(root, "error_code");
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetDecimal(out var value)
            ? value
            : 0m;
    }
}

public sealed record XenditPaymentResult(
    bool Success,
    string? Error,
    string? PaymentRequestId,
    string? ReferenceId,
    string Status,
    decimal Amount,
    string? QrString,
    string? CheckoutUrl)
{
    public static XenditPaymentResult Failed(string error) =>
        new(false, error, null, null, "FAILED", 0m, null, null);
}
