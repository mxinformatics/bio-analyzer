using BioAnalyzer.App.Contracts.Clients;
using BioAnalyzer.App.Contracts.Services;
using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.Messages;
using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;


namespace BioAnalyzer.App.Services;

public class SearchService(IResearchApiClient researchApiClient, IEventBusClient eventBusClient, ILogger<SearchService> logger) : ISearchService
{
    private static readonly HashSet<string> AllowedManualUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".xml",
        ".nxml"
    };
    public async Task<LiteratureReferenceList> Search(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            logger.LogWarning("Search attempted with empty search term");
            return new LiteratureReferenceList();
        }

        try
        {
            logger.LogInformation("Searching for literature with term: {SearchTerm}, StartIndex: {StartIndex}", 
                criteria.SearchTerm, criteria.StartIndex);
            
            var searchResult = await researchApiClient.GetLiteratureReferences(criteria.SearchTerm, criteria.StartIndex, cancellationToken);

            if (searchResult.Count > 0)
            {
                logger.LogDebug("Found {Count} results, fetching summaries for {ReferenceCount} references", 
                    searchResult.Count, searchResult.ReferenceIds.Count());
                
                var literatureSummaries = await researchApiClient.GetLiteratureSummary(searchResult.ReferenceIds.ToList(), cancellationToken);
                var references = literatureSummaries.Select(summary => 
                    new LiteratureReference{ Id  = summary.Uid, Title = summary.Title, Doi =  summary.Doi, PmcId = summary.PmcId}).ToList();
                
                logger.LogInformation("Successfully retrieved {ReferenceCount} literature references", references.Count);
                
                return new LiteratureReferenceList
                {
                    Count = searchResult.Count,
                    RetMax = searchResult.RetMax,
                    RetStart = searchResult.RetStart,
                    References = references
                };
            }
            else
            {
                logger.LogInformation("No results found for search term: {SearchTerm}", criteria.SearchTerm);
                return new LiteratureReferenceList();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search literature for term: {SearchTerm}", criteria.SearchTerm);
            throw;
        }
    }
    public async Task ManualUploadAndProcess(
        LiteratureReference reference,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));
        ArgumentNullException.ThrowIfNull(file, nameof(file));

        var fileExtension = Path.GetExtension(file.Name);
        if (!AllowedManualUploadExtensions.Contains(fileExtension))
        {
            throw new InvalidOperationException("Only .pdf, .xml, and .nxml files are supported.");
        }

        await using var fileStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
        var uploadedDownload = await researchApiClient
            .UploadManualReference(
                title: reference.Title,
                pmcId: reference.PmcId,
                doi: reference.Doi,
                fileName: file.Name,
                content: fileStream,
                contentType: file.ContentType,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var processingRequest = new DocumentProcessingRequest
        {
            FileName = uploadedDownload.FileName,
            Title = uploadedDownload.Title,
            PmcId = uploadedDownload.PmcId,
            Doi = uploadedDownload.Doi,
            DownloadLink = uploadedDownload.DownloadLink,
            XmlDownloadLink = uploadedDownload.XmlDownloadLink
        };

        await eventBusClient
            .PublishDocumentProcessingRequest(processingRequest, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LiteratureAbstract> GetAbstract(string pmcId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pmcId, nameof(pmcId));
        
        try
        {
            logger.LogInformation("Retrieving abstract for PmcId: {PmcId}", pmcId);
            var result = await researchApiClient.GetLiteratureAbstract(pmcId, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Successfully retrieved abstract for PmcId: {PmcId}", pmcId);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve abstract for PmcId: {PmcId}", pmcId);
            throw;
        }
    }

    public async Task DownloadReference(LiteratureReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));
        
        if (string.IsNullOrWhiteSpace(reference.PmcId))
        {
            throw new ArgumentException("Reference must have a valid PmcId to be downloaded", nameof(reference));
        }
        
        try
        {
            logger.LogInformation("Initiating download for reference: {ReferenceId}, Title: {Title}", 
                reference.Id, reference.Title);
            
            var downloadLinkResponse = await researchApiClient.DownloadReference(reference, cancellationToken).ConfigureAwait(false);
            var downloadRequest = new LiteratureDownloadRequest(
                downloadLinkResponse.PmcId, 
                downloadLinkResponse.DownloadLink, 
                reference.Title, 
                reference.Doi);
            
            if (!string.IsNullOrWhiteSpace(downloadRequest.DownloadLink))
            {
                await eventBusClient.Publish(new List<LiteratureDownloadRequest> { downloadRequest }, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Successfully published download request for reference: {ReferenceId}", reference.Id);
            }
            else
            {
                logger.LogWarning("No valid download link found for literature reference: {ReferenceId}", reference.Id);
                throw new InvalidOperationException($"No valid download link found for literature reference {reference.Id}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to download reference: {ReferenceId}", reference.Id);
            throw;
        }
    }

    public async Task<IList<LiteratureDownload>> GetDownloads(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Retrieving literature downloads");
            var response = await researchApiClient.GetDownloads(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Retrieved {DownloadCount} downloads", response.Downloads.Count);
            return response.Downloads;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve literature downloads");
            throw;
        }
    }

    public async Task<byte[]> DownloadFile(string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        
        try
        {
            logger.LogInformation("Downloading file: {FileName}", fileName);
            var result = await researchApiClient.DownloadFile(fileName, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Successfully downloaded file: {FileName}, Size: {FileSize} bytes", 
                fileName, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download file: {FileName}", fileName);
            throw;
        }
    }
}