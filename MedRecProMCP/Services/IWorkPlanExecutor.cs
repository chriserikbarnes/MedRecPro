/**************************************************************/
/// <summary>
/// Interface for the work plan execution service.
/// </summary>
/// <remarks>
/// The work plan executor orchestrates the interpret → collect → synthesize
/// flow for AI-powered pharmaceutical queries. It delegates interpretation
/// and synthesis to the MedRecPro API while handling endpoint execution
/// locally with parallel batching and streaming progress updates.
/// </remarks>
/// <seealso cref="WorkPlanExecutor"/>
/// <seealso cref="MedRecProMCP.Models.WorkPlanProgress"/>
/**************************************************************/

using MedRecProMCP.Models;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for executing AI-powered pharmaceutical queries with
/// streaming progress updates.
/// </summary>
/// <remarks>
/// Implementation should:
/// - Delegate interpretation and synthesis to the MedRecPro API
/// - Execute endpoint specifications locally with parallel batching
/// - Stream progress updates during execution
/// - Support checkpoint logic for large result sets
/// </remarks>
/// <seealso cref="WorkPlanProgress"/>
/// <seealso cref="WorkPlanExecutionOptions"/>
/**************************************************************/
public interface IWorkPlanExecutor
{
    /**************************************************************/
    /// <summary>
    /// Executes a pharmaceutical query with streaming progress updates.
    /// </summary>
    /// <param name="query">The natural language query from the user.</param>
    /// <param name="conversationId">Optional conversation ID for context continuity.</param>
    /// <param name="selectedProductGuids">Optional array of product GUIDs selected after a checkpoint.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// An async enumerable of progress updates throughout the execution lifecycle,
    /// from interpretation through synthesis.
    /// </returns>
    /// <remarks>
    /// The execution flow:
    /// 1. Yields "Interpreting query..." progress
    /// 2. Calls interpret API to get endpoint specifications
    /// 3. If direct response, yields final result and returns
    /// 4. Executes discovery phase (step 1)
    /// 5. If products exceed threshold, yields checkpoint message
    /// 6. If selectedProductGuids provided, continues execution
    /// 7. Batches synthesis and yields progressive results
    ///
    /// <example>
    /// Simple query (no checkpoint):
    /// <code>
    /// await foreach (var progress in executor.ExecuteWithProgressAsync("What is Lipitor?"))
    /// {
    ///     Console.WriteLine($"[{progress.Phase}] {progress.Message}");
    /// }
    /// </code>
    /// </example>
    ///
    /// <example>
    /// Query with checkpoint:
    /// <code>
    /// // First call - may hit checkpoint
    /// await foreach (var progress in executor.ExecuteWithProgressAsync(
    ///     "Find all buprenorphine products"))
    /// {
    ///     if (progress.RequiresCheckpoint)
    ///     {
    ///         // Present products to user for selection
    ///         var selected = GetUserSelection(progress.Products);
    ///
    ///         // Resume with selection
    ///         await foreach (var p in executor.ExecuteWithProgressAsync(
    ///             "Find all buprenorphine products",
    ///             progress.ConversationId,
    ///             selected.ToArray()))
    ///         {
    ///             // Handle final results
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="WorkPlanProgress"/>
    /**************************************************************/
    IAsyncEnumerable<WorkPlanProgress> ExecuteWithProgressAsync(
        string query,
        string? conversationId = null,
        string[]? selectedProductGuids = null,
        CancellationToken cancellationToken = default);

    /**************************************************************/
    /// <summary>
    /// Gets the current system context from the MedRecPro API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The current system context including authentication and capabilities.</returns>
    /// <seealso cref="AiSystemContext"/>
    /**************************************************************/
    Task<AiSystemContext?> GetContextAsync(CancellationToken cancellationToken = default);
}
