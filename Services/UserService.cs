using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace GaesdeWeb.Services;

public sealed class UserService
{
    private const AccessLevel ProfessorAccessLevel = AccessLevel.Professor;
    private readonly ApiService api;

    public UserService(ApiService api) => this.api = api;

    public async Task<UserPageResult> GetAllAsync(string token, int page = 1, int pageSize = 10, string? search = null, AccessLevel? accessLevel = null)
    {
        var query = $"api/Users?Page={page}&PageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search.Trim())}";
        if (accessLevel is not null)
            query += $"&accessLevel={(int)accessLevel.Value}";

        using var response = await SendAsync(HttpMethod.Get, query, token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return ReadPage(payload, page, pageSize);
    }

    public async Task<UserManagementDto?> CreateAsync(string token, UserRequestDto user)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/Users", token, user);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserManagementDto>();
    }

    public async Task<UserManagementDto?> UpdateAsync(string token, string id, UserRequestDto user)
    {
        using var response = await SendJsonAsync(HttpMethod.Put, $"api/Users/{Uri.EscapeDataString(id)}", token, user);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserManagementDto>();
    }

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/Users/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    public async Task<UserManagementDto?> UploadPhotoAsync(string token, string id, IBrowserFile file)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/Users/{Uri.EscapeDataString(id)}/photo", token);
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(20 * 1024 * 1024);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        request.Content = content;

        using var response = await api.Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserManagementDto>();
    }

    public async Task<IReadOnlyList<UserOptionDto>> GetProfessorsAsync(string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/Users?Page=1&PageSize=100&accessLevel={(int)ProfessorAccessLevel}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await api.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.ValueKind == JsonValueKind.Array
            ? payload.Deserialize<List<UserOptionDto>>() ?? []
            : FindProperty(payload, "items", "data", "results").Deserialize<List<UserOptionDto>>() ?? [];

        return items.Where(user => user.AccessLevel == ProfessorAccessLevel).ToArray();
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

    private static UserPageResult ReadPage(JsonElement payload, int requestedPage, int requestedPageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var arrayItems = payload.Deserialize<List<UserManagementDto>>() ?? [];
            return new UserPageResult(arrayItems, arrayItems.Count, requestedPage, requestedPageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var items = itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.Deserialize<List<UserManagementDto>>() ?? []
            : [];
        var page = ReadInt(payload, requestedPage, "page", "currentPage");
        var pageSize = ReadInt(payload, requestedPageSize, "pageSize", "currentPageSize");
        var totalCount = ReadInt(payload, items.Count, "totalCount", "totalItems", "count");
        return new UserPageResult(items, totalCount, page, pageSize);
    }

    private static int ReadInt(JsonElement element, int fallback, params string[] names)
    {
        var property = FindProperty(element, names);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : fallback;
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
}
