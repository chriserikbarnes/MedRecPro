namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Maps Claude-facing skill names to the internal skill configuration keys.
/// </summary>
/// <remarks>
/// This transformation belongs outside <see cref="ClaudeSkillService"/> because
/// it is a deterministic policy with no file-system, HTTP, or DI dependency.
/// Keeping it focused makes the mapping directly testable without reflection.
/// </remarks>
/// <seealso cref="ClaudeSkillService"/>
internal static class ClaudeSkillNameMapper
{
    #region implementation

    private static readonly IReadOnlyDictionary<string, string> SkillNameMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["indicationDiscovery"] = "labelIndicationWorkflow",
            ["labelContent"] = "label",
            ["inventorySummary"] = "label",
            ["equianalgesicConversion"] = "equianalgesicConversion",
            ["userActivity"] = "userActivity",
            ["cacheManagement"] = "settings",
            ["sessionManagement"] = "sessionManagement",
            ["dataRescue"] = "rescueWorkflow",
            ["retryFallback"] = "retry",
            ["pharmacologicClass"] = "pharmacologicClassSearch",
            ["pharmacologicClassSearch"] = "pharmacologicClassSearch",
            ["orangeBookPatents"] = "orangeBookPatents"
        };

    /**************************************************************/
    /// <summary>
    /// Maps and de-duplicates an AI-selected set of skill names.
    /// </summary>
    /// <param name="aiSkillNames">Skill names returned by the AI selector.</param>
    /// <returns>Internal configuration keys, with the default label skill when input is empty.</returns>
    /// <seealso cref="ClaudeSkillService.SelectSkillsAsync"/>
    internal static List<string> Map(IEnumerable<string> aiSkillNames)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(aiSkillNames);

        var mappedSkills = new List<string>();

        foreach (var aiName in aiSkillNames)
        {
            var mappedName = SkillNameMappings.TryGetValue(aiName, out var internalName)
                ? internalName
                : aiName;

            if (!mappedSkills.Contains(mappedName, StringComparer.OrdinalIgnoreCase))
            {
                mappedSkills.Add(mappedName);
            }
        }

        if (mappedSkills.Count == 0)
        {
            mappedSkills.Add("label");
        }

        return mappedSkills;

        #endregion
    }

    #endregion
}
