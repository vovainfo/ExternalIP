namespace ExternalIpWidget.Core;

public sealed class PublicIpLookupException : Exception
{
    public PublicIpLookupException(string message, IReadOnlyList<string> attempts, Exception? inner = null)
        : base(message, inner)
    {
        Attempts = attempts;
    }

    public IReadOnlyList<string> Attempts { get; }
}
