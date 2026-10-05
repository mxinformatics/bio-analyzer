using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
// ReSharper disable InconsistentNaming

var builder = DistributedApplication.CreateBuilder(args);
const string uvVenvPython = "./.venv/bin/python";
const string uvCertifiCaBundle = ".venv/lib/python3.13/site-packages/certifi/cacert.pem";

//
// var sql = builder.AddSqlServer("bioAnalyzerSql")
//     .WithVolume("bioAnalyzerSqlData", "/var/opt/mssql");
//    //.WithLifetime(ContainerLifetime.Persistent);
//
// var db = sql
//     .AddDatabase("BioAnalyzer");

var keyVaultUrl = builder.Configuration["KeyVault:Url"]
    ?? throw new InvalidOperationException("KeyVault:Url is not configured.");
var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
var neo4jPassword = secretClient.GetSecret("GraphDatabase--Password").Value.Value;

var neo4j = builder
    .AddContainer("neo4j", "neo4j", "2025.05.0-enterprise")
    .WithEndpoint(port: 7474, targetPort: 7474, name: "http", scheme: "http")
    .WithEndpoint(port: 7687, targetPort: 7687, name: "bolt", scheme: "bolt")
    .WithEnvironment("NEO4J_AUTH", $"neo4j/{neo4jPassword}")
    .WithEnvironment("NEO4J_ACCEPT_LICENSE_AGREEMENT", "yes")
    .WithEnvironment("NEO4J_apoc_export_file_enabled", "true")
    .WithEnvironment("NEO4J_apoc_import_file_enabled", "true")
    .WithEnvironment("NEO4J_apoc_import_file_use__neo4j__config", "true")
    .WithEnvironment("NEO4J_PLUGINS", "[\"apoc\", \"graph-data-science\"]")
    .WithEnvironment("NEO4J_dbms_security_procedures_unrestricted", "apoc.*,gds.*,semantics.*,n10s.*")
    .WithEnvironment("NEO4J_dbms_unmanaged__extension__classes", "n10s.endpoint=/rdf")
    .WithEnvironment("NEO4J_dbms_security_allow__csv__import__from__file__urls", "true")
    .WithEnvironment("NEO4J_server_directories_import", "/var/lib/neo4j/import")
    .WithEnvironment("NEO4J_dbms_databases_seed__from__uri__providers", "URLConnectionSeedProvider")
    .WithBindMount("../../pipeline/.pipeline-data/data/data", "/var/lib/neo4j/data")
    .WithBindMount("../../pipeline/.pipeline-data/data/import", "/var/lib/neo4j/import")
    .WithBindMount("../../pipeline/.pipeline-data/data/conf", "/var/lib/neo4j/conf")
    .WithBindMount("../../pipeline/.pipeline-data/data/plugins", "/var/lib/neo4j/plugins")
    .WithBindMount("../../pipeline/.pipeline-data/data/rdf", "/rdf");

var chunkApi = builder
    .AddExecutable(
        "chunk-api",
        uvVenvPython,
        "../../pipeline/chunk-api",
        "-m",
        "uvicorn",
        "main:app",
        "--host",
        "0.0.0.0",
        "--port",
        "8001")
    .WithEnvironment("REQUIRE_API_KEY", "false")
    .WithEnvironment("AZURE_STORAGE_ACCOUNT", "bioanalyzerpoc")
    .WithEnvironment("AZURE_STORAGE_CHUNKS_CONTAINER", "extractedtext")
    .WithEnvironment("SOURCE_TIMEOUT_SECONDS", "60")
    .WithEnvironment("MAX_SOURCE_BYTES", "41943040")
    .WithEnvironment("AZURE_STORAGE_SOURCE_CONTAINER", "literature")
    .WithEnvironment("DEFAULT_CHUNK_SIZE", "1600")
    .WithEnvironment("DEFAULT_CHUNK_OVERLAP", "250")
    .WithHttpEndpoint(port: 8001, targetPort: 8001, name: "http", isProxied: false);

var embeddingApi = builder
    .AddExecutable(
        "embedding-api",
        uvVenvPython,
        "../../pipeline/embed-api",
        "-m",
        "uvicorn",
        "main:app",
        "--host",
        "0.0.0.0",
        "--port",
        "8002")
    .WithEnvironment("REQUIRE_API_KEY", "false")
    .WithEnvironment("AZURE_STORAGE_ACCOUNT", "bioanalyzerpoc")
    .WithEnvironment("AZURE_STORAGE_CHUNKS_CONTAINER", "extractedtext")
    .WithEnvironment("AZURE_STORAGE_EMBEDDING_CONTAINER", "embeddedchunks")
    .WithEnvironment("EMBEDDING_BATCH_SIZE", "100")
    .WithEnvironment("EMBEDDING_MAX_RETRIES", "1")
    .WithEnvironment("MAX_CHUNKS_PER_REQUEST", "25000")
    .WithEnvironment("SSL_CERT_FILE", uvCertifiCaBundle)
    .WithEnvironment("FOUNDRY_AI_ENDPOINT", "https://aif-mxinfo-bioanalyzer-poc-eus.services.ai.azure.com/")
    .WithEnvironment("FOUNDRY_AI_EMBEDDING_DEPLOYMENT", "text-embedding-3-small")
    .WithHttpEndpoint(port: 8002, targetPort: 8002, name: "http", isProxied: false);

var graphDataApi = builder
    .AddExecutable(
        "graph-data-api",
        uvVenvPython,
        "../../pipeline/graph-data-api",
        "-m",
        "uvicorn",
        "main:app",
        "--host",
        "0.0.0.0",
        "--port",
        "8080")
    .WithEnvironment("REQUIRE_API_KEY", "false")
    .WithEnvironment("AZURE_STORAGE_ACCOUNT", "bioanalyzerpoc")
    .WithEnvironment("AZURE_STORAGE_EMBEDDING_CONTAINER", "embeddedchunks")
    .WithEnvironment("NEO4J_URI", "bolt://localhost:7687")
    .WithEnvironment("NEO4J_DATABASE", "bio")
    .WithEnvironment("NEO4J_VECTOR_INDEX", "bio_embedding_index")
    .WithEnvironment("NEO4J_TEXT_INDEX", "bio_text_index")
    .WithEnvironment("NEO4J_VECTOR_DIMENSIONS", "1536")
    .WithEnvironment("KEY_VAULT_URL", "https://kv-mxinfo-bioanalyze-poc.vault.azure.net/")
    .WithEnvironment("NEO4J_USERNAME_SECRET_NAME", "GraphDatabase--Username")
    .WithEnvironment("NEO4J_PASSWORD_SECRET_NAME", "GraphDatabase--Password")
    .WithEnvironment("GRAPH_QUERY_ALPHA", "0.7")
    .WithEnvironment("DEFAULT_TOP_K", "5")
    .WithEnvironment("MAX_QUERY_RESULTS", "10")
    .WithEnvironment("SSL_CERT_FILE", uvCertifiCaBundle)
    .WithEnvironment("FOUNDRY_AI_ENDPOINT", "https://aif-mxinfo-bioanalyzer-poc-eus.services.ai.azure.com/")
    .WithEnvironment("FOUNDRY_AI_EMBEDDING_DEPLOYMENT", "text-embedding-3-small")
    .WithEnvironment("FOUNDRY_AI_CHAT_DEPLOYMENT", "gpt-5.4-nano")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http", isProxied: false)
    .WithEnvironment("NEO4J_URI", neo4j.GetEndpoint("bolt"))
    .WaitFor(neo4j);

// Shared service credential for Research.Api (Phase B). Override via parameters/secrets in non-dev.
var researchApiKey = builder.Configuration["ResearchApi:ApiKey"]
    ?? builder.Configuration["RESEARCH_API_KEY"]
    ?? "local-dev-research-api-key";

var researchApi = builder
    .AddProject<Projects.BioAnalyzer_Research_Api>("researchApi")
    .WithEnvironment("ApiAuthentication__Enabled", "true")
    .WithEnvironment("ApiAuthentication__ApiKey", researchApiKey)
    .WithEnvironment("ApiAuthentication__HeaderName", "X-Api-Key")
    //.WithHttpEndpoint(env: "RESEARCH_API_PORT")
   // .WaitFor(db)
   // .WithReference(db)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.BioAnalyzer_App>("researchApp")
    .WithReference(researchApi)
    .WaitFor(researchApi)
    .WithEnvironment("RESEARCH_API_KEY", researchApiKey)
    .WithEnvironment("ApiAuthentication__ApiKey", researchApiKey)
    //.WithHttpEndpoint(env: "RESEARCH_APP_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();


builder.AddNpmApp("react", "../../bioanalyzer-chat")
    .WithReference(researchApi)
    .WaitFor(researchApi)
    .WithHttpEndpoint(env: "PORT", name: "http", port: 3000)
    .WithEnvironment("RESEARCH_API_KEY", researchApiKey)
    //.WithEnvironment("SEARCH_API_BASE_URL", searchApi.Resource.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();




builder.AddAzureFunctionsProject<Projects.BioAnalyzer_EventHandlers>("eventHandlers")
    .WithEnvironment("DocumentProcessing__ChunkApiUrl", chunkApi.GetEndpoint("http"))
    .WithEnvironment("DocumentProcessing__EmbeddingApiUrl", embeddingApi.GetEndpoint("http"))
    .WithEnvironment("DocumentProcessing__GraphDataApiUrl", graphDataApi.GetEndpoint("http"))
    .WithEnvironment("DocumentProcessing__ResearchApiUrl", researchApi.GetEndpoint("http"))
    .WithEnvironment("DocumentProcessing__ResearchApiKey", researchApiKey)
    .WithEnvironment("RESEARCH_API_KEY", researchApiKey)
    .WithEnvironment("ApiAuthentication__ApiKey", researchApiKey)
    .WaitFor(chunkApi)
    .WaitFor(embeddingApi)
    .WaitFor(graphDataApi)
    .WaitFor(researchApi)
    .PublishAsDockerFile();

builder.Build().Run();