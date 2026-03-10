import NextAuth, { NextAuthOptions } from 'next-auth';
import KeycloakProvider from 'next-auth/providers/keycloak';
import CredentialsProvider from 'next-auth/providers/credentials';

const isDevMode = process.env.AUTH_MODE === 'dev';

const providers = isDevMode
  ? [
      CredentialsProvider({
        name: 'Development Login',
        credentials: {
          username: { label: 'Username', type: 'text', placeholder: 'admin' },
          password: { label: 'Password', type: 'password', placeholder: 'admin' },
        },
        async authorize(credentials) {
          // In dev mode, accept admin/admin as default credentials
          const validUsers = [
            { id: '1', name: 'Admin', email: 'admin@mimir.local', role: 'admin' },
          ];
          if (
            credentials?.username === 'admin' &&
            credentials?.password === 'admin'
          ) {
            return validUsers[0];
          }
          return null;
        },
      }),
    ]
  : [
      KeycloakProvider({
        clientId: process.env.KEYCLOAK_CLIENT_ID!,
        clientSecret: process.env.KEYCLOAK_CLIENT_SECRET!,
        issuer: process.env.KEYCLOAK_ISSUER!,
      }),
    ];

export const authOptions: NextAuthOptions = {
  providers,
  session: {
    strategy: isDevMode ? 'jwt' : 'jwt',
  },
  callbacks: {
    async jwt({ token, account, user }) {
      // On initial sign-in, persist OAuth tokens
      if (account) {
        token.accessToken = account.access_token ?? 'dev-token';
        token.refreshToken = account.refresh_token;
        token.expiresAt = account.expires_at;
      }
      if (user) {
        token.name = user.name;
        token.email = user.email;
      }
      return token;
    },
    async session({ session, token }) {
      // Expose accessToken to the client session
      session.accessToken = (token.accessToken as string) ?? 'dev-token';
      return session;
    },
  },
  pages: {},
};

export default NextAuth(authOptions);
