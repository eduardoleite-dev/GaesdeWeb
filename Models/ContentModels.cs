using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record ContentRequestDto(
    [property: JsonPropertyName("moduleId")] string ModuleId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("isFreePreview")] bool IsFreePreview,
    [property: JsonPropertyName("durationSeconds")] int? DurationSeconds,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("fileUrl")] string? FileUrl,
    [property: JsonPropertyName("fileSizeBytes")] long? FileSizeBytes);

public sealed record ContentUpdateRequestDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("isFreePreview")] bool IsFreePreview,
    [property: JsonPropertyName("durationSeconds")] int? DurationSeconds,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("fileUrl")] string? FileUrl,
    [property: JsonPropertyName("fileSizeBytes")] long? FileSizeBytes);

public sealed record ContentResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("moduleId")] string ModuleId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("photoUrl")] string? PhotoUrl,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("isFreePreview")] bool IsFreePreview,
    [property: JsonPropertyName("durationSeconds")] int? DurationSeconds,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("fileUrl")] string? FileUrl,
    [property: JsonPropertyName("fileSizeBytes")] long? FileSizeBytes);

public sealed record ContentPageResult(
    IReadOnlyList<ContentResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}
