using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class AssignmentSubmissionService
{
    private readonly ApiService api;

    public AssignmentSubmissionService(ApiService api) => this.api = api;

    public async Task<AssignmentSubmissionPageResult> GetAllAsync(string token, int page = 1, int pageSize = 10, string? contentId = null, string? enrollmentId = null, bool? graded = null)
    {
        var query = $"api/AssignmentSubmissions?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(contentId)) query += $"&contentId={Uri.EscapeDataString(contentId)}";
        if (!string.IsNullOrWhiteSpace(enrollmentId)) query += $"&enrollmentId={Uri.EscapeDataString(enrollmentId)}";
        if (graded.HasValue) query += $"&graded={graded.Value.ToString().ToLowerInvariant()}";

        using var response = await SendRequestAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        return ReadPage(await response.Content.ReadFromJsonAsync<JsonElement>(), page, pageSize);
    }

    public Task<JsonElement?> GetByIdAsync(string token, string id) =>
        SendAsync(HttpMethod.Get, $"api/AssignmentSubmissions/{Uri.EscapeDataString(id)}", token);

    public Task<JsonElement?> CreateAsync(string token, AssignmentSubmissionRequestDto submission) =>
        SendJsonAsync(HttpMethod.Post, "api/AssignmentSubmissions", token, submission);

    public Task<JsonElement?> GradeAsync(string token, string id, AssignmentGradeRequestDto grade) =>
        SendJsonAsync(HttpMethod.Patch, $"api/AssignmentSubmissions/{Uri.EscapeDataString(id)}/grade", token, grade);

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendRequestAsync(HttpMethod.Delete, $"api/AssignmentSubmissions/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    private async Task<JsonElement?> SendJsonAsync(HttpMethod method, string endpoint, string token, object body)
    {
        using var request = CreateRequest(method, endpoint, token);
        request.Content = JsonContent.Create(body);
        using var response = await api.Client.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private async Task<JsonElement?> SendAsync(HttpMethod method, string endpoint, string token)
    {
        using var response = await SendRequestAsync(method, endpoint, token);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string endpoint, string token) =>
        await api.Client.SendAsync(CreateRequest(method, endpoint, token));

    private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, string token)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static AssignmentSubmissionPageResult ReadPage(JsonElement payload, int page, int pageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var items = payload.EnumerateArray().ToArray();
            return new AssignmentSubmissionPageResult(items, items.Length, page, pageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var pageItems = itemsElement.ValueKind == JsonValueKind.Array ? itemsElement.EnumerateArray().ToArray() : [];
        return new AssignmentSubmissionPageResult(
            pageItems,
            ReadInt(payload, pageItems.Length, "totalItems", "totalCount", "count"),
            ReadInt(payload, page, "page", "currentPage"),
            ReadInt(payload, pageSize, "pageSize", "currentPageSize"));
    }

    private static JsonElement FindProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var property)) return property;
        return default;
    }

    private static int ReadInt(JsonElement element, int fallback, params string[] names)
    {
        var property = FindProperty(element, names);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : fallback;
    }
}