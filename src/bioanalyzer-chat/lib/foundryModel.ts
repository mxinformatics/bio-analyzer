import { createAzure } from '@ai-sdk/azure';
import { foundryFetch } from './foundryAuth';

function requireEnv(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is required when AGENTIC_CHAT_ENABLED is on.`);
  }
  return value;
}

/**
 * Builds a Foundry/Azure OpenAI chat model using DefaultAzureCredential (foundryFetch).
 * Endpoint example: https://aif-....services.ai.azure.com
 */
export function createFoundryChatModel() {
  const endpoint = requireEnv('AZURE_FOUNDRY_ENDPOINT').replace(/\/$/, '');
  const deployment = requireEnv('AZURE_FOUNDRY_DEPLOYMENT');
  const apiVersion = process.env.AZURE_FOUNDRY_API_VERSION?.trim() || '2024-12-01-preview';

  const azure = createAzure({
    baseURL: `${endpoint}/openai`,
    apiVersion,
    // apiKey is required by the provider type but unused — foundryFetch strips it
    // and injects a Bearer token from DefaultAzureCredential.
    apiKey: 'managed-identity',
    fetch: foundryFetch,
    useDeploymentBasedUrls: true,
  });

  return azure(deployment);
}

export function getAgentGenerationSettings() {
  const maxOutputTokens = Number.parseInt(process.env.AI_MAX_OUTPUT_TOKENS || '1000', 10);
  const temperature = Number.parseFloat(process.env.AI_TEMPERATURE || '0.1');

  return {
    maxOutputTokens: Number.isFinite(maxOutputTokens) && maxOutputTokens > 0 ? maxOutputTokens : 1000,
    temperature: Number.isFinite(temperature) ? temperature : 0.1,
  };
}
