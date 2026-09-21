using GaesdeWeb.Models;
using GaesdeWeb.Services;
using Microsoft.AspNetCore.Components;

namespace GaesdeWeb.Pages.Students;

public partial class StudentsPage : ComponentBase
{
    [Inject] protected UserService UsersApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<UserManagementDto> Students { get; private set; } = [];
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected bool loading;
    protected bool saving;
    protected bool showForm;
    protected string? editingId;
    protected string name = string.Empty;
    protected string email = string.Empty;
    protected string password = string.Empty;
    protected string bio = string.Empty;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;

    private const int PageSize = 10;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
    }

    protected async Task SearchAsync()
    {
        Page = 1;
        await LoadStudentsAsync();
    }

    protected void OpenCreateForm()
    {
        editingId = null;
        name = string.Empty;
        email = string.Empty;
        password = string.Empty;
        bio = string.Empty;
        formMessage = null;
        showForm = true;
    }

    protected void OpenEditForm(UserManagementDto student)
    {
        if (student.AccessLevel != AccessLevel.Student)
            return;

        editingId = student.Id;
        name = student.Name;
        email = student.Email;
        password = string.Empty;
        bio = student.Bio ?? string.Empty;
        formMessage = null;
        showForm = true;
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
        var student = new UserRequestDto(name.Trim(), email.Trim(),
            string.IsNullOrWhiteSpace(password) ? null : password,
            AccessLevel.Student, null, string.IsNullOrWhiteSpace(bio) ? null : bio.Trim());
        try
        {
            var result = editingId is null
                ? await UsersApi.CreateAsync(session.Token, student)
                : await UsersApi.UpdateAsync(session.Token, editingId, student);

            if (result is null)
            {
                formMessage = "Não foi possível salvar o aluno.";
                return;
            }

            CloseForm();
            await LoadStudentsAsync(session.Token);
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

    protected async Task DeleteAsync(UserManagementDto student)
    {
        if (student.AccessLevel != AccessLevel.Student)
            return;

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        loading = true;
        try
        {
            if (!await UsersApi.DeleteAsync(session.Token, student.Id))
                errorMessage = "Não foi possível excluir o aluno.";
            else
                await LoadStudentsAsync(session.Token);
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
        await LoadStudentsAsync();
    }

    protected async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await LoadStudentsAsync();
    }

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        if (!session.CanManageStudents)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        await LoadStudentsAsync(session.Token);
    }

    private async Task LoadStudentsAsync(string? token = null)
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
            var result = await UsersApi.GetAllAsync(sessionToken, Page, PageSize, search, AccessLevel.Student);
            Students = result.Items.Where(student => student.AccessLevel == AccessLevel.Student).ToArray();
            TotalPages = result.TotalPages;
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os alunos.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}