using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class CategoryService
{
    private readonly ApiService api;

    public CategoryService(ApiService api) => this.api = api;

    public async Task<CategoryPageResult> GetAllAsync(
        string token,
        int page = 1,
        int pageSize = 12,
        string? search = null)
    {
        var query = $"api/Categories?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";

        using var request = new HttpRequestMessage(HttpMethod.Get, query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await api.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ReadPage(payload, page, pageSize);
    }

    public async Task<CategoryResponseDto?> CreateAsync(string token, string name)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/Categories", token);
        request.Content = JsonContent.Create(new CategoryRequestDto(name));
        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CategoryResponseDto>();
    }

    public async Task<CategoryResponseDto?> UpdateAsync(string token, string id, string name)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/Categories/{Uri.EscapeDataString(id)}", token);
        request.Content = JsonContent.Create(new CategoryRequestDto(name));
        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CategoryResponseDto>();
    }

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"api/Categories/{Uri.EscapeDataString(id)}", token);
        using var response = await api.Client.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<CategoryResponseDto?> UploadPhotoAsync(string token, string id, IBrowserFile file)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/Categories/{Uri.EscapeDataString(id)}/photo", token);
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(20 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        request.Content = content;

        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CategoryResponseDto>();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static CategoryPageResult ReadPage(JsonElement payload, int requestedPage, int requestedPageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var arrayItems = payload.Deserialize<List<CategoryResponseDto>>() ?? [];
            return new CategoryPageResult(arrayItems, arrayItems.Count, requestedPage, requestedPageSize);
        }

        if (payload.ValueKind != JsonValueKind.Object)
            return new CategoryPageResult([], 0, requestedPage, requestedPageSize);

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var items = itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.Deserialize<List<CategoryResponseDto>>() ?? []
            : [];

        var page = ReadInt(payload, requestedPage, "page", "currentPage");
        var pageSize = ReadInt(payload, requestedPageSize, "pageSize", "currentPageSize");
        var totalCount = ReadInt(payload, items.Count, "totalCount", "totalItems", "count");

        return new CategoryPageResult(items, totalCount, page, pageSize);
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
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : fallback;
    }
}
