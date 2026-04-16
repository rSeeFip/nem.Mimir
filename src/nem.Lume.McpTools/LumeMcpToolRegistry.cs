using System.Text.Json;
using nem.Lume.McpTools.Models;

namespace nem.Lume.McpTools;

public static class LumeMcpToolRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<LumeMcpToolDefinition> GetTools()
    {
        return
        [
            CreateLumeCreateTask(),
            CreateLumeListProjects(),
            CreateLumeSearchKnowledge(),
            CreateLumeGetDocument(),
            CreateLumeScheduleEvent(),
        ];
    }

    private static LumeMcpToolDefinition CreateLumeCreateTask()
        => new(
            Name: "lume_create_task",
            Description: "Creates a task in a Lume project board column.",
            Action: "create_task",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["workspaceId", "boardId", "columnId", "title"],
              "properties": {
                "workspaceId": {
                  "type": "string",
                  "description": "Workspace identifier that owns the board.",
                  "minLength": 1
                },
                "boardId": {
                  "type": "string",
                  "description": "Board identifier containing the target column.",
                  "minLength": 1
                },
                "columnId": {
                  "type": "string",
                  "description": "Board column identifier where the task will be created.",
                  "minLength": 1
                },
                "title": {
                  "type": "string",
                  "description": "Task title shown on the board.",
                  "minLength": 1,
                  "maxLength": 500
                },
                "description": {
                  "type": "string",
                  "description": "Optional task description or body.",
                  "maxLength": 10000
                }
              },
              "additionalProperties": false
            }
            """),
            OutputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["status", "action", "data"],
              "properties": {
                "status": { "type": "string", "enum": ["success", "error"] },
                "action": { "type": "string", "const": "create_task" },
                "data": {
                  "type": "object",
                  "properties": {
                    "taskId": { "type": "string" },
                    "title": { "type": "string" }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static LumeMcpToolDefinition CreateLumeListProjects()
        => new(
            Name: "lume_list_projects",
            Description: "Lists projects in a Lume workspace.",
            Action: "list_projects",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["workspaceId"],
              "properties": {
                "workspaceId": {
                  "type": "string",
                  "description": "Workspace identifier to query for projects.",
                  "minLength": 1
                }
              },
              "additionalProperties": false
            }
            """),
            OutputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["status", "action", "data"],
              "properties": {
                "status": { "type": "string", "enum": ["success", "error"] },
                "action": { "type": "string", "const": "list_projects" },
                "data": {
                  "type": "object",
                  "properties": {
                    "projects": {
                      "type": "array",
                      "items": { "type": "object", "additionalProperties": true }
                    }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static LumeMcpToolDefinition CreateLumeSearchKnowledge()
        => new(
            Name: "lume_search_knowledge",
            Description: "Searches the Lume knowledge base for relevant results.",
            Action: "search_knowledge",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["workspaceId", "query"],
              "properties": {
                "workspaceId": {
                  "type": "string",
                  "description": "Workspace identifier used for access scoping.",
                  "minLength": 1
                },
                "query": {
                  "type": "string",
                  "description": "Natural language query to run against the knowledge base.",
                  "minLength": 1,
                  "maxLength": 5000
                },
                "topK": {
                  "type": "integer",
                  "description": "Maximum number of results to return.",
                  "minimum": 1,
                  "maximum": 50,
                  "default": 5
                }
              },
              "additionalProperties": false
            }
            """),
            OutputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["status", "action", "data"],
              "properties": {
                "status": { "type": "string", "enum": ["success", "error"] },
                "action": { "type": "string", "const": "search_knowledge" },
                "data": {
                  "type": "object",
                  "properties": {
                    "results": {
                      "type": "array",
                      "items": { "type": "object", "additionalProperties": true }
                    }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static LumeMcpToolDefinition CreateLumeGetDocument()
        => new(
            Name: "lume_get_document",
            Description: "Retrieves a Lume document by identifier.",
            Action: "get_document",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["workspaceId", "documentId"],
              "properties": {
                "workspaceId": {
                  "type": "string",
                  "description": "Workspace identifier used for access validation.",
                  "minLength": 1
                },
                "documentId": {
                  "type": "string",
                  "description": "Document identifier to load.",
                  "minLength": 1
                }
              },
              "additionalProperties": false
            }
            """),
            OutputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["status", "action", "data"],
              "properties": {
                "status": { "type": "string", "enum": ["success", "error"] },
                "action": { "type": "string", "const": "get_document" },
                "data": {
                  "type": "object",
                  "properties": {
                    "document": {
                      "type": "object",
                      "additionalProperties": true
                    }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static LumeMcpToolDefinition CreateLumeScheduleEvent()
        => new(
            Name: "lume_schedule_event",
            Description: "Schedules a calendar event in a Lume workspace.",
            Action: "schedule_event",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["workspaceId", "title", "startTime"],
              "properties": {
                "workspaceId": {
                  "type": "string",
                  "description": "Workspace identifier used for access scoping.",
                  "minLength": 1
                },
                "title": {
                  "type": "string",
                  "description": "Event title.",
                  "minLength": 1
                },
                "startTime": {
                  "type": "string",
                  "description": "Event start time.",
                  "minLength": 1
                },
                "endTime": {
                  "type": "string",
                  "description": "Optional event end time.",
                  "minLength": 1
                },
                "description": {
                  "type": "string",
                  "description": "Optional event description.",
                  "minLength": 1
                }
              },
              "additionalProperties": false
            }
            """),
            OutputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["status", "action", "data"],
              "properties": {
                "status": { "type": "string", "enum": ["success", "error"] },
                "action": { "type": "string", "const": "schedule_event" },
                "data": {
                  "type": "object",
                  "properties": {
                    "eventId": { "type": "string" },
                    "title": { "type": "string" }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static JsonDocument ParseSchema(string json)
        => JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
}
