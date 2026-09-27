namespace ExternalIpWidget.Core;

/// <summary>
/// Внешний адрес, который сообщил сторонний сервис.
/// </summary>
public sealed record PublicIpResult(
    string Address,
    string ProviderName,
    string ProviderUrl,
    DateTimeOffset RetrievedAt);
