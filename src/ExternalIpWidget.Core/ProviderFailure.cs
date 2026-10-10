using System.Text.Json;

namespace ExternalIpWidget.Core;

/// <summary>
/// Ошибки одного сервиса, после которых стоит спросить следующий. Отмену пользователем сюда не относим.
/// </summary>
internal static class ProviderFailure
{
    public static bool Is(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or IOException or FormatException or JsonException or OperationCanceledException;
    }

    public static string Short(Exception ex)
    {
        if (ex is OperationCanceledException)
            return "таймаут";

        var message = ex.Message.ReplaceLineEndings(" ");
        return message.Length > 140 ? message[..140] : message;
    }
}
