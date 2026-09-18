using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages.Users;

public partial class UsersPage : ComponentBase
{
    [Inject] protected UserService UsersApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<UserManagementDto> Users { get; private set; } = [];
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected bool loading;
    protected bool saving;
    protected IBrowserFile? selectedAvatar;
    protected string? selectedAvatarName;
    protected bool showForm;
    protected string? editingId;
    protected string name = string.Empty;
    protected string email = string.Empty;
    protected string password = string.Empty;
    protected int accessLevel = 3;
    protected string avatarUrl = string.Empty;
    protected string bio = string.Empty;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;
    protected int TotalCount { get; private set; }

    private const int PageSize = 10;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
    }

    protected async Task SearchAsync()
    {
        Page = 1;
        await LoadUsersAsync();
    }

    protected void OpenCreateForm()
    {
        editingId = null;
        name = string.Empty;
        email = string.Empty;
        password = string.Empty;
        accessLevel = 3;
        avatarUrl = string.Empty;
        selectedAvatar = null;
        selectedAvatarName = null;
        bio = string.Empty;
        formMessage = null;
        showForm = true;
    }

    protected void OpenEditForm(UserManagementDto user)
    {
        editingId = user.Id;
        name = user.Name;
        email = user.Email;
        password = string.Empty;
        accessLevel = user.AccessLevel;
        avatarUrl = user.AvatarUrl ?? string.Empty;
        selectedAvatar = null;
        selectedAvatarName = null;
        bio = user.Bio ?? string.Empty;
        formMessage = null;
        showForm = true;
    }

    protected void SelectAvatar(InputFileChangeEventArgs args)
    {
        selectedAvatar = args.File;
        selectedAvatarName = selectedAvatar.Name;
        formMessage = null;
    }

    protected void CloseForm()
    {
        showForm = false;
        formMessage = null;
    }

    protected async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
            (editingId is null && string.IsNullOrWhiteSpace(password)))
        {
            formMessage = editingId is null
                ? "Preencha nome, e-mail e senha."
                : "Preencha nome e e-mail. A senha é opcional na edição.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        saving = true;
        formMessage = null;
        var user = new UserRequestDto(
            name.Trim(), email.Trim(), string.IsNullOrWhiteSpace(password) ? null : password,
            accessLevel, string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
            string.IsNullOrWhiteSpace(bio) ? null : bio.Trim());
        try
        {
            var result = editingId is null
                ? await UsersApi.CreateAsync(session.Token, user)
                : await UsersApi.UpdateAsync(session.Token, editingId, user);

            if (result is null)
            {
                formMessage = "Não foi possível salvar o usuário.";
                return;
            }

            if (selectedAvatar is not null)
            {
                var photoResult = await UsersApi.UploadPhotoAsync(session.Token, result.Id, selectedAvatar);
                if (photoResult is null)
                {
                    editingId = result.Id;
                    showForm = true;
                    formMessage = "Usuário salvo, mas não foi possível enviar o avatar.";
                    return;
                }
            }

            CloseForm();
            await LoadUsersAsync(session.Token);
        }
        catch (HttpRequestException)
        {
            formMessage = "Não foi possível conectar à API.";
        }
        finally
        {
            saving = false;
        }
    }

    protected async Task DeleteAsync(UserManagementDto user)
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        loading = true;
        try
        {
            if (!await UsersApi.DeleteAsync(session.Token, user.Id))
                errorMessage = "Não foi possível excluir o usuário.";
            else
                await LoadUsersAsync(session.Token);
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível conectar à API.";
        }
        finally
        {
            loading = false;
        }
    }

    protected async Task PreviousPageAsync()
    {
        if (Page <= 1) return;
        Page--;
        await LoadUsersAsync();
    }

    protected async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await LoadUsersAsync();
    }

    protected static string AccessLevelName(int value) => value switch
    {
        0 => "Administrador",
        2 => "Professor",
        3 => "Aluno",
        4 => "Vendedor",
        _ => "Desconhecido"
    };

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        if (!session.CanManageUsers)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        await LoadUsersAsync(session.Token);
    }

    private async Task LoadUsersAsync(string? token = null)
    {
        var sessionToken = token ?? (await Session.GetAsync())?.Token;
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        loading = true;
        errorMessage = null;
        try
        {
            var result = await UsersApi.GetAllAsync(sessionToken, Page, PageSize, search);
            Users = result.Items;
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os usuários.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
