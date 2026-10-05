using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.AzureStorage.Contracts.Models;
using BioAnalyzer.Research.Api.Domain.DataTransfer;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class StorageClient(ITableContext tableContext, IBlobContext blobContext, IOptions<ResearchApiStorageConfiguration> storageConfiguration) : IStorageClient
{
    private readonly ResearchApiStorageConfiguration _storageConfiguration = storageConfiguration.Value;
    private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".xml",
        ".nxml"
    };

    public async Task<LiteratureDownloadList> GetDownloadsAsync(CancellationToken cancellationToken = default)
    {
        var downloadList = new LiteratureDownloadList();

        var downloads = await tableContext.GetAllAsync<LiteratureDownloadDto>(_storageConfiguration.DownloadTableName);
        downloadList.Downloads = MapDownloads(downloads);
        return downloadList;
    }

    public async Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (!DownloadFileName.IsValid(fileName, out var normalized, out var error))
        {
            throw new ArgumentException(error ?? "Invalid fileName.", nameof(fileName));
        }

        // Only serve blobs that are registered in the download table (known literature keys).
        var downloads = await tableContext
            .GetAllAsync<LiteratureDownloadDto>(_storageConfiguration.DownloadTableName)
            .ConfigureAwait(false);

        var match = downloads.FirstOrDefault(d =>
            string.Equals(d.FileName, normalized, StringComparison.OrdinalIgnoreCase));

        if (match is null || string.IsNullOrWhiteSpace(match.FileName))
        {
            throw new FileNotFoundException(
                $"No download catalog entry found for fileName '{normalized}'.",
                normalized);
        }

        // Use the catalog value as the canonical blob key (prevents case/alias tricks).
        var blobKey = match.FileName.Trim();
        return await blobContext
            .GetDocumentStream(blobKey, new StorageContainer(_storageConfiguration.DownloadContainerName), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LiteratureDownload> UploadManualAsync(
        ManualLiteratureUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.File == null || request.File.Length <= 0)
        {
            throw new InvalidOperationException("A valid file is required for manual upload.");
        }

        var fileExtension = Path.GetExtension(request.File.FileName);
        if (!AllowedUploadExtensions.Contains(fileExtension))
        {
            throw new InvalidOperationException("Only .pdf, .xml, and .nxml files are supported.");
        }

        var fileName = ResolveUploadedFileName(request.PmcId, request.File.FileName);
        byte[] fileContent;
        await using (var inputStream = request.File.OpenReadStream())
        await using (var memoryStream = new MemoryStream())
        {
            await inputStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            fileContent = memoryStream.ToArray();
        }

        var contentType = ResolveContentType(fileExtension);
        var document = new ByteDocument(fileName, contentType, fileContent);
        await blobContext
            .Upload(document, new StorageContainer(_storageConfiguration.DownloadContainerName))
            .ConfigureAwait(false);

        var downloadDto = new LiteratureDownloadDto
        {
            PartitionKey = "DownloadedLiterature",
            RowKey = Guid.NewGuid().ToString(),
            DownloadLink = ResolveDoiLink(request.Doi),
            XmlDownloadLink = string.Empty,
            FileName = fileName,
            Title = request.Title ?? string.Empty,
            PmcId = request.PmcId ?? string.Empty,
            Doi = request.Doi ?? string.Empty
        };
        await tableContext
            .UpsertEntityAsync(_storageConfiguration.DownloadTableName, downloadDto)
            .ConfigureAwait(false);

        return new LiteratureDownload
        {
            DownloadLink = downloadDto.DownloadLink,
            XmlDownloadLink = downloadDto.XmlDownloadLink,
            FileName = downloadDto.FileName,
            Title = downloadDto.Title,
            PmcId = downloadDto.PmcId,
            Doi = downloadDto.Doi
        };
    }

    private IList<LiteratureDownload> MapDownloads(IList<LiteratureDownloadDto> downloads)
    {
        var downloadList = new List<LiteratureDownload>();
        foreach (var download in downloads)
        {
            var literatureDownload = new LiteratureDownload
            {
                DownloadLink = download.DownloadLink,
                XmlDownloadLink = download.XmlDownloadLink,
                FileName = download.FileName,
                Title = download.Title,
                PmcId = download.PmcId,
                Doi = download.Doi
            };
            downloadList.Add(literatureDownload);
        }
        return downloadList;
    }

    private static string ResolveUploadedFileName(string? pmcId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? ".pdf" : extension.ToLowerInvariant();
        if (!AllowedUploadExtensions.Contains(normalizedExtension))
        {
            normalizedExtension = ".pdf";
        }

        var idPart = string.IsNullOrWhiteSpace(pmcId)
            ? "manual"
            : pmcId.Trim();
        return $"{idPart}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{normalizedExtension}";
    }

    private static DocumentContentType ResolveContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".xml" => DocumentContentType.Xml,
            ".nxml" => DocumentContentType.Xml,
            _ => DocumentContentType.Pdf
        };
    }

    private static string ResolveDoiLink(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
        {
            return string.Empty;
        }

        return $"https://doi.org/{Uri.EscapeDataString(doi.Trim())}";
    }
}
