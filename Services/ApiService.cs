using System.Net.Http.Headers;
using System.Net.Http.Json;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class ApiService
{
    public HttpClient Client { get; }

    public ApiService(HttpClient client) => Client = client;

    public async Task<string> GetPublicMessageAsync()
    {
        try
        {
            var result = await Client.GetFromJsonAsync<HelloWorldResponse>("api/HelloWorld/publico");
            return result?.Message ?? "A API não retornou uma mensagem.";
        }
        catch
        {
            return "Não foi possível consultar a API pública.";
        }
    }

    public async Task<string> GetPrivateMessageAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/HelloWorld/privado");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return $"A API recusou a chamada ({(int)response.StatusCode}).";

            var result = await response.Content.ReadFromJsonAsync<HelloWorldResponse>();
            return result is null ? "A API não retornou uma mensagem." : $"{result.Message} {result.MongoStatus}";
        }
        catch
        {
            return "Não foi possível consultar a API protegida.";
        }
    }
}