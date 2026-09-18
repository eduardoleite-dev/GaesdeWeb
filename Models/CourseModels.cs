using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record CourseRequestDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("coverImage")] string? CoverImage,
    [property: JsonPropertyName("categoryId")] string? CategoryId,
    [property: JsonPropertyName("instructorId")] string InstructorId);

public sealed record CourseResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("coverImage")] string? CoverImage,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("instructorId")] string InstructorId,
    [property: JsonPropertyName("categoryId")] string? CategoryId);

public sealed record UserOptionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("accessLevel")] int AccessLevel);

public sealed record CoursePageResult(
    IReadOnlyList<CourseResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}
