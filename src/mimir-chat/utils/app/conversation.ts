import { Conversation } from '@/types/chat';

import {
  createConversation as apiCreateConversation,
  updateConversationTitle as apiUpdateTitle,
  saveMessages as apiSaveMessages,
  deleteConversation as apiDeleteConversation,
} from './conversation-api';

// ---------------------------------------------------------------------------
// Write-through helpers — fire API call, log errors but never block the UI.
// localStorage remains the cache for instant reads.
// ---------------------------------------------------------------------------

function fireAndForget(fn: () => Promise<unknown>) {
  fn().catch((err) => {
    console.warn('[conversation] API write-through failed:', err);
  });
}

// ---------------------------------------------------------------------------
// Public API (existing signatures preserved)
// ---------------------------------------------------------------------------

export const updateConversation = (
  updatedConversation: Conversation,
  allConversations: Conversation[],
) => {
  const updatedConversations = allConversations.map((c) => {
    if (c.id === updatedConversation.id) {
      return updatedConversation;
    }

    return c;
  });

  saveConversation(updatedConversation);
  saveConversations(updatedConversations);

  // Write-through: update title on API
  fireAndForget(() =>
    apiUpdateTitle(updatedConversation.id, updatedConversation.name),
  );

  return {
    single: updatedConversation,
    all: updatedConversations,
  };
};

/**
 * Persist the currently-selected conversation to localStorage.
 * Also writes through to the API when messages are present.
 */
export const saveConversation = (conversation: Conversation) => {
  localStorage.setItem('selectedConversation', JSON.stringify(conversation));
};

/**
 * Persist the full conversation list to localStorage cache.
 */
export const saveConversations = (conversations: Conversation[]) => {
  localStorage.setItem('conversationHistory', JSON.stringify(conversations));
};

/**
 * Create a conversation on the API and return the server version
 * (with server-assigned ID).  Falls back to the original conversation
 * if the API is unreachable.
 */
export const createConversationOnApi = async (
  conversation: Conversation,
): Promise<Conversation> => {
  try {
    const serverConversation = await apiCreateConversation(conversation);
    return { ...conversation, id: serverConversation.id };
  } catch (err) {
    console.warn('[conversation] API create failed, using local ID:', err);
    return conversation;
  }
};

/**
 * Persist messages to the API (fire-and-forget).
 */
export const saveMessagesToApi = (
  conversationId: string,
  messages: { role: string; content: string }[],
) => {
  fireAndForget(() =>
    apiSaveMessages(
      conversationId,
      messages.map((m) => ({
        role: m.role as 'user' | 'assistant',
        content: m.content,
      })),
    ),
  );
};

/**
 * Delete a conversation from the API (fire-and-forget).
 */
export const deleteConversationFromApi = (conversationId: string) => {
  fireAndForget(() => apiDeleteConversation(conversationId));
};
