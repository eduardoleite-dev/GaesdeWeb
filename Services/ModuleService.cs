using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class ModuleService
{
    private readonly ApiService api;

    public ModuleService(ApiService api) => this.api = api;

    public async Task<ModulePageResult> GetAllAsync(
        string token,
        string? courseId = null,
        int page = 1,
        int pageSize = 10,
        string? search = null)
    {
        var query = $"api/Modules?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(courseId))
            query += $"&courseId={Uri.EscapeDataString(courseId)}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";

        using var response = await SendAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ReadPage(payload, page, pageSize);
    }

    public async Task<ModuleResponseDto?> CreateAsync(string token, ModuleRequestDto module)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/Modules", token, module);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<ModuleResponseDto>();
    }

    public async Task<ModuleResponseDto?> UpdateAsync(string token, string id, ModuleUpdateRequestDto module)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/Modules/{Uri.EscapeDataString(id)}", token, module);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<ModuleResponseDto>();
    }

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/Modules/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string uri, string token, object body)
    {
        var request = CreateRequest(method, uri, token);
        request.Content = JsonContent.Create(body);
        return await api.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token) =>
        await api.Client.SendAsync(CreateRequest(method, uri, token));

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static ModulePageResult ReadPage(JsonElement payload, int requestedPage, int requestedPageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var arrayItems = payload.Deserialize<List<ModuleResponseDto>>() ?? [];
            return new ModulePageResult(arrayItems, arrayItems.Count, requestedPage, requestedPageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var items = itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.Deserialize<List<ModuleResponseDto>>() ?? []
            : [];
        var page = ReadInt(payload, requestedPage, "page", "currentPage");
        var pageSize = ReadInt(payload, requestedPageSize, "pageSize", "currentPageSize");
        var totalCount = ReadInt(payload, items.Count, "totalCount", "totalItems", "count");
        return new ModulePageResult(items, totalCount, page, pageSize);
    }

    private static JsonElement FindProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property))
                return property;
        }

        return default;
    }

    private static int ReadInt(JsonElement element, int fallback, params string[] names)
    {
        var property = FindProperty(element, names);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : fallback;
    }
}
