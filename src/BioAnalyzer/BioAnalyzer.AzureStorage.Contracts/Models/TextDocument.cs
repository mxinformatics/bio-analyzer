namespace BioAnalyzer.AzureStorage.Contracts.Models;


public class TextDocument(string name, string contentType, string content, IDictionary<string, string> metaData)
    : IBlobDocument
{
    public TextDocument(string name, DocumentContentType contentType, string content) : this(name, contentType.Value, content, new Dictionary<string, string>()){}

    public TextDocument(string name, DocumentContentType contentType, string content, IDictionary<string, string> metadata) : this(name, contentType.Value, content, metadata){}


    public string Name { get; set; } = name;
    public string ContentType { get; set;  } = contentType;
    public IDictionary<string, string> Metadata { get; set; } = metaData;
    public string Content { get; set;  } = content;
}