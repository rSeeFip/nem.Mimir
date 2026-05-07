import { useState, useMemo } from 'react';
import Head from 'next/head';
import { useQuery } from 'react-query';
import { useTranslation } from 'next-i18next';
import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import { IconArrowLeft, IconHistory, IconSearch } from '@tabler/icons-react';
import Link from 'next/link';

import { apiFetch } from '@/utils/app/api';
import { ConversationList } from '@/components/history/ConversationList';
import { LoadingState } from '@/components/states/LoadingState';
import { ErrorState } from '@/components/states/ErrorState';
import { ConversationListDto, PaginatedList } from '@/types/history';

const PAGE_SIZE = 20;

export default function HistoryPage() {
  const { t } = useTranslation('common');
  const [page, setPage] = useState(1);
  const [searchTerm, setSearchTerm] = useState('');

  const { data, isLoading, error, refetch } = useQuery<PaginatedList<ConversationListDto>, Error>(
    ['conversations', page],
    () => apiFetch(`/api/conversations?page=${page}&pageSize=${PAGE_SIZE}`),
    { keepPreviousData: true },
  );

  const filteredConversations = useMemo(() => {
    if (!data?.items) return [];
    if (!searchTerm.trim()) return data.items;
    const lower = searchTerm.toLowerCase();
    return data.items.filter((c) => (c.title || '').toLowerCase().includes(lower));
  }, [data?.items, searchTerm]);

  return (
    <>
      <Head>
        <title>Chat History - nem.Mimir</title>
      </Head>
      <div className="flex h-screen w-screen flex-col bg-[#202123] text-white overflow-auto">
        {/* Header */}
        <header className="flex items-center justify-between px-6 py-4 border-b border-white/20 bg-[#343541]">
          <div className="flex items-center gap-4">
            <Link href="/" className="text-gray-400 hover:text-white transition-colors">
              <IconArrowLeft size={24} />
            </Link>
            <div className="flex items-center gap-2">
              <IconHistory size={24} />
              <h1 className="text-xl font-semibold">Chat History</h1>
            </div>
          </div>
        </header>

        {/* Content */}
        <main className="flex-1 max-w-5xl w-full mx-auto p-6 space-y-6">
          {/* Search */}
          <div className="relative">
            <IconSearch
              size={18}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
            />
            <input
              type="text"
              placeholder="Search conversations..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full bg-[#343541] border border-white/20 rounded-lg pl-10 pr-4 py-2 text-sm text-white placeholder-gray-400 focus:outline-none focus:border-blue-500"
              data-testid="search-input"
            />
          </div>

          {/* State handling */}
          {isLoading && <LoadingState />}
          {error && (
            <ErrorState
              message={error.message || 'Failed to load conversations.'}
              onRetry={() => refetch()}
            />
          )}
          {!isLoading && !error && (
            <>
              <ConversationList conversations={filteredConversations} />

              {/* Pagination */}
              {data && data.hasNextPage && !searchTerm && (
                <div className="flex justify-center pt-4">
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    className="px-6 py-2 bg-[#343541] border border-white/20 rounded-lg text-sm hover:border-white/40 transition-colors"
                    data-testid="load-more"
                  >
                    Load More
                  </button>
                </div>
              )}
            </>
          )}
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
