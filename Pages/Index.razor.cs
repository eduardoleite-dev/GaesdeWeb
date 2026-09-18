using Microsoft.AspNetCore.Components;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages;

public partial class IndexPage : ComponentBase
{
    [Inject] protected ApiService Api { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected bool publicLoading;
    protected bool privateLoading;
    protected string? publicMessage;
    protected string? privateMessage;
    protected SessionData? SessionData { get; private set; }
    protected bool HasSession => SessionData is not null;
    protected string DisplayName => SessionData?.DisplayName ?? "visitante";
    protected string UserInitial => DisplayName[..1].ToUpperInvariant();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        SessionData = await Session.GetAsync();
        await LoadPublicAsync();
        StateHasChanged();
    }

    protected async Task LoadPublicAsync()
    {
        publicLoading = true;
        publicMessage = await Api.GetPublicMessageAsync();
        publicLoading = false;
    }

    protected async Task LoadPrivateAsync()
    {
        SessionData ??= await Session.GetAsync();
        if (SessionData is null)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        privateLoading = true;
        privateMessage = await Api.GetPrivateMessageAsync(SessionData.Token);
        privateLoading = false;
    }

    protected async Task LogoutAsync()
    {
        await Session.ClearAsync();
        Navigation.NavigateTo("/login", forceLoad: true);
    }
}