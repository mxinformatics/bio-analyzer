'use client';

import {signIn, useSession} from 'next-auth/react';

function Login() {
  const { data: session } = useSession();

  if (session) {
    return (
      <div>
        <h1>Welcome, {session.user?.name}</h1>
        <p>You are logged in.</p>
      </div>
    );
  }

  return (
    <div>
      <h1>Please Log In</h1>
      <button onClick={() => signIn('azure-ad')}>Log in with Azure AD</button>
    </div>
  );
}

export default Login;