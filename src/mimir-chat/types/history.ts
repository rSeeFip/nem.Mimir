export interface ConversationListDto {
  id: string;
  userId: string;
  title: string;
  status: string;
  messageCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
