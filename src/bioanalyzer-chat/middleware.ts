export { default } from 'next-auth/middleware';

/**
 * Protect App Router API routes (except NextAuth handlers).
 * UI pages already redirect unauthenticated users via useSession in page.tsx.
 */
export const config = {
  matcher: ['/api/((?!auth).*)'],
};
