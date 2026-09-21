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
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Unknown;
    public string Role { get; init; } = "Usuário";
    public string DisplayName => string.IsNullOrWhiteSpace(UserId) ? Username : UserId;
    public bool IsExpired => ExpiresAt <= DateTimeOffset.UtcNow;
    public bool CanManageUsers => AccessLevel == AccessLevel.Administrator;
    public bool CanManageStudents => AccessLevel == AccessLevel.Professor;
    public bool CanManageEnrollments => AccessLevel is AccessLevel.Administrator or AccessLevel.Professor or AccessLevel.Seller;
}

public sealed record HelloWorldResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("mongoStatus")] string? MongoStatus = null);