using System.Text.Json;
using GaesdeWeb.Models;
using GaesdeWeb.Services;
using Microsoft.AspNetCore.Components;

namespace GaesdeWeb.Pages.Chat;

public partial class ChatPage : ComponentBase
{
    protected sealed record ChatConversation(string Id, string Name, IReadOnlyList<JsonElement> Messages)
    {
        public JsonElement LastMessage => Messages[^1];
    }

    [Inject] protected CommentService CommentsApi { get; set; } = default!;
    [Inject] protected UserService UsersApi { get; set; } = default!;
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected IReadOnlyList<JsonElement> messages { get; private set; } = [];
    protected IReadOnlyList<ChatConversation> conversations { get; private set; } = [];
    protected ChatConversation? selectedConversation;
    protected IReadOnlyList<UserManagementDto> recipients { get; private set; } = [];
    protected string recipientSearch = string.Empty;
    protected string messageContent = string.Empty;
    protected string inlineMessageContent = string.Empty;
    protected string? selectedRecipientId;
    protected string? errorMessage;
    protected string? composerMessage;
    protected bool loading;
    protected bool recipientLoading;
    protected bool sending;
    protected bool showComposer;
    private IReadOnlyDictionary<string, string> userNamesById = new Dictionary<string, string>();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
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
            messages = (await CommentsApi.GetChatAsync(session.Token)).Items;
            var users = (await UsersApi.GetAllAsync(session.Token, 1, 100)).Items;
            BuildConversations(session.UserId, users);
        }
        catch (HttpRequestException)
        {
            errorMessage = "Não foi possível carregar suas mensagens.";
        }
        finally
        {
            loading = false;
        }
    }

    protected async Task OpenComposer()
    {
        showComposer = true;
        recipientSearch = string.Empty;
        messageContent = string.Empty;
        selectedRecipientId = null;
        composerMessage = null;
        var session = await Session.GetAsync();
        if (session is not null)
            await SearchRecipientsAsync();
    }

    protected void CloseComposer() => showComposer = false;

    protected async Task SearchRecipientsAsync()
    {
        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        recipientLoading = true;
        try
        {
            recipients = (await UsersApi.GetAllAsync(session.Token, 1, 100, recipientSearch)).Items
                .Where(user => user.Id != session.UserId)
                .ToArray();
        }
        catch (HttpRequestException)
        {
            composerMessage = "Não foi possível carregar os usuários.";
            recipients = [];
        }
        finally
        {
            recipientLoading = false;
        }
    }

    protected void SelectRecipient(UserManagementDto recipient)
    {
        selectedRecipientId = recipient.Id;
        composerMessage = null;
    }

    protected async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedRecipientId) || string.IsNullOrWhiteSpace(messageContent))
        {
            composerMessage = "Escolha um destinatário e escreva uma mensagem.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        sending = true;
        composerMessage = null;
        try
        {
            var result = await CommentsApi.CreateAsync(session.Token, new CommentRequestDto(CommentType.Chat.ToString(), messageContent.Trim(), [selectedRecipientId]));
            if (!result.Success)
                composerMessage = result.ErrorMessage ?? "Não foi possível enviar a mensagem.";
            else
            {
                messageContent = string.Empty;
                showComposer = false;
                messages = (await CommentsApi.GetChatAsync(session.Token)).Items;
                var users = (await UsersApi.GetAllAsync(session.Token, 1, 100)).Items;
                BuildConversations(session.UserId, users);
            }
        }
        catch (HttpRequestException)
        {
            composerMessage = "Não foi possível enviar a mensagem.";
        }
        finally
        {
            sending = false;
        }
    }

    protected async Task SendInlineMessageAsync()
    {
        if (selectedConversation is null || string.IsNullOrWhiteSpace(inlineMessageContent))
            return;

        if (string.IsNullOrWhiteSpace(selectedConversation.Id) || selectedConversation.Id == MessagePerson(selectedConversation.LastMessage))
        {
            composerMessage = "Não foi possível identificar o destinatário desta conversa. Inicie uma nova conversa.";
            return;
        }

        var session = await Session.GetAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
            return;
        }

        sending = true;
        try
        {
            var result = await CommentsApi.CreateAsync(
                session.Token,
                new CommentRequestDto(CommentType.Chat.ToString(), inlineMessageContent.Trim(), [selectedConversation.Id]));
            if (!result.Success)
            {
                composerMessage = result.ErrorMessage ?? "Não foi possível enviar a mensagem.";
                return;
            }

            inlineMessageContent = string.Empty;
            messages = (await CommentsApi.GetChatAsync(session.Token)).Items;
            var users = (await UsersApi.GetAllAsync(session.Token, 1, 100)).Items;
            BuildConversations(session.UserId, users);
        }
        catch (HttpRequestException)
        {
            composerMessage = "Não foi possível enviar a mensagem.";
        }
        finally
        {
            sending = false;
        }
    }

    protected static string MessageText(JsonElement message) => GetString(message, "content") ?? string.Empty;
    protected string MessagePerson(JsonElement message)
    {
        var name = GetPersonName(message);
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        foreach (var propertyName in new[] { "senderId", "userId", "authorId", "createdBy" })
        {
            var id = GetString(message, propertyName);
            if (!string.IsNullOrWhiteSpace(id) && userNamesById.TryGetValue(id, out var userName))
                return userName;
        }

        return "Usuário";
    }
    protected static string MessageDate(JsonElement message) => DateTimeOffset.TryParse(GetString(message, "createdAt", "dateCreated"), out var date) ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : string.Empty;

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        return null;
    }

    private static string? GetPersonName(JsonElement message)
    {
        var directName = GetString(message, "userName", "authorName", "senderName", "recipientName", "creatorName", "createdByName", "createdByUserName");
        if (!string.IsNullOrWhiteSpace(directName))
            return directName;

        foreach (var propertyName in new[] { "user", "author", "sender", "creator", "createdBy" })
        {
            if (!message.TryGetProperty(propertyName, out var person) || person.ValueKind != JsonValueKind.Object)
                continue;

            var nestedName = GetString(person, "name", "fullName", "userName", "displayName", "username");
            if (!string.IsNullOrWhiteSpace(nestedName))
                return nestedName;
        }

        return null;
    }

    protected void SelectConversation(ChatConversation conversation) => selectedConversation = conversation;

    private void BuildConversations(string currentUserId, IReadOnlyList<UserManagementDto> users)
    {
        userNamesById = users.ToDictionary(user => user.Id, user => user.Name, StringComparer.Ordinal);
        var groups = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            var otherUserId = FindOtherUserId(message, currentUserId);
            var key = otherUserId ?? MessagePerson(message);
            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups[key] = group;
            }

            group.Add(message);
        }

        conversations = groups
            .Select(group => new ChatConversation(
                group.Key,
                userNamesById.TryGetValue(group.Key, out var name) ? name : (group.Key.Length > 0 ? group.Key : "Usuário"),
                group.Value.OrderBy(message => MessageDateValue(message)).ToArray()))
            .OrderByDescending(conversation => MessageDateValue(conversation.LastMessage))
            .ToArray();
        selectedConversation = conversations.FirstOrDefault(conversation => conversation.Id == selectedConversation?.Id) ?? conversations.FirstOrDefault();
    }

    private static string? FindOtherUserId(JsonElement message, string currentUserId)
    {
        foreach (var propertyName in new[] { "senderId", "userId", "authorId", "createdBy" })
        {
            if (message.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var id = property.GetString();
                if (!string.IsNullOrWhiteSpace(id) && !string.Equals(id, currentUserId, StringComparison.Ordinal))
                    return id;
            }
        }

        if (message.TryGetProperty("recipientIds", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
        {
            var otherRecipient = recipients.EnumerateArray()
                .FirstOrDefault(item => item.ValueKind == JsonValueKind.String && item.GetString() != currentUserId);
            return otherRecipient.ValueKind == JsonValueKind.String ? otherRecipient.GetString() : null;
        }

        return null;
    }

    private static DateTimeOffset MessageDateValue(JsonElement message) =>
        DateTimeOffset.TryParse(GetString(message, "createdAt", "dateCreated"), out var date) ? date : DateTimeOffset.MinValue;
}