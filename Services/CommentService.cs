using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class CommentService
{
    private readonly ApiService api;

    public CommentService(ApiService api) => this.api = api;

    public Task<CommentPageResult> GetCourseAsync(string token, string courseId, int page = 1, int pageSize = 100) =>
        GetPageAsync(token, $"api/Comments?Page={page}&PageSize={pageSize}&courseId={Uri.EscapeDataString(courseId)}&type=Course", page, pageSize);

    public Task<CommentPageResult> GetChatAsync(string token, int page = 1, int pageSize = 100) =>
        GetPageAsync(token, $"api/Comments?Page={page}&PageSize={pageSize}&type=Chat", page, pageSize);

    public Task<CommentMutationResult> CreateAsync(string token, CommentRequestDto comment) =>
        SendCommentAsync(HttpMethod.Post, "api/Comments", token, comment);

    public Task<JsonElement?> UpdateAsync(string token, string id, string content) =>
        SendJsonAsync(HttpMethod.Put, $"api/Comments/{Uri.EscapeDataString(id)}", token, new { content, recipientIds = (string[]?)null, attachments = (object[]?)null });

    public async Task<bool> DeleteAsync(string token, string id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/Comments/{Uri.EscapeDataString(id)}", token);
        return response.IsSuccessStatusCode;
    }

    private async Task<CommentPageResult> GetPageAsync(string token, string uri, int page, int pageSize)
    {
        using var response = await SendAsync(HttpMethod.Get, uri, token);
        response.EnsureSuccessStatusCode();
        return ReadPage(await response.Content.ReadFromJsonAsync<JsonElement>(), page, pageSize);
    }

    private async Task<JsonElement?> SendJsonAsync(HttpMethod method, string uri, string token, object body)
    {
        using var request = CreateRequest(method, uri, token);
        request.Content = JsonContent.Create(body);
        using var response = await api.Client.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private async Task<CommentMutationResult> SendCommentAsync(HttpMethod method, string uri, string token, object body)
    {
        using var request = CreateRequest(method, uri, token);
        request.Content = JsonContent.Create(body);
        using var response = await api.Client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return new CommentMutationResult(true, null, null);

            return new CommentMutationResult(true, JsonSerializer.Deserialize<JsonElement>(responseText), null);
        }

        return new CommentMutationResult(false, null, ReadErrorMessage(responseText, response.StatusCode));
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token) =>
        api.Client.SendAsync(CreateRequest(method, uri, token));

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static CommentPageResult ReadPage(JsonElement payload, int page, int pageSize)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var items = payload.EnumerateArray().ToArray();
            return new CommentPageResult(items, items.Length, page, pageSize);
        }

        var itemsElement = FindProperty(payload, "items", "data", "results");
        var itemsFromPage = itemsElement.ValueKind == JsonValueKind.Array ? itemsElement.EnumerateArray().ToArray() : [];
        return new CommentPageResult(itemsFromPage, ReadInt(payload, itemsFromPage.Length, "totalItems", "totalCount", "count"), ReadInt(payload, page, "page", "currentPage"), ReadInt(payload, pageSize, "pageSize", "currentPageSize"));
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

    private static string ReadErrorMessage(string responseText, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                var error = JsonSerializer.Deserialize<JsonElement>(responseText);
                foreach (var name in new[] { "detail", "title", "message" })
                    if (error.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
                        return property.GetString() ?? responseText;
            }
            catch (JsonException)
            {
                return responseText;
            }
        }

        return $"A API recusou a mensagem ({(int)statusCode}).";
    }
}