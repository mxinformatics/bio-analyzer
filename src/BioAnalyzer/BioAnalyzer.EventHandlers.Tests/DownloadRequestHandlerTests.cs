using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BioAnalyzer.AzureStorage.Contracts.Models;
using BioAnalyzer.EventHandlers.Domain.Clients;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BioAnalyzer.EventHandlers.Tests;

public class DownloadRequestHandlerTests
{
    [Fact]
    public async Task Run_NullPayload_DeadLettersAndReturnsNull()
    {
        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        var jobs = new Mock<IIngestJobStatusClient>(MockBehavior.Strict);
        var httpFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var messageActions = CreateMessageActionsMock();

        messageActions
            .Setup(a => a.DeadLetterMessageAsync(
                It.IsAny<ServiceBusReceivedMessage>(),
                It.IsAny<Dictionary<string, object>>(),
                "InvalidPayload",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sut = CreateSut(storage, httpFactory, jobs);
        var message = CreateMessage(BinaryData.FromString("null"));

        var result = await sut.Run(message, messageActions.Object);

        Assert.Null(result);
        messageActions.Verify(
            a => a.DeadLetterMessageAsync(
                message,
                It.IsAny<Dictionary<string, object>>(),
                "InvalidPayload",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        messageActions.Verify(
            a => a.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        storage.Verify(
            s => s.UploadDocumentAsync(It.IsAny<string>(), It.IsAny<DocumentContentType>(), It.IsAny<byte[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_DownloadFailure_CompletesWithoutOutput_AndMarksJobFailed()
    {
        const string jobId = "job-fail-1";
        const string pmcId = "PMC99999999";

        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        var jobs = new Mock<IIngestJobStatusClient>(MockBehavior.Strict);
        jobs
            .Setup(j => j.MarkItemProgressAsync(
                jobId,
                pmcId,
                "Downloading",
                "Download",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        jobs
            .Setup(j => j.MarkItemProgressAsync(
                jobId,
                pmcId,
                "Failed",
                "Download",
                null,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var httpFactory = CreateHttpFactory(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)));

        var messageActions = CreateMessageActionsMock();
        messageActions
            .Setup(a => a.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sut = CreateSut(storage, httpFactory, jobs);
        var request = new DownloadRequest
        {
            PmcId = pmcId,
            Title = "Missing OA paper",
            Doi = "10.1/x",
            JobId = jobId,
            DownloadLink = "https://example.com/ignored.pdf"
        };
        var message = CreateMessage(BinaryData.FromObjectAsJson(request));

        var result = await sut.Run(message, messageActions.Object);

        Assert.Null(result);
        messageActions.Verify(
            a => a.CompleteMessageAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
        messageActions.Verify(
            a => a.DeadLetterMessageAsync(
                It.IsAny<ServiceBusReceivedMessage>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        jobs.Verify(
            j => j.MarkItemProgressAsync(
                jobId,
                pmcId,
                "Failed",
                "Download",
                null,
                It.Is<string?>(e => !string.IsNullOrWhiteSpace(e)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        storage.Verify(
            s => s.UploadDocumentAsync(It.IsAny<string>(), It.IsAny<DocumentContentType>(), It.IsAny<byte[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_DownloadSuccess_ReturnsDownloadedLiterature_AndUploadsBlob()
    {
        const string jobId = "job-ok-1";
        const string pmcId = "PMC5334499";
        const string normalizedPmc = "PMC5334499";

        var storage = new Mock<IStorageClient>(MockBehavior.Strict);
        storage
            .Setup(s => s.UploadDocumentAsync(
                $"{normalizedPmc}.xml",
                DocumentContentType.Xml,
                It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var jobs = new Mock<IIngestJobStatusClient>(MockBehavior.Strict);
        jobs
            .Setup(j => j.MarkItemProgressAsync(
                jobId,
                pmcId,
                "Downloading",
                "Download",
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var listXml =
            """
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <Contents><Key>metadata/PMC5334499.1.json</Key></Contents>
            </ListBucketResult>
            """;
        var metadataJson = JsonSerializer.Serialize(new
        {
            xml_url = "https://pmc-oa-opendata.s3.amazonaws.com/oa_comm/xml/all/PMC5334499.xml",
            pdf_url = "https://pmc-oa-opendata.s3.amazonaws.com/oa_comm/pdf/all/PMC5334499.pdf"
        });
        var xmlBytes = Encoding.UTF8.GetBytes("<article>ok</article>");

        var httpFactory = CreateHttpFactory(new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("list-type=2", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(listXml, Encoding.UTF8, "application/xml")
                };
            }

            if (url.Contains("metadata/PMC5334499.1.json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(metadataJson, Encoding.UTF8, "application/json")
                };
            }

            if (url.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(xmlBytes)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var messageActions = CreateMessageActionsMock();
        messageActions
            .Setup(a => a.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var sut = CreateSut(storage, httpFactory, jobs);
        var request = new DownloadRequest
        {
            PmcId = pmcId,
            Title = "OA paper",
            Doi = "10.1/ok",
            JobId = jobId
        };
        var message = CreateMessage(BinaryData.FromObjectAsJson(request));

        var result = await sut.Run(message, messageActions.Object);

        Assert.NotNull(result);
        Assert.Equal(pmcId, result!.PmcId);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("OA paper", result.Title);
        Assert.Equal($"{normalizedPmc}.xml", result.FileName);
        Assert.Contains("PMC5334499.xml", result.DownloadLink, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.XmlDownloadLink));

        messageActions.Verify(
            a => a.CompleteMessageAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
        storage.VerifyAll();
        jobs.Verify(
            j => j.MarkItemProgressAsync(
                jobId,
                pmcId,
                "Failed",
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DownloadRequestHandler CreateSut(
        Mock<IStorageClient> storage,
        Mock<IHttpClientFactory> httpFactory,
        Mock<IIngestJobStatusClient> jobs)
    {
        return new DownloadRequestHandler(
            NullLogger<DownloadRequestHandler>.Instance,
            storage.Object,
            httpFactory.Object,
            jobs.Object);
    }

    private static Mock<IHttpClientFactory> CreateHttpFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        // HttpClientFactoryExtensions.CreateClient() calls CreateClient(string) with the default name.
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory;
    }

    private static Mock<ServiceBusMessageActions> CreateMessageActionsMock()
    {
        return new Mock<ServiceBusMessageActions>(MockBehavior.Strict) { CallBase = false };
    }

    private static ServiceBusReceivedMessage CreateMessage(BinaryData body)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: body,
            messageId: Guid.NewGuid().ToString("N"));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
