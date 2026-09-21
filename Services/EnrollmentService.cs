using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class EnrollmentService
{
    private readonly ApiService api;

    public EnrollmentService(ApiService api) => this.api = api;

    public Task<EnrollmentPageResult> GetAllAsync(string token, int page = 1, int pageSize = 10, string? status = null, string? courseId = null) =>
        GetPageAsync("api/Enrollments", token, page, pageSize, status, courseId);

    public Task<EnrollmentPageResult> GetMineAsync(string token, int page = 1, int pageSize = 10, string? status = null, string? courseId = null) =>
        GetPageAsync("api/Enrollments/me", token, page, pageSize, status, courseId);

    public Task<JsonElement?> GetByIdAsync(string token, string id) =>
        GetJsonAsync(token, $"api/Enrollments/{Uri.EscapeDataString(id)}");

    public Task<JsonElement?> CreateAsync(string token, EnrollmentRequestDto enrollment) =>
        SendJsonAsync(HttpMethod.Post, "api/Enrollments", token, enrollment);

    public Task<JsonElement?> UpdateProgressAsync(string token, string id, EnrollmentProgressRequestDto progress) =>
        SendJsonAsync(HttpMethod.Patch, $"api/Enrollments/{Uri.EscapeDataString(id)}/progress", token, progress);

    public Task<JsonElement?> UpdateStatusAsync(string token, string id, string status) =>
        SendAsync(HttpMethod.Patch, $"api/Enrollments/{Uri.EscapeDataString(id)}/status/{Uri.EscapeDataString(status)}", token);

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendRequestAsync(HttpMethod.Delete, $"api/Enrollments/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    private async Task<EnrollmentPageResult> GetPageAsync(string endpoint, string token, int page, int pageSize, string? status, string? courseId)
    {
        var query = $"{endpoint}?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrWhiteSpace(courseId)) query += $"&courseId={Uri.EscapeDataString(courseId)}";

        using var response = await SendRequestAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        return ReadPage(await response.Content.ReadFromJsonAsync<JsonElement>(), page, pageSize);
    }

    private async Task<JsonElement?> GetJsonAsync(string token, string endpoint) => await SendAsync(HttpMethod.Get, endpoint, token);

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

    private static EnrollmentPageResult ReadPage(JsonElement payload, int page, int pageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var items = payload.EnumerateArray().ToArray();
            return new EnrollmentPageResult(items, items.Length, page, pageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var pageItems = itemsElement.ValueKind == JsonValueKind.Array ? itemsElement.EnumerateArray().ToArray() : [];
        return new EnrollmentPageResult(
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