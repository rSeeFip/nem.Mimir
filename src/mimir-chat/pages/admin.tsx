import Head from 'next/head';
import { useRouter } from 'next/router';
import { useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useQuery } from 'react-query';
import { useTranslation } from 'next-i18next';
import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import { IconArrowLeft, IconShield } from '@tabler/icons-react';
import Link from 'next/link';

import { apiFetch } from '@/utils/app/api';
import { LoadingState } from '@/components/states/LoadingState';
import { ErrorState } from '@/components/states/ErrorState';

interface TenantDto {
  id: string;
  name: string;
  status: string;
  createdAt: string;
}

interface TenantUsageSummary {
  tenantId: string;
  totalTokens: number;
  totalCostUsd: number;
}

function hasRole(session: ReturnType<typeof useSession>['data'], role: string): boolean {
  const roles: string[] = (session?.user as { roles?: string[] })?.roles ?? [];
  return roles.includes(role);
}

export default function AdminPage() {
  const { t } = useTranslation('common');
  const router = useRouter();
  const { data: session, status } = useSession();

  const isPlatformAdmin = hasRole(session, 'platform-admin');
  const isTenantAdmin = hasRole(session, 'tenant-admin');
  const isAdmin = isPlatformAdmin || isTenantAdmin;

  useEffect(() => {
    if (status === 'authenticated' && !isAdmin) {
      router.push('/');
    }
  }, [status, isAdmin, router]);

  const { data: tenants, isLoading: tenantsLoading, error: tenantsError } = useQuery<TenantDto[], Error>(
    ['tenants'],
    () => apiFetch('/api/tenants'),
    { enabled: isPlatformAdmin },
  );

  const { data: usage, isLoading: usageLoading, error: usageError } = useQuery<TenantUsageSummary, Error>(
    ['admin-usage'],
    () => apiFetch('/api/billing/usage'),
    { enabled: isAdmin },
  );

  if (status === 'loading') return <LoadingState />;
  if (!isAdmin) return null;

  return (
    <>
      <Head>
        <title>Admin Dashboard - nem.Mimir</title>
      </Head>
      <div className="flex h-screen w-screen flex-col bg-[#202123] text-white overflow-auto">
        <header className="flex items-center justify-between px-6 py-4 border-b border-white/20 bg-[#343541]">
          <div className="flex items-center gap-4">
            <Link href="/" className="text-gray-400 hover:text-white transition-colors">
              <IconArrowLeft size={24} />
            </Link>
            <div className="flex items-center gap-2">
              <IconShield size={24} />
              <h1 className="text-xl font-semibold">Admin Dashboard</h1>
            </div>
          </div>
          <span className="text-xs text-gray-400 bg-[#202123] px-3 py-1 rounded-full border border-white/10">
            {isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
          </span>
        </header>

        <main className="flex-1 max-w-5xl w-full mx-auto p-6 space-y-8">
          {isPlatformAdmin && (
            <section data-testid="tenant-list">
              <h2 className="text-lg font-semibold mb-4">Tenants</h2>
              {tenantsLoading && <LoadingState />}
              {tenantsError && <ErrorState message={tenantsError.message} />}
              {tenants && (
                <div className="bg-[#343541] rounded-lg border border-white/20 overflow-hidden">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-white/10 text-gray-400">
                        <th className="px-4 py-3 text-left">Name</th>
                        <th className="px-4 py-3 text-left">Status</th>
                        <th className="px-4 py-3 text-left">Created</th>
                      </tr>
                    </thead>
                    <tbody>
                      {tenants.map((tenant) => (
                        <tr key={tenant.id} className="border-b border-white/5 hover:bg-white/5">
                          <td className="px-4 py-3">{tenant.name}</td>
                          <td className="px-4 py-3">
                            <span className={`px-2 py-0.5 rounded-full text-xs ${tenant.status === 'Active' ? 'bg-green-500/20 text-green-400' : 'bg-gray-500/20 text-gray-400'}`}>
                              {tenant.status}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-gray-400">
                            {new Date(tenant.createdAt).toLocaleDateString()}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          )}

          <section data-testid="usage-chart">
            <h2 className="text-lg font-semibold mb-4">Usage Metrics</h2>
            {usageLoading && <LoadingState />}
            {usageError && <ErrorState message={usageError.message} />}
            {usage && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-[#343541] rounded-lg border border-white/20 p-6">
                  <p className="text-sm text-gray-400 mb-1">Total Tokens</p>
                  <p className="text-3xl font-bold">{usage.totalTokens.toLocaleString()}</p>
                </div>
                <div className="bg-[#343541] rounded-lg border border-white/20 p-6">
                  <p className="text-sm text-gray-400 mb-1">Total Cost</p>
                  <p className="text-3xl font-bold text-green-400">${usage.totalCostUsd.toFixed(4)}</p>
                </div>
              </div>
            )}
          </section>

          <section data-testid="rate-limit-config">
            <h2 className="text-lg font-semibold mb-4">Rate Limit Configuration</h2>
            <div className="bg-[#343541] rounded-lg border border-white/20 p-6 space-y-3">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Default Limit</span>
                <span className="text-sm font-mono">100 req/min</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Window</span>
                <span className="text-sm font-mono">Sliding (60s)</span>
              </div>
              <p className="text-xs text-gray-500 pt-2">
                Per-tenant overrides available via Tenant Configuration API (T18).
              </p>
            </div>
          </section>
        </main>
      </div>
    </>
  );
}

export const getServerSideProps = async ({ locale }: { locale: string }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? 'en', ['common'])),
    },
  };
};
