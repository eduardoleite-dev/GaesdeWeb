using System.Text.Json;
using GaesdeWeb.Models;
using GaesdeWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GaesdeWeb.Pages.Learn;

public partial class CourseLearningPage : ComponentBase
{
    [Parameter] public string CourseId { get; set; } = string.Empty;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected CourseService CoursesApi { get; set; } = default!;
    [Inject] protected ModuleService ModulesApi { get; set; } = default!;
    [Inject] protected ContentService ContentsApi { get; set; } = default!;
    [Inject] protected QuizService QuizzesApi { get; set; } = default!;
    [Inject] protected ContentCompletionService CompletionsApi { get; set; } = default!;
    [Inject] protected CommentService CommentsApi { get; set; } = default!;
    [Inject] protected QuestionService QuestionsApi { get; set; } = default!;
    [Inject] protected QuestionOptionService OptionsApi { get; set; } = default!;
    [Inject] protected QuizAttemptService AttemptsApi { get; set; } = default!;
    [Inject] protected EnrollmentService EnrollmentsApi { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime Js { get; set; } = default!;

    private const string QuizReviewStorageKey = "gaesde.quiz-review-available";

    protected CourseResponseDto? course;
    protected IReadOnlyList<ModuleResponseDto> modules = [];
    protected IReadOnlyList<ContentResponseDto> contents = [];
    protected IReadOnlyList<QuizResponseDto> quizzes = [];
    protected HashSet<string> CompletedContentIds { get; } = [];
    protected IReadOnlyList<JsonElement> forumComments = [];
    protected bool loading = true;
    protected bool forumLoading;
    protected bool posting;
    protected bool showForum;
    protected string forumContent = string.Empty;
    protected string? message;
    protected string? enrollmentId;
    protected bool showQuiz;
    protected bool quizLoading;
    protected bool quizSubmitting;
    protected bool quizReviewMode;
    protected QuizResponseDto? activeQuiz;
    protected IReadOnlyList<QuestionResponseDto> quizQuestions = [];
    protected HashSet<string> quizReviewAvailable { get; } = [];
    protected Dictionary<string, IReadOnlyList<QuestionOptionResponseDto>> quizOptions { get; } = [];
    protected Dictionary<string, string> selectedOptions { get; } = [];
    protected Dictionary<string, string> writtenAnswers { get; } = [];
    protected Dictionary<string, Dictionary<string, string>> quizSubmittedSelections { get; } = [];
    protected Dictionary<string, Dictionary<string, string>> quizSubmittedWrittenAnswers { get; } = [];
    protected string? attemptId;
    protected string? quizMessage;
    protected int TotalContents => contents.Count;
    protected int ProgressPercent => TotalContents == 0 ? 0 : (int)Math.Round(CompletedContentIds.Count * 100d / TotalContents);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        var session = await Session.GetAsync();
        if (session is null) { Navigation.NavigateTo("/login", true); return; }
        if (session.AccessLevel != AccessLevel.Student) { Navigation.NavigateTo("/courses", true); return; }

        try
        {
            course = await CoursesApi.GetByIdAsync(session.Token, CourseId);
            if (course is null) return;
            var enrollmentResult = await EnrollmentsApi.GetMineAsync(session.Token, 1, 100);
            var enrollment = enrollmentResult.Items.FirstOrDefault(item => GetEnrollmentCourseId(item) == CourseId);
            var enrollmentStatus = enrollment.ValueKind == JsonValueKind.Object ? GetJsonString(enrollment, "status") : null;
            if (enrollment.ValueKind != JsonValueKind.Object || enrollmentStatus is not ("Active" or "Completed"))
            {
                message = "Você precisa estar matriculado neste curso para acessar seus módulos, conteúdos e quizzes.";
                return;
            }

            enrollmentId = GetJsonString(enrollment, "id");
            if (string.IsNullOrWhiteSpace(enrollmentId))
            {
                message = "Não foi possível identificar sua matrícula neste curso.";
                return;
            }

            var moduleResult = await ModulesApi.GetAllAsync(session.Token, CourseId, 1, 100);
            modules = moduleResult.Items.OrderBy(item => item.OrderIndex).ToArray();
            var contentTasks = modules.Select(module => ContentsApi.GetAllAsync(session.Token, module.Id, 1, 100)).ToArray();
            var contentResults = await Task.WhenAll(contentTasks);
            contents = contentResults.SelectMany(result => result.Items).OrderBy(item => item.OrderIndex).ToArray();
            quizzes = await QuizzesApi.GetAllAsync(session.Token);
            var progress = await CompletionsApi.GetCourseProgressAsync(session.Token, CourseId);
            if (progress is { ValueKind: JsonValueKind.Object } && progress.Value.TryGetProperty("completedContentIds", out var completed) && completed.ValueKind == JsonValueKind.Array)
                foreach (var id in completed.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(id => id is not null)) CompletedContentIds.Add(id!);

            await LoadQuizReviewAvailabilityAsync();
        }
        catch (HttpRequestException) { message = "Não foi possível carregar os conteúdos deste curso."; }
        finally
        {
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void GoBack() => Navigation.NavigateTo("/progress");
    protected string ContentLabel(string type) => type.ToUpperInvariant() switch { "VIDEO" => "VÍDEO", "PDF" => "MATERIAL", "QUIZ" => "QUIZ", "ASSIGNMENT" => "ATIVIDADE", _ => "LEITURA" };

    protected async Task ToggleCompletionAsync(string contentId)
    {
        var session = await Session.GetAsync();
        if (session is null) return;
        var completed = CompletedContentIds.Contains(contentId);
        var success = completed ? await CompletionsApi.UncompleteAsync(session.Token, contentId) : await CompletionsApi.CompleteAsync(session.Token, contentId);
        if (success) { if (completed) CompletedContentIds.Remove(contentId); else CompletedContentIds.Add(contentId); }
        else message = "Este conteúdo ainda não pode ser concluído. Siga a ordem da trilha.";
    }

    protected async Task TryQuizAsync(QuizResponseDto quiz, bool clearState = true)
    {
        var session = await Session.GetAsync();
        if (session is null) return;
        if (string.IsNullOrWhiteSpace(enrollmentId))
        {
            quizMessage = "Não foi possível localizar sua matrícula neste curso.";
            showQuiz = true;
            return;
        }

        activeQuiz = quiz;
        showQuiz = true;
        quizLoading = true;
        quizReviewMode = false;
        quizMessage = null;
        attemptId = null;
        if (clearState)
        {
            selectedOptions.Clear();
            writtenAnswers.Clear();
        }
        else
        {
            LoadSubmittedAnswers(quiz.Id);
        }
        quizOptions.Clear();
        try
        {
            quizQuestions = (await QuestionsApi.GetAllAsync(session.Token, quiz.Id)).OrderBy(question => question.OrderIndex).ToArray();
            var optionTasks = quizQuestions.Select(async question => (question.Id, Options: await OptionsApi.GetAllAsync(session.Token, question.Id))).ToArray();
            var options = await Task.WhenAll(optionTasks);
            foreach (var item in options) quizOptions[item.Id] = item.Options;
        }
        catch (HttpRequestException) { quizMessage = "Não foi possível carregar as questões deste quiz."; }
        finally { quizLoading = false; }
    }

    protected async Task OpenQuizReviewAsync(QuizResponseDto quiz)
    {
        await TryQuizAsync(quiz, clearState: false);
        quizReviewMode = true;
    }

    protected void LoadSubmittedAnswers(string quizId)
    {
        selectedOptions.Clear();
        writtenAnswers.Clear();

        if (quizSubmittedSelections.TryGetValue(quizId, out var submittedSelections))
            foreach (var answer in submittedSelections)
                selectedOptions[answer.Key] = answer.Value;

        if (quizSubmittedWrittenAnswers.TryGetValue(quizId, out var submittedWrittenAnswers))
            foreach (var answer in submittedWrittenAnswers)
                writtenAnswers[answer.Key] = answer.Value;
    }

    protected async Task SaveSubmittedAnswersAsync(string quizId)
    {
        quizSubmittedSelections[quizId] = new Dictionary<string, string>(selectedOptions);
        quizSubmittedWrittenAnswers[quizId] = new Dictionary<string, string>(writtenAnswers);
        quizReviewAvailable.Add(quizId);
        await PersistQuizReviewAvailabilityAsync();
    }

    protected async Task LoadQuizReviewAvailabilityAsync()
    {
        try
        {
            var stored = await Js.InvokeAsync<string?>("sessionStorage.getItem", QuizReviewStorageKey);
            if (string.IsNullOrWhiteSpace(stored))
                return;

            foreach (var quizId in stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!string.IsNullOrWhiteSpace(quizId))
                    quizReviewAvailable.Add(quizId);
        }
        catch
        {
            // Ignora falha do armazenamento local da sessão.
        }
    }

    protected async Task PersistQuizReviewAvailabilityAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("sessionStorage.setItem", QuizReviewStorageKey, string.Join(',', quizReviewAvailable));
        }
        catch
        {
            // Ignora falha do armazenamento local da sessão.
        }
    }

