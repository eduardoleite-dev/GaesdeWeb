using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class QuestionService
{
    private readonly ApiService api;
    public QuestionService(ApiService api) => this.api = api;

    public async Task<IReadOnlyList<QuestionResponseDto>> GetAllAsync(string token, string quizId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/Questions?Page=1&PageSize=100&quizId={Uri.EscapeDataString(quizId)}", token); response.EnsureSuccessStatusCode();
        return await ReadListAsync<QuestionResponseDto>(response);
    }
    public async Task<QuestionResponseDto?> CreateAsync(string token, QuestionRequestDto value) => await SendJsonAsync<QuestionResponseDto>(HttpMethod.Post, "api/Questions", token, value);
    public async Task<QuestionResponseDto?> UpdateAsync(string token, string id, QuestionUpdateRequestDto value) => await SendJsonAsync<QuestionResponseDto>(HttpMethod.Put, $"api/Questions/{id}", token, value);
    public async Task<bool> DeleteAsync(string token, string id) => (await SendAsync(HttpMethod.Delete, $"api/Questions/{id}", token)).IsSuccessStatusCode;
    private async Task<T?> SendJsonAsync<T>(HttpMethod method, string uri, string token, object body) { using var request = Create(method, uri, token); request.Content = JsonContent.Create(body); using var response = await api.Client.SendAsync(request); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default; }
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token) => await api.Client.SendAsync(Create(method, uri, token));
    private static HttpRequestMessage Create(HttpMethod method, string uri, string token) { var request = new HttpRequestMessage(method, uri); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return request; }
    private static async Task<IReadOnlyList<T>> ReadListAsync<T>(HttpResponseMessage response) { var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); if (payload.ValueKind == System.Text.Json.JsonValueKind.Array) return payload.Deserialize<List<T>>(JsonOptions) ?? []; foreach (var property in payload.EnumerateObject()) if (new[] { "items", "data", "results" }.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) return property.Value.Deserialize<List<T>>(JsonOptions) ?? []; return []; }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
}
