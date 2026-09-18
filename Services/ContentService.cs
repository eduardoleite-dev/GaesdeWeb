using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class ContentService
{
    private readonly ApiService api;

    public ContentService(ApiService api) => this.api = api;

    public async Task<ContentPageResult> GetAllAsync(string token, string? moduleId = null, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = $"api/Contents?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(moduleId)) query += $"&moduleId={Uri.EscapeDataString(moduleId)}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search.Trim())}";
        using var response = await SendAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        return ReadPage(await response.Content.ReadFromJsonAsync<JsonElement>(), page, pageSize);
    }

    public async Task<ContentResponseDto?> CreateAsync(string token, ContentRequestDto content)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/Contents", token, content);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContentResponseDto>() : null;
    }

    public async Task<ContentResponseDto?> UpdateAsync(string token, string id, ContentUpdateRequestDto content)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/Contents/{Uri.EscapeDataString(id)}", token, content);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContentResponseDto>() : null;
    }

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/Contents/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    public async Task<(string Url, long Size)?> UploadFileAsync(string token, IBrowserFile file)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/media/upload", token);
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(20 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.Name);
        request.Content = form;
        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("url", out var value) ? value.GetString() : null;
        return string.IsNullOrWhiteSpace(url) ? null : (url, file.Size);
    }

    public async Task<ContentResponseDto?> CreatePhotoAsync(string token, string id, IBrowserFile file) => await SendPhotoAsync(HttpMethod.Post, token, id, file);
    public async Task<ContentResponseDto?> UpdatePhotoAsync(string token, string id, IBrowserFile file) => await SendPhotoAsync(HttpMethod.Put, token, id, file);

    private async Task<ContentResponseDto?> SendPhotoAsync(HttpMethod method, string token, string id, IBrowserFile file)
    {
        using var request = CreateRequest(method, $"api/Contents/{Uri.EscapeDataString(id)}/photo", token);
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(20 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.Name);
        request.Content = form;
        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body) ? new ContentResponseDto(id, string.Empty, string.Empty, null, string.Empty, 0, false, null, null, null, null, null) : JsonSerializer.Deserialize<ContentResponseDto>(body);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string uri, string token, object body)
    {
        var request = CreateRequest(method, uri, token);
        request.Content = JsonContent.Create(body);
        return await api.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token) => await api.Client.SendAsync(CreateRequest(method, uri, token));
    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static ContentPageResult ReadPage(JsonElement payload, int page, int pageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var array = payload.Deserialize<List<ContentResponseDto>>() ?? [];
            return new ContentPageResult(array, array.Count, page, pageSize);
        }
        var element = FindProperty(payload, "items", "data", "results");
        var items = element.ValueKind == JsonValueKind.Array ? element.Deserialize<List<ContentResponseDto>>() ?? [] : [];
        return new ContentPageResult(items, ReadInt(payload, items.Count, "totalCount", "totalItems", "count"), ReadInt(payload, page, "page", "currentPage"), ReadInt(payload, pageSize, "pageSize", "currentPageSize"));
    }

    private static JsonElement FindProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names) if (element.TryGetProperty(name, out var property)) return property;
        return default;
    }

    private static int ReadInt(JsonElement element, int fallback, params string[] names)
    {
        var property = FindProperty(element, names);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : fallback;
    }
}
