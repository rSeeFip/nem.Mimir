# API Documentation - nem.Mimir

## Overview
nem.Mimir is the legacy conversational AI service, providing multi-channel chat and LLM routing capabilities. Note: For new development, prefer `nem.Mimir-typed-ids`.

## OpenAPI Specification
- **URL**: `http://localhost:5030/swagger`
- **Specification**: `http://localhost:5030/swagger/v1/swagger.json`

## Authentication
This service requires a JWT issued by **nem.Sentinel**.
- **Header**: `Authorization: Bearer <token>`

## Key Endpoints
- `POST /api/chat/message`: Send a message to the AI and get a response
- `GET /api/chat/history/{conversationId}`: Retrieve chat history
- `POST /api/chat/conversation`: Initialize a new conversation
- `GET /api/llm/providers`: List available LLM providers and models
