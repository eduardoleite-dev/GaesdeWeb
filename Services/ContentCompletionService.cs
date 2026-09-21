using System.Net.Http.Headers;
using System.Text.Json;

namespace GaesdeWeb.Services;

public sealed class ContentCompletionService
{
    private readonly ApiService api;

    public ContentCompletionService(ApiService api) => this.api = api;

    public async Task<JsonElement?> GetCourseProgressAsync(string token, string courseId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/content-completions/course/{Uri.EscapeDataString(courseId)}/progress", token);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<bool> CompleteAsync(string token, string contentId)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/content-completions/{Uri.EscapeDataString(contentId)}", token);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UncompleteAsync(string token, string contentId)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"api/content-completions/{Uri.EscapeDataString(contentId)}", token);
        return response.IsSuccessStatusCode;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return api.Client.SendAsync(request);
    }
}