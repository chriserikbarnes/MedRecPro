/**************************************************************/
/// <summary>
/// Models for work plan execution progress tracking and configuration.
/// </summary>
/// <remarks>
/// These models support the streaming progress updates during AI-powered
/// pharmaceutical queries. The MCP gateway uses these to communicate
/// execution state back to Claude during multi-step operations.
/// </remarks>
/// <seealso cref="MedRecProMCP.Services.IWorkPlanExecutor"/>
/**************************************************************/

using System.Text.Json.Serialization;

namespace MedRecProMCP.Models;

#region enums

/**************************************************************/
/// <summary>
/// Represents the current phase of work plan execution.
/// </summary>
/// <remarks>
/// Phases progress sequentially during execution:
/// Interpreting → Discovery → AwaitingCheckpoint (optional) → Executing → Synthesizing → Complete
/// </remarks>
/// <seealso cref="WorkPlanProgress"/>
/**************************************************************/
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkPlanPhase
{
    /**************************************************************/
    /// <summary>
    /// Initial phase where the user's query is being interpreted by the AI.
    /// </summary>
    /**************************************************************/
    Interpreting,

    /**************************************************************/
    /// <summary>
    /// Discovery phase where initial search/lookup operations are performed
    /// to identify products or documents matching the query.
    /// </summary>
    /**************************************************************/
    Discovery,

    /**************************************************************/
    /// <summary>
    /// Checkpoint phase when too many products are discovered and user
    /// selection is required before continuing with detailed analysis.
    /// </summary>
    /**************************************************************/
    AwaitingCheckpoint,

    /**************************************************************/
    /// <summary>
    /// Main execution phase where remaining API endpoints are called
    /// to gather detailed data for the selected products.
    /// </summary>
    /**************************************************************/
    Executing,

    /**************************************************************/
    /// <summary>
    /// Synthesis phase where gathered data is analyzed and formatted
    /// into a human-readable response.
    /// </summary>
    /**************************************************************/
    Synthesizing,

    /**************************************************************/
    /// <summary>
    /// Work plan completed successfully.
    /// </summary>
    /**************************************************************/
    Complete,

    /**************************************************************/
    /// <summary>
    /// Work plan encountered an error and could not complete.
    /// </summary>
    /**************************************************************/
    Error
}

#endregion

#region progress models

/**************************************************************/
/// <summary>
/// Represents a progress update during work plan execution.
/// </summary>
/// <remarks>
/// Progress updates are streamed to Claude during execution to provide
/// real-time visibility into multi-step pharmaceutical queries. The MCP
/// SDK supports IAsyncEnumerable for streaming these updates.
/// </remarks>
/// <seealso cref="WorkPlanPhase"/>
/// <seealso cref="ProductDiscovery"/>
/**************************************************************/
public class WorkPlanProgress
{
    /**************************************************************/
    /// <summary>
    /// Current execution phase.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("phase")]
    public WorkPlanPhase Phase { get; set; }

    /**************************************************************/
    /// <summary>
    /// Current step number within the work plan (1-based).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("currentStep")]
    public int CurrentStep { get; set; }

    /**************************************************************/
    /// <summary>
    /// Total number of steps in the work plan.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }

    /**************************************************************/
    /// <summary>
    /// Human-readable message describing the current operation.
    /// </summary>
    /// <example>"Searching for products matching 'buprenorphine'..."</example>
    /**************************************************************/
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Indicates whether a checkpoint is required before continuing.
    /// When true, the user should select which products to analyze.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("requiresCheckpoint")]
    public bool RequiresCheckpoint { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of discovered products when in Discovery or AwaitingCheckpoint phase.
    /// Null during other phases.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("products")]
    public List<ProductDiscovery>? Products { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of discovered product GUIDs for use in checkpoint resumption.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("discoveredProductGuids")]
    public List<string>? DiscoveredProductGuids { get; set; }

    /**************************************************************/
    /// <summary>
    /// Current batch index when synthesizing (1-based).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("batchIndex")]
    public int? BatchIndex { get; set; }

    /**************************************************************/
    /// <summary>
    /// Total number of synthesis batches.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("totalBatches")]
    public int? TotalBatches { get; set; }

    /**************************************************************/
    /// <summary>
    /// Partial content from streaming synthesis.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /**************************************************************/
    /// <summary>
    /// Indicates if this is a partial synthesis result with more to come.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("isPartial")]
    public bool IsPartial { get; set; }

    /**************************************************************/
    /// <summary>
    /// Error message if phase is Error.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /**************************************************************/
    /// <summary>
    /// The conversation ID for tracking this session.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    /**************************************************************/
    /// <summary>
    /// Direct response content when no API calls are needed.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("directResponse")]
    public string? DirectResponse { get; set; }

    /**************************************************************/
    /// <summary>
    /// Suggested follow-up queries after completion.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("suggestedFollowUps")]
    public List<string>? SuggestedFollowUps { get; set; }

    /**************************************************************/
    /// <summary>
    /// Data references (links to full documents) after completion.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("dataReferences")]
    public Dictionary<string, string>? DataReferences { get; set; }
}

/**************************************************************/
/// <summary>
/// Represents a discovered product during the discovery phase.
/// </summary>
/// <remarks>
/// Product discoveries are presented to the user during checkpoint
/// so they can select which products to analyze in detail.
/// </remarks>
/// <seealso cref="WorkPlanProgress"/>
/**************************************************************/
public class ProductDiscovery
{
    /**************************************************************/
    /// <summary>
    /// Unique identifier (GUID) for the product.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Product display name.
    /// </summary>
    /// <example>SUBOXONE</example>
    /**************************************************************/
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Labeler (manufacturer) name.
    /// </summary>
    /// <example>Indivior Inc</example>
    /**************************************************************/
    [JsonPropertyName("labeler")]
    public string? Labeler { get; set; }

    /**************************************************************/
    /// <summary>
    /// Brief description or additional identifying information.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

#endregion


