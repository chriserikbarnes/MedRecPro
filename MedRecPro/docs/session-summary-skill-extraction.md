# Session Summary: Skill Prompt Extraction from ClaudeSearchService

**Date:** 2026-01-23
**Branch:** sweet-wu
**Purpose:** Extract inline AI prompt text blocks from `ClaudeSearchService.cs` into separate skill files

---

## Overview

This session refactored `ClaudeSearchService.cs` to externalize AI prompt templates into separate files, following the existing skill architecture pattern. The goal was to separate concerns and avoid building large skill strings with StringBuilder in services.

---

## What Was Done

### 1. Identified Skill Prompts in ClaudeSearchService

Two AI prompts were identified for extraction:

1. **Product Extraction Prompt** (`buildProductExtractionPrompt`)
   - Extracts drug/ingredient names from natural language descriptions
   - Used for UNII resolution fallback when interpret phase provides incorrect UNIIs

2. **Pharmacologic Class Matching Prompt** (`buildClassMatchingPrompt`)
   - Matches user queries (e.g., "beta blockers") to database class names (e.g., "Beta-Adrenergic Blockers [EPC]")

### 2. Created Skill Files

| File | Purpose |
|------|---------|
| `Skills/product-extraction.md` | Documentation for product extraction skill |
| `Skills/pharmacologic-class-matching.md` | Documentation for class matching skill |
| `Skills/interfaces/api/product-extraction-api.md` | API contract for `/api/Label/extract-product` endpoint |
| `Skills/prompts/product-extraction-prompt.md` | Actual prompt template with `{{DESCRIPTION}}` placeholder |
| `Skills/prompts/pharmacologic-class-matching-prompt.md` | Actual prompt template with `{{USER_QUERY}}` and `{{CLASS_LIST}}` placeholders |

### 3. Updated Configuration Files

