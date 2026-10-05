import type { Session } from 'next-auth';
import { getServerSession } from 'next-auth/next';
import { NextResponse } from 'next/server';
import { authOptions } from '@/lib/authOptions';

export type AppSession = Session & {
  accessToken?: string;
  user?: Session['user'] & { id?: string; oid?: string };
};

export type ApiSessionResult =
  | { ok: true; session: AppSession }
  | { ok: false; response: NextResponse };

/**
 * Require a valid NextAuth session for agent/API routes.
 * Returns 401 JSON when unauthenticated.
 */
export async function requireApiSession(): Promise<ApiSessionResult> {
  const session = (await getServerSession(authOptions)) as AppSession | null;
  if (!session?.user) {
    return {
      ok: false,
      response: NextResponse.json({ error: 'Unauthorized' }, { status: 401 }),
    };
  }

  return { ok: true, session };
}

/** Pure helper for tests: treat missing/empty user as unauthorized. */
export function isAuthenticatedSession(session: Session | null | undefined): boolean {
  return Boolean(session?.user);
}
