import { useState } from 'react';
import Head from 'next/head';
import { useQuery } from 'react-query';
import { useTranslation } from 'next-i18next';
import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import { IconArrowLeft, IconReceipt } from '@tabler/icons-react';
import Link from 'next/link';

import { apiFetch } from '@/utils/app/api';

interface ModelUsage {
  model: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  totalCostUsd: number;
  requestCount: number;
}

interface TenantUsageSummary {
  tenantId: string;
  from: string;
  to: string;
  totalTokens: number;
  totalCostUsd: number;
  modelBreakdown: Record<string, ModelUsage>;
}

export default function BillingPage() {
  const { t } = useTranslation('billing');
  
  // Set default dates (e.g., first day of current month to today)
  const today = new Date();
  const firstDayOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
  
  const [fromDate, setFromDate] = useState(firstDayOfMonth.toISOString().split('T')[0]);
  const [toDate, setToDate] = useState(today.toISOString().split('T')[0]);

  const { data, isLoading, error } = useQuery<TenantUsageSummary, Error>(
    ['billing-usage', fromDate, toDate],
    () => {
      const params = new URLSearchParams();
      if (fromDate) params.set('from', `${fromDate}T00:00:00.000Z`);
      if (toDate) params.set('to', `${toDate}T23:59:59.999Z`);
      return apiFetch(`/api/billing/usage?${params.toString()}`);
    },
    {
      keepPreviousData: true,
    }
  );

  return (
    <>
      <Head>
        <title>Billing & Usage - nem.Mimir</title>
      </Head>
      <div className="flex h-screen w-screen flex-col bg-[#202123] text-white overflow-auto">
        {/* Header */}
        <header className="flex items-center justify-between px-6 py-4 border-b border-white/20 bg-[#343541]">
          <div className="flex items-center gap-4">
            <Link href="/" className="text-gray-400 hover:text-white transition-colors">
              <IconArrowLeft size={24} />
            </Link>
            <div className="flex items-center gap-2">
              <IconReceipt size={24} />
              <h1 className="text-xl font-semibold">Billing & Usage Dashboard</h1>
            </div>
          </div>
        </header>

        {/* Content */}
        <main className="flex-1 max-w-5xl w-full mx-auto p-6 space-y-8">
          {/* Controls */}
          <div className="flex items-end gap-4 bg-[#343541] p-4 rounded-lg border border-white/20">
            <div className="flex flex-col gap-1">
              <label htmlFor="fromDate" className="text-sm text-gray-400">From</label>
              <input 
                id="fromDate"
                type="date" 
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="bg-[#202123] border border-white/20 rounded p-2 text-sm text-white focus:outline-none focus:border-blue-500"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label htmlFor="toDate" className="text-sm text-gray-400">To</label>
              <input 
                id="toDate"
                type="date" 
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="bg-[#202123] border border-white/20 rounded p-2 text-sm text-white focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>

          {/* State handling */}
          {isLoading && (
            <div className="flex justify-center items-center py-20">
              <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-white"></div>
            </div>
          )}

          {error && (
            <div className="bg-red-500/10 border border-red-500 text-red-500 p-4 rounded-lg">
              <h3 className="font-semibold">Failed to load billing data</h3>
              <p className="text-sm mt-1">{error.message}</p>
            </div>
          )}

          {data && (
            <div className="space-y-6">
              {/* Summary Cards */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-[#343541] p-6 rounded-lg border border-white/20 flex flex-col items-center justify-center text-center">
                  <h3 className="text-gray-400 text-sm uppercase tracking-wider mb-2">Total Cost</h3>
                  <div className="text-4xl font-bold text-green-400">
                    ${data.totalCostUsd.toFixed(4)}
                  </div>
                </div>
                <div className="bg-[#343541] p-6 rounded-lg border border-white/20 flex flex-col items-center justify-center text-center">
                  <h3 className="text-gray-400 text-sm uppercase tracking-wider mb-2">Total Tokens</h3>
                  <div className="text-4xl font-bold text-blue-400">
                    {data.totalTokens.toLocaleString()}
                  </div>
                </div>
              </div>

              {/* Model Breakdown */}
              <div className="bg-[#343541] rounded-lg border border-white/20 overflow-hidden">
                <div className="px-6 py-4 border-b border-white/20 bg-black/10">
                  <h2 className="text-lg font-semibold">Model Breakdown</h2>
                </div>
                
                {Object.keys(data.modelBreakdown).length === 0 ? (
                  <div className="p-6 text-center text-gray-400">
                    No usage data found for the selected period.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm text-left">
                      <thead className="text-xs text-gray-400 uppercase bg-black/20">
                        <tr>
                          <th className="px-6 py-3 font-medium">Model</th>
                          <th className="px-6 py-3 font-medium text-right">Requests</th>
                          <th className="px-6 py-3 font-medium text-right">Prompt Tokens</th>
                          <th className="px-6 py-3 font-medium text-right">Completion Tokens</th>
                          <th className="px-6 py-3 font-medium text-right">Total Tokens</th>
                          <th className="px-6 py-3 font-medium text-right">Cost (USD)</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-white/10">
                        {Object.values(data.modelBreakdown).map((usage) => (
                          <tr key={usage.model} className="hover:bg-white/5 transition-colors">
                            <td className="px-6 py-4 font-medium whitespace-nowrap">{usage.model}</td>
                            <td className="px-6 py-4 text-right">{usage.requestCount.toLocaleString()}</td>
                            <td className="px-6 py-4 text-right text-gray-300">{usage.promptTokens.toLocaleString()}</td>
                            <td className="px-6 py-4 text-right text-gray-300">{usage.completionTokens.toLocaleString()}</td>
                            <td className="px-6 py-4 text-right font-medium">{usage.totalTokens.toLocaleString()}</td>
                            <td className="px-6 py-4 text-right text-green-400 font-medium">
                              ${usage.totalCostUsd.toFixed(4)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}
        </main>
      </div>
    </>
  );
}

export const getServerSideProps = async ({ locale }: { locale: string }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? 'en', [
        'common',
        'billing',
      ])),
    },
  };
};