**MedRecPro.csproj** - Added CopyToOutputDirectory entries:
```xml
<!-- AI service skill files (ClaudeSearchService) -->
<None Update="Skills\product-extraction.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<None Update="Skills\pharmacologic-class-matching.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<None Update="Skills\interfaces\api\product-extraction-api.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<!-- AI prompt templates (ClaudeSearchService) -->
<None Update="Skills\prompts\product-extraction-prompt.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<None Update="Skills\prompts\pharmacologic-class-matching-prompt.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

**appsettings.json** - Added skill/prompt paths in ClaudeApiSettings:
```json
"Skill-ProductExtraction": "skills/product-extraction.md",
"Skill-PharmacologicClassMatching": "skills/pharmacologic-class-matching.md",
"Prompt-ProductExtraction": "skills/prompts/product-extraction-prompt.md",
"Prompt-PharmacologicClassMatching": "skills/prompts/pharmacologic-class-matching-prompt.md",
```

### 4. Updated ClaudeSearchService.cs

**Added constants:**
```csharp
private const string SkillConfigSection = "ClaudeApiSettings";
private const double PromptCacheHours = 8.0;
```

**Added methods:**
- `loadPromptTemplate(string configKey, string cacheKeyPrefix)` - Loads prompt from file with caching
- `readPromptFileByPath(string promptFilePath)` - Reads file from disk

**Modified methods:**
- `buildProductExtractionPrompt(string description)` - Now loads from file, throws `InvalidOperationException` if not found
- `buildClassMatchingPrompt(string userQuery, List<string> classNames)` - Now loads from file, throws `InvalidOperationException` if not found

**Removed methods:**
- `buildProductExtractionPromptInline(string description)` - Large inline prompt text removed
- `buildClassMatchingPromptInline(string userQuery, string classListFormatted)` - Large inline prompt text removed

---

## Files Modified/Created

### Created Files
- `C:\Users\chris\Documents\Repos\MedRecPro\Skills\product-extraction.md`
- `C:\Users\chris\Documents\Repos\MedRecPro\Skills\pharmacologic-class-matching.md`
- `C:\Users\chris\Documents\Repos\MedRecPro\Skills\interfaces\api\product-extraction-api.md`
- `C:\Users\chris\Documents\Repos\MedRecPro\Skills\prompts\product-extraction-prompt.md`
- `C:\Users\chris\Documents\Repos\MedRecPro\Skills\prompts\pharmacologic-class-matching-prompt.md`

### Modified Files
- `C:\Users\chris\Documents\Repos\MedRecPro\MedRecPro.csproj`
- `C:\Users\chris\Documents\Repos\MedRecPro\appsettings.json`
- `C:\Users\chris\Documents\Repos\MedRecPro\Service\ClaudeSearchService.cs`

---

## Prompt Template Placeholders

### product-extraction-prompt.md
- `{{DESCRIPTION}}` - The description text to extract drug names from

### pharmacologic-class-matching-prompt.md
- `{{USER_QUERY}}` - The user's natural language query
- `{{CLASS_LIST}}` - Formatted list of available class names (one per line with `- ` prefix)

---

## How It Works at Runtime

1. `buildProductExtractionPrompt()` or `buildClassMatchingPrompt()` is called
2. `loadPromptTemplate()` checks cache first (8-hour TTL)
3. If not cached, reads config key from `ClaudeApiSettings:Prompt-*`
4. `readPromptFileByPath()` loads file from:
   - First: `AppContext.BaseDirectory` + relative path
   - Fallback: `Directory.GetCurrentDirectory()` + relative path
5. Template content is cached
6. Placeholders are replaced with actual values
7. Final prompt is returned for AI call

---

## Debugging Notes for Next Session

### Verify Files Are Copied to Output
After build, check that prompt files exist in output directory:
```
bin/Debug/net8.0/skills/prompts/product-extraction-prompt.md
bin/Debug/net8.0/skills/prompts/pharmacologic-class-matching-prompt.md
```

### Test Points

1. **Product Extraction Endpoint**: `GET /api/Label/extract-product?description=Search for sevelamer`
   - Should call `ExtractProductFromDescriptionAsync`
   - Which calls `buildProductExtractionPrompt`
   - Which loads `skills/prompts/product-extraction-prompt.md`

2. **Pharmacologic Class Search**: `GET /api/Label/pharmacologic-class/search?query=beta blockers`
   - Should call `MatchUserQueryToClassesAsync`
   - Which calls `buildClassMatchingPrompt`
   - Which loads `skills/prompts/pharmacologic-class-matching-prompt.md`

### Potential Issues

1. **File not found**: Check that csproj CopyToOutputDirectory is working
2. **Path resolution**: Check `AppContext.BaseDirectory` vs `Directory.GetCurrentDirectory()`
3. **Cache issues**: Cache key is `{cacheKeyPrefix}_{configKey}` - may need to clear cache during testing
4. **Placeholder not replaced**: Ensure placeholders in file match exactly: `{{DESCRIPTION}}`, `{{USER_QUERY}}`, `{{CLASS_LIST}}`

### Logging to Watch
```
[PROMPT LOAD] Configuration key '...' not found
[PROMPT LOAD] File not found at: ...
[PROMPT LOAD] Successfully loaded: ... (N chars)
[PRODUCT EXTRACTION] Prompt template file not found
[CLASS MATCHING] Prompt template file not found
```

---

## Related Existing Patterns

The pattern follows `ClaudeSkillService.cs` which already loads skills from files:
- `readSkillFile(string configKey, string cacheKeyPrefix)` at line ~1191
- `readSkillFileByPath(string skillFilePath)` at line ~1227

---

## Build Status

**Final build: 0 errors, 170 warnings** (pre-existing warnings only)

---

## Rollback Instructions

If needed, revert to inline prompts by:
1. Restore the `buildProductExtractionPromptInline` and `buildClassMatchingPromptInline` methods
2. Update `buildProductExtractionPrompt` and `buildClassMatchingPrompt` to call the inline methods as fallback instead of throwing exceptions
3. The inline prompt text can be found in the git history of `ClaudeSearchService.cs`
