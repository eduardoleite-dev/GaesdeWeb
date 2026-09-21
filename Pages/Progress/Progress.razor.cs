using System.Text.Json;
using GaesdeWeb.Models;
using GaesdeWeb.Services;
using Microsoft.AspNetCore.Components;

namespace GaesdeWeb.Pages.Progress;

public partial class ProgressPage : ComponentBase
{
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected EnrollmentService EnrollmentsApi { get; set; } = default!;
    [Inject] protected CourseService CoursesApi { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<CourseResponseDto> courses { get; private set; } = [];
    protected IReadOnlyList<JsonElement> enrollments { get; private set; } = [];
    protected bool loading;
    protected string? errorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        var session = await Session.GetAsync();
        if (session is null) { Navigation.NavigateTo("/login", true); return; }
        if (session.AccessLevel != AccessLevel.Student) { Navigation.NavigateTo("/", true); return; }

        loading = true;
        try
        {
            var courseResult = await CoursesApi.GetAllAsync(session.Token, 1, 100);
            // Para alunos, a API já aplica a regra de acesso à listagem de cursos.
            courses = courseResult.Items.ToArray();

            try
            {
                var enrollmentResult = await EnrollmentsApi.GetMineAsync(session.Token, 1, 100);
                enrollments = enrollmentResult.Items;
            }
            catch (Exception)
            {
                // O progresso inicial continua útil mesmo quando a matrícula vem sem o envelope esperado.
                enrollments = [];
            }
        }
        catch (HttpRequestException) { errorMessage = "Não foi possível carregar sua trilha."; }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void OpenCourse(string courseId) => Navigation.NavigateTo($"/learn/course/{Uri.EscapeDataString(courseId)}");

    protected int ProgressFor(string courseId)
    {
        var enrollment = enrollments.FirstOrDefault(item => GetCourseId(item) == courseId);
        return enrollment.ValueKind == JsonValueKind.Object && TryNumber(enrollment, out var progress, "progressPercentage", "progress") ? Math.Clamp((int)Math.Round(progress), 0, 100) : 0;
    }

    protected string? EnrollmentStatusFor(string courseId)
    {
        var enrollment = enrollments.FirstOrDefault(item => GetCourseId(item) == courseId);
        return enrollment.ValueKind == JsonValueKind.Object ? GetString(enrollment, "status") : null;
    }

    private static string? GetCourseId(JsonElement enrollment)
    {
        var direct = GetString(enrollment, "courseId", "courseID");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        if (enrollment.ValueKind == JsonValueKind.Object && TryGetProperty(enrollment, "course", out var course))
            return GetString(course, "id", "courseId");

        return null;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }
    private static bool TryNumber(JsonElement element, out double value, params string[] names) { foreach (var name in names) if (element.TryGetProperty(name, out var property) && property.TryGetDouble(out value)) return true; value = 0; return false; }
}