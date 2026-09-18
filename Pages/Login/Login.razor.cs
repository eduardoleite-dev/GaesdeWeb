using Microsoft.AspNetCore.Components;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages;

public partial class LoginPage : ComponentBase
{
    [Inject] protected AuthService Auth { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected string username = string.Empty;
    protected string password = string.Empty;
    protected string? errorMessage;
    protected bool loading;
    protected string BannerEyebrow { get; set; } = "GAMES · ESTUDO · DESENHO";
    protected string BannerTitle { get; set; } = "Toda ideia pode virar";
    protected string BannerHighlight { get; set; } = "uma nova descoberta.";
    protected string BannerDescription { get; set; } = "Jogue, estude e desenhe enquanto transforma curiosidade em novas possibilidades.";

    protected async Task HandleLoginAsync()
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Informe usuário e senha.";
            return;
        }

        loading = true;
        try
        {
            if (await Auth.LoginAsync(username, password))
                Navigation.NavigateTo("/");
            else
                errorMessage = "Usuário ou senha inválidos.";
        }
        catch
        {
            errorMessage = "Não foi possível conectar à API.";
        }
        finally
        {
            loading = false;
        }
    }
}