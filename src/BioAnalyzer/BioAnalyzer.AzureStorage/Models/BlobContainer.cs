using Azure.Storage.Blobs;
using BioAnalyzer.AzureStorage.Contracts;

namespace BioAnalyzer.AzureStorage.Models;

public class BlobContainer(BlobContainerClient blobContainerClient) : IBlobContainer
{
    private readonly BlobContainerClient _blobContainerClient = blobContainerClient ?? throw new ArgumentNullException(nameof(blobContainerClient));


    public async Task<bool> Exists()
    {
        return await _blobContainerClient.ExistsAsync().ConfigureAwait(false);
    }

    public async Task<bool> Exists(CancellationToken cancellationToken)
    {
        return await _blobContainerClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
    }
}