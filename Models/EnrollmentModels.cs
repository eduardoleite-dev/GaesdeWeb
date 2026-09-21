using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record EnrollmentRequestDto(
    [property: JsonPropertyName("courseId")] string? CourseId,
    [property: JsonPropertyName("userId")] string? UserId,
    [property: JsonPropertyName("expiresAt")] DateTime? ExpiresAt);

public sealed record EnrollmentProgressRequestDto(
    [property: JsonPropertyName("progressPercentage")] double ProgressPercentage);

public sealed record EnrollmentPageResult(
    IReadOnlyList<JsonElement> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}