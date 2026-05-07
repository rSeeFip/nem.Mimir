import { getSession } from 'next-auth/react';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function apiClient<T = unknown>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const session: any = await getSession();
  const headers = new Headers(options.headers);
  
  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  if (session?.accessToken) {
    headers.set('Authorization', `Bearer ${session.accessToken}`);
  } else if (session?.token) {
    headers.set('Authorization', `Bearer ${session.token}`);
  } else if (session?.idToken) {
    headers.set('Authorization', `Bearer ${session.idToken}`);
  }

  const res = await fetch(path, { ...options, headers });

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, text);
  }

  if (res.status === 204) {
    return undefined as unknown as T;
  }

  return res.json() as Promise<T>;
}
