namespace ExternalIpWidget.Core;

public sealed class GeoIpLookupException : Exception
{
    public GeoIpLookupException(string message, IReadOnlyList<string> attempts, Exception? inner = null)
        : base(message, inner)
    {
        Attempts = attempts;
    }

    public IReadOnlyList<string> Attempts { get; }
}
