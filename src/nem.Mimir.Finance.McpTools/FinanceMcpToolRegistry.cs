using System.Text.Json;
using nem.Mimir.Finance.McpTools.Models;

namespace nem.Mimir.Finance.McpTools;

public static class FinanceMcpToolRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<FinanceMcpToolDefinition> GetTools()
    {
        return
        [
            CreateGetStockPrice(),
            CreateAnalyzeSentiment(),
            CreateGetPrediction(),
            CreateScreenStocks(),
            CreatePortfolioAnalysis(),
        ];
    }

    private static FinanceMcpToolDefinition CreateGetStockPrice()
        => new(
            Name: "get_stock_price",
            Description: "Get latest stock quote and recent OHLCV history for a ticker.",
            Action: "get_stock_data",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["ticker"],
              "properties": {
                "ticker": {
                  "type": "string",
                  "description": "Ticker symbol in exchange format, e.g. ASML.AS or SAP.DE.",
                  "minLength": 1
                },
                "days": {
                  "type": "integer",
                  "description": "Number of days of historical data to include.",
                  "minimum": 1,
                  "maximum": 365,
                  "default": 30
                },
                "include_quote": {
                  "type": "boolean",
                  "description": "Whether to include current quote snapshot in the response.",
                  "default": true
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
                "action": { "type": "string", "const": "get_stock_data" },
                "data": {
                  "type": "object",
                  "properties": {
                    "ticker": { "type": "string" },
                    "prices": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "date": { "type": "string" },
                          "open": { "type": "number" },
                          "high": { "type": "number" },
                          "low": { "type": "number" },
                          "close": { "type": "number" },
                          "volume": { "type": "number" }
                        },
                        "required": ["date", "open", "high", "low", "close", "volume"],
                        "additionalProperties": true
                      }
                    },
                    "quote": {
                      "type": ["object", "null"],
                      "properties": {
                        "price": { "type": "number" },
                        "change": { "type": "number" },
                        "change_percent": { "type": "number" },
                        "currency": { "type": "string" }
                      },
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

    private static FinanceMcpToolDefinition CreateAnalyzeSentiment()
        => new(
            Name: "analyze_sentiment",
            Description: "Analyze sentiment for provided financial text or headlines.",
            Action: "analyze_sentiment",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["text"],
              "properties": {
                "text": {
                  "type": "string",
                  "description": "Financial text to analyze.",
                  "minLength": 1,
                  "maxLength": 20000
                },
                "ticker": {
                  "type": "string",
                  "description": "Optional ticker context for the sentiment analysis."
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
                "action": { "type": "string", "const": "analyze_sentiment" },
                "data": {
                  "type": "object",
                  "properties": {
                    "polarity": { "type": "string", "enum": ["positive", "neutral", "negative"] },
                    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                    "score": { "type": "number" },
                    "source": { "type": "string" }
                  },
                  "required": ["polarity", "confidence", "score"],
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static FinanceMcpToolDefinition CreateGetPrediction()
        => new(
            Name: "get_prediction",
            Description: "Generate short-horizon market prediction for a ticker using Kronos pipeline.",
            Action: "predict",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["ticker"],
              "properties": {
                "ticker": {
                  "type": "string",
                  "description": "Ticker symbol to forecast.",
                  "minLength": 1
                },
                "horizon": {
                  "type": "integer",
                  "description": "Prediction horizon in days.",
                  "minimum": 1,
                  "maximum": 30,
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
                "action": { "type": "string", "const": "predict" },
                "data": {
                  "type": "object",
                  "properties": {
                    "ticker": { "type": "string" },
                    "predictions": {
                      "type": "array",
                      "items": { "type": "number" }
                    },
                    "confidence": {
                      "type": ["array", "number"],
                      "items": { "type": "number" }
                    },
                    "horizon": { "type": "integer" }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static FinanceMcpToolDefinition CreateScreenStocks()
        => new(
            Name: "screen_stocks",
            Description: "Run fundamental and market filters to screen stock universe candidates.",
            Action: "screen_stocks",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["stocks_data", "filters"],
              "properties": {
                "stocks_data": {
                  "type": "array",
                  "description": "Stock universe with metrics for screening.",
                  "items": {
                    "type": "object",
                    "required": ["ticker"],
                    "properties": {
                      "ticker": { "type": "string" },
                      "pe_ratio": { "type": "number" },
                      "market_cap": { "type": "number" },
                      "dividend_yield": { "type": "number" },
                      "volume": { "type": "number" },
                      "price_change_pct": { "type": "number" },
                      "sector": { "type": "string" }
                    },
                    "additionalProperties": true
                  }
                },
                "filters": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["field", "operator", "value"],
                    "properties": {
                      "field": { "type": "string" },
                      "operator": { "type": "string", "enum": ["lt", "gt", "lte", "gte", "eq", "in"] },
                      "value": {}
                    },
                    "additionalProperties": false
                  }
                },
                "logic": {
                  "type": "string",
                  "enum": ["AND", "OR"],
                  "default": "AND"
                },
                "limit": {
                  "type": "integer",
                  "minimum": 1,
                  "maximum": 500
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
                "action": { "type": "string", "const": "screen_stocks" },
                "data": {
                  "type": "object",
                  "properties": {
                    "results": {
                      "type": "array",
                      "items": { "type": "object", "additionalProperties": true }
                    },
                    "count": { "type": "integer" }
                  },
                  "additionalProperties": true
                },
                "error": { "type": ["string", "null"] }
              },
              "additionalProperties": true
            }
            """)
        );

    private static FinanceMcpToolDefinition CreatePortfolioAnalysis()
        => new(
            Name: "portfolio_analysis",
            Description: "Analyze portfolio holdings for return, diversification, currency exposure, and risk profile.",
            Action: "analyze_portfolio",
            InputSchema: ParseSchema("""
            {
              "type": "object",
              "required": ["holdings"],
              "properties": {
                "holdings": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["ticker", "quantity", "avg_price"],
                    "properties": {
                      "ticker": { "type": "string" },
                      "quantity": { "type": "number", "exclusiveMinimum": 0 },
                      "avg_price": { "type": "number", "exclusiveMinimum": 0 },
                      "currency": { "type": "string" },
                      "sector": { "type": "string" }
                    },
                    "additionalProperties": true
                  },
                  "minItems": 1
                },
                "base_currency": {
                  "type": "string",
                  "description": "Target normalization currency.",
                  "default": "EUR"
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
                "action": { "type": "string", "const": "analyze_portfolio" },
                "data": {
                  "type": "object",
                  "properties": {
                    "total_value": { "type": "number" },
                    "returns": {
                      "type": "object",
                      "additionalProperties": true
                    },
                    "diversification_score": { "type": "number" },
                    "currency_exposure": {
                      "type": "object",
                      "additionalProperties": { "type": "number" }
                    },
                    "risk": {
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

    private static JsonDocument ParseSchema(string json)
        => JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
}
