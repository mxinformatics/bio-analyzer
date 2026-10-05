namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
/// ByteDocument represents a document stored in Azure Blob Storage.
/// </summary>
public class ByteDocument : IByteDocument
{
    public ByteDocument(string name, DocumentContentType contentType, byte[] content) : this(name,
        contentType.Value, content, new Dictionary<string, string>())
    {}

    public ByteDocument(string name, DocumentContentType contentType, byte[] content, IDictionary<string,string> metadata) : this(name, contentType.Value, content, metadata){}

    public ByteDocument(string name, string contentType, byte[] content, IDictionary<string, string> metadata)
    {
        Name = name;
        ContentType = contentType;
        Content = content;
        Metadata = metadata;
    }

    public string Name { get; set; }
    public string ContentType { get; set; }
        
    public IDictionary<string, string> Metadata { get; set;  }
    public byte[] Content { get; set; }
}