using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class QuizAttemptService
{
    private readonly ApiService api;

    public QuizAttemptService(ApiService api) => this.api = api;

    public async Task<JsonElement?> StartAsync(string token, StartQuizAttemptRequestDto request) =>
        await SendJsonAsync(HttpMethod.Post, "api/quiz-attempts", token, request);

    public async Task<bool> SendAnswerAsync(string token, UserAnswerRequestDto answer) =>
        (await SendJsonAsync(HttpMethod.Post, "api/UserAnswers", token, answer)).HasValue;

    public async Task<JsonElement?> FinishAsync(string token, string attemptId) =>
        await SendAsync(HttpMethod.Patch, $"api/quiz-attempts/{Uri.EscapeDataString(attemptId)}/finish", token);

    private async Task<JsonElement?> SendJsonAsync(HttpMethod method, string uri, string token, object body)
    {
        using var request = CreateRequest(method, uri, token);
        request.Content = JsonContent.Create(body);
        using var response = await api.Client.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private async Task<JsonElement?> SendAsync(HttpMethod method, string uri, string token)
    {
        using var response = await api.Client.SendAsync(CreateRequest(method, uri, token));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}