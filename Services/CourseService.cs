using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class CourseService
{
    private readonly ApiService api;

    public CourseService(ApiService api) => this.api = api;

    public async Task<CoursePageResult> GetAllAsync(string token, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = $"api/Courses?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";

        using var response = await SendAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ReadPage(payload, page, pageSize);
    }

    public async Task<CourseResponseDto?> CreateAsync(string token, CourseRequestDto course)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/Courses", token, course);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException("A API não autorizou a criação do curso.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CourseResponseDto>();
    }

    public async Task<CourseResponseDto?> UpdateAsync(string token, string id, CourseRequestDto course)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/Courses/{Uri.EscapeDataString(id)}", token, course);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException("A API não autorizou a edição do curso.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CourseResponseDto>();
    }

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/Courses/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetPhotoAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/Courses/{Uri.EscapeDataString(id)}/photo", token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (payload.ValueKind == JsonValueKind.String)
            return payload.GetString();

        return payload.ValueKind == JsonValueKind.Object &&
               (payload.TryGetProperty("coverImage", out var coverImage) ||
                payload.TryGetProperty("photoUrl", out coverImage) ||
                payload.TryGetProperty("url", out coverImage))
            ? coverImage.GetString()
            : null;
    }

    public Task<CourseResponseDto?> CreatePhotoAsync(string token, string id, IBrowserFile file) =>
        SendPhotoAsync(HttpMethod.Post, token, id, file);

    public Task<CourseResponseDto?> UpdatePhotoAsync(string token, string id, IBrowserFile file) =>
        SendPhotoAsync(HttpMethod.Put, token, id, file);

    public Task<CourseResponseDto?> UploadPhotoAsync(string token, string id, IBrowserFile file) =>
        UpdatePhotoAsync(token, id, file);

    private async Task<CourseResponseDto?> SendPhotoAsync(
        HttpMethod method,
        string token,
        string id,
        IBrowserFile file)
    {
        using var request = CreateRequest(method, $"api/Courses/{Uri.EscapeDataString(id)}/photo", token);
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(20 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        request.Content = content;

        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        if (response.StatusCode == HttpStatusCode.NoContent)
            return new CourseResponseDto(id, string.Empty, string.Empty, null, null, 0, string.Empty, string.Empty, string.Empty, null);

        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return new CourseResponseDto(id, string.Empty, string.Empty, null, null, 0, string.Empty, string.Empty, string.Empty, null);

        return JsonSerializer.Deserialize<CourseResponseDto>(body);
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

    private static CoursePageResult ReadPage(JsonElement payload, int requestedPage, int requestedPageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var arrayItems = payload.Deserialize<List<CourseResponseDto>>() ?? [];
            return new CoursePageResult(arrayItems, arrayItems.Count, requestedPage, requestedPageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var items = itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.Deserialize<List<CourseResponseDto>>() ?? []
            : [];
        var page = ReadInt(payload, requestedPage, "page", "currentPage");
        var pageSize = ReadInt(payload, requestedPageSize, "pageSize", "currentPageSize");
        var totalCount = ReadInt(payload, items.Count, "totalCount", "totalItems", "count");
        return new CoursePageResult(items, totalCount, page, pageSize);
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
