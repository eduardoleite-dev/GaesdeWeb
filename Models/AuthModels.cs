using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("userId")] string UserId);

public sealed record SessionData(string Token, DateTimeOffset ExpiresAt, string Username, string UserId)
{
    public string AccessLevel { get; init; } = "-";
    public string Role { get; init; } = "Usuário";
    public string DisplayName => string.IsNullOrWhiteSpace(UserId) ? Username : UserId;
    public bool IsExpired => ExpiresAt <= DateTimeOffset.UtcNow;
}

public sealed record HelloWorldResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("mongoStatus")] string? MongoStatus = null);