using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public enum CommentType
{
    Course,
    Chat
}

public sealed record CommentRequestDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("recipientIds")] IReadOnlyList<string>? RecipientIds = null,
    [property: JsonPropertyName("courseId")] string? CourseId = null,
    [property: JsonPropertyName("parentId")] string? ParentId = null,
    [property: JsonPropertyName("attachments")] IReadOnlyList<JsonElement>? Attachments = null);

public sealed record CommentMutationResult(bool Success, JsonElement? Data, string? ErrorMessage);

public sealed record CommentPageResult(
    IReadOnlyList<JsonElement> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize))
        : 1;
}