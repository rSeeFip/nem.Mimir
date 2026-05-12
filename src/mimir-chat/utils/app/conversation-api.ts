/**
 * REST-backed conversation CRUD operations.
 *
 * Calls same-origin BFF routes (/api/conversations/*) which proxy
 * to Mimir.Api with JWT forwarding.
 */
import { Conversation, Message } from '@/types/chat';
import { OpenAIModelID, OpenAIModels, fallbackModelID } from '@/types/openai';
import { DEFAULT_SYSTEM_PROMPT, DEFAULT_TEMPERATURE } from '@/utils/app/const';
import { apiFetch } from './api';

// ---------------------------------------------------------------------------
// Mimir.Api DTO shapes (matching C# records)
// ---------------------------------------------------------------------------

interface MimirMessageDto {
  id: string;
  conversationId: string;
  role: string;        // "User" | "Assistant" | "System"
  content: string;
  model: string | null;
  tokenCount: number | null;
  createdAt: string;
}

interface MimirConversationDto {
  id: string;
  userId: string;
  title: string;
  status: string;
  messages: MimirMessageDto[];
  createdAt: string;
  updatedAt: string | null;
}

interface MimirConversationListDto {
  id: string;
  userId: string;
  title: string;
  status: string;
  messageCount: number;
  createdAt: string;
  updatedAt: string | null;
}

interface MimirPaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
}

// ---------------------------------------------------------------------------
// Mapping helpers
// ---------------------------------------------------------------------------

/** Map Mimir role string ("User"/"Assistant"/"System") to openchat-ui Role */
function mapRole(role: string): 'user' | 'assistant' {
  const lower = role.toLowerCase();
  if (lower === 'assistant') return 'assistant';
  return 'user';
}

/** Resolve an OpenAIModel object from a model ID string, falling back gracefully */
function resolveModel(modelId: string | null | undefined) {
  if (modelId) {
    for (const [, value] of Object.entries(OpenAIModelID)) {
      if (value === modelId && OpenAIModels[value as OpenAIModelID]) {
        return OpenAIModels[value as OpenAIModelID];
      }
    }
    // Return a generic model for unknown IDs
    return {
      id: modelId,
      name: modelId,
      maxLength: 4096 * 3,
      tokenLimit: 4096,
    };
  }
  return OpenAIModels[fallbackModelID];
}

/** Convert MimirMessageDto → openchat-ui Message */
function mapMessage(dto: MimirMessageDto): Message {
  return {
    role: mapRole(dto.role),
    content: dto.content,
  };
}

/** Convert MimirConversationDto → openchat-ui Conversation (with messages) */
function mapConversation(dto: MimirConversationDto): Conversation {
  // Try to infer model from first assistant message, fallback to default
  const assistantMsg = dto.messages?.find(
    (m) => m.role.toLowerCase() === 'assistant' && m.model,
  );

  return {
    id: dto.id,
    name: dto.title || 'Untitled',
    messages: (dto.messages || []).map(mapMessage),
    model: resolveModel(assistantMsg?.model),
    prompt: DEFAULT_SYSTEM_PROMPT,
    temperature: DEFAULT_TEMPERATURE,
    folderId: null,
  };
}

/** Convert MimirConversationListDto → openchat-ui Conversation (without messages) */
function mapConversationListItem(dto: MimirConversationListDto): Conversation {
  return {
    id: dto.id,
    name: dto.title || 'Untitled',
    messages: [],
    model: OpenAIModels[fallbackModelID],
    prompt: DEFAULT_SYSTEM_PROMPT,
    temperature: DEFAULT_TEMPERATURE,
    folderId: null,
  };
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Fetch all conversations for the authenticated user.
 * Returns lightweight Conversation objects (no messages).
 */
export async function fetchConversations(): Promise<Conversation[]> {
  const data = await apiFetch<MimirPaginatedList<MimirConversationListDto>>(
    '/api/conversations?pageSize=100',
  );
  return data.items.map(mapConversationListItem);
}

/**
 * Fetch a single conversation with its full message history.
 */
export async function fetchConversation(id: string): Promise<Conversation> {
  const data = await apiFetch<MimirConversationDto>(
    `/api/conversations/${id}`,
  );
  return mapConversation(data);
}

/**
 * Create a new conversation on the server.
 * Returns the server-assigned conversation (with GUID id).
 */
export async function createConversation(
  conversation: Conversation,
): Promise<Conversation> {
  const data = await apiFetch<MimirConversationDto>('/api/conversations', {
    method: 'POST',
    body: JSON.stringify({
      title: conversation.name,
      systemPrompt: conversation.prompt || null,
      model: conversation.model?.id || null,
    }),
  });
  return mapConversation(data);
}

/**
 * Update the title of an existing conversation.
 */
export async function updateConversationTitle(
  id: string,
  title: string,
): Promise<void> {
  await apiFetch(`/api/conversations/${id}/title`, {
    method: 'PUT',
    body: JSON.stringify({ title }),
  });
}

/**
 * Save a batch of messages to a conversation.
 * Uses the BFF proxy which forwards individual messages to Mimir.Api.
 */
export async function saveMessages(
  conversationId: string,
  messages: Message[],
): Promise<void> {
  await apiFetch(`/api/conversations/${conversationId}/messages`, {
    method: 'POST',
    body: JSON.stringify({ messages }),
  });
}

/**
 * Delete a conversation on the server.
 */
export async function deleteConversation(id: string): Promise<void> {
  await apiFetch(`/api/conversations/${id}`, { method: 'DELETE' });
}
