using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dynamic.Promotions.Application.Contracts;
using Dynamic.Promotions.Application.Models;
using Dynamic.Promotions.Application.Options;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace Dynamic.Promotions.Infrastructure.Push;

public class FirebasePromotionPushSender : IPromotionPushSender
{
    private const string FirebaseScope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly HttpClient _httpClient;
    private readonly FirebasePushOptions _options;
    private readonly GoogleCredential? _credential;

    public FirebasePromotionPushSender(HttpClient httpClient, IOptions<FirebasePushOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _credential = _options.Enabled && !string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
            ? CredentialFactory.FromJson<ServiceAccountCredential>(_options.ServiceAccountJson)
                .ToGoogleCredential()
                .CreateScoped(FirebaseScope)
            : null;
    }

    public async Task<PromotionPushResult> SendAsync(
        PromotionPushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _credential is null || string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            return PromotionPushResult.Failure("Firebase push is disabled or not configured.", retryable: false);
        }

        string accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
            cancellationToken: cancellationToken);
        string endpoint = $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(_options.ProjectId)}/messages:send";

        Dictionary<string, string> data = new()
        {
            ["type"] = "promotion",
            ["promotionRecipientId"] = message.PromotionRecipientId.ToString(),
            ["campaignId"] = message.CampaignId.ToString(),
            ["negocioId"] = message.NegocioId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(message.DeepLink))
        {
            data["deepLink"] = message.DeepLink;
        }

        object payload = new
        {
            message = new
            {
                token = message.Token,
                notification = new
                {
                    title = message.Title,
                    body = message.Body,
                    image = message.ImageUrl
                },
                data,
                android = new
                {
                    priority = "normal",
                    notification = new
                    {
                        channel_id = _options.AndroidChannelId
                    }
                }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            string? providerMessageId = document.RootElement.TryGetProperty("name", out JsonElement name)
                ? name.GetString()
                : null;
            return PromotionPushResult.Success(providerMessageId);
        }

        bool invalidToken = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest &&
            (responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase) ||
             responseBody.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase));
        bool retryable = response.StatusCode is HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

        return PromotionPushResult.Failure(
            $"FCM {(int)response.StatusCode}: {Truncate(responseBody, 1200)}",
            retryable,
            invalidToken);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
