/**************************************************************/
/// <summary>
/// Implements the work plan execution service for AI-powered pharmaceutical queries.
/// </summary>
/// <remarks>
/// The WorkPlanExecutor orchestrates the interpret → collect → synthesize flow:
/// 1. Delegates interpretation to the MedRecPro API (single source of truth for skills)
/// 2. Handles endpoint execution locally with parallel batching
/// 3. Streams progress updates to Claude during execution
/// 4. Implements checkpoint logic for large result sets
///
/// This approach avoids duplicating complex skill routing logic while giving
/// the MCP gateway control over execution flow and progress streaming.
/// </remarks>
/// <seealso cref="IWorkPlanExecutor"/>
/// <seealso cref="MedRecProApiClient"/>
/// <seealso cref="WorkPlanProgress"/>
/**************************************************************/

using MedRecProMCP.Models;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Orchestrates AI-powered pharmaceutical queries with streaming progress updates.
/// </summary>
/**************************************************************/
public class WorkPlanExecutor : IWorkPlanExecutor
{
    #region Fields and Constants

    private readonly MedRecProApiClient _apiClient;
    private readonly ILogger<WorkPlanExecutor> _logger;
    private readonly WorkPlanExecutionOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Property name mappings for JSON extraction
    private static readonly string[] GuidPropertyNames = { "documentGUID", "documentGuid", "productGUID", "productGuid", "guid", "id", "encryptedId" };
    private static readonly string[] NamePropertyNames = { "productName", "name", "title", "displayName", "proprietaryName" };
    private static readonly string[] LabelerPropertyNames = { "labeler", "labelerName", "manufacturer", "company" };
    private static readonly string[] DescriptionPropertyNames = { "description", "dosageForm", "routeOfAdministration" };
    private static readonly string[] WrapperPropertyNames = { "items", "data", "results", "products", "documents", "labels" };
    private static readonly string[] NestedArrayPropertyNames = { "products", "items", "documents", "labels" };

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the WorkPlanExecutor.
    /// </summary>
    /// <param name="apiClient">The MedRecPro API client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Work plan execution configuration options.</param>
    /**************************************************************/
    public WorkPlanExecutor(
        MedRecProApiClient apiClient,
        ILogger<WorkPlanExecutor> logger,
        IOptions<WorkPlanExecutionOptions> options)
    {
        _apiClient = apiClient;
        _logger = logger;
        _options = options.Value;
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Gets the current system context from the MedRecPro API.
    /// </summary>
    /// <seealso cref="IWorkPlanExecutor.GetContextAsync"/>
    /**************************************************************/
    public async Task<AiSystemContext?> GetContextAsync(CancellationToken cancellationToken = default)
    {
        #region implementation
        try
        {
            return await _apiClient.GetContextAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkPlan] Failed to get system context");
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes a pharmaceutical query with streaming progress updates.
    /// </summary>
    /// <remarks>
    /// Orchestrates the four-phase execution flow:
    /// 1. Interpretation - Parse query and determine required API calls
    /// 2. Discovery - Execute initial search endpoints
    /// 3. Execution - Run remaining endpoint steps with variable substitution
    /// 4. Synthesis - Combine results into a coherent response
    /// </remarks>
    /// <seealso cref="IWorkPlanExecutor.ExecuteWithProgressAsync"/>
    /// <seealso cref="interpretQueryAsync"/>
    /// <seealso cref="executeDiscoveryPhaseAsync"/>
    /// <seealso cref="executeSynthesisPhaseAsync"/>
    /**************************************************************/
    public async IAsyncEnumerable<WorkPlanProgress> ExecuteWithProgressAsync(
        string query,
        string? conversationId = null,
        string[]? selectedProductGuids = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        #region implementation

        _logger.LogInformation("[WorkPlan] Starting execution for query: {Query}",
            query.Length > 100 ? query[..100] + "..." : query);

        // Phase 1: Interpretation
        yield return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Interpreting,
            Message = "Interpreting your query...",
            ConversationId = conversationId
        };

        // Call interpretation API (wrapped to avoid yield issues)
        var (interpretation, interpretError) = await interpretQueryAsync(query, conversationId);

        // Handle interpretation errors
        if (interpretError != null)
        {
            yield return interpretError;
            yield break;
        }

        if (interpretation == null)
        {
            yield return createErrorProgress("Failed to interpret query - no response from API");
            yield break;
        }

        // Update conversation ID from response
        conversationId = interpretation.ConversationId ?? conversationId;

        // Log interpretation details for debugging
        _logger.LogInformation("[WorkPlan] Interpretation received: Success={Success}, IsDirectResponse={IsDirect}, EndpointCount={Count}",
            interpretation.Success,
            interpretation.IsDirectResponse,
            interpretation.Endpoints?.Count ?? 0);

        if (interpretation.Endpoints != null && interpretation.Endpoints.Count > 0)
        {
            foreach (var ep in interpretation.Endpoints)
            {
                _logger.LogDebug("[WorkPlan] Endpoint: Step={Step}, Method={Method}, Path={Path}",
                    ep.Step, ep.Method, ep.Path);
            }
        }

        // Handle direct response (no API calls needed)
        if (interpretation.IsDirectResponse)
        {
            _logger.LogInformation("[WorkPlan] Direct response - no API calls needed. Content preview: {Preview}",
                interpretation.DirectResponse?.Length > 100
                    ? interpretation.DirectResponse[..100] + "..."
                    : interpretation.DirectResponse ?? "(empty)");
            yield return createDirectResponseProgress(interpretation, conversationId);
            yield break;
        }

        // Handle interpretation failure
        if (!interpretation.Success)
        {
            _logger.LogWarning("[WorkPlan] Interpretation failed: {Error}", interpretation.Error);
            yield return createErrorProgress(
                interpretation.Error ?? "Failed to interpret query",
                conversationId);
            yield break;
        }

        // Handle empty endpoints
        if (interpretation.Endpoints == null || interpretation.Endpoints.Count == 0)
        {
            _logger.LogWarning("[WorkPlan] No endpoints returned from interpretation");
            yield return createErrorProgress(
                "No API endpoints identified for this query",
                conversationId);
            yield break;
        }

        // Phase 2-4: Discovery, Execution, and Synthesis
        await foreach (var execProgress in executeDataCollectionPhasesAsync(
            query,
            interpretation,
            conversationId,
            selectedProductGuids,
            cancellationToken))
        {
            yield return execProgress;

            // Stop iteration on terminal states
            if (execProgress.Phase == WorkPlanPhase.AwaitingCheckpoint ||
                execProgress.Phase == WorkPlanPhase.Error ||
                execProgress.Phase == WorkPlanPhase.Complete)
            {
                yield break;
            }
        }

        #endregion
    }

    #endregion

    #region Phase Execution Helpers

    /**************************************************************/
    /// <summary>
    /// Executes data collection phases including discovery and remaining steps.
    /// </summary>
    /// <remarks>
    /// Coordinates the discovery phase (step 1) and any subsequent execution
    /// steps, handling variable extraction and substitution between steps.
    /// Also manages checkpoint logic for large result sets.
    /// </remarks>
    /// <seealso cref="executeDiscoveryPhaseAsync"/>
    /// <seealso cref="executeRemainingStepsAsync"/>
    /// <seealso cref="executeSynthesisPhaseAsync"/>
    /**************************************************************/
    private async IAsyncEnumerable<WorkPlanProgress> executeDataCollectionPhasesAsync(
        string originalQuery,
        AiAgentInterpretation interpretation,
        string? conversationId,
        string[]? selectedProductGuids,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        #region implementation

        var endpoints = interpretation.Endpoints!;
        var totalSteps = endpoints.Max(e => e.Step ?? e.ExecutionOrder) + 1;

        _logger.LogInformation("[WorkPlan] Interpretation returned {Count} endpoints across {Steps} steps",
            endpoints.Count, totalSteps);

        // Initialize tracking structures
        var allResults = new Dictionary<int, List<AiEndpointResult>>();
        var extractedVariables = new Dictionary<string, List<string>>();

        // Execute discovery phase
        await foreach (var discoveryProgress in executeDiscoveryPhaseAsync(
            endpoints,
            totalSteps,
            conversationId,
            allResults,
            extractedVariables,
            cancellationToken))
        {
            yield return discoveryProgress;
        }

        // Extract products and check for checkpoint
        var discoveredProducts = extractProductsFromResults(allResults.GetValueOrDefault(1) ?? new List<AiEndpointResult>());
        _logger.LogInformation("[WorkPlan] Discovery found {Count} products", discoveredProducts.Count);

        // Handle checkpoint logic
        var checkpointProgress = checkForCheckpoint(
            selectedProductGuids,
            discoveredProducts,
            totalSteps,
            conversationId);

        if (checkpointProgress != null)
        {
            yield return checkpointProgress;
            yield break;
        }

        // Apply product selection filter if provided
        if (selectedProductGuids != null && selectedProductGuids.Length > 0)
        {
            filterResultsBySelectedProducts(allResults, selectedProductGuids, extractedVariables);
            _logger.LogInformation("[WorkPlan] Filtered to {Count} selected products", selectedProductGuids.Length);
        }

        // Execute remaining steps (if any)
        var remainingEndpoints = endpoints
            .Where(e => (e.Step ?? e.ExecutionOrder) > 1)
            .OrderBy(e => e.Step ?? e.ExecutionOrder)
            .ToList();

        if (remainingEndpoints.Count > 0)
        {
            await foreach (var progress in executeRemainingStepsAsync(
                remainingEndpoints,
                allResults,
                extractedVariables,
                totalSteps,
                conversationId,
                cancellationToken))
            {
                yield return progress;
            }
        }

        // Execute synthesis phase
        await foreach (var synthesisProgress in executeSynthesisPhaseAsync(
            originalQuery,
            allResults,
            discoveredProducts,
            totalSteps,
            conversationId,
            selectedProductGuids,
            cancellationToken))
        {
            yield return synthesisProgress;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes the discovery phase (step 1) of data collection.
    /// </summary>
    /// <remarks>
    /// Identifies and executes discovery endpoints, which are typically
    /// search or listing operations that find relevant products or documents.
    /// Extracts variables from results for use in subsequent steps.
    /// </remarks>
    /// <seealso cref="executeEndpointsParallel"/>
    /// <seealso cref="extractVariablesFromResult"/>
    /**************************************************************/
    private async IAsyncEnumerable<WorkPlanProgress> executeDiscoveryPhaseAsync(
        List<AiEndpointSpecification> endpoints,
        int totalSteps,
        string? conversationId,
        Dictionary<int, List<AiEndpointResult>> allResults,
        Dictionary<string, List<string>> extractedVariables,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        #region implementation

        // Identify discovery endpoints (step 1)
        var discoveryEndpoints = endpoints.Where(e => (e.Step ?? e.ExecutionOrder) == 1).ToList();

        // If no explicit step 1, use first endpoint as discovery
        if (discoveryEndpoints.Count == 0)
        {
            discoveryEndpoints = endpoints
                .OrderBy(e => e.Step ?? e.ExecutionOrder)
                .Take(1)
                .ToList();
        }

        // Yield discovery phase progress
        yield return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Discovery,
            CurrentStep = 1,
            TotalSteps = totalSteps,
            Message = discoveryEndpoints.FirstOrDefault()?.Description ?? "Searching for matching products...",
            ConversationId = conversationId
        };

        // Execute discovery endpoints in parallel
        var discoveryResults = await executeEndpointsParallel(discoveryEndpoints, cancellationToken);
        allResults[1] = discoveryResults;

        // Extract variables from discovery results for use in subsequent steps
        foreach (var result in discoveryResults)
        {
            if (result.Specification.OutputMapping != null && result.Result != null)
            {
                extractVariablesFromResult(result, extractedVariables);
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes the synthesis phase to combine results into a response.
    /// </summary>
    /// <remarks>
    /// Handles both single synthesis calls for small result sets and
    /// batched synthesis for larger sets that might exceed token limits.
    /// Builds data references from discovered products for user navigation.
    /// </remarks>
    /// <seealso cref="synthesizeResultsAsync"/>
    /// <seealso cref="synthesizeBatchedAsync"/>
    /// <seealso cref="buildDataReferencesFromProducts"/>
    /**************************************************************/
    private async IAsyncEnumerable<WorkPlanProgress> executeSynthesisPhaseAsync(
        string originalQuery,
        Dictionary<int, List<AiEndpointResult>> allResults,
        List<ProductDiscovery> discoveredProducts,
        int totalSteps,
        string? conversationId,
        string[]? selectedProductGuids,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        #region implementation

        // Yield synthesizing progress
        yield return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Synthesizing,
            CurrentStep = totalSteps,
            TotalSteps = totalSteps,
            Message = "Analyzing results...",
            ConversationId = conversationId
        };

        // Collect all results for synthesis
        var allEndpointResults = allResults.Values.SelectMany(r => r).ToList();

        // Determine product count for batching decision
        var productCount = calculateProductCountForSynthesis(
            discoveredProducts,
            selectedProductGuids,
            allEndpointResults);

        // Choose synthesis strategy based on result size
        if (productCount > _options.SynthesisBatchSize)
        {
            // Batched synthesis for large result sets
            await foreach (var progress in synthesizeBatchedAsync(
                originalQuery,
                allEndpointResults,
                discoveredProducts,
                conversationId,
                cancellationToken))
            {
                yield return progress;
            }
        }
        else
        {
            // Single synthesis call for smaller result sets
            var completionProgress = await executeSingleSynthesisAsync(
                originalQuery,
                allEndpointResults,
                discoveredProducts,
                totalSteps,
                conversationId,
                cancellationToken);

            yield return completionProgress;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes a single synthesis call and builds the completion progress.
    /// </summary>
    /// <seealso cref="synthesizeResultsAsync"/>
    /// <seealso cref="buildDataReferencesFromProducts"/>
    /**************************************************************/
    private async Task<WorkPlanProgress> executeSingleSynthesisAsync(
        string originalQuery,
        List<AiEndpointResult> allEndpointResults,
        List<ProductDiscovery> discoveredProducts,
        int totalSteps,
        string? conversationId,
        CancellationToken cancellationToken)
    {
        #region implementation

        var synthesis = await synthesizeResultsAsync(
            originalQuery,
            allEndpointResults,
            conversationId,
            cancellationToken);

        // Build data references, preferring synthesis-provided ones
        var dataReferences = synthesis?.DataReferences ?? new Dictionary<string, string>();
        if (dataReferences.Count == 0 && discoveredProducts.Count > 0)
        {
            dataReferences = buildDataReferencesFromProducts(discoveredProducts);
        }

        return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Complete,
            CurrentStep = totalSteps,
            TotalSteps = totalSteps,
            Message = "Analysis complete",
            ConversationId = synthesis?.ConversationId ?? conversationId,
            Content = synthesis?.Response ?? "No results to summarize",
            SuggestedFollowUps = synthesis?.SuggestedFollowUps,
            DataReferences = dataReferences,
            Products = discoveredProducts
        };

        #endregion
    }

    #endregion

    #region Interpretation Helpers

    /**************************************************************/
    /// <summary>
    /// Interprets a query by calling the MedRecPro API.
    /// </summary>
    /// <remarks>
    /// Wraps the API call in try/catch to allow yielding in the main method.
    /// Returns a tuple with either the interpretation or an error progress.
    /// </remarks>
    /// <seealso cref="MedRecProApiClient.InterpretAsync"/>
    /**************************************************************/
    private async Task<(AiAgentInterpretation? interpretation, WorkPlanProgress? error)> interpretQueryAsync(
        string query,
        string? conversationId)
    {
        #region implementation
        try
        {
            var request = new AiAgentRequest
            {
                UserMessage = query,
                ConversationId = conversationId
            };

            var interpretation = await _apiClient.InterpretAsync(request);
            return (interpretation, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkPlan] Interpretation failed");
            return (null, createErrorProgress($"Interpretation failed: {ex.Message}"));
        }
        #endregion
    }

    #endregion

    #region Endpoint Execution Helpers

    /**************************************************************/
    /// <summary>
    /// Executes endpoints in parallel with concurrency control and retry logic.
    /// </summary>
    /// <remarks>
    /// Includes retry logic for endpoints that return empty results, as sometimes
    /// specific sections may come back empty and need a retry to get the full content.
    /// Uses semaphore to limit concurrent API calls per configuration.
    /// </remarks>
    /// <seealso cref="MedRecProApiClient.ExecuteEndpointAsync"/>
    /// <seealso cref="resultHasData"/>
    /**************************************************************/
    private async Task<List<AiEndpointResult>> executeEndpointsParallel(
        List<AiEndpointSpecification> endpoints,
        CancellationToken cancellationToken,
        int maxRetries = 1)
    {
        #region implementation

        var results = new List<AiEndpointResult>();
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrentCalls);

        var tasks = endpoints.Select(async endpoint =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await executeEndpointWithRetryAsync(endpoint, maxRetries, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        return results;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes a single endpoint with retry logic for empty results.
    /// </summary>
    /// <seealso cref="MedRecProApiClient.ExecuteEndpointAsync"/>
    /// <seealso cref="resultHasData"/>
    /**************************************************************/
    private async Task<AiEndpointResult> executeEndpointWithRetryAsync(
        AiEndpointSpecification endpoint,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        #region implementation

        AiEndpointResult result;
        var retryCount = 0;

        do
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.PerCallTimeoutSeconds));

            result = await _apiClient.ExecuteEndpointAsync(endpoint, cts.Token);

            // Check if result has data or if we should stop retrying
            if (resultHasData(result.Result) || result.StatusCode >= 400 || retryCount >= maxRetries)
            {
                break;
            }

            // Wait before retry
            retryCount++;
            _logger.LogDebug("[WorkPlan] Empty result for {Path}, retry {Retry}/{Max}",
                endpoint.Path, retryCount, maxRetries);

            await Task.Delay(500, cancellationToken);

        } while (retryCount <= maxRetries);

        return result;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes remaining steps (after discovery) with variable substitution.
    /// </summary>
    /// <remarks>
    /// Processes endpoint steps sequentially by step number, applying
    /// variable substitution and array expansion. Supports conditional
    /// skipping based on previous step results.
    /// </remarks>
    /// <seealso cref="substituteVariables"/>
    /// <seealso cref="expandArrayVariables"/>
    /// <seealso cref="extractVariablesFromResult"/>
    /**************************************************************/
    private async IAsyncEnumerable<WorkPlanProgress> executeRemainingStepsAsync(
        List<AiEndpointSpecification> endpoints,
        Dictionary<int, List<AiEndpointResult>> allResults,
        Dictionary<string, List<string>> extractedVariables,
        int totalSteps,
        string? conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        #region implementation

        var stepGroups = endpoints
            .GroupBy(e => e.Step ?? e.ExecutionOrder)
            .OrderBy(g => g.Key);

        foreach (var stepGroup in stepGroups)
        {
            var stepNumber = stepGroup.Key;
            var stepEndpoints = stepGroup.ToList();

            // Filter endpoints based on skip conditions
            var endpointsToExecute = filterEndpointsBySkipConditions(stepEndpoints, allResults);

            if (endpointsToExecute.Count == 0)
            {
                continue;
            }

            // Yield step progress
            yield return new WorkPlanProgress
            {
                Phase = WorkPlanPhase.Executing,
                CurrentStep = stepNumber,
                TotalSteps = totalSteps,
                Message = endpointsToExecute.FirstOrDefault()?.Description ?? $"Executing step {stepNumber}...",
                ConversationId = conversationId
            };

            // Apply variable substitution and expansion
            var substitutedEndpoints = endpointsToExecute
                .Select(e => substituteVariables(e, extractedVariables))
                .ToList();

            var expandedEndpoints = expandArrayVariables(substitutedEndpoints, extractedVariables);

            // Execute and store results
            var stepResults = await executeEndpointsParallel(expandedEndpoints, cancellationToken);
            allResults[stepNumber] = stepResults;

            // Extract variables for subsequent steps
            foreach (var result in stepResults)
            {
                if (result.Specification.OutputMapping != null && result.Result != null)
                {
                    extractVariablesFromResult(result, extractedVariables);
                }
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Filters endpoints based on skipIfPreviousHasResults conditions.
    /// </summary>
    /// <seealso cref="resultHasData"/>
    /**************************************************************/
    private List<AiEndpointSpecification> filterEndpointsBySkipConditions(
        List<AiEndpointSpecification> endpoints,
        Dictionary<int, List<AiEndpointResult>> allResults)
    {
        #region implementation

        var endpointsToExecute = new List<AiEndpointSpecification>();

        foreach (var endpoint in endpoints)
        {
            if (endpoint.SkipIfPreviousHasResults.HasValue)
            {
                var checkStep = endpoint.SkipIfPreviousHasResults.Value;
                if (allResults.TryGetValue(checkStep, out var previousResults))
                {
                    var hasResults = previousResults.Any(r => resultHasData(r.Result));
                    if (hasResults)
                    {
                        _logger.LogDebug("[WorkPlan] Skipping endpoint - step {CheckStep} has results", checkStep);
                        continue;
                    }
                }
            }

            endpointsToExecute.Add(endpoint);
        }

        return endpointsToExecute;

        #endregion
    }

    #endregion

    #region Synthesis Helpers

    /**************************************************************/
    /// <summary>
    /// Synthesizes results by calling the MedRecPro API.
    /// </summary>
    /// <seealso cref="MedRecProApiClient.SynthesizeAsync"/>
    /**************************************************************/
    private async Task<AiAgentSynthesis?> synthesizeResultsAsync(
        string originalQuery,
        List<AiEndpointResult> results,
        string? conversationId,
        CancellationToken cancellationToken)
    {
        #region implementation
        try
        {
            var request = new AiSynthesisRequest
            {
                OriginalQuery = originalQuery,
                ConversationId = conversationId,
                ExecutedEndpoints = results
            };

            return await _apiClient.SynthesizeAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkPlan] Synthesis failed");
            return new AiAgentSynthesis
            {
                ConversationId = conversationId,
                Response = $"Analysis completed but synthesis failed: {ex.Message}",
                IsComplete = false,
                Warnings = new List<string> { "Synthesis failed - showing raw results" }
            };
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Synthesizes results in batches for large result sets.
    /// </summary>
    /// <remarks>
    /// Processes large result sets in batches to avoid token limits,
    /// accumulating content and including data references in the final response.
    /// </remarks>
    /// <seealso cref="synthesizeResultsAsync"/>
    /// <seealso cref="buildDataReferencesFromProducts"/>
    /**************************************************************/
    private async IAsyncEnumerable<WorkPlanProgress> synthesizeBatchedAsync(
        string originalQuery,
        List<AiEndpointResult> results,
        List<ProductDiscovery> discoveredProducts,
        string? conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        #region implementation

        var batches = createResultBatches(results);
        var totalBatches = batches.Count;
        var accumulatedContent = new List<string>();
        Dictionary<string, string>? lastDataReferences = null;
        List<string>? lastSuggestedFollowUps = null;

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var batchNumber = i + 1;

            // Yield batch progress
            yield return new WorkPlanProgress
            {
                Phase = WorkPlanPhase.Synthesizing,
                Message = $"Analyzing batch {batchNumber} of {totalBatches}...",
                BatchIndex = batchNumber,
                TotalBatches = totalBatches,
                IsPartial = true,
                ConversationId = conversationId
            };

            // Synthesize batch
            var synthesis = await synthesizeResultsAsync(originalQuery, batch, conversationId, cancellationToken);

            if (synthesis != null && !string.IsNullOrEmpty(synthesis.Response))
            {
                accumulatedContent.Add(synthesis.Response);
                trackSynthesisReferences(synthesis, ref lastDataReferences, ref lastSuggestedFollowUps);

                yield return new WorkPlanProgress
                {
                    Phase = WorkPlanPhase.Synthesizing,
                    Message = $"Batch {batchNumber} complete",
                    BatchIndex = batchNumber,
                    TotalBatches = totalBatches,
                    Content = synthesis.Response,
                    IsPartial = batchNumber < totalBatches,
                    ConversationId = conversationId
                };
            }
        }

        // Build final combined result
        var dataReferences = lastDataReferences ?? new Dictionary<string, string>();
        if (dataReferences.Count == 0 && discoveredProducts.Count > 0)
        {
            dataReferences = buildDataReferencesFromProducts(discoveredProducts);
        }

        yield return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Complete,
            Message = "Analysis complete",
            Content = string.Join("\n\n---\n\n", accumulatedContent),
            ConversationId = conversationId,
            DataReferences = dataReferences,
            SuggestedFollowUps = lastSuggestedFollowUps,
            Products = discoveredProducts
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates batches of results for synthesis processing.
    /// </summary>
    /**************************************************************/
    private List<List<AiEndpointResult>> createResultBatches(List<AiEndpointResult> results)
    {
        #region implementation

        return results
            .Select((result, index) => new { result, index })
            .GroupBy(x => x.index / _options.SynthesisBatchSize)
            .Select(g => g.Select(x => x.result).ToList())
            .ToList();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Tracks data references and follow-ups from synthesis responses.
    /// </summary>
    /**************************************************************/
    private static void trackSynthesisReferences(
        AiAgentSynthesis synthesis,
        ref Dictionary<string, string>? lastDataReferences,
        ref List<string>? lastSuggestedFollowUps)
    {
        #region implementation

        if (synthesis.DataReferences != null && synthesis.DataReferences.Count > 0)
        {
            lastDataReferences = synthesis.DataReferences;
        }

        if (synthesis.SuggestedFollowUps != null && synthesis.SuggestedFollowUps.Count > 0)
        {
            lastSuggestedFollowUps = synthesis.SuggestedFollowUps;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Calculates product count to determine synthesis batching strategy.
    /// </summary>
    /**************************************************************/
    private static int calculateProductCountForSynthesis(
        List<ProductDiscovery> discoveredProducts,
        string[]? selectedProductGuids,
        List<AiEndpointResult> allEndpointResults)
    {
        #region implementation

        return discoveredProducts.Count > 0
            ? (selectedProductGuids?.Length ?? discoveredProducts.Count)
            : allEndpointResults.Count;

        #endregion
    }

    #endregion

    #region Product Extraction Helpers

    /**************************************************************/
    /// <summary>
    /// Extracts product information from discovery results.
    /// </summary>
    /// <remarks>
    /// Handles various API response formats including:
    /// - Direct array results
    /// - Paged results with items/data/results arrays
    /// - Nested pharmacologicClasses with products arrays
    /// Returns deduplicated products by GUID.
    /// </remarks>
    /// <seealso cref="extractProductsFromJsonElement"/>
    /// <seealso cref="extractProductsFromElement"/>
    /**************************************************************/
    private List<ProductDiscovery> extractProductsFromResults(List<AiEndpointResult> results)
    {
        #region implementation

        var products = new List<ProductDiscovery>();

        foreach (var result in results)
        {
            // Skip failed or empty results
            if (!isValidResultForExtraction(result))
            {
                continue;
            }

            try
            {
                var json = convertResultToJsonElement(result.Result!);
                extractProductsFromJsonElement(json, products);

                _logger.LogDebug("[WorkPlan] Extracted {Count} products from result so far", products.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WorkPlan] Failed to extract products from result");
            }
        }

        // Deduplicate by GUID
        var uniqueProducts = deduplicateProducts(products);
        _logger.LogDebug("[WorkPlan] Final unique product count: {Count}", uniqueProducts.Count);

        return uniqueProducts;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates whether a result can be used for product extraction.
    /// </summary>
    /**************************************************************/
    private bool isValidResultForExtraction(AiEndpointResult result)
    {
        #region implementation

        if (result.Result == null || result.StatusCode < 200 || result.StatusCode >= 300)
        {
            _logger.LogDebug("[WorkPlan] Skipping result with status {Status}", result.StatusCode);
            return false;
        }

        return true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Converts a result object to a JsonElement for parsing.
    /// </summary>
    /**************************************************************/
    private static JsonElement convertResultToJsonElement(object result)
    {
        #region implementation

        return result is JsonElement element
            ? element
            : JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(result, JsonOptions), JsonOptions);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from a JSON element based on its structure.
    /// </summary>
    /// <remarks>
    /// Handles array results, object wrappers, and nested pharmacologic class structures.
    /// </remarks>
    /// <seealso cref="extractProductsFromArray"/>
    /// <seealso cref="extractProductsFromObject"/>
    /**************************************************************/
    private void extractProductsFromJsonElement(JsonElement json, List<ProductDiscovery> products)
    {
        #region implementation

        _logger.LogDebug("[WorkPlan] Extracting products from {Kind} result", json.ValueKind);

        switch (json.ValueKind)
        {
            case JsonValueKind.Array:
                extractProductsFromArray(json, products);
                break;

            case JsonValueKind.Object:
                extractProductsFromObject(json, products);
                break;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from a JSON array.
    /// </summary>
    /**************************************************************/
    private void extractProductsFromArray(JsonElement arrayElement, List<ProductDiscovery> products)
    {
        #region implementation

        foreach (var item in arrayElement.EnumerateArray())
        {
            extractProductsFromElement(item, products);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from a JSON object, checking wrapper properties.
    /// </summary>
    /// <remarks>
    /// Checks common wrapper property names and handles pharmacologic class structures.
    /// Falls back to treating the root object as a product if no wrappers found.
    /// </remarks>
    /// <seealso cref="tryExtractFromWrapperProperty"/>
    /// <seealso cref="extractProductsFromPharmacologicClasses"/>
    /// <seealso cref="extractProductsFromProductsByClass"/>
    /**************************************************************/
    private void extractProductsFromObject(JsonElement objectElement, List<ProductDiscovery> products)
    {
        #region implementation

        JsonElement? items = null;

        // Try common wrapper properties
        items = tryExtractFromWrapperProperty(objectElement);

        // Handle pharmacologic class search results (legacy format with array)
        if (!items.HasValue)
        {
            extractProductsFromPharmacologicClasses(objectElement, products);
        }

        // Handle productsByClass structure (dictionary with class names as keys)
        // This is the format returned by /api/Label/pharmacologic-class/search
        if (products.Count == 0)
        {
            extractProductsFromProductsByClass(objectElement, products);
        }

        // Process wrapper items if found
        if (items.HasValue && items.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.Value.EnumerateArray())
            {
                extractProductsFromElement(item, products);
            }
        }
        else if (!items.HasValue && products.Count == 0)
        {
            // Single object result - try to extract from root
            extractProductsFromElement(objectElement, products);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Attempts to extract items array from common wrapper properties.
    /// </summary>
    /**************************************************************/
    private JsonElement? tryExtractFromWrapperProperty(JsonElement objectElement)
    {
        #region implementation

        foreach (var prop in WrapperPropertyNames)
        {
            if (tryGetPropertyCaseInsensitive(objectElement, prop, out var wrapperElement) &&
                wrapperElement.ValueKind == JsonValueKind.Array)
            {
                _logger.LogDebug("[WorkPlan] Found items in '{Prop}' property", prop);
                return wrapperElement;
            }
        }

        return null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from pharmacologic class search results (legacy array format).
    /// </summary>
    /// <remarks>
    /// Handles nested structure where pharmacologic classes contain products arrays.
    /// This is the legacy format - see <see cref="extractProductsFromProductsByClass"/>
    /// for the current productsByClass dictionary format.
    /// </remarks>
    /**************************************************************/
    private void extractProductsFromPharmacologicClasses(JsonElement objectElement, List<ProductDiscovery> products)
    {
        #region implementation

        if (!tryGetPropertyCaseInsensitive(objectElement, "pharmacologicClasses", out var classesElement) ||
            classesElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        _logger.LogDebug("[WorkPlan] Found pharmacologicClasses array");

        foreach (var classItem in classesElement.EnumerateArray())
        {
            // Each class may have a products array
            if (tryGetPropertyCaseInsensitive(classItem, "products", out var classProducts) &&
                classProducts.ValueKind == JsonValueKind.Array)
            {
                foreach (var productItem in classProducts.EnumerateArray())
                {
                    extractProductsFromElement(productItem, products);
                }
            }
            else
            {
                // The class item itself might be a product-like object
                extractProductsFromElement(classItem, products);
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Recursively finds all arrays nested within an object structure and extracts products.
    /// </summary>
    /// <remarks>
    /// This is a generic method that mirrors the JavaScript findNestedArrays() function.
    /// It recursively searches through nested objects to find arrays at any depth,
    /// which handles various response formats including:
    /// <list type="bullet">
    ///   <item>productsByClass: { "ClassName": [...products...] }</item>
    ///   <item>pharmacologicClasses: [{ products: [...] }]</item>
    ///   <item>Any other nested structure with arrays of product objects</item>
    /// </list>
    ///
    /// <example>
    /// Example structures handled:
    /// <code>
    /// // productsByClass format (pharmacologic-class/search endpoint)
    /// { "productsByClass": { "Anti-epileptic Agent [EPC]": [{...}, {...}] } }
    ///
    /// // Nested wrapper format
    /// { "data": { "results": [{...}, {...}] } }
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="objectElement">The JSON object element to search</param>
    /// <param name="products">List to populate with discovered products</param>
    /// <seealso cref="findNestedArraysRecursive"/>
    /// <seealso cref="extractProductsFromPharmacologicClasses"/>
    /**************************************************************/
    private void extractProductsFromProductsByClass(JsonElement objectElement, List<ProductDiscovery> products)
    {
        #region implementation

        // Use the generic recursive array finder
        var nestedItems = findNestedArraysRecursive(objectElement, maxDepth: 5, currentDepth: 0);

        if (nestedItems.Count > 0)
        {
            _logger.LogDebug("[WorkPlan] findNestedArraysRecursive found {Count} items from nested arrays", nestedItems.Count);

            foreach (var item in nestedItems)
            {
                extractProductsFromElement(item, products);
            }

            _logger.LogDebug("[WorkPlan] Extracted {Count} products from nested arrays", products.Count);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Recursively finds all arrays nested within an object structure.
    /// </summary>
    /// <remarks>
    /// Mirrors the JavaScript findNestedArrays() function from endpoint-executor.js.
    /// Recursively searches through nested objects to find arrays at any depth.
    /// When an array is found, its contents are added to the result list.
    /// When a nested object is found, the function recurses into it.
    /// </remarks>
    /// <param name="element">The JSON element to search</param>
    /// <param name="maxDepth">Maximum recursion depth to prevent infinite loops</param>
    /// <param name="currentDepth">Current recursion depth</param>
    /// <param name="currentPath">Current path for logging (optional)</param>
    /// <returns>List of all JSON elements found within nested arrays</returns>
    /// <seealso cref="extractProductsFromProductsByClass"/>
    /**************************************************************/
    private List<JsonElement> findNestedArraysRecursive(
        JsonElement element,
        int maxDepth = 5,
        int currentDepth = 0,
        string currentPath = "$")
    {
        #region implementation

        var foundItems = new List<JsonElement>();

        // Depth limit check
        if (currentDepth > maxDepth)
        {
            return foundItems;
        }

        // Handle based on element type
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                // If element is an array, return its contents
                foreach (var item in element.EnumerateArray())
                {
                    foundItems.Add(item);
                }
                _logger.LogDebug("[WorkPlan] Found array at path '{Path}' with {Count} elements",
                    currentPath, foundItems.Count);
                break;

            case JsonValueKind.Object:
                // Search through object properties
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{currentPath}.{property.Name}";
                    var value = property.Value;

                    if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
                    {
                        // Found an array with items - add its contents
                        _logger.LogDebug("[WorkPlan] Found nested array at key '{Key}' with {Count} elements",
                            property.Name, value.GetArrayLength());

                        foreach (var item in value.EnumerateArray())
                        {
                            foundItems.Add(item);
                        }
                    }
                    else if (value.ValueKind == JsonValueKind.Object)
                    {
                        // Recurse into nested objects
                        var nestedItems = findNestedArraysRecursive(value, maxDepth, currentDepth + 1, propertyPath);
                        if (nestedItems.Count > 0)
                        {
                            foundItems.AddRange(nestedItems);
                        }
                    }
                }
                break;
        }

        return foundItems;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from a JSON element, handling nested structures.
    /// </summary>
    /// <seealso cref="extractProductFromElement"/>
    /**************************************************************/
    private void extractProductsFromElement(JsonElement element, List<ProductDiscovery> products)
    {
        #region implementation

        // Try to extract product from this element
        var product = extractProductFromElement(element);
        if (product != null)
        {
            products.Add(product);
        }

        // Check for nested products arrays within this element
        if (element.ValueKind == JsonValueKind.Object)
        {
            extractNestedProducts(element, products);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts products from nested array properties within an element.
    /// </summary>
    /**************************************************************/
    private void extractNestedProducts(JsonElement element, List<ProductDiscovery> products)
    {
        #region implementation

        foreach (var prop in NestedArrayPropertyNames)
        {
            if (tryGetPropertyCaseInsensitive(element, prop, out var nestedArray) &&
                nestedArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var nestedItem in nestedArray.EnumerateArray())
                {
                    var nestedProduct = extractProductFromElement(nestedItem);
                    if (nestedProduct != null)
                    {
                        products.Add(nestedProduct);
                    }
                }
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts a single product from a JSON element.
    /// </summary>
    /// <remarks>
    /// Attempts to find GUID, name, labeler, and description properties
    /// using case-insensitive matching against known property names.
    /// </remarks>
    /**************************************************************/
    private static ProductDiscovery? extractProductFromElement(JsonElement element)
    {
        #region implementation

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Extract product properties using configured property name lists
        var guid = tryGetFirstMatchingStringProperty(element, GuidPropertyNames);
        var name = tryGetFirstMatchingStringProperty(element, NamePropertyNames);
        var labeler = tryGetFirstMatchingStringProperty(element, LabelerPropertyNames);
        var description = tryGetFirstMatchingStringProperty(element, DescriptionPropertyNames);

        // Require at least GUID or name to be valid
        if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(name))
        {
            return null;
        }

        return new ProductDiscovery
        {
            Guid = guid ?? Guid.NewGuid().ToString(),
            Name = name ?? "Unknown Product",
            Labeler = labeler,
            Description = description
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Tries to get the first matching string property from a list of property names.
    /// </summary>
    /**************************************************************/
    private static string? tryGetFirstMatchingStringProperty(JsonElement element, string[] propertyNames)
    {
        #region implementation

        foreach (var prop in propertyNames)
        {
            if (tryGetStringProperty(element, prop, out var value))
            {
                return value;
            }
        }

        return null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Deduplicates products by GUID.
    /// </summary>
    /**************************************************************/
    private static List<ProductDiscovery> deduplicateProducts(List<ProductDiscovery> products)
    {
        #region implementation

        return products
            .Where(p => !string.IsNullOrEmpty(p.Guid))
            .GroupBy(p => p.Guid, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        #endregion
    }

    #endregion

    #region Variable Extraction and Substitution Helpers

    /**************************************************************/
    /// <summary>
    /// Extracts variables from an endpoint result based on output mapping.
    /// </summary>
    /// <seealso cref="extractValuesFromPath"/>
    /**************************************************************/
    private void extractVariablesFromResult(
        AiEndpointResult result,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        if (result.Specification.OutputMapping == null || result.Result == null)
        {
            return;
        }

        try
        {
            var json = convertResultToJsonElement(result.Result);

            foreach (var mapping in result.Specification.OutputMapping)
            {
                extractVariableFromMapping(json, mapping, variables);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WorkPlan] Failed to extract variables from result");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts a single variable from a mapping configuration.
    /// </summary>
    /**************************************************************/
    private void extractVariableFromMapping(
        JsonElement json,
        KeyValuePair<string, string> mapping,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        var variableName = mapping.Key;
        var path = mapping.Value;

        // Handle array notation (e.g., "documentGUID[]")
        if (variableName.EndsWith("[]"))
        {
            variableName = variableName[..^2];
        }

        var values = extractValuesFromPath(json, path);

        if (!variables.ContainsKey(variableName))
        {
            variables[variableName] = new List<string>();
        }

        variables[variableName].AddRange(values);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts values from a JSON element using a simple path notation.
    /// </summary>
    /// <remarks>
    /// Supports dot notation with array access (e.g., "items[].documentGUID").
    /// </remarks>
    /**************************************************************/
    private List<string> extractValuesFromPath(JsonElement element, string path)
    {
        #region implementation

        var values = new List<string>();
        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (part.EndsWith("[]"))
            {
                // Array access - extract from each element
                values = extractValuesFromArrayPath(current, part, parts);
                return values;
            }
            else
            {
                // Simple property access
                if (!tryGetPropertyCaseInsensitive(current, part, out current))
                {
                    return values;
                }
            }
        }

        // Extract final value
        extractFinalValues(current, values);

        return values;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts values from an array path segment.
    /// </summary>
    /**************************************************************/
    private List<string> extractValuesFromArrayPath(JsonElement current, string part, string[] parts)
    {
        #region implementation

        var values = new List<string>();
        var propName = part[..^2];

        // Navigate to array property if specified
        if (!string.IsNullOrEmpty(propName))
        {
            if (!tryGetPropertyCaseInsensitive(current, propName, out var arrayElement) ||
                arrayElement.ValueKind != JsonValueKind.Array)
            {
                return values;
            }
            current = arrayElement;
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        // Calculate remaining path
        var remainingPath = string.Join(".", parts.SkipWhile(p => p != part).Skip(1));

        foreach (var item in current.EnumerateArray())
        {
            if (string.IsNullOrEmpty(remainingPath))
            {
                // End of path - extract string value
                if (item.ValueKind == JsonValueKind.String)
                {
                    var val = item.GetString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        values.Add(val);
                    }
                }
            }
            else
            {
                // Recursively extract from nested path
                values.AddRange(extractValuesFromPath(item, remainingPath));
            }
        }

        return values;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts final values from a JSON element at the end of a path.
    /// </summary>
    /**************************************************************/
    private static void extractFinalValues(JsonElement current, List<string> values)
    {
        #region implementation

        if (current.ValueKind == JsonValueKind.String)
        {
            var val = current.GetString();
            if (!string.IsNullOrEmpty(val))
            {
                values.Add(val);
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in current.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var val = item.GetString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        values.Add(val);
                    }
                }
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Substitutes variable placeholders in an endpoint specification.
    /// </summary>
    /// <remarks>
    /// Supports both single-brace {variable} and double-brace {{variable}} placeholder formats
    /// to maintain compatibility with UI endpoint specifications.
    /// </remarks>
    /// <seealso cref="substituteInString"/>
    /**************************************************************/
    private AiEndpointSpecification substituteVariables(
        AiEndpointSpecification endpoint,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        var result = new AiEndpointSpecification
        {
            Method = endpoint.Method,
            Path = substituteInString(endpoint.Path, variables),
            Description = endpoint.Description,
            ExpectedResponseType = endpoint.ExpectedResponseType,
            ExecutionOrder = endpoint.ExecutionOrder,
            Id = endpoint.Id,
            Step = endpoint.Step,
            DependsOn = endpoint.DependsOn,
            OutputMapping = endpoint.OutputMapping,
            SkipIfPreviousHasResults = endpoint.SkipIfPreviousHasResults
        };

        // Substitute in query parameters
        if (endpoint.QueryParameters != null)
        {
            result.QueryParameters = new Dictionary<string, string>();
            foreach (var kvp in endpoint.QueryParameters)
            {
                result.QueryParameters[kvp.Key] = substituteInString(kvp.Value, variables);
            }
        }

        // Handle body substitution if needed
        if (endpoint.Body != null)
        {
            var bodyJson = JsonSerializer.Serialize(endpoint.Body, JsonOptions);
            bodyJson = substituteInString(bodyJson, variables);
            result.Body = JsonSerializer.Deserialize<object>(bodyJson, JsonOptions);
        }

        return result;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Substitutes variable placeholders in a string.
    /// </summary>
    /// <remarks>
    /// Supports both single-brace {variable} and double-brace {{variable}} placeholder formats.
    /// </remarks>
    /**************************************************************/
    private static string substituteInString(string? input, Dictionary<string, List<string>> variables)
    {
        #region implementation

        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = input;

        foreach (var variable in variables)
        {
            if (variable.Value.Count == 0)
            {
                continue;
            }

            // Support both single-brace and double-brace formats
            var doubleBracePlaceholder = $"{{{{{variable.Key}}}}}";
            var singleBracePlaceholder = $"{{{variable.Key}}}";

            if (result.Contains(doubleBracePlaceholder))
            {
                result = result.Replace(doubleBracePlaceholder, variable.Value[0]);
            }
            else if (result.Contains(singleBracePlaceholder))
            {
                result = result.Replace(singleBracePlaceholder, variable.Value[0]);
            }
        }

        return result;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Expands array variables into multiple endpoints.
    /// </summary>
    /// <remarks>
    /// Creates one endpoint per array value when a path contains a variable
    /// with multiple values. Supports both placeholder formats.
    /// </remarks>
    /**************************************************************/
    private List<AiEndpointSpecification> expandArrayVariables(
        List<AiEndpointSpecification> endpoints,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        var expanded = new List<AiEndpointSpecification>();

        foreach (var endpoint in endpoints)
        {
            var (needsExpansion, arrayVariable, isDoubleBrace) = findArrayVariableForExpansion(endpoint, variables);

            if (needsExpansion && arrayVariable != null)
            {
                var expandedEndpoints = createExpandedEndpoints(endpoint, arrayVariable, isDoubleBrace, variables);
                expanded.AddRange(expandedEndpoints);
            }
            else
            {
                expanded.Add(endpoint);
            }
        }

        return expanded;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Finds an array variable that needs expansion in an endpoint path.
    /// </summary>
    /**************************************************************/
    private static (bool needsExpansion, string? arrayVariable, bool isDoubleBrace) findArrayVariableForExpansion(
        AiEndpointSpecification endpoint,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        foreach (var variable in variables)
        {
            var doubleBracePlaceholder = $"{{{{{variable.Key}}}}}";
            var singleBracePlaceholder = $"{{{variable.Key}}}";

            if (endpoint.Path.Contains(doubleBracePlaceholder) && variable.Value.Count > 1)
            {
                return (true, variable.Key, true);
            }
            else if (endpoint.Path.Contains(singleBracePlaceholder) && variable.Value.Count > 1)
            {
                return (true, variable.Key, false);
            }
        }

        return (false, null, false);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates expanded endpoints for each value in an array variable.
    /// </summary>
    /**************************************************************/
    private static List<AiEndpointSpecification> createExpandedEndpoints(
        AiEndpointSpecification endpoint,
        string arrayVariable,
        bool isDoubleBrace,
        Dictionary<string, List<string>> variables)
    {
        #region implementation

        var expanded = new List<AiEndpointSpecification>();
        var placeholder = isDoubleBrace
            ? $"{{{{{arrayVariable}}}}}"
            : $"{{{arrayVariable}}}";

        foreach (var value in variables[arrayVariable])
        {
            var clone = new AiEndpointSpecification
            {
                Method = endpoint.Method,
                Path = endpoint.Path.Replace(placeholder, value),
                Description = endpoint.Description,
                ExpectedResponseType = endpoint.ExpectedResponseType,
                ExecutionOrder = endpoint.ExecutionOrder,
                Id = endpoint.Id,
                Step = endpoint.Step,
                DependsOn = endpoint.DependsOn,
                OutputMapping = endpoint.OutputMapping,
                SkipIfPreviousHasResults = endpoint.SkipIfPreviousHasResults
            };

            if (endpoint.QueryParameters != null)
            {
                clone.QueryParameters = new Dictionary<string, string>(endpoint.QueryParameters);
            }

            clone.Body = endpoint.Body;

            expanded.Add(clone);
        }

        return expanded;

        #endregion
    }

    #endregion

    #region Checkpoint and Filtering Helpers

    /**************************************************************/
    /// <summary>
    /// Checks if checkpoint is required based on discovered products.
    /// </summary>
    /// <remarks>
    /// Returns checkpoint progress if product count exceeds threshold
    /// and no products have been pre-selected.
    /// </remarks>
    /**************************************************************/
    private WorkPlanProgress? checkForCheckpoint(
        string[]? selectedProductGuids,
        List<ProductDiscovery> discoveredProducts,
        int totalSteps,
        string? conversationId)
    {
        #region implementation

        // Skip checkpoint if products already selected or below threshold
        if (selectedProductGuids != null || discoveredProducts.Count < _options.CheckpointThreshold)
        {
            return null;
        }

        _logger.LogInformation("[WorkPlan] Checkpoint triggered - {Count} products exceed threshold of {Threshold}",
            discoveredProducts.Count, _options.CheckpointThreshold);

        return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.AwaitingCheckpoint,
            CurrentStep = 1,
            TotalSteps = totalSteps,
            Message = $"Found {discoveredProducts.Count} products. Select which to include for detailed analysis.",
            RequiresCheckpoint = true,
            Products = discoveredProducts,
            DiscoveredProductGuids = discoveredProducts.Select(p => p.Guid).ToList(),
            ConversationId = conversationId
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Filters results to only include selected products.
    /// </summary>
    /**************************************************************/
    private void filterResultsBySelectedProducts(
        Dictionary<int, List<AiEndpointResult>> allResults,
        string[] selectedGuids,
        Dictionary<string, List<string>> extractedVariables)
    {
        #region implementation

        var selectedSet = new HashSet<string>(selectedGuids, StringComparer.OrdinalIgnoreCase);

        // Filter extracted variables that contain GUIDs or IDs
        foreach (var variable in extractedVariables.ToList())
        {
            if (variable.Key.Contains("guid", StringComparison.OrdinalIgnoreCase) ||
                variable.Key.Contains("id", StringComparison.OrdinalIgnoreCase))
            {
                extractedVariables[variable.Key] = variable.Value
                    .Where(v => selectedSet.Contains(v))
                    .ToList();
            }
        }

        #endregion
    }

    #endregion

    #region JSON Utility Helpers

    /**************************************************************/
    /// <summary>
    /// Tries to get a string property value from a JSON element (case-insensitive).
    /// </summary>
    /**************************************************************/
    private static bool tryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        #region implementation

        value = null;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    value = prop.Value.GetString();
                    return !string.IsNullOrEmpty(value);
                }
            }
        }

        return false;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Tries to get a property from a JSON element (case-insensitive).
    /// </summary>
    /**************************************************************/
    private static bool tryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement result)
    {
        #region implementation

        result = default;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                result = prop.Value;
                return true;
            }
        }

        return false;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Checks if a result contains data (non-empty array/object).
    /// </summary>
    /**************************************************************/
    private static bool resultHasData(object? result)
    {
        #region implementation

        if (result == null)
        {
            return false;
        }

        try
        {
            var json = result is JsonElement element
                ? element
                : JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(result), new JsonSerializerOptions());

            return json.ValueKind switch
            {
                JsonValueKind.Array => json.GetArrayLength() > 0,
                JsonValueKind.Object => json.EnumerateObject().Any(),
                JsonValueKind.String => !string.IsNullOrEmpty(json.GetString()),
                JsonValueKind.Number => true,
                JsonValueKind.True => true,
                JsonValueKind.False => true,
                _ => false
            };
        }
        catch
        {
            return result != null;
        }

        #endregion
    }

    #endregion

    #region Progress Factory Helpers

    /**************************************************************/
    /// <summary>
    /// Creates an error progress object.
    /// </summary>
    /**************************************************************/
    private static WorkPlanProgress createErrorProgress(string error, string? conversationId = null)
    {
        #region implementation

        return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Error,
            Message = error,
            Error = error,
            ConversationId = conversationId
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a direct response progress object for queries that don't need API calls.
    /// </summary>
    /**************************************************************/
    private static WorkPlanProgress createDirectResponseProgress(
        AiAgentInterpretation interpretation,
        string? conversationId)
    {
        #region implementation

        return new WorkPlanProgress
        {
            Phase = WorkPlanPhase.Complete,
            Message = "Query answered directly",
            ConversationId = conversationId,
            DirectResponse = interpretation.DirectResponse,
            Content = interpretation.DirectResponse
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds data references (View Full Label links) from discovered products.
    /// </summary>
    /// <remarks>
    /// Creates links to the full label documents based on product GUIDs.
    /// Limits to 10 products to avoid overwhelming the response.
    /// </remarks>
    /**************************************************************/
    private Dictionary<string, string> buildDataReferencesFromProducts(List<ProductDiscovery> products)
    {
        #region implementation

        var references = new Dictionary<string, string>();

        foreach (var product in products.Take(10))
        {
            if (string.IsNullOrEmpty(product.Guid))
            {
                continue;
            }

            // Build display name from product info
            var displayName = !string.IsNullOrEmpty(product.Labeler)
                ? $"View Full Label ({product.Name} - {product.Labeler})"
                : $"View Full Label ({product.Name})";

            // Build URL to the label viewer
            var url = $"/label/{product.Guid}";

            // Use display name as key to avoid duplicates
            if (!references.ContainsKey(displayName))
            {
                references[displayName] = url;
            }
        }

        return references;

        #endregion
    }

    #endregion
}