    protected void CloseQuiz()
    {
        showQuiz = false;
        activeQuiz = null;
        attemptId = null;
        quizReviewMode = false;
        quizMessage = null;
    }

    protected void SelectOption(string questionId, string optionId) => selectedOptions[questionId] = optionId;
    protected void SetWrittenAnswer(string questionId, ChangeEventArgs args) => writtenAnswers[questionId] = args.Value?.ToString() ?? string.Empty;
    protected string GetWrittenAnswer(string questionId) => writtenAnswers.TryGetValue(questionId, out var answer) ? answer : string.Empty;

    protected async Task SubmitQuizAsync()
    {
        if (activeQuiz is null || string.IsNullOrWhiteSpace(enrollmentId)) return;
        var session = await Session.GetAsync();
        if (session is null) return;
        quizSubmitting = true;
        quizMessage = null;
        try
        {
            var attempt = await AttemptsApi.StartAsync(session.Token, new StartQuizAttemptRequestDto(activeQuiz.Id, enrollmentId));
            attemptId = attempt is { ValueKind: JsonValueKind.Object } && attempt.Value.TryGetProperty("id", out var id) ? id.GetString() : null;
            if (string.IsNullOrWhiteSpace(attemptId)) { quizMessage = "Não foi possível iniciar a tentativa."; return; }
            foreach (var question in quizQuestions)
            {
                selectedOptions.TryGetValue(question.Id, out var optionId);
                writtenAnswers.TryGetValue(question.Id, out var text);
                await AttemptsApi.SendAnswerAsync(session.Token, new UserAnswerRequestDto(attemptId, question.Id, optionId, null, text));
            }
            var result = await AttemptsApi.FinishAsync(session.Token, attemptId);
            if (result is not null)
            {
                await SaveSubmittedAnswersAsync(activeQuiz.Id);
                quizMessage = "Respostas enviadas com sucesso. Você pode revisar o seu quiz realizado.";
                quizReviewMode = false;
                showQuiz = false;
                activeQuiz = null;
                attemptId = null;
            }
            else
            {
                quizMessage = "A tentativa foi enviada, mas não foi possível carregar o resultado.";
            }
        }
        catch (HttpRequestException) { quizMessage = "Não foi possível enviar suas respostas."; }
        finally { quizSubmitting = false; }
    }

