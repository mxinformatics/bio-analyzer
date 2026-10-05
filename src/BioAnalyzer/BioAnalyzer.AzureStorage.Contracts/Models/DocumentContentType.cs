namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
/// Wrap the available content type for storage in Azure Blob Storage to provide a limited list of valid content types
/// </summary>
public sealed class DocumentContentType(string value)
{
    public static readonly DocumentContentType Text = new DocumentContentType("text/plain");
    public static readonly DocumentContentType Pdf = new DocumentContentType("application/pdf");
    public static readonly DocumentContentType Xml = new DocumentContentType("application/xml");
    public static readonly DocumentContentType Json = new DocumentContentType("application/json");
    public static readonly DocumentContentType Xslt = new DocumentContentType("application/xslt+xml");
    public static readonly DocumentContentType OctetStream = new DocumentContentType("application/octet-stream");

    public string Value { get; } = value;
}
