namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Optional diagnostic surface for parser routers that downgrade tables before
    /// parser selection.
    /// </summary>
    /// <remarks>
    /// The value describes the most recent <see cref="ITableParserRouter.Route"/>
    /// decision for audit reporting. It is intended for sequential routing flows.
    /// </remarks>
    /// <seealso cref="ITableParserRouter"/>
    public interface ITableParserRouterDiagnostics
    {
        /**************************************************************/
        /// <summary>
        /// Most recent skip or downgrade reason, or null when the table routed normally.
        /// </summary>
        string? LastRouteReason { get; }
    }
}
