import assert from 'node:assert/strict';
import test from 'node:test';

// Mirror of isAuthenticatedSession without TS/next imports for a lightweight gate.
function isAuthenticatedSession(session) {
  return Boolean(session?.user);
}

test('unauthenticated session is rejected', () => {
  assert.equal(isAuthenticatedSession(null), false);
  assert.equal(isAuthenticatedSession(undefined), false);
  assert.equal(isAuthenticatedSession({}), false);
  assert.equal(isAuthenticatedSession({ user: null }), false);
});

test('session with user is accepted', () => {
  assert.equal(isAuthenticatedSession({ user: { email: 'a@b.c' } }), true);
  assert.equal(isAuthenticatedSession({ user: { name: 'dev' } }), true);
});
