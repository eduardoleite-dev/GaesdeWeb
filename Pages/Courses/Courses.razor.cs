using System.Text.Json;
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
    [Inject] protected EnrollmentService EnrollmentsApi { get; set; } = default!;
    [Inject] protected CommentService CommentsApi { get; set; } = default!;
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
    protected bool CanManageEnrollments { get; private set; }
    protected bool showEnrollmentModal;
    protected CourseResponseDto? enrollmentCourse;
    protected IReadOnlyList<UserManagementDto> enrollmentStudents { get; private set; } = [];
    protected string enrollmentSearch = string.Empty;
    protected string? enrollmentMessage;
    protected bool enrollmentLoading;
    protected bool enrolling;
    protected bool showForumModal;
    protected CourseResponseDto? forumCourse;
    protected IReadOnlyList<JsonElement> forumComments { get; private set; } = [];
    protected string forumContent = string.Empty;
    protected bool forumLoading;
    protected bool forumPosting;
    protected string? forumMessage;
    private IReadOnlyDictionary<string, string> forumUserNamesById = new Dictionary<string, string>();
    protected bool showingEnrolledStudents;
    protected IReadOnlyList<UserManagementDto> enrolledStudents { get; private set; } = [];
    private HashSet<string> enrolledStudentIds { get; set; } = [];
    protected HashSet<string> selectedStudentIds { get; } = [];

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

    protected async Task OpenEnrollmentModalAsync(CourseResponseDto course)
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        if (!session.CanManageEnrollments)
            return;

        CanManageEnrollments = true;
        enrollmentCourse = course;
        enrollmentSearch = string.Empty;
        enrollmentMessage = null;
        showingEnrolledStudents = false;
        selectedStudentIds.Clear();
        showEnrollmentModal = true;
        await LoadEnrollmentStudentsAsync(session.Token);
    }

    protected async Task OpenForumAsync(CourseResponseDto course)
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        forumCourse = course;
        forumContent = string.Empty;
        forumMessage = null;
        showForumModal = true;
        await LoadForumAsync(session.Token);
    }

    protected void CloseForum()
    {
        showForumModal = false;
        forumCourse = null;
        forumComments = [];
        forumContent = string.Empty;
        forumMessage = null;
    }

    protected async Task PublishForumMessageAsync()
    {
        if (forumCourse is null || string.IsNullOrWhiteSpace(forumContent))
        {
            forumMessage = "Digite uma mensagem antes de publicar.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        forumPosting = true;
        forumMessage = null;
        try
        {
            var enrollmentPage = session.AccessLevel == AccessLevel.Student
                ? await EnrollmentsApi.GetMineAsync(session.Token, 1, 100, courseId: forumCourse.Id)
                : await EnrollmentsApi.GetAllAsync(session.Token, 1, 100, courseId: forumCourse.Id);
            var recipientIds = enrollmentPage.Items
                .Select(GetEnrollmentUserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(forumCourse.InstructorId))
                recipientIds.Add(forumCourse.InstructorId);

            var result = await CommentsApi.CreateAsync(
                session.Token,
                new CommentRequestDto(CommentType.Course.ToString(), forumContent.Trim(), recipientIds.ToArray(), forumCourse.Id));
            if (!result.Success)
                forumMessage = result.ErrorMessage ?? "Não foi possível publicar a mensagem.";
            else
            {
                forumContent = string.Empty;
                await LoadForumAsync(session.Token);
            }
        }
        catch (HttpRequestException)
        {
            forumMessage = "Não foi possível publicar no fórum.";
        }
        finally
        {
            forumPosting = false;
        }
    }

    protected void CloseEnrollmentModal()
    {
        showEnrollmentModal = false;
        enrollmentCourse = null;
        enrollmentMessage = null;
        enrolledStudents = [];
        enrolledStudentIds.Clear();
        selectedStudentIds.Clear();
    }

    protected async Task SearchEnrollmentStudentsAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        await LoadEnrollmentStudentsAsync(session.Token);
    }

    protected void ToggleStudent(string studentId)
    {
        if (!selectedStudentIds.Add(studentId))
            selectedStudentIds.Remove(studentId);
    }

    protected async Task EnrollSelectedStudentsAsync()
    {
        if (enrollmentCourse is null || selectedStudentIds.Count == 0)
        {
            enrollmentMessage = "Selecione pelo menos um aluno.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        if (!session.CanManageEnrollments)
        {
            enrollmentMessage = "Seu perfil não pode inscrever alunos em cursos.";
            return;
        }

        enrolling = true;
        enrollmentMessage = null;
        var enrolledCount = 0;
        var failedStudents = new List<string>();
        foreach (var studentId in selectedStudentIds.ToArray())
        {
            try
            {
                var result = await EnrollmentsApi.CreateAsync(
                    session.Token,
                    new EnrollmentRequestDto(enrollmentCourse.Id, studentId, null));
                if (result is null)
                    failedStudents.Add(studentId);
                else
                {
                    enrolledCount++;
                    selectedStudentIds.Remove(studentId);
                }
            }
            catch (HttpRequestException)
            {
                failedStudents.Add(studentId);
            }
        }

        enrollmentMessage = failedStudents.Count == 0
            ? $"{enrolledCount} aluno(s) inscrito(s) com sucesso."
            : $"{enrolledCount} aluno(s) inscrito(s). {failedStudents.Count} não puderam ser inscritos; verifique se já possuem matrícula ou as permissões.";
        await LoadEnrollmentStudentsAsync(session.Token);
        enrolling = false;
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
        CanManageEnrollments = session.CanManageEnrollments;

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
        CanManageEnrollments = session.CanManageEnrollments;
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
        CanManageEnrollments = session.CanManageEnrollments;
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

    private async Task LoadEnrollmentStudentsAsync(string token)
    {
        enrollmentLoading = true;
        enrollmentMessage = null;
        try
        {
            if (enrollmentCourse is null)
                return;

            var enrollmentsTask = EnrollmentsApi.GetAllAsync(token, 1, 100, courseId: enrollmentCourse.Id);
            var studentsTask = UsersApi.GetAllAsync(token, 1, 100, enrollmentSearch, AccessLevel.Student);
            await Task.WhenAll(enrollmentsTask, studentsTask);

            enrolledStudentIds = (await enrollmentsTask).Items
                .Select(GetEnrollmentUserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);

            var result = await studentsTask;
            var students = result.Items
                .Where(student => student.AccessLevel == AccessLevel.Student)
                .ToArray();
            enrolledStudents = students
                .Where(student => enrolledStudentIds.Contains(student.Id))
                .ToArray();
            enrollmentStudents = students
                .Where(student => !enrolledStudentIds.Contains(student.Id))
                .ToArray();
        }
        catch (HttpRequestException)
        {
            enrollmentMessage = "Não foi possível carregar os alunos. Verifique sua permissão para criar matrículas.";
            enrollmentStudents = [];
        }
        finally
        {
            enrollmentLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadForumAsync(string token)
    {
        if (forumCourse is null)
            return;

        forumLoading = true;
        try
        {
            forumComments = (await CommentsApi.GetCourseAsync(token, forumCourse.Id)).Items;
            try
            {
                forumUserNamesById = (await UsersApi.GetAllAsync(token, 1, 100)).Items
                    .ToDictionary(user => user.Id, user => user.Name, StringComparer.Ordinal);
            }
            catch (HttpRequestException)
            {
                forumUserNamesById = new Dictionary<string, string>();
            }
        }
        catch (HttpRequestException)
        {
            forumMessage = "Não foi possível carregar as mensagens do fórum.";
            forumComments = [];
        }
        finally
        {
            forumLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected static string CommentText(JsonElement comment) =>
        comment.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? string.Empty
            : string.Empty;

    protected string CommentAuthor(JsonElement comment)
    {
        var name = GetCommentPersonName(comment);
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        var authorId = GetCommentPersonId(comment);
        return authorId is not null && forumUserNamesById.TryGetValue(authorId, out var authorName)
            ? authorName
            : "Usuário";
    }

    private static string? GetCommentPersonName(JsonElement comment)
    {
        foreach (var propertyName in new[] { "userName", "authorName", "senderName", "creatorName", "createdByName", "createdByUserName" })
        {
            if (comment.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        }

        foreach (var propertyName in new[] { "user", "author", "sender", "creator", "createdBy" })
        {
            if (!comment.TryGetProperty(propertyName, out var person) || person.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var nameProperty in new[] { "name", "fullName", "userName", "displayName", "username" })
            {
                if (person.TryGetProperty(nameProperty, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
            }
        }

        return null;
    }

    private static string? GetCommentPersonId(JsonElement comment)
    {
        foreach (var propertyName in new[] { "userId", "senderId", "authorId", "creatorId", "createdById", "createdBy" })
        {
            if (comment.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        }

        foreach (var propertyName in new[] { "user", "author", "sender", "creator", "createdBy" })
        {
            if (!comment.TryGetProperty(propertyName, out var person) || person.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var idProperty in new[] { "id", "userId" })
            {
                if (person.TryGetProperty(idProperty, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
            }
        }

        return null;
    }

    protected static string CommentDate(JsonElement comment) =>
        comment.TryGetProperty("createdAt", out var createdAt) && createdAt.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(createdAt.GetString(), out var date)
                ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : string.Empty;

    private static string? GetEnrollmentUserId(JsonElement enrollment)
    {
        if (enrollment.ValueKind != JsonValueKind.Object)
            return null;

        return enrollment.TryGetProperty("userId", out var userId) && userId.ValueKind == JsonValueKind.String
            ? userId.GetString()
            : null;
    }
}
