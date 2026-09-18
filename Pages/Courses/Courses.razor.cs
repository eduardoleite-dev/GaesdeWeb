using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages.Courses;

public partial class CoursesPage : ComponentBase
{
    [Inject] protected CourseService CoursesApi { get; set; } = default!;
    [Inject] protected CategoryService CategoriesApi { get; set; } = default!;
    [Inject] protected UserService UsersApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<CourseResponseDto> Courses { get; private set; } = [];
    protected IReadOnlyList<CategoryResponseDto> Categories { get; private set; } = [];
    protected IReadOnlyList<UserOptionDto> Professors { get; private set; } = [];
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected bool loading;
    protected bool saving;
    protected IBrowserFile? selectedCover;
    protected string? selectedCoverName;
    protected bool showForm;
    protected string? editingId;
    protected string title = string.Empty;
    protected string slug = string.Empty;
    protected string level = "Beginner";
    protected decimal price;
    protected string description = string.Empty;
    protected string coverImage = string.Empty;
    protected string categoryId = string.Empty;
    protected string instructorId = string.Empty;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;
    protected int TotalCount { get; private set; }
    protected bool CanManageCourses { get; private set; }

    private const int PageSize = 10;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
    }

    protected async Task SearchAsync()
    {
        Page = 1;
        await LoadCoursesAsync();
    }

    protected void OpenCreateForm()
    {
        editingId = null;
        title = string.Empty;
        slug = string.Empty;
        level = "Beginner";
        price = 0;
        description = string.Empty;
        coverImage = string.Empty;
        selectedCover = null;
        selectedCoverName = null;
        categoryId = Categories.FirstOrDefault()?.Id ?? string.Empty;
        instructorId = Professors.FirstOrDefault()?.Id ?? string.Empty;
        formMessage = null;
        showForm = true;
    }

    protected void OpenEditForm(CourseResponseDto course)
    {
        editingId = course.Id;
        title = course.Title;
        slug = course.Slug;
        level = course.Level;
        price = course.Price;
        description = course.Description ?? string.Empty;
        coverImage = course.CoverImage ?? string.Empty;
        selectedCover = null;
        selectedCoverName = null;
        categoryId = course.CategoryId ?? string.Empty;
        instructorId = course.InstructorId;
        formMessage = null;
        showForm = true;
    }

    protected void SelectCover(InputFileChangeEventArgs args)
    {
        selectedCover = args.File;
        selectedCoverName = selectedCover.Name;
        formMessage = null;
    }

    protected void CloseForm()
    {
        showForm = false;
        formMessage = null;
    }

    protected async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug) ||
            string.IsNullOrWhiteSpace(categoryId) || string.IsNullOrWhiteSpace(instructorId))
        {
            formMessage = "Preencha título, slug, categoria e professor.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        CanManageCourses = session.CanManageUsers;

        saving = true;
        formMessage = null;
        var course = new CourseRequestDto(
            title.Trim(), slug.Trim(), level, price, description.Trim(),
            string.IsNullOrWhiteSpace(coverImage) ? null : coverImage.Trim(), categoryId, instructorId);
        try
        {
            var result = editingId is null
                ? await CoursesApi.CreateAsync(session.Token, course)
                : await CoursesApi.UpdateAsync(session.Token, editingId, course);

            if (result is null)
            {
                formMessage = "Não foi possível salvar o curso. Verifique as permissões e os dados informados.";
                return;
            }

            if (selectedCover is not null)
            {
                var photoResult = editingId is null
                    ? await CoursesApi.CreatePhotoAsync(session.Token, result.Id, selectedCover)
                    : await CoursesApi.UpdatePhotoAsync(session.Token, result.Id, selectedCover);
                if (photoResult is null)
                {
                    editingId = result.Id;
                    showForm = true;
                    formMessage = "Curso salvo, mas não foi possível enviar a capa.";
                    return;
                }

                coverImage = await CoursesApi.GetPhotoAsync(session.Token, result.Id) ?? coverImage;
            }

            CloseForm();
            await LoadCoursesAsync();
        }
        catch (HttpRequestException)
        {
            formMessage = "A API não autorizou esta operação. Administradores e professores precisam estar liberados no endpoint de cursos.";
        }
        finally
        {
            saving = false;
        }
    }

    protected async Task DeleteAsync(CourseResponseDto course)
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        CanManageCourses = session.CanManageUsers;
        loading = true;
        try
        {
            if (!await CoursesApi.DeleteAsync(session.Token, course.Id))
                errorMessage = "Não foi possível excluir o curso.";
            else
                await LoadCoursesAsync();
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
        await LoadCoursesAsync();
    }

    protected async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await LoadCoursesAsync();
    }

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        CanManageCourses = session.CanManageUsers;
        loading = true;
        try
        {
            var categoriesTask = CategoriesApi.GetAllAsync(session.Token, 1, 100);
            var professorsTask = UsersApi.GetProfessorsAsync(session.Token);
            await Task.WhenAll(categoriesTask, professorsTask);
            Categories = (await categoriesTask).Items;
            Professors = await professorsTask;
            await LoadCoursesAsync(session.Token);
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os cursos, categorias e professores.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadCoursesAsync(string? token = null)
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
            var result = await CoursesApi.GetAllAsync(sessionToken, Page, PageSize, search);
            Courses = result.Items;
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar os cursos.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
