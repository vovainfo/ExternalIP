namespace ExternalIpWidget.Core;

/// <summary>Сервис, который можно узнать по имени в сохранённом порядке обхода.</summary>
public interface INamedProvider
{
    string Name { get; }
}
