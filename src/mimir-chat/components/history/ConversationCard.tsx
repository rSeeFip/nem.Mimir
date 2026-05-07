import React from 'react';
import { useRouter } from 'next/router';
import { IconMessage } from '@tabler/icons-react';
import { ConversationListDto } from '@/types/history';

interface ConversationCardProps {
  conversation: ConversationListDto;
}

export const ConversationCard: React.FC<ConversationCardProps> = ({ conversation }) => {
  const router = useRouter();

  const handleClick = () => {
    router.push(`/?conversationId=${conversation.id}`);
  };

  const formattedDate = new Date(conversation.createdAt).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  return (
    <div 
      onClick={handleClick}
      className="p-4 rounded-lg bg-[#343541] border border-white/10 hover:border-white/30 cursor-pointer transition-colors"
      data-testid="conversation-card"
    >
      <div className="flex items-start gap-3">
        <IconMessage className="mt-1 flex-shrink-0 text-gray-400" size={20} />
        <div className="flex-1 overflow-hidden">
          <h3 className="font-medium text-white truncate">{conversation.title || 'New Conversation'}</h3>
          <div className="flex items-center gap-4 mt-2 text-xs text-gray-400">
            <span>{formattedDate}</span>
            <span>•</span>
            <span>{conversation.messageCount} {conversation.messageCount === 1 ? 'message' : 'messages'}</span>
          </div>
        </div>
      </div>
    </div>
  );
};
