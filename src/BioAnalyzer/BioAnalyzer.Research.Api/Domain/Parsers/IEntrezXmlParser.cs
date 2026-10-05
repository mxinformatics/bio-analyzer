using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Parsers;

public interface IEntrezXmlParser
{
    EntrezSummaryResponse ParseSummary(string xmlContent);
}

