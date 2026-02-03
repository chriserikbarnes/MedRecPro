/**************************************************************/
/// <summary>
/// MCP tools for AI-powered pharmaceutical queries.
/// </summary>
/// <remarks>
/// These tools provide Claude with the ability to execute natural language
/// queries against the MedRecPro pharmaceutical database. The primary tool,
/// ask_medrecpro, handles the full interpret → collect → synthesize workflow
/// with streaming progress updates.
///
/// The checkpoint mechanism allows Claude to present discovered products
/// to the user for selection before performing detailed analysis, preventing
/// runaway processing on large result sets.
/// </remarks>
/// <seealso cref="MedRecProMCP.Services.IWorkPlanExecutor"/>
/// <seealso cref="MedRecProMCP.Models.WorkPlanProgress"/>
/**************************************************************/

using MedRecProMCP.Models;
using MedRecProMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MedRecProMCP.Tools;

/**************************************************************/
/// <summary>
/// MCP tools for AI-powered pharmaceutical data queries.
/// </summary>
/**************************************************************/
[McpServerToolType]
public class AiAgentTools
{
    private readonly IWorkPlanExecutor _workPlanExecutor;
    private readonly ILogger<AiAgentTools> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of AiAgentTools.
    /// </summary>
    /// <param name="workPlanExecutor">The work plan executor service.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public AiAgentTools(IWorkPlanExecutor workPlanExecutor, ILogger<AiAgentTools> logger)
    {
        _workPlanExecutor = workPlanExecutor;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Asks a question about pharmaceutical data using natural language.
    /// </summary>
    /// <param name="question">Natural language question about drug labels, medications, or pharmaceutical data.</param>
    /// <param name="selectedProducts">Optional: Comma-separated product GUIDs to analyze (from previous checkpoint).</param>
    /// <param name="conversationId">Optional: Conversation ID for context continuity.</param>
    /// <returns>
    /// A streaming response with progress updates and final analysis.
    /// When a checkpoint is triggered (5+ products found), returns the product
    /// list for user selection. Resume by calling again with selectedProducts.
    /// </returns>
    /// <remarks>
    /// This tool interprets natural language queries, executes necessary API calls,
    /// and synthesizes a response. It streams progress for multi-step queries.
    ///
    /// **Workflow:**
    /// 1. Interprets the query to determine what data is needed
    /// 2. Executes discovery to find matching products
    /// 3. If 5+ products found, returns checkpoint for user selection
    /// 4. Executes detailed queries for selected products
    /// 5. Synthesizes results into a comprehensive response
    ///
    /// **Examples:**
    /// - "What is the indication for Lipitor?"
    /// - "Find all buprenorphine products"
    /// - "Compare dosage forms for metformin"
    /// - "What are the black box warnings for warfarin?"
    ///
    /// **Checkpoint Flow:**
    /// When many products are found, the tool returns a checkpoint with product
    /// list. Present these to the user, get their selection, then call the tool
    /// again with the selectedProducts parameter containing their chosen GUIDs.
    /// </remarks>
    /// <seealso cref="IWorkPlanExecutor.ExecuteWithProgressAsync"/>
    /**************************************************************/
    [McpServerTool]
    [Description("Ask a question about pharmaceutical data. Interprets your query, executes necessary API calls, and synthesizes a response. Streams progress for multi-step queries.")]
    public async Task<string> AskMedRecPro(
        [Description("Natural language question about drug labels, medications, or pharmaceutical data")]
        string question,
        [Description("Optional: Comma-separated product GUIDs to analyze (from previous checkpoint)")]
        string? selectedProducts = null,
        [Description("Optional: Conversation ID for context continuity")]
        string? conversationId = null)
    {
        #region implementation
        _logger.LogInformation("[Tool] AskMedRecPro: {Question}",
            question.Length > 100 ? question[..100] + "..." : question);

        // Parse selected products if provided
        string[]? selectedProductGuids = null;
        if (!string.IsNullOrWhiteSpace(selectedProducts))
        {
            selectedProductGuids = selectedProducts
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

            _logger.LogInformation("[Tool] AskMedRecPro: Resuming with {Count} selected products",
                selectedProductGuids.Length);
        }

        var responseBuilder = new StringBuilder();
        WorkPlanProgress? lastProgress = null;

        try
        {
            await foreach (var progress in _workPlanExecutor.ExecuteWithProgressAsync(
                question,
                conversationId,
                selectedProductGuids))
            {
                lastProgress = progress;

                // Handle different phases
                switch (progress.Phase)
                {
                    case WorkPlanPhase.Interpreting:
                    case WorkPlanPhase.Discovery:
                    case WorkPlanPhase.Executing:
                        // Log progress but don't include in final response
                        _logger.LogDebug("[Tool] Progress: [{Phase}] {Message}",
                            progress.Phase, progress.Message);
                        break;

                    case WorkPlanPhase.AwaitingCheckpoint:
                        // Return checkpoint response for user selection
                        return formatCheckpointResponse(progress);

                    case WorkPlanPhase.Synthesizing:
                        if (progress.IsPartial && !string.IsNullOrEmpty(progress.Content))
                        {
                            responseBuilder.AppendLine(progress.Content);
                            responseBuilder.AppendLine();
                        }
                        break;

                    case WorkPlanPhase.Complete:
                        if (!string.IsNullOrEmpty(progress.Content))
                        {
                            if (responseBuilder.Length > 0)
                            {
                                // Already have partial content, add final
                                responseBuilder.AppendLine(progress.Content);
                            }
                            else
                            {
                                responseBuilder.Append(progress.Content);
                            }
                        }
                        else if (!string.IsNullOrEmpty(progress.DirectResponse))
                        {
                            responseBuilder.Append(progress.DirectResponse);
                        }

                        // Add follow-up suggestions if available
                        if (progress.SuggestedFollowUps != null && progress.SuggestedFollowUps.Count > 0)
                        {
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine("**Suggested follow-up questions:**");
                            foreach (var followUp in progress.SuggestedFollowUps)
                            {
                                responseBuilder.AppendLine($"- {followUp}");
                            }
                        }

                        // Add data references if available (View Full Label links)
                        if (progress.DataReferences != null && progress.DataReferences.Count > 0)
                        {
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine("**View Full Labels:**");
                            foreach (var reference in progress.DataReferences)
                            {
                                responseBuilder.AppendLine($"- [{reference.Key}]({reference.Value})");
                            }
                        }

                        // Add data sources information from discovered products
                        if (progress.Products != null && progress.Products.Count > 0)
                        {
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine();
                            responseBuilder.AppendLine("**Data sources:**");
                            foreach (var product in progress.Products.Take(10)) // Limit to avoid overwhelming
                            {
                                var sourceLine = $"- {product.Name}";
                                if (!string.IsNullOrEmpty(product.Description))
                                {
                                    sourceLine += $" - {product.Description}";
                                }
                                responseBuilder.AppendLine(sourceLine);
                            }
                            if (progress.Products.Count > 10)
                            {
                                responseBuilder.AppendLine($"- ...and {progress.Products.Count - 10} more");
                            }
                        }
                        break;

                    case WorkPlanPhase.Error:
                        return formatErrorResponse(progress);
                }
            }

            // Return accumulated response
            var response = responseBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(response))
            {
                return "No results found for your query. Try rephrasing your question or being more specific.";
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] AskMedRecPro failed");
            return JsonSerializer.Serialize(new
            {
                error = "Query failed",
                message = ex.Message,
                conversationId = lastProgress?.ConversationId
            }, JsonOptions);
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current MedRecPro system context.
    /// </summary>
    /// <returns>System context including authentication status and available capabilities.</returns>
    /// <remarks>
    /// Use this to understand what data is available and what operations are possible.
    /// The context includes:
    /// - Authentication status
    /// - Demo mode status
    /// - Document and product counts
    /// - Available sections and views
    /// - Enabled features
    /// </remarks>
    /// <seealso cref="AiSystemContext"/>
    /**************************************************************/
    [McpServerTool]
    [Description("Get the current MedRecPro system context including available data and authentication status.")]
    public async Task<string> GetMedRecProContext()
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMedRecProContext");

        try
        {
            var context = await _workPlanExecutor.GetContextAsync();

            if (context == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Failed to retrieve system context",
                    message = "The MedRecPro API did not return context information"
                }, JsonOptions);
            }

            // Format context as readable summary
            var summary = new StringBuilder();
            summary.AppendLine("## MedRecPro System Context");
            summary.AppendLine();

            // Authentication
            summary.AppendLine("### Authentication");
            summary.AppendLine($"- Authenticated: {(context.IsAuthenticated ? "Yes" : "No")}");
            if (!string.IsNullOrEmpty(context.UserName))
            {
                summary.AppendLine($"- User: {context.UserName}");
            }
            summary.AppendLine();

            // Demo mode
            if (context.IsDemoMode)
            {
                summary.AppendLine("### Demo Mode");
                summary.AppendLine($"- Status: Active");
                if (!string.IsNullOrEmpty(context.DemoModeMessage))
                {
                    summary.AppendLine($"- Note: {context.DemoModeMessage}");
                }
                summary.AppendLine();
            }

            // Available data
            summary.AppendLine("### Available Data");
            summary.AppendLine($"- Documents: {context.DocumentCount:N0}");
            summary.AppendLine($"- Products: {context.ProductCount:N0}");
            if (context.IsDatabaseEmpty)
            {
                summary.AppendLine("- Note: Database is empty - consider importing SPL data");
            }
            summary.AppendLine();

            // Features
            summary.AppendLine("### Features");
            summary.AppendLine($"- Import: {(context.ImportEnabled ? "Enabled" : "Disabled")}");
            summary.AppendLine($"- Comparison Analysis: {(context.ComparisonAnalysisEnabled ? "Enabled" : "Disabled")}");
            summary.AppendLine();

            // Available sections
            if (context.AvailableSections.Count > 0)
            {
                summary.AppendLine("### Available Sections");
                foreach (var section in context.AvailableSections)
                {
                    summary.AppendLine($"- {section}");
                }
                summary.AppendLine();
            }

            // Available views
            if (context.AvailableViews.Count > 0)
            {
                summary.AppendLine("### Available Views");
                foreach (var view in context.AvailableViews)
                {
                    summary.AppendLine($"- {view}");
                }
            }

            return summary.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Tool] GetMedRecProContext failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve context",
                message = ex.Message
            }, JsonOptions);
        }
        #endregion
    }

    #region private methods

    /**************************************************************/
    /// <summary>
    /// Formats a checkpoint response for user selection.
    /// </summary>
    /**************************************************************/
    private string formatCheckpointResponse(WorkPlanProgress progress)
    {
        #region implementation
        var response = new StringBuilder();
        response.AppendLine($"## Checkpoint Required");
        response.AppendLine();
        response.AppendLine(progress.Message);
        response.AppendLine();
        response.AppendLine("### Discovered Products");
        response.AppendLine();

        if (progress.Products != null)
        {
            foreach (var product in progress.Products)
            {
                var line = $"- **{product.Name}**";
                if (!string.IsNullOrEmpty(product.Labeler))
                {
                    line += $" ({product.Labeler})";
                }
                if (!string.IsNullOrEmpty(product.Description))
                {
                    line += $" - {product.Description}";
                }
                response.AppendLine(line);
                response.AppendLine($"  - GUID: `{product.Guid}`");
            }
        }

        response.AppendLine();
        response.AppendLine("### To continue analysis:");
        response.AppendLine("Call `ask_medrecpro` again with:");
        response.AppendLine($"- `conversationId`: `{progress.ConversationId}`");
        response.AppendLine("- `selectedProducts`: comma-separated list of GUIDs you want to analyze");
        response.AppendLine();
        response.AppendLine("Example GUIDs (first 5):");

        if (progress.DiscoveredProductGuids != null)
        {
            var exampleGuids = progress.DiscoveredProductGuids.Take(5);
            response.AppendLine($"`{string.Join(",", exampleGuids)}`");
        }

        return response.ToString();
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Formats an error response.
    /// </summary>
    /**************************************************************/
    private static string formatErrorResponse(WorkPlanProgress progress)
    {
        #region implementation
        return JsonSerializer.Serialize(new
        {
            error = progress.Error ?? "Unknown error",
            phase = progress.Phase.ToString(),
            conversationId = progress.ConversationId
        }, JsonOptions);
        #endregion
    }

    #endregion
}
