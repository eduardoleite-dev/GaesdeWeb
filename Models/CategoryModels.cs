using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record CategoryRequestDto(string Name);

public sealed record CategoryResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

public sealed record CategoryPageResult(
    IReadOnlyList<CategoryResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}
