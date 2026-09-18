using System.Text.Json;
using Microsoft.JSInterop;
using GaesdeWeb.Models;

namespace GaesdeWeb.Services;

public sealed class SessionService
{
    private const string StorageKey = "arquiteturaBaseWeb.session";
    private readonly IJSRuntime js;

    public SessionService(IJSRuntime js) => this.js = js;

    public async Task<SessionData?> GetAsync()
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var session = JsonSerializer.Deserialize<SessionData>(json);
        if (session is null || session.IsExpired)
        {
            await ClearAsync();
            return null;
        }

        return session;
    }

    public Task SetAsync(SessionData session) =>
        js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, JsonSerializer.Serialize(session)).AsTask();

    public Task ClearAsync() => js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey).AsTask();
}