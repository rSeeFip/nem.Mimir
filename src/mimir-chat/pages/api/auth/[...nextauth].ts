import NextAuth, { NextAuthOptions } from 'next-auth';
import KeycloakProvider from 'next-auth/providers/keycloak';
import CredentialsProvider from 'next-auth/providers/credentials';

const isDevMode = process.env.AUTH_MODE === 'dev';

type DevUser = {
  id: string;
  name: string;
  email: string;
  password: string;
  role: 'platform-admin' | 'tenant-admin' | 'user';
  tenantId: string;
  tenantName: string;
  accessToken: string;
};

const devUsers: DevUser[] = [
  {
    id: 'admin',
    name: 'Platform Admin',
    email: 'admin@mimir.local',
    password: 'admin',
    role: 'platform-admin',
    tenantId: 'default',
    tenantName: 'Default',
    accessToken: 'dev-token-admin',
  },
  {
    id: 'tenant-admin-a',
    name: 'Tenant Admin A',
    email: 'tenant-admin-a@mimir.local',
    password: 'tenant-admin-a',
    role: 'tenant-admin',
    tenantId: 'tenant-a',
    tenantName: 'Tenant A',
    accessToken: 'dev-token-tenant-admin-a',
  },
  {
    id: 'user-a',
    name: 'User A',
    email: 'user-a@mimir.local',
    password: 'user-a',
    role: 'user',
    tenantId: 'tenant-a',
    tenantName: 'Tenant A',
    accessToken: 'dev-token-user-a',
  },
  {
    id: 'user-b',
    name: 'User B',
    email: 'user-b@mimir.local',
    password: 'user-b',
    role: 'user',
    tenantId: 'tenant-b',
    tenantName: 'Tenant B',
    accessToken: 'dev-token-user-b',
  },
];

function isDevUser(user: unknown): user is DevUser {
  return typeof user === 'object' && user !== null && 'role' in user && 'tenantId' in user;
}

const providers = isDevMode
  ? [
      CredentialsProvider({
        name: 'Development Login',
        credentials: {
          username: { label: 'Username', type: 'text', placeholder: 'admin' },
          password: { label: 'Password', type: 'password', placeholder: 'admin' },
        },
        async authorize(credentials) {
          return (
            devUsers.find(
              (user) =>
                user.id === credentials?.username &&
                user.password === credentials?.password,
            ) ?? null
          );
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
      if (isDevUser(user)) {
        token.name = user.name;
        token.email = user.email;
        token.role = user.role;
        token.roles = [user.role];
        token.tenantId = user.tenantId;
        token.tenantName = user.tenantName;
        token.accessToken = user.accessToken;
      }
      return token;
    },
    async session({ session, token }) {
      // Expose accessToken to the client session
      session.accessToken = (token.accessToken as string) ?? 'dev-token';
      if (session.user) {
        session.user.name = session.user.name ?? token.name ?? null;
        session.user.email = session.user.email ?? token.email ?? null;
      }
      session.role = token.role as string | undefined;
      session.roles = token.roles as string[] | undefined;
      session.tenantId = token.tenantId as string | undefined;
      session.tenantName = token.tenantName as string | undefined;
      return session;
    },
  },
  pages: {},
};

export default NextAuth(authOptions);
