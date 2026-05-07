import React from 'react';
import { ConversationCard } from './ConversationCard';
import { EmptyState } from '@/components/states/EmptyState';
import { ConversationListDto } from '@/types/history';

interface ConversationListProps {
  conversations: ConversationListDto[];
}

export const ConversationList: React.FC<ConversationListProps> = ({ conversations }) => {
  if (!conversations || conversations.length === 0) {
    return <EmptyState message="No conversations found." />;
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {conversations.map((conv) => (
        <ConversationCard key={conv.id} conversation={conv} />
      ))}
    </div>
  );
};
