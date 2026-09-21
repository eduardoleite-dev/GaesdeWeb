using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record UserRequestDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string? Password,
    [property: JsonPropertyName("accessLevel")] AccessLevel AccessLevel,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("bio")] string? Bio);

public sealed record UserManagementDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("accessLevel")] AccessLevel AccessLevel,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("bio")] string? Bio);

public sealed record UserPageResult(
    IReadOnlyList<UserManagementDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}
