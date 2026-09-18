using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class QuizService
{
    private readonly ApiService api;
    public QuizService(ApiService api) => this.api = api;

    public async Task<IReadOnlyList<QuizResponseDto>> GetAllAsync(string token)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/Quizzes?Page=1&PageSize=100", token);
        response.EnsureSuccessStatusCode();
        return await ReadListAsync<QuizResponseDto>(response);
    }

    public async Task<QuizResponseDto?> CreateAsync(string token, QuizRequestDto value) => await SendJsonAsync<QuizResponseDto>(HttpMethod.Post, "api/Quizzes", token, value);
    public async Task<QuizResponseDto?> UpdateAsync(string token, string id, QuizUpdateRequestDto value) => await SendJsonAsync<QuizResponseDto>(HttpMethod.Put, $"api/Quizzes/{id}", token, value);
    public async Task<bool> DeleteAsync(string token, string id) => (await SendAsync(HttpMethod.Delete, $"api/Quizzes/{id}", token)).IsSuccessStatusCode;

    private async Task<T?> SendJsonAsync<T>(HttpMethod method, string uri, string token, object body)
    {
        using var request = Create(method, uri, token); request.Content = JsonContent.Create(body);
        using var response = await api.Client.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token) => await api.Client.SendAsync(Create(method, uri, token));
    private static HttpRequestMessage Create(HttpMethod method, string uri, string token) { var request = new HttpRequestMessage(method, uri); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return request; }

    private static async Task<IReadOnlyList<T>> ReadListAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        if (payload.ValueKind == System.Text.Json.JsonValueKind.Array) return payload.Deserialize<List<T>>() ?? [];
        foreach (var name in new[] { "items", "data", "results" }) if (payload.TryGetProperty(name, out var value)) return value.Deserialize<List<T>>() ?? [];
        return [];
    }
}
