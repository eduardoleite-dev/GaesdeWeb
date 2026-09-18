using Microsoft.AspNetCore.Components;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages.Modules;

public partial class ModulesPage : ComponentBase
{
    [Inject] protected ModuleService ModulesApi { get; set; } = default!;
    [Inject] protected CourseService CoursesApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<ModuleResponseDto> Modules { get; private set; } = [];
    protected IReadOnlyList<CourseResponseDto> Courses { get; private set; } = [];
    protected string? selectedCourseId;
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected bool loading;
    protected bool saving;
    protected bool showForm;
    protected string? editingId;
    protected string title = string.Empty;
    protected string description = string.Empty;
    protected int orderIndex;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;
    protected int TotalCount { get; private set; }
    protected bool CanManageModules { get; private set; }

    private const int PageSize = 10;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
    }

    protected async Task SelectCourseAsync(ChangeEventArgs args)
    {
        selectedCourseId = args.Value?.ToString();
        Page = 1;
        await LoadModulesAsync();
    }

    protected async Task SearchAsync()
    {
        Page = 1;
        await LoadModulesAsync();
    }

    protected void OpenCreateForm()
    {
        editingId = null;
        title = string.Empty;
        description = string.Empty;
        orderIndex = Modules.Count;
        formMessage = null;
        showForm = true;
    }

    protected void OpenEditForm(ModuleResponseDto module)
    {
        editingId = module.Id;
        title = module.Title;
        description = module.Description ?? string.Empty;
        orderIndex = module.OrderIndex;
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
        if (string.IsNullOrWhiteSpace(selectedCourseId))
        {
            formMessage = "Selecione um curso antes de salvar o módulo.";
            return;
        }

        if (string.IsNullOrWhiteSpace(title) || orderIndex < 0)
        {
            formMessage = "Informe o título e uma ordem válida.";
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
        try
        {
            var result = editingId is null
                ? await ModulesApi.CreateAsync(session.Token, new ModuleRequestDto(
                    selectedCourseId, title.Trim(), EmptyToNull(description), orderIndex))
                : await ModulesApi.UpdateAsync(session.Token, editingId, new ModuleUpdateRequestDto(
                    title.Trim(), EmptyToNull(description), orderIndex));

            if (result is null)
            {
                formMessage = "Não foi possível salvar o módulo. Verifique o curso, a ordem e suas permissões.";
                return;
            }

            CloseForm();
            await LoadModulesAsync(session.Token);
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

    protected async Task DeleteAsync(ModuleResponseDto module)
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
            if (!await ModulesApi.DeleteAsync(session.Token, module.Id))
                errorMessage = "Não foi possível excluir o módulo.";
            else
                await LoadModulesAsync(session.Token);
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
        await LoadModulesAsync();
    }

    protected async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await LoadModulesAsync();
    }

    protected string CourseName(string courseId) =>
        Courses.FirstOrDefault(course => course.Id == courseId)?.Title ?? "Curso não encontrado";

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        CanManageModules = session.CanManageUsers;
        loading = true;
        try
        {
            var courses = await CoursesApi.GetAllAsync(session.Token, 1, 100);
            Courses = courses.Items;
            selectedCourseId ??= Courses.FirstOrDefault()?.Id;
            await LoadModulesAsync(session.Token);
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os cursos e módulos.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadModulesAsync(string? token = null)
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
            var result = await ModulesApi.GetAllAsync(sessionToken, selectedCourseId, Page, PageSize, search);
            Modules = result.Items;
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os módulos.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
