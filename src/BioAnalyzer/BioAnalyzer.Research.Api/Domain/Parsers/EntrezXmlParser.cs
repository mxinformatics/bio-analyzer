using System.Xml;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Parsers;

public class EntrezXmlParser : IEntrezXmlParser
{
    public EntrezSummaryResponse ParseSummary(string xmlContent)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        var results = new List<EntrezSummaryResult>();
        var documents = xmlDoc.SelectNodes("//DocSum");
        if (documents == null)
        {
            return new EntrezSummaryResponse(results);
        }

        foreach (XmlNode docSumNode in documents)
        {
            var result = new EntrezSummaryResult();
            var uidNode = docSumNode.SelectSingleNode("Id");
            if (uidNode != null)
            {
                result.Uid = uidNode.InnerText;
            }

            var titleNode = docSumNode.SelectSingleNode("Item[@Name='Title']");
            if (titleNode != null)
            {
                result.Title = titleNode.InnerText;
            }

            var articleIds = docSumNode.SelectSingleNode("Item[@Name='ArticleIds']");
            var pmcIdNode = articleIds?.SelectSingleNode("Item[@Name='pmc']");
            if (pmcIdNode != null)
            {
                result.SetPmcId(pmcIdNode.InnerText);
            }

            var doiNode = articleIds?.SelectSingleNode("Item[@Name='doi']");
            if (doiNode != null)
            {
                result.Doi = doiNode.InnerText;
            }

            results.Add(result);
        }

        return new EntrezSummaryResponse(results);
    }
}

