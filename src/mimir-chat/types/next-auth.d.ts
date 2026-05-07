import { DefaultSession } from 'next-auth';
import { DefaultJWT } from 'next-auth/jwt';

declare module 'next-auth' {
  interface Session extends DefaultSession {
    accessToken: string;
    role?: string;
    roles?: string[];
    tenantId?: string;
    tenantName?: string;
  }
}

declare module 'next-auth/jwt' {
  interface JWT extends DefaultJWT {
    accessToken?: string;
    refreshToken?: string;
    expiresAt?: number;
    role?: string;
    roles?: string[];
    tenantId?: string;
    tenantName?: string;
  }
}