    protected async Task OpenForum()
    {
        showForum = true;
        forumLoading = true;
        var session = await Session.GetAsync();
        if (session is null) return;
        try { forumComments = (await CommentsApi.GetCourseAsync(session.Token, CourseId)).Items; }
        catch (HttpRequestException) { message = "Não foi possível carregar o fórum."; }
        finally { forumLoading = false; }
    }

    protected void CloseForum() => showForum = false;

    protected async Task PublishForumAsync()
    {
        if (string.IsNullOrWhiteSpace(forumContent)) return;
        var session = await Session.GetAsync();
        if (session is null) return;
        posting = true;
        var result = await CommentsApi.CreateAsync(session.Token, new CommentRequestDto(CommentType.Course.ToString(), forumContent.Trim(), [], CourseId));
        if (result.Success) { forumContent = string.Empty; forumComments = (await CommentsApi.GetCourseAsync(session.Token, CourseId)).Items; }
        else message = result.ErrorMessage;
        posting = false;
    }

    protected static string CommentText(JsonElement item) => GetString(item, "content") ?? string.Empty;
    protected static string CommentAuthor(JsonElement item) => GetString(item, "userName", "authorName", "senderName", "creatorName") ?? GetNestedName(item) ?? "Participante";
    protected static string CommentDate(JsonElement item) => DateTimeOffset.TryParse(GetString(item, "createdAt", "dateCreated"), out var date) ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : string.Empty;
    private static string? GetString(JsonElement item, params string[] names) { foreach (var name in names) if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString(); return null; }
    private static string? GetNestedName(JsonElement item) { foreach (var name in new[] { "user", "author", "creator", "createdBy" }) if (item.TryGetProperty(name, out var person) && person.ValueKind == JsonValueKind.Object) return GetString(person, "name", "fullName", "displayName"); return null; }

    private static string? GetEnrollmentCourseId(JsonElement enrollment)
    {
        var direct = GetJsonString(enrollment, "courseId", "courseID");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        if (TryGetJsonProperty(enrollment, "course", out var course))
            return GetJsonString(course, "id", "courseId");

        return null;
    }

    private static string? GetJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (TryGetJsonProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static bool TryGetJsonProperty(JsonElement element, string name, out JsonElement value)
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

    private sealed class EnrollmentServiceProxy
    {
        private readonly EnrollmentService service;
        public EnrollmentServiceProxy(EnrollmentService service) => this.service = service;
        public async Task<string?> GetMineAsync(string token, string courseId)
        {
            var result = await service.GetMineAsync(token, 1, 100, courseId: courseId);
            var item = result.Items.FirstOrDefault();
            return item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
    }
}