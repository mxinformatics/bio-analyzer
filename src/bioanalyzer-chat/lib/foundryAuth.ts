import { DefaultAzureCredential, type AccessToken } from '@azure/identity';
import type { FetchFunction } from '@ai-sdk/provider-utils';

// Microsoft Foundry resources are Cognitive Services accounts, so Entra ID
// tokens must be scoped to the Cognitive Services audience.
const FOUNDRY_SCOPE = 'https://cognitiveservices.azure.com/.default';
const TOKEN_REFRESH_MARGIN_MS = 5 * 60 * 1000;

const credential = new DefaultAzureCredential();

let cachedToken: AccessToken | null = null;

async function getAccessToken(): Promise<string> {
  if (cachedToken && cachedToken.expiresOnTimestamp - TOKEN_REFRESH_MARGIN_MS > Date.now()) {
    return cachedToken.token;
  }

  const token = await credential.getToken(FOUNDRY_SCOPE);
  if (!token) {
    throw new Error('Failed to acquire an Azure AD access token for Microsoft Foundry.');
  }

  cachedToken = token;
  return token.token;
}

// Replaces the Azure OpenAI provider's api-key header with a Bearer token
// obtained via DefaultAzureCredential.
export const foundryFetch: FetchFunction = async (input, init) => {
  const accessToken = await getAccessToken();
  const headers = new Headers(init?.headers);
  headers.delete('api-key');
  headers.set('Authorization', `Bearer ${accessToken}`);

  return fetch(input, { ...init, headers });
};
