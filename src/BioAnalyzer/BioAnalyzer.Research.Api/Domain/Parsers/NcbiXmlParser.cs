using System.Xml;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Parsers;

public class NcbiXmlParser : INcbiXmlParser
{
    private const string OaiRoot = "/*[local-name()='OAI-PMH']/*[local-name()='GetRecord']/*[local-name()='record']/*[local-name()='metadata']/*[local-name()='dc']";

    public NcbiArticleResponse ParseArticle(string xmlContent)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        var title = xmlDoc.SelectSingleNode($"{OaiRoot}/*[local-name()='title']")?.InnerText ?? string.Empty;
        var description = xmlDoc.SelectSingleNode($"{OaiRoot}/*[local-name()='description']")?.InnerText ?? string.Empty;
        var publishedDate = xmlDoc.SelectSingleNode($"{OaiRoot}/*[local-name()='date']")?.InnerText ?? string.Empty;

        var authors = xmlDoc
            .SelectNodes($"{OaiRoot}/*[local-name()='creator']")
            ?.Cast<XmlNode>()
            .Select(n => n.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList() ?? [];

        var publicationTypes = xmlDoc
            .SelectNodes($"{OaiRoot}/*[local-name()='subject']")
            ?.Cast<XmlNode>()
            .Select(n => n.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList() ?? [];

        return new NcbiArticleResponse
        {
            Title = title,
            Description = description,
            Authors = authors,
            PublishedDate = publishedDate,
            PublicationTypes = publicationTypes
        };
    }
}

