using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record AssignmentSubmissionRequestDto(
    [property: JsonPropertyName("contentId")] string? ContentId,
    [property: JsonPropertyName("enrollmentId")] string? EnrollmentId,
    [property: JsonPropertyName("fileUrl")] string? FileUrl);

public sealed record AssignmentGradeRequestDto(
    [property: JsonPropertyName("grade")] double Grade,
    [property: JsonPropertyName("instructorFeedback")] string? InstructorFeedback);

public sealed record AssignmentSubmissionPageResult(
    IReadOnlyList<JsonElement> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}