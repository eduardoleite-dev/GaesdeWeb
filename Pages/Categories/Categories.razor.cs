using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages.Categories;

public partial class CategoriesPage : ComponentBase
{
    [Inject] protected CategoryService CategoriesApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<CategoryResponseDto> Categories { get; private set; } = [];
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected string categoryName = string.Empty;
    protected string? editingId;
    protected bool showForm;
    protected bool loading;
    protected bool saving;
    protected IBrowserFile? selectedImage;
    protected string? selectedImageName;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;
    protected int TotalCount { get; private set; }

    private const int PageSize = 12;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
    }

    protected async Task SearchAsync()
    {
        Page = 1;
        await LoadAsync();
    }

    protected void OpenCreateForm()
    {
        editingId = null;
        categoryName = string.Empty;
        selectedImage = null;
        selectedImageName = null;
        formMessage = null;
        showForm = true;
    }

    protected void OpenEditForm(CategoryResponseDto category)
    {
        editingId = category.Id;
        categoryName = category.Name;
        selectedImage = null;
        selectedImageName = null;
        formMessage = null;
        showForm = true;
    }

    protected void SelectImage(InputFileChangeEventArgs args)
    {
        selectedImage = args.File;
        selectedImageName = selectedImage.Name;
        formMessage = null;
    }

    protected void CloseForm()
    {
        showForm = false;
        formMessage = null;
    }

    protected async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            formMessage = "Informe o nome da categoria.";
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
                ? await CategoriesApi.CreateAsync(session.Token, categoryName.Trim())
                : await CategoriesApi.UpdateAsync(session.Token, editingId, categoryName.Trim());

            if (result is null)
            {
                formMessage = "Não foi possível salvar a categoria.";
                return;
            }

            if (selectedImage is not null)
            {
                var photoResult = await CategoriesApi.UploadPhotoAsync(session.Token, result.Id, selectedImage);
                if (photoResult is null)
                {
                    editingId = result.Id;
                    showForm = true;
                    formMessage = "Categoria salva, mas não foi possível enviar a imagem.";
                    return;
                }
            }

            CloseForm();
            await LoadAsync();
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

    protected async Task DeleteAsync(CategoryResponseDto category)
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        loading = true;
        errorMessage = null;
        try
        {
            if (!await CategoriesApi.DeleteAsync(session.Token, category.Id))
            {
                errorMessage = "Não foi possível excluir a categoria.";
                return;
            }

            await LoadAsync();
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
        if (Page <= 1)
            return;

        Page--;
        await LoadAsync();
    }

    protected async Task NextPageAsync()
    {
        if (Page >= TotalPages)
            return;

        Page++;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        loading = true;
        errorMessage = null;
        try
        {
            var result = await CategoriesApi.GetAllAsync(session.Token, Page, PageSize, search);
            Categories = result.Items;
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar as categorias.";
        }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
