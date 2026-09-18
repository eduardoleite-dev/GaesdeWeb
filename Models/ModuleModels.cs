using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record ModuleRequestDto(
    [property: JsonPropertyName("courseId")] string CourseId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);

public sealed record ModuleUpdateRequestDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);

public sealed record ModuleResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("courseId")] string CourseId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

public sealed record ModulePageResult(
    IReadOnlyList<ModuleResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}
