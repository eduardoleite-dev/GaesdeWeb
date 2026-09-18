using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using GaesdeWeb.Models;
using GaesdeWeb.Services;

namespace GaesdeWeb.Pages.Contents;

public partial class ContentsPage : ComponentBase
{
    [Inject] protected ContentService ContentsApi { get; set; } = default!;
    [Inject] protected ModuleService ModulesApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected QuizService QuizzesApi { get; set; } = default!;
    [Inject] protected QuestionService QuestionsApi { get; set; } = default!;
    [Inject] protected QuestionOptionService OptionsApi { get; set; } = default!;

    protected IReadOnlyList<ContentResponseDto> Contents { get; private set; } = [];
    protected IReadOnlyList<ModuleResponseDto> Modules { get; private set; } = [];
    protected string? selectedModuleId;
    protected string search = string.Empty;
    protected string? errorMessage;
    protected string? formMessage;
    protected bool loading, saving, showForm;
    protected string? editingId;
    protected string title = string.Empty, type = "Text", videoUrl = string.Empty, body = string.Empty, fileUrl = string.Empty;
    protected int orderIndex;
    protected int? durationSeconds;
    protected long? fileSizeBytes;
    protected bool isFreePreview;
    protected IBrowserFile? selectedPhoto;
    protected string? selectedPhotoName;
    protected IBrowserFile? selectedPdf;
    protected string? selectedPdfName;
    protected int Page { get; private set; } = 1;
    protected int TotalPages { get; private set; } = 1;
    protected bool CanManageContents { get; private set; }
    protected string? selectedQuizContentId;
    protected QuizResponseDto? selectedQuiz;
    protected IReadOnlyList<QuestionResponseDto> QuizQuestions { get; private set; } = [];
    protected IReadOnlyList<QuestionOptionResponseDto> QuestionOptions { get; private set; } = [];
    protected string? selectedQuestionId;
    protected bool showQuizForm, showQuestionForm, showOptionForm;
    protected string? editingQuestionId, editingOptionId;
    protected decimal passingScore = 60, questionPoints = 1;
    protected int attempts = 1, questionOrder;
    protected int? timeLimit;
    protected bool shuffle, optionCorrect;
    protected string questionType = "MultipleChoice", questionText = string.Empty, optionText = string.Empty;
    private const int PageSize = 10;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await LoadAsync();
    }

    protected async Task SelectModuleAsync(ChangeEventArgs args)
    {
        selectedModuleId = args.Value?.ToString();
        Page = 1;
        await LoadContentsAsync();
    }

    protected void TypeChanged(ChangeEventArgs args)
    {
        type = args.Value?.ToString() ?? "Text";
        videoUrl = string.Empty;
        body = string.Empty;
        fileUrl = string.Empty;
        durationSeconds = null;
        fileSizeBytes = null;
        selectedPdf = null; selectedPdfName = null;
    }

    protected async Task SearchAsync() { Page = 1; await LoadContentsAsync(); }

    protected void OpenCreateForm()
    {
        editingId = null; title = string.Empty; type = "Text"; orderIndex = Contents.Count; durationSeconds = null; videoUrl = string.Empty; body = string.Empty; fileUrl = string.Empty; fileSizeBytes = null; isFreePreview = false; selectedPhoto = null; selectedPhotoName = null; selectedPdf = null; selectedPdfName = null; formMessage = null; showForm = true;
    }

    protected void OpenEditForm(ContentResponseDto content)
    {
        editingId = content.Id; title = content.Title; type = content.Type; orderIndex = content.OrderIndex; durationSeconds = content.DurationSeconds; videoUrl = content.VideoUrl ?? string.Empty; body = content.Body ?? string.Empty; fileUrl = content.FileUrl ?? string.Empty; fileSizeBytes = content.FileSizeBytes; isFreePreview = content.IsFreePreview; selectedPhoto = null; selectedPhotoName = null; selectedPdf = null; selectedPdfName = null; formMessage = null; showForm = true;
    }

    protected void SelectPhoto(InputFileChangeEventArgs args) { selectedPhoto = args.File; selectedPhotoName = selectedPhoto.Name; formMessage = null; }
    protected void SelectPdf(InputFileChangeEventArgs args) { selectedPdf = args.File; selectedPdfName = selectedPdf.Name; fileUrl = string.Empty; fileSizeBytes = selectedPdf.Size; formMessage = null; }
    protected void CloseForm() { showForm = false; formMessage = null; }

    protected async Task OpenQuizPanelAsync(ContentResponseDto content)
    {
        if (content.Type != "Quiz") return;
        selectedQuizContentId = content.Id;
        var session = await Session.GetAsync();
        if (session is null) return;
        var quizzes = await QuizzesApi.GetAllAsync(session.Token);
        selectedQuiz = quizzes.FirstOrDefault(quiz => quiz.ContentId == content.Id);
        await LoadQuizQuestionsAsync(session.Token);
    }

    protected void OpenQuizForm()
    {
        passingScore = selectedQuiz?.PassingScorePercentage ?? 60;
        attempts = selectedQuiz?.AttemptsAllowed ?? 1;
        timeLimit = selectedQuiz?.TimeLimitMinutes;
        shuffle = selectedQuiz?.ShuffleQuestions ?? false;
        showQuizForm = true; formMessage = null;
    }

    protected void OpenQuestionForm(QuestionResponseDto? question = null)
    {
        editingQuestionId = question?.Id; questionType = question?.Type ?? "MultipleChoice"; questionText = question?.QuestionText ?? string.Empty; questionPoints = question?.Points ?? 1; questionOrder = question?.OrderIndex ?? QuizQuestions.Count; showQuestionForm = true; formMessage = null;
    }

    protected void OpenOptionForm(QuestionOptionResponseDto? option = null)
    {
        editingOptionId = option?.Id; optionText = option?.OptionText ?? string.Empty; optionCorrect = option?.IsCorrect ?? false; showOptionForm = true; formMessage = null;
    }

    protected void CloseQuizForms() { showQuizForm = showQuestionForm = showOptionForm = false; formMessage = null; }

    protected async Task SaveQuizAsync()
    {
        var session = await Session.GetAsync();
        if (session is null || string.IsNullOrWhiteSpace(selectedQuizContentId)) return;
        var result = selectedQuiz is null
            ? await QuizzesApi.CreateAsync(session.Token, new QuizRequestDto(selectedQuizContentId, passingScore, attempts, shuffle, timeLimit))
            : await QuizzesApi.UpdateAsync(session.Token, selectedQuiz.Id, new QuizUpdateRequestDto(passingScore, attempts, shuffle, timeLimit));
        if (result is null) { formMessage = "Não foi possível salvar a configuração da prova."; return; }
        selectedQuiz = result; CloseQuizForms(); await LoadQuizQuestionsAsync(session.Token);
    }

    protected async Task SaveQuestionAsync()
    {
        var session = await Session.GetAsync();
        if (session is null || selectedQuiz is null || string.IsNullOrWhiteSpace(questionText)) { formMessage = "Informe o texto da questão."; return; }
        var result = editingQuestionId is null
            ? await QuestionsApi.CreateAsync(session.Token, new QuestionRequestDto(selectedQuiz.Id, questionType, questionText, questionPoints, questionOrder))
            : await QuestionsApi.UpdateAsync(session.Token, editingQuestionId, new QuestionUpdateRequestDto(questionType, questionText, questionPoints, questionOrder));
        if (result is null) { formMessage = "Não foi possível salvar a questão."; return; }
        CloseQuizForms(); await LoadQuizQuestionsAsync(session.Token);
    }

    protected async Task SaveOptionAsync()
    {
        var session = await Session.GetAsync();
        if (session is null || string.IsNullOrWhiteSpace(selectedQuestionId) || string.IsNullOrWhiteSpace(optionText)) { formMessage = "Informe o texto da opção."; return; }
        var result = editingOptionId is null
            ? await OptionsApi.CreateAsync(session.Token, new QuestionOptionRequestDto(selectedQuestionId, optionText, optionCorrect))
            : await OptionsApi.UpdateAsync(session.Token, editingOptionId, new QuestionOptionUpdateRequestDto(optionText, optionCorrect));
        if (result is null) { formMessage = "Não foi possível salvar a opção."; return; }
        CloseQuizForms(); QuestionOptions = await OptionsApi.GetAllAsync(session.Token, selectedQuestionId);
    }

    protected async Task SelectQuestionAsync(QuestionResponseDto question)
    {
        selectedQuestionId = question.Id;
        var session = await Session.GetAsync();
        if (session is not null) QuestionOptions = await OptionsApi.GetAllAsync(session.Token, question.Id);
    }

    protected async Task DeleteQuestionAsync(QuestionResponseDto question)
    {
        var session = await Session.GetAsync(); if (session is null) return;
        await QuestionsApi.DeleteAsync(session.Token, question.Id); selectedQuestionId = null; QuestionOptions = []; await LoadQuizQuestionsAsync(session.Token);
    }

    protected async Task DeleteOptionAsync(QuestionOptionResponseDto option)
    {
        var session = await Session.GetAsync(); if (session is null) return;
        await OptionsApi.DeleteAsync(session.Token, option.Id); QuestionOptions = await OptionsApi.GetAllAsync(session.Token, option.QuestionId);
    }

    private async Task LoadQuizQuestionsAsync(string token)
    {
        QuizQuestions = selectedQuiz is null ? [] : await QuestionsApi.GetAllAsync(token, selectedQuiz.Id);
        selectedQuestionId = null; QuestionOptions = [];
    }

    protected async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedModuleId) || string.IsNullOrWhiteSpace(title) || orderIndex < 0) { formMessage = "Selecione um módulo e informe título e ordem."; return; }
        if (type == "Video" && string.IsNullOrWhiteSpace(videoUrl) || type == "Text" && string.IsNullOrWhiteSpace(body) || type == "Pdf" && string.IsNullOrWhiteSpace(fileUrl) && selectedPdf is null) { formMessage = "Preencha o campo obrigatório para o tipo de conteúdo selecionado."; return; }
        var session = await Session.GetAsync();
        if (session is null) { Navigation.NavigateTo("/login", forceLoad: true); return; }
        saving = true; formMessage = null;
        try
        {
            if (type == "Pdf" && selectedPdf is not null)
            {
                var uploadedFile = await ContentsApi.UploadFileAsync(session.Token, selectedPdf);
                if (uploadedFile is null) { formMessage = "Não foi possível enviar o arquivo PDF."; return; }
                fileUrl = uploadedFile.Value.Url;
                fileSizeBytes = uploadedFile.Value.Size;
            }

            ContentResponseDto? result = editingId is null
                ? await ContentsApi.CreateAsync(session.Token, new ContentRequestDto(selectedModuleId, title.Trim(), type, orderIndex, isFreePreview, durationSeconds, EmptyToNull(videoUrl), EmptyToNull(body), EmptyToNull(fileUrl), fileSizeBytes))
                : await ContentsApi.UpdateAsync(session.Token, editingId, new ContentUpdateRequestDto(title.Trim(), type, orderIndex, isFreePreview, durationSeconds, EmptyToNull(videoUrl), EmptyToNull(body), EmptyToNull(fileUrl), fileSizeBytes));
            if (result is null) { formMessage = "Não foi possível salvar o conteúdo. Verifique a ordem e os campos do tipo escolhido."; return; }
            if (selectedPhoto is not null)
            {
                var photo = editingId is null ? await ContentsApi.CreatePhotoAsync(session.Token, result.Id, selectedPhoto) : await ContentsApi.UpdatePhotoAsync(session.Token, result.Id, selectedPhoto);
                if (photo is null) { formMessage = "Conteúdo salvo, mas a imagem não foi enviada."; showForm = true; return; }
            }
            CloseForm(); await LoadContentsAsync(session.Token);
        }
        catch (HttpRequestException) { formMessage = "Não foi possível conectar à API."; }
        finally { saving = false; }
    }

    protected async Task DeleteAsync(ContentResponseDto content)
    {
        var session = await Session.GetAsync();
        if (session is null) { Navigation.NavigateTo("/login", forceLoad: true); return; }
        loading = true;
        try { if (!await ContentsApi.DeleteAsync(session.Token, content.Id)) errorMessage = "Não foi possível excluir o conteúdo."; else await LoadContentsAsync(session.Token); }
        catch (HttpRequestException) { errorMessage = "Não foi possível conectar à API."; }
        finally { loading = false; }
    }

    protected async Task PreviousPageAsync() { if (Page > 1) { Page--; await LoadContentsAsync(); } }
    protected async Task NextPageAsync() { if (Page < TotalPages) { Page++; await LoadContentsAsync(); } }
    protected string ModuleName(string id) => Modules.FirstOrDefault(module => module.Id == id)?.Title ?? "Módulo não encontrado";

    private async Task LoadAsync()
    {
        var session = await Session.GetAsync();
        if (session is null) { Navigation.NavigateTo("/login", forceLoad: true); return; }
        CanManageContents = session.CanManageUsers; loading = true;
        try { var modules = await ModulesApi.GetAllAsync(session.Token, null, 1, 100); Modules = modules.Items; selectedModuleId ??= Modules.FirstOrDefault()?.Id; await LoadContentsAsync(session.Token); }
        catch (HttpRequestException) { errorMessage = "Não foi possível carregar módulos e conteúdos."; }
        finally { loading = false; await InvokeAsync(StateHasChanged); }
    }

    private async Task LoadContentsAsync(string? token = null)
    {
        var sessionToken = token ?? (await Session.GetAsync())?.Token;
        if (string.IsNullOrWhiteSpace(sessionToken)) { Navigation.NavigateTo("/login", forceLoad: true); return; }
        loading = true; errorMessage = null;
        try { var result = await ContentsApi.GetAllAsync(sessionToken, selectedModuleId, Page, PageSize, search); Contents = result.Items; TotalPages = result.TotalPages; }
        catch (HttpRequestException) { errorMessage = "Não foi possível carregar os conteúdos."; }
        finally { loading = false; await InvokeAsync(StateHasChanged); }
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
