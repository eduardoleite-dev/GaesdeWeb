using System.Text.Json.Serialization;

namespace GaesdeWeb.Models;

public sealed record QuizRequestDto(
    [property: JsonPropertyName("contentId")] string ContentId,
    [property: JsonPropertyName("passingScorePercentage")] decimal PassingScorePercentage,
    [property: JsonPropertyName("attemptsAllowed")] int AttemptsAllowed,
    [property: JsonPropertyName("shuffleQuestions")] bool ShuffleQuestions,
    [property: JsonPropertyName("timeLimitMinutes")] int? TimeLimitMinutes);

public sealed record QuizUpdateRequestDto(
    [property: JsonPropertyName("passingScorePercentage")] decimal PassingScorePercentage,
    [property: JsonPropertyName("attemptsAllowed")] int AttemptsAllowed,
    [property: JsonPropertyName("shuffleQuestions")] bool ShuffleQuestions,
    [property: JsonPropertyName("timeLimitMinutes")] int? TimeLimitMinutes);

public sealed record QuizResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("contentId")] string ContentId,
    [property: JsonPropertyName("timeLimitMinutes")] int? TimeLimitMinutes,
    [property: JsonPropertyName("passingScorePercentage")] decimal PassingScorePercentage,
    [property: JsonPropertyName("attemptsAllowed")] int AttemptsAllowed,
    [property: JsonPropertyName("shuffleQuestions")] bool ShuffleQuestions);

public sealed record QuestionRequestDto(
    [property: JsonPropertyName("quizId")] string QuizId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("questionText")] string QuestionText,
    [property: JsonPropertyName("points")] decimal Points,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);

public sealed record QuestionUpdateRequestDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("questionText")] string QuestionText,
    [property: JsonPropertyName("points")] decimal Points,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);

public sealed record QuestionResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("quizId")] string QuizId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("questionText")] string QuestionText,
    [property: JsonPropertyName("photoUrl")] string? PhotoUrl,
    [property: JsonPropertyName("points")] decimal Points,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);

public sealed record QuestionOptionRequestDto(
    [property: JsonPropertyName("questionId")] string QuestionId,
    [property: JsonPropertyName("optionText")] string OptionText,
    [property: JsonPropertyName("isCorrect")] bool IsCorrect);

public sealed record QuestionOptionUpdateRequestDto(
    [property: JsonPropertyName("optionText")] string OptionText,
    [property: JsonPropertyName("isCorrect")] bool IsCorrect);

public sealed record QuestionOptionResponseDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("questionId")] string QuestionId,
    [property: JsonPropertyName("optionText")] string OptionText,
    [property: JsonPropertyName("isCorrect")] bool IsCorrect);
