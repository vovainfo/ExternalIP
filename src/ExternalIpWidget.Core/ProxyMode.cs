namespace ExternalIpWidget.Core;

public enum ProxyMode
{
    None = 0,
    Environment = 1,
    System = 2,
    Custom = 3,
}

public static class ProxyModeMigration
{
    public static ProxyMode Resolve(bool choiceSaved, ProxyMode current, bool useEnvironmentProxy, bool useSystemProxy)
    {
        if (choiceSaved && Enum.IsDefined(current))
            return current;
        if (useEnvironmentProxy)
            return ProxyMode.Environment;
        if (useSystemProxy)
            return ProxyMode.System;
        return ProxyMode.None;
    }
}
