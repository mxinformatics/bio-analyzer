using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Parsers;

public interface INcbiXmlParser
{
    NcbiArticleResponse ParseArticle(string xmlContent);
}

