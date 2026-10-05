import type { NextAuthOptions } from 'next-auth';
import AzureAdProvider from 'next-auth/providers/azure-ad';

function resolveResearchApiScope(): string {
  return (process.env.RESEARCH_API_SCOPE || process.env.AZURE_AD_RESEARCH_API_SCOPE || '').trim();
}

function buildAzureAdScopes(): string {
  const base = ['openid', 'profile', 'email', 'offline_access'];
  const apiScope = resolveResearchApiScope();
  if (apiScope) {
    base.push(apiScope);
  }
  return base.join(' ');
}

export const authOptions: NextAuthOptions = {
  providers: [
    AzureAdProvider({
      clientId: process.env.AZURE_AD_CLIENT_ID || '',
      clientSecret: process.env.AZURE_AD_CLIENT_SECRET || '',
      tenantId: process.env.AZURE_AD_TENANT_ID || '',
      authorization: {
        params: {
          scope: buildAzureAdScopes(),
        },
      },
    }),
  ],
  secret: process.env.NEXTAUTH_SECRET,
  callbacks: {
    async jwt({ token, account, profile }) {
      if (account?.access_token) {
        token.accessToken = account.access_token;
        token.accessTokenExpires = account.expires_at
          ? account.expires_at * 1000
          : Date.now() + 3600 * 1000;
      }
      if (account?.id_token) {
        // Prefer oid from profile when present for requester identity.
        const p = profile as { oid?: string; sub?: string } | undefined;
        token.oid = p?.oid || p?.sub || token.sub;
      }
      return token;
    },
    async session({ session, token }) {
      const extended = session as typeof session & {
        accessToken?: string;
        user: typeof session.user & { id?: string; oid?: string };
      };
      if (typeof token.accessToken === 'string') {
        extended.accessToken = token.accessToken;
      }
      if (session.user) {
        extended.user.id = (token.oid as string) || (token.sub as string) || session.user.email || undefined;
        extended.user.oid = (token.oid as string) || undefined;
      }
      return extended;
    },
  },
};
