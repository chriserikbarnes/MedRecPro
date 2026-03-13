# Journal

---

### 2026-02-24 12:25 PM EST — Orange Book Patent Import Service
Created `OrangeBookPatentParsingService.cs` for importing FDA Orange Book patent.txt data. The service follows the same patterns as `OrangeBookProductParsingService`: tilde-delimited file parsing, batch upsert (5,000 rows) with ChangeTracker.Clear(), dictionary-based natural key lookup, and progress reporting via callbacks.

Key decisions:
- **Upsert natural key:** (ApplType, ApplNo, ProductNo, PatentNo) — uniquely identifies a patent per product
- **FK resolution:** Loads all OrangeBook.Product records into a dictionary, resolves OrangeBookProductID by (ApplType, ApplNo, ProductNo) lookup; unlinked patents get null FK
- **Shared result class:** Extended `OrangeBookImportResult` with patent fields (PatentsCreated, PatentsUpdated, PatentsLinkedToProduct, UnlinkedPatents) rather than creating a separate result type, so the console orchestrator passes one result through both import phases
- **Flag parsing:** "Y"/blank convention → `parseYFlag()` helper (distinct from product service's "Yes"/"No" → `parseYesNo()`)
- **Console orchestrator:** Updated `OrangeBookImportService` to extract both products.txt and patent.txt from ZIP, refactored `extractProductsFileFromZip` → generic `extractFileFromZip(zipPath, fileName)`, added patent progress task after category matching phase

Files modified:
- **Created:** `MedRecProImportClass/Service/ParsingServices/OrangeBookPatentParsingService.cs`
- **Edited:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs` (extended OrangeBookImportResult)
- **Edited:** `MedRecProConsole/Services/OrangeBookImportService.cs` (DI registration, patent extraction, progress tracking)

Both projects build with 0 errors.

---

### 2026-02-24 12:45 PM EST — Refactored OrangeBookImportService: Extract Private Methods from Monolithic Lambda
Refactored `executeImportWithProgressAsync()` in `OrangeBookImportService.cs`. The `.StartAsync` lambda was ~185 lines mixing progress callback construction, message routing, phase transitions, and import orchestration. Broke it into focused private methods to eliminate duplication (DRY).

**New members added (7):**
- `formatActiveDescription(message)` — eliminated 8 duplicate `$"[orange1]{Markup.Escape(truncateForDisplay(...))}[/]"` expressions
- `completeProgressTask(task, label, color)` — eliminated 7 duplicate 3-line task-completion blocks; null-safe with default green color and red override for patent failures
- `tryUpdateBatchProgress(task, message)` — eliminated 2 duplicate batch regex parse + task update handlers (products & patents)
- `tryUpdateSubstringProgress(task, pattern, message)` — eliminated 2 duplicate substring regex handlers (ingredients & categories), parameterized on regex pattern
- `buildProductProgressCallback(ctx, productTask, matchingTasks)` — returns the product `Action<string>` callback; contains all message routing using the DRY helpers
- `buildPatentProgressCallback(patentTask)` — returns the patent `Action<string>` callback
- `ProductMatchingTasks` (private inner class) — holds mutable refs to lazily-created matching-phase progress tasks so both the callback and post-import completion can access them

**Result:** Lambda shrank from ~185 lines to ~40 lines of pure orchestration. No behavioral changes — purely structural refactoring. Build succeeds with 0 errors, 0 warnings.

**File modified:** `MedRecProConsole/Services/OrangeBookImportService.cs`

---

### 2026-02-24 1:10 PM EST — Orange Book Exclusivity Import Service
Created `OrangeBookExclusivityParsingService.cs` for importing FDA Orange Book exclusivity.txt data. Follows the same patterns as `OrangeBookPatentParsingService`: tilde-delimited file parsing (5 columns), batch upsert (5,000 rows) with ChangeTracker.Clear(), dictionary-based product lookup for FK resolution, and progress reporting via callbacks.

Key decisions:
- **Upsert natural key:** (ApplType, ApplNo, ProductNo, ExclusivityCode) — one product can have multiple exclusivity codes simultaneously (e.g., ODE-417, ODE-420, ODE-421 on the same product)
- **FK resolution:** Same product lookup pattern as patent service — Dictionary<(ApplType, ApplNo, ProductNo), int> for O(1) resolution
- **Shared result class:** Extended `OrangeBookImportResult` with 4 exclusivity fields (ExclusivityCreated, ExclusivityUpdated, ExclusivityLinkedToProduct, UnlinkedExclusivity)
- **Simpler than patents:** Only 5 columns (no boolean flags, no use codes) — just natural key + code + date
- **Console display:** Added patent AND exclusivity rows to `DisplayOrangeBookResults` (patents were previously missing from display output)
- **Quality metrics:** Added UnlinkedPatents and UnlinkedExclusivity to the quality metrics table

Files:
- **Created:** `MedRecProImportClass/Service/ParsingServices/OrangeBookExclusivityParsingService.cs`
- **Edited:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs` (extended OrangeBookImportResult with exclusivity counters)
- **Edited:** `MedRecProConsole/Services/OrangeBookImportService.cs` (DI registration, exclusivity.txt extraction, Phase C progress tracking, buildExclusivityProgressCallback)
- **Edited:** `MedRecProConsole/Helpers/ConsoleHelper.cs` (added patent + exclusivity rows to import results and quality metrics)
- **Edited:** `MedRecProConsole/README.md` (documented patent and exclusivity import phases)

Both projects build with 0 errors.

---

### 2026-02-24 2:30 PM EST — Patent Import Error Diagnostics & Row-Level Retry
Fixed the Orange Book patent import error handling. The import was failing on batch 4 (rows 15001-20000) with the generic EF Core message "An error occurred while saving the entity changes. See the inner exception for details." — the actual SQL Server error was buried in `ex.InnerException` but the catch block only captured `ex.Message`.

**Changes made to `OrangeBookPatentParsingService.cs`:**

1. **`getFullExceptionMessage` helper** — walks the full `InnerException` chain and joins all messages with " → ", so the actual SQL error (e.g., string truncation, constraint violation) surfaces in console output
2. **Top-level catch updated** — now reports the unwrapped exception chain instead of the generic wrapper
3. **Batch-level error recovery** — when `SaveChangesAsync` fails on a 5,000-row batch, the service now:
   - Logs the failing batch number and row range
   - Clears the change tracker and re-loads existing patents
   - Retries every row in the failed batch individually with its own `SaveChangesAsync`
   - For each failing row, logs all field values (ApplType, ApplNo, ProductNo, PatentNo, UseCode, dates, flags) plus the full exception chain
   - Adds specific failing row details to `result.Errors` for console display
   - Continues processing remaining rows (doesn't abort the import for one bad row)
   - Corrects linked/unlinked counts via `countBatchLinked`/`countBatchUnlinked` helpers

Both projects build with 0 errors.

---

### 2026-02-24 3:15 PM EST — Widen PatentNo Column for Exclusivity Code Suffixes
The row-level retry diagnostics from the previous session pinpointed the root cause: FDA patent.txt includes patent numbers with exclusivity code suffixes (e.g., `11931377*PED` = 12 chars). The `PatentNo` column was `VARCHAR(11)`, truncating at `11931377*PE` — ~50+ rows affected.

**Fix:** Widened `PatentNo` from `VARCHAR(11)` to `VARCHAR(17)`. Chose 17 to accommodate all known exclusivity suffixes (*NCE, *ODE, *PED, *GAIN, *PC, *CGT) plus future 9-digit patent numbers — worst case: 9 digits + `*` + 5-char code (*GAIN) = 15, with 2 chars buffer.

**Files:**
- **Created:** `MedRecPro/SQL/MedRecPro-TableAlter-OrangeBookPatent.sql` — idempotent ALTER script: drops 3 indexes referencing PatentNo, widens column, recreates indexes, updates MS_Description extended property
- **Edited:** `MedRecPro/SQL/MedRecPro-TableCreate-OrangeBook.sql` — updated column definition for new deployments
- **Edited:** `MedRecProImportClass/Models/OrangeBook.cs` — updated XML summary to document all exclusivity suffix types

No C# logic changes needed — the entity uses `string?` with no `[MaxLength]` and the parser reads values as-is. Both projects build with 0 errors.

---

### 2026-02-24 3:45 PM EST — Orange Book Patent Use Code Lookup Table (Phase D)
Added a patent use code lookup table to the Orange Book import pipeline. The FDA `patent.txt` file contains use code values (e.g., `U-141`) in the `PatentUseCode` column but does NOT include their definitions — those are only published separately on the FDA website. Created a new embedded JSON resource + parsing service to upsert 4,409 code-to-definition mappings during import.

**Approach:** Embedded JSON resource (no new NuGet dependencies — Newtonsoft.Json already available). The user had already converted the FDA Excel data to JSON. Natural PK (`PatentUseCode` VARCHAR(6)) since the code IS the key and no FK references point to this table.

**Files created (3):**
- `MedRecProImportClass/Resources/OrangeBookPatentUseCodes.json` — 4,409 entries, embedded assembly resource
- `MedRecProImportClass/Service/ParsingServices/OrangeBookPatentUseCodeParsingService.cs` — loads JSON via `Assembly.GetManifestResourceStream()`, single-batch upsert (small dataset), follows existing service pattern
- `MedRecPro/SQL/MedRecPro-TableCreate-OrangeBookPatentUseCode.sql` — standalone migration script with IF NOT EXISTS guard

**Files modified (5):**
- `MedRecProImportClass/MedRecProImportClass.csproj` — added `<EmbeddedResource>` for the JSON file
- `MedRecProImportClass/Models/OrangeBook.cs` — added `PatentUseCodeDefinition` nested class (class name avoids C# "Color Color" collision; `Code` property mapped to `[Column("PatentUseCode")]`)
- `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs` — added `PatentUseCodesLoaded` to `OrangeBookImportResult`
- `MedRecProConsole/Services/OrangeBookImportService.cs` — added to truncation array, DI registration, Phase D orchestration with progress callback
- `MedRecProConsole/Helpers/ConsoleHelper.cs` — added "Patent Use Codes" row to results display

**Import pipeline is now 4 phases:** Products → Patents → Exclusivity → Patent Use Codes. Phase D is independent of A-C (no data dependencies). Both projects build with 0 errors.

Also updated `MedRecProImportClass/README.md` to document all four import phases, the complete entity table (8 entities), and step-by-step instructions for updating patent use code definitions (download Excel from FDA, convert to JSON, replace embedded resource). Added `OrangeBookPatentUseCode` as section 8 to the main `MedRecPro-TableCreate-OrangeBook.sql` script (header, table creation, extended properties, summary).

---

### 2026-02-25 9:05 AM EST — Fix: Country Organization Suffixes Causing False Matches in Orange Book Import
Fixed a bug in `OrangeBookProductParsingService.cs` where dotted country-specific organization suffixes like "S.P.A." (Italian) caused false applicant-to-organization associations. The dots in "S.P.A." broke regex `\b` word boundaries, so the suffix regex couldn't strip them. When dots were later removed as punctuation, "S.P.A." became three single-character tokens `{"S", "P", "A"}` that inflated containment scores to 0.75 (above the 0.67 threshold), causing every Italian S.p.A. company to false-match with every other.

**Three changes made:**
1. **Expanded `_corporateSuffixPattern` regex** — added `SPA` (Italian), `SL` (Spanish), `KGAA` (German) to the corporate suffix alternation
2. **Added dot stripping in `normalizeCompanyName`** — `result.Replace(".", "")` runs after ampersand stripping but before the suffix regex, collapsing "S.P.A." → "SPA" so it gets matched and removed
3. **Filtered single-char tokens in `tokenize`** — added `.Where(t => t.Length >= 2)` safety net to exclude stray single-letter tokens that carry no discriminating value

**File modified:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs`

Build verified with 0 errors.

---

### 2026-02-25 9:20 AM EST — Fix: Noise-Only Tokens Causing Massive Over-Matching in Orange Book Import
Follow-up to the S.P.A. suffix fix. The single-char token filter added earlier correctly strips stray letters from dotted abbreviations, but it also strips legitimate short tokens from names like "I 3 PHARMACEUTICALS LLC". After suffix stripping removes "LLC" and single-char filtering removes "I" and "3", the only remaining token is `{"PHARMACEUTICALS"}` — a pharma noise word. Containment of a single noise token against any org containing "Pharmaceuticals" = 1/1 = 1.0, causing every pharma company to false-match.

**Fix:** Added a third condition to the `fullViable` guard in `matchByTokenSimilarity` — at least one token must NOT be a pharma noise word (`fullTokens.Any(t => !_pharmaNoisePattern.IsMatch(t))`). Reuses the existing `_pharmaNoisePattern` regex. Applicants with only noise tokens are skipped from fuzzy matching but can still match via the exact match tier (Tier 1).

**File modified:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs`

Build verified with 0 errors.

---

### 2026-02-25 9:39 AM EST — Fix: Cross-Jurisdiction Entity Type Mismatch in Orange Book Fuzzy Matching
After suffix stripping, "MERCK SERONO S.p.A" (Italian) and "MERCK KGAA" (German) both reduce to names containing "MERCK" and token similarity exceeds the 0.67 threshold. The entity codes (SPA = Italian, KGAA = German) were discarded during normalization rather than compared. Same issue caused "Merck Sharp & Dohme LLC" (US) to match "MERCK KGAA" (DE).

**Fix:** Added jurisdiction-aware cross-check to fuzzy matching. Before suffix stripping, the rightmost corporate suffix is detected from the raw name and mapped to a jurisdiction group via `_entityJurisdictionGroups` dictionary. During fuzzy matching, if both applicant and org have detected jurisdictions and they differ → pairing is skipped. Same-jurisdiction codes are compatible (INC vs LLC both US → OK). Neutral suffixes (CO, SA, COMPANY, etc.) never trigger rejection.

**Six changes made:**
1. **`_entityJurisdictionGroups` dictionary** — maps suffixes to jurisdiction codes (US, UK, DE, IT, ES, FR, NL, EU)
2. **`EntityJurisdiction` field on `OrgCacheEntry`** — pre-computed per org at cache load time
3. **`detectEntityJurisdiction` method** — extracts rightmost suffix from raw name, looks up jurisdiction group
4. **Populated jurisdiction in `loadOrganizationCacheAsync`** — calls `detectEntityJurisdiction(org.OrganizationName!)`
5. **Jurisdiction guard in Pass 1 inner loop** — skips orgs with incompatible jurisdiction
6. **Jurisdiction guard in Pass 2 inner loop** — same check, reuses `applicantJurisdiction`

**File modified:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs`

Build verified with 0 errors.

---

### 2026-02-25 10:57 AM EST — Fix: Cross-Jurisdiction Match Leak in Tier 1 Exact Matching
The jurisdiction guards added in the previous session only covered Tier 2 (fuzzy matching). Tier 1 (`matchByNormalizedExact`) had no jurisdiction check at all. "Corden Pharma GmbH" (DE) and "CORDEN PHARMA LATINA SPA" (IT) both normalize to "CORDEN PHARMA" after suffix stripping, sharing the same `orgNameLookup` bucket. When an applicant's name matched that bucket, both orgs were linked regardless of jurisdiction.

**Fix:** Extended the existing jurisdiction infrastructure to Tier 1. Built an org-ID → jurisdiction lookup dictionary from `orgCache`, detected the applicant's jurisdiction from `ApplicantFullName` (falling back to `ApplicantName`), and added jurisdiction guards in both the full-name and short-name `foreach` loops inside `matchByNormalizedExact`. Cross-jurisdiction pairings (e.g., IT applicant → DE org) are now skipped. No new methods or fields needed — reused `detectEntityJurisdiction()` and `OrgCacheEntry.EntityJurisdiction` from the prior session.

**File modified:** `MedRecProImportClass/Service/ParsingServices/OrangeBookProductParsingService.cs`

Build verified with 0 errors.

---

### 2026-02-25 2:41 PM EST — Fix: Orange Book Test Pipeline Failures (Shared-Cache SQLite Pattern)
Fixed 22 failing pipeline tests across 3 test files. All pipeline tests (`ProcessProductsFileAsync_*`, `ProcessPatentsFileAsync_*`, `ProcessPatentUseCodesAsync_*`) were failing because the services' `finally { await connection.CloseAsync(); }` block destroys SQLite in-memory databases created with `DataSource=:memory:` — the database only exists while its connection is open.

**Root cause:** The services correctly receive the test's in-memory `ApplicationDbContext` via the mocked `IServiceScopeFactory` chain, write data to the correct SQLite database, then destroy it by closing the connection in the `finally` block. When the test's assertion code subsequently queries via `context.Set<T>().FirstAsync()`, it finds an empty database.

**Fix:** Applied the same shared-cache named in-memory DB pattern already proven in `OrangeBookExclusivityParsingServiceTests.cs`:
- `createSharedMemoryDb()` helper creates a unique `file:test_{guid}?mode=memory&cache=shared` URI
- A **sentinel connection** stays open for the test's lifetime, keeping the DB alive
- A **service connection** is passed to the context — when the service closes it, the sentinel preserves the data
- After the service returns, tests reopen the connection before asserting: `if (connection.State != Open) await connection.OpenAsync()`

**Additionally added:** try/catch blocks with `Debug.WriteLine` tracing in every pipeline test to surface exception details, result.Success/Errors state, and inner exception chains in the Test Explorer Output pane.

**Files modified (3):**
- `MedRecProTest/OrangeBookProductParsingServiceTests.cs` — 14 pipeline tests + `createSharedMemoryDb()` helper
- `MedRecProTest/OrangeBookPatentParsingServiceTests.cs` — 5 pipeline tests + `createSharedMemoryDb()` helper
- `MedRecProTest/OrangeBookPatentUseCodeParsingServiceTests.cs` — 3 pipeline tests + `createSharedMemoryDb()` helper

No service code changes — the `finally { connection.CloseAsync() }` is correct for production. Build verified with 0 errors.

---

### 2026-02-25 3:16 PM EST — Orange Book BCP Migration Script
Created `SQL/MedRecPro-OrangeBook-Export-Import.ps1` — a BCP-based export/import utility for migrating all 8 Orange Book tables from local SQL Server to Azure SQL Database. Cloned from the existing TempTable migration script with key differences: dependency-aware truncation order (junctions first, parents last), dependency-aware import order (parents first, junctions last), `-E` flag for identity value preservation (surrogate PKs referenced by child/junction tables), and import-order sorting when running standalone import from discovered .dat files. Handles OrangeBookPatentUseCode, OrangeBookApplicant, OrangeBookProduct, OrangeBookPatent, OrangeBookExclusivity, and three junction tables.

---

### 2026-02-26 1:15 PM EST — Add vw_OrangeBookPatent View and Covering Indexes
Added `vw_OrangeBookPatent` to `MedRecPro_Views.sql` — joins NDA Orange Book products with patent records, cross-references to SPL label DocumentGUIDs via `vw_ActiveIngredients`, resolves patent use code definitions, and computes three derived flags (HasWithdrawnCommercialReasonFlag, HasPediatricFlag, HasLevothyroxineFlag). Filters to ApplType = 'N' with non-null patent expiration dates.

Added two covering indexes to `MedRecPro_Indexes.sql` in Section 16 (Orange Book):
- **IX_OrangeBookPatent_PatentExpireDate_Covering** — filtered index on PatentExpireDate (WHERE NOT NULL) with INCLUDE for join keys and flag columns; supports date range queries against the view.
- **IX_OrangeBookPatent_Flags_Covering** — composite index on (DrugSubstanceFlag, DrugProductFlag, DelistFlag) with INCLUDE for patent fields and join keys; supports flag-based filtering.

Existing indexes on PatentNo, OrangeBookProductID, and ApplNo already cover the DocumentGUID cross-reference path and ApplicationNumber/PatentNumber lookups.

---

### 2026-02-26 2:36 PM EST — Add C# Model, DTO, and Data Access for vw_OrangeBookPatent
Created the full C# data access layer for the `vw_OrangeBookPatent` database view across 4 files:

- **LabelView.cs** — Added `OrangeBookPatent` nested entity class with 18 properties matching view columns. Auto-registered in DbContext via reflection (no DbContext changes needed).
- **LabelViewDto.cs** — Added `OrangeBookPatentDto` with encrypted dictionary, computed `LabelLink` property (relative URL to FDA label when DocumentGUID is available), and `[JsonIgnore]` helper properties for type-safe access.
- **DtoLabelAccess-Views.cs** — Added private `buildOrangeBookPatentDtos` builder that transforms entities via `ToEntityWithEncryptedId` and computes LabelLink from DocumentGUID.
- **DtoLabelAccess.cs** — Added public `SearchOrangeBookPatentsAsync` with 9 optional filters (all AND logic): `expiringInMonths` (date range), `documentGuid`, `applicationNumber`, `ingredient` (partial match, no phonetic), `tradeName` (partial match, no phonetic), `patentNo`, `patentExpireDate` (exact, lower precedence than expiringInMonths), `hasPediatricFlag`, `hasWithdrawnCommercialReasonFlag`. Includes caching, pagination, and ordering by soonest-expiring first.

Build verified: 0 errors.

---

### 2026-02-26 — DtoLabelAccess Document Tests (DtoLabelAccessDocumentTests.cs)
Created `MedRecProTest/DtoLabelAccessDocumentTests.cs` — 14 MSTest unit tests covering the three Document-related public methods of `DtoLabelAccess`: `BuildDocumentsAsync` (paginated overload), `BuildDocumentsAsync` (GUID overload), and `GetPackageIdentifierAsync`.

**Tests written (14):**
- **Paginated overload (7):** empty database, single document, multiple documents, first page pagination, second page pagination, batch loading flag, sequential loading flag
- **GUID overload (4):** empty database, non-existent GUID, valid GUID with filtering, batch loading with GUID
- **GetPackageIdentifierAsync (3):** null packaging level ID returns null, non-existent ID returns null, valid ID returns DTO with full hierarchy seeded (Document -> StructuredBody -> Section -> Product -> PackagingLevel -> PackageIdentifier)

Uses shared `DtoLabelAccessTestHelper` infrastructure: `CreateSharedMemoryDb()` sentinel pattern, `CreateTestContext()`, `ClearCache()` in `[TestInitialize]`, and `SeedFullDocumentHierarchyAsync` / individual seed methods. Follows all project conventions: `#region implementation` blocks, `/**************************************************************/` separators, XML doc with `<seealso cref>`, `{Method}_{Condition}_{Expected}` naming.

Build verified: 0 errors (6 pre-existing warnings in other files).

---

### 2026-02-26 3:08 PM EST — Orange Book Patent Search Tests

Created `MedRecProTest/DtoLabelAccessOrangeBookTests.cs` with 12 MSTest tests for `DtoLabelAccess.SearchOrangeBookPatentsAsync`. Tests cover the complete filter surface: empty database, no-filter return-all, individual filters (ApplicationNumber exact match, Ingredient partial match via FilterBySearchTerms, TradeName partial match, PatentNo exact match, DocumentGuid, HasPediatricFlag, HasWithdrawnCommercialReasonFlag), non-matching patent number returning empty, pagination (page 1 size 2 of 3 seeded), and multi-filter AND intersection (ApplicationNumber + HasPediatricFlag).

Used `DtoLabelAccessTestHelper.SeedOrangeBookPatentView` for all seeding. Each test creates an isolated shared-cache SQLite in-memory database with sentinel connection. Asserts verify count, and spot-check DTO properties (TradeName, Ingredient) via the convenience accessors on `OrangeBookPatentDto`.

Follows all project conventions: `[TestInitialize]` calls `ClearCache()`, `#region implementation` blocks, `/**************************************************************/` separators, XML doc with `<seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>`, `{Method}_{Condition}_{Expected}` naming. Build verified: 0 errors.

---

### 2026-02-26 3:30 PM EST — DtoLabelAccess View Navigation Tests

Created `MedRecProTest/DtoLabelAccessViewNavigationTests.cs` with 67 MSTest tests covering all View Navigation methods (#4-#22) of `DtoLabelAccess`. Methods tested: SearchByApplicationNumberAsync (5 tests: empty DB, mapped DTO, numeric-only search, no match, partial/prefix match), GetApplicationNumberSummariesAsync (4 tests: empty DB, mapped DTO, marketingCategory filter, no filter returns all), SearchByPharmacologicClassAsync (3 tests: empty DB, mapped DTO, partial match), SearchByPharmacologicClassExactAsync (3 tests: empty DB, exact match, partial does NOT match), GetPharmacologicClassHierarchyAsync (3 tests: empty DB, mapped DTO, multiple rows), GetPharmacologicClassSummariesAsync (3 tests: empty DB, mapped DTO, ordering by ProductCount desc), GetIngredientActiveSummariesAsync (4 tests: empty DB, mapped DTO, minProductCount filter, ingredient name filter), GetIngredientInactiveSummariesAsync (3 tests: empty DB, mapped DTO, minProductCount filter), SearchByIngredientAsync (4 tests: empty DB, UNII exact match, substance name search, no match), GetIngredientSummariesAsync (3 tests: empty DB, mapped DTO, ingredient filter), SearchIngredientsAdvancedAsync (3 tests: empty DB, UNII search, activeOnly filter), FindProductsByApplicationNumberWithSameIngredientAsync (3 tests: empty DB, with data, no match), FindRelatedIngredientsAsync (3 tests: empty DB, active ingredient, inactive ingredient), SearchByNDCAsync (4 tests: empty DB, mapped DTO, partial match, no match), SearchByPackageNDCAsync (3 tests: empty DB, mapped DTO, partial match), SearchByLabelerAsync (3 tests: empty DB, mapped DTO, no match), GetLabelerSummariesAsync (3 tests: empty DB, mapped DTO, ordering by ProductCount desc), GetDocumentNavigationAsync (5 tests: empty DB, mapped DTO, latestOnly filter, setGuid filter, all versions), GetDocumentVersionHistoryAsync (5 tests: empty DB, by SetGUID, by DocumentGUID, no match, ordering by VersionNumber desc).

All tests use isolated shared-cache SQLite in-memory databases with sentinel connections via `DtoLabelAccessTestHelper`. Follows all conventions: `[TestInitialize]` calls `ClearCache()`, `#region implementation` blocks, `/**************************************************************/` separators, XML doc with `<seealso cref>`, `{Method}_{Condition}_{Expected}` naming. Build verified: 0 errors.

---

### 2026-02-26 3:45 PM EST — DtoLabelAccess Content Tests (#23-#36)

Created `MedRecProTest/DtoLabelAccessContentTests.cs` with 48 MSTest tests covering all content-oriented methods (#23-#36) of `DtoLabelAccess`. Methods tested: SearchBySectionCodeAsync (3 tests: empty DB, mapped DTO, non-matching code), GetSectionTypeSummariesAsync (3 tests: empty DB, mapped DTO, ordering by DocumentCount descending), GetSectionContentAsync (4 tests: empty DB, by documentGuid, by documentGuid + sectionCode filter, different documentGuid returns empty), GetDrugInteractionsAsync (3 tests: empty DB, matching UNII, no matching UNII), GetDEAScheduleProductsAsync (3 tests: empty DB, no filter returns all, scheduleCode filter), SearchProductSummaryAsync (3 tests: empty DB, mapped DTO, non-matching name), GetRelatedProductsAsync (4 tests: empty DB, by sourceProductId, by sourceDocumentGuid, by relationshipType), GetAPIEndpointGuideAsync (3 tests: empty DB, no category returns all, category filter), GetInventorySummaryAsync (3 tests: empty DB, no category returns all, category filter -- note: no pkSecret parameter), GetProductLatestLabelsAsync (4 tests: empty DB, mapped DTO, UNII filter, productName filter), GetProductIndicationsAsync (5 tests: empty DB, mapped DTO, UNII filter, productName filter, indicationSearch text filter), GetLabelSectionMarkdownAsync (4 tests: empty DB, all sections, sectionCode filter, different documentGuid), GenerateLabelMarkdownAsync (3 tests: empty export, assembled markdown with metadata, metadata extraction from first section), GenerateCleanLabelMarkdownAsync (3 tests: empty DB returns empty string + Claude API NOT called, with data returns cleaned markdown + Claude API called once, passes document title to service via Moq callback capture).

All tests use isolated shared-cache SQLite in-memory databases with sentinel connections via `DtoLabelAccessTestHelper`. GenerateCleanLabelMarkdownAsync tests use Moq to mock `IClaudeApiService.GenerateCleanMarkdownAsync(string, string?)`. Build verified: 0 errors.

---

### 2026-02-26 3:45 PM EST — DtoLabelAccess Test Fixes (4 Failures Resolved)

Fixed 4 failing tests out of 141 total DtoLabelAccess tests:

**Root cause 1 — GUID format mismatch (3 tests):** EF Core 8.0 SQLite sends Guid parameters as uppercase TEXT (`'AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA'`), but our seed methods stored GUIDs as lowercase via `.ToString()`. SQLite text comparison is case-sensitive, so the WHERE clause never matched. Fix: Changed all GUID seed parameters from `.ToString()` to `.ToString("D").ToUpper()` in `DtoLabelAccessTestHelper.cs`.

**Root cause 2 — LIKE overlap in DEA schedule test (1 test):** `FilterBySearchTerms("CII")` generates `LIKE '%CII%'` which matched both "CII" and "CIII" rows. Fix: Changed the non-target seed row from "CIII" to "CV" so it doesn't contain "CII".

**Diagnostic approach:** Wrote a temporary test that checked `typeof(SetGUID)`, `hex(SetGUID)`, raw SQL matches (BLOB/TEXT/uppercase), EF Core's `ToQueryString()` output, and EF Core query results. This revealed that EF Core DDL uses TEXT for Guid columns and sends parameters as uppercase TEXT strings — not BLOB as initially assumed from EF Core 8.0 breaking change docs.

Final results: 342 total tests (141 new DtoLabelAccess + 201 existing), all passing, zero regressions.

---

### 2026-02-27 10:20 AM EST — Application Insights Failure Log Noise Filtering & Workbook Setup

Analyzed MedRecPro's Application Insights Failures blade which was showing 261+ failed requests, virtually all from automated vulnerability scanners. Built a Kusto (KQL) regex filter to exclude bot noise and surface only real application failures.

**Problem:** Scanner bots were probing for AWS credentials, Stripe keys, Terraform state, SharePoint exploits, WordPress paths, config files (appsettings.json, composer.json, parameters.yml), and framework artifacts (Next.js, Nuxt, Vercel, Netlify). This drowned out real failures like SqlExceptions and Azure blob dependency errors. The "double-URL" pattern (`/Home/https:/medrec.pro/...`) was confirmed as bot behavior (single-slash mangling), not an application routing bug.

**Solution — Iterative KQL regex filter:** Built a `where not(tolower(url) matches regex ...)` filter across three iterations, adding patterns as new noise surfaced in each export. Resolved RE2 regex engine compatibility issues: `(?i)` flag not supported (used `tolower()` instead), lookaheads `(?!/)` not supported (replaced with `$` anchor), character class escaping `[_/\-]` required hyphen-first positioning `[-_/]`.

**Workbook integration:** Added the filtered query as a new tile in the Application Insights "Failure Analysis" Workbook, wired to existing `TimeRange` and `Apps` parameters. Updated the built-in Request Details table and all chart sections (Failed Operations, All Operations, Top Failure Codes, Top Exceptions) to use the same noise filter. Result: 261 failures reduced to 2 real failures with 99.32% success rate.

**Evaluated but deferred:** Azure Front Door + WAF (~$35/mo) would block scanner traffic at the edge. Deferred until production traffic or acquisition demo readiness.

**Still to address:** SPL image .jpg 404s (spl.js loading images not captured during import — filtered for now, fix in spl.js later), CSS cache-busting hash mismatches (site.css, chat.css — low priority), SqlExceptions (4) and Azure blob failures (12) visible now that noise is cleared.

---

### 2026-02-27 11:21 PM EST — OrangeBookController: Patent Expiration Discovery Endpoint

Created a new `OrangeBookController` with a `GET /api/OrangeBook/expiring` endpoint for discovering NDA patents expiring within a configurable time horizon. The endpoint calls `SearchOrangeBookPatentsAsync` and returns JSON with both structured patent data and a pre-rendered markdown table.

**DRY refactor:** Promoted `validatePagingParameters`, `addPaginationHeaders`, and `DefaultPageNumber`/`DefaultPageSize` from `LabelController` (private) to `ApiControllerBase` (protected). Removed the duplicates from `LabelController` — all 26+ existing call sites continue working via inheritance. This enables any future controller to reuse pagination logic without duplication.

**Pediatric deduplication:** When both a base patent row and its `*PED` companion appear in results, the base row is filtered out. Only the `*PED` row (carrying the extended pediatric exclusivity expiration date) is retained, marked with a warning emoji in the markdown table.

**Markdown table:** Columns are Type (always NDA), Application#, Prod#, Trade Name (with lowercase ingredient in italics), Strength, Patent#, and Expires. When a DocumentGUID cross-reference exists, Trade Name becomes a markdown link to the original FDA label. Footer legend explains the pediatric warning emoji.

**Files created:** `Controllers/OrangeBookController.cs`. **Files modified:** `Controllers/ApiControllerBase.cs` (pagination promotion), `Controllers/LabelController.cs` (removed private duplicates), `Models/LabelViewDto.cs` (added `OrangeBookPatentExpirationResponseDto`). Build clean, 79 Orange Book tests pass.

---

### 2026-02-27 11:44 AM EST — OrangeBookController: Trade Name & Ingredient Filters

Added optional `tradeName` and `ingredient` query parameters to `GET /api/OrangeBook/expiring` so users can ask questions like "when will there be a generic Ozempic?" Both use partial matching (PartialMatchAny) — "Ozem" matches "Ozempic", "semaglut" matches "semaglutide". Parameters are passed through to `SearchOrangeBookPatentsAsync` (which already supported them) and to the updated `countExpiringPatentsAsync` (which now applies `EF.Functions.Like` with `%term%` wrapping for accurate total counts).

Made `expiringInMonths` optional (`int?`). When omitted with a `tradeName` or `ingredient`, the date range scopes from today through all future patents using `MaxExpirationMonths` (2880 months / 240 years). This supports open-ended queries where the caller doesn't know the expiration timeframe. At least one search parameter (`expiringInMonths`, `tradeName`, or `ingredient`) is required — returns 400 if all are blank. Build clean, 342 tests pass.

---

### 2026-02-27 12:47 PM EST — Organize Orange Book Data Access Layer

Consolidated all Orange Book data access code into a dedicated partial class file `DtoLabelAccess-OrangeBook.cs`. Previously the code was scattered across three files: `DtoLabelAccess.cs` (SearchOrangeBookPatentsAsync), `DtoLabelAccess-Views.cs` (buildOrangeBookPatentDtos), and `OrangeBookController.cs` (countExpiringPatentsAsync as a private method with direct DB queries).

**Key changes:**
- **Created** `DataAccess/DtoLabelAccess-OrangeBook.cs` — new partial class consolidating all Orange Book queries
- **Moved** `SearchOrangeBookPatentsAsync` from `DtoLabelAccess.cs` (removed ~193-line `#region Orange Book Patent Navigation`)
- **Moved** `buildOrangeBookPatentDtos` from `DtoLabelAccess-Views.cs` (removed ~43-line `#region Orange Book Patent Views`)
- **Extracted** `countExpiringPatentsAsync` from `OrangeBookController.cs` → renamed to `CountExpiringPatentsAsync` (public static), now takes `ApplicationDbContext db` and `int maxExpirationMonths` parameters instead of relying on controller instance fields
- **Fixed** CS1574 broken cref: `EntitySearchHelper.FilterBySearchTerms{T}` → `SearchFilterExtensions.FilterBySearchTerms{T}` (the class in `EntitySearchHelper.cs` is actually named `SearchFilterExtensions`)
- **Updated** controller call site to use `DtoLabelAccess.CountExpiringPatentsAsync(...)`
- **Added** 9 new tests for `CountExpiringPatentsAsync`: empty DB, no filters, date range filtering, expired patents exclusion, null fallback to maxExpirationMonths, tradeName partial match, ingredient partial match, combined AND logic, non-matching filter

Build: 0 errors. Tests: 21 Orange Book tests pass (12 existing + 9 new).

---

### 2026-02-27 1:28 PM EST — Add search_expiring_patents MCP Tool

Added `search_expiring_patents` MCP tool to `MedRecProMCP/Tools/DrugLabelTools.cs`, wrapping the `GET /api/OrangeBook/expiring` endpoint. Enables AI assistants to answer patent expiration and generic drug availability questions ("When will generic Ozempic be available?", "What patents expire in 6 months?").

**Tool design:**
- Parameters: `tradeName` (brand, partial match), `ingredient` (generic, partial match), `expiringInMonths` (nullable int for open-ended search), `pageNumber`, `pageSize`
- Returns raw API response containing structured Patents list, pre-rendered Markdown table (with clickable FDA label links where available), TotalCount, and TotalPages
- Description documents a fallback strategy for brand/generic name ambiguity — LLM retries with `ingredient` if `tradeName` returns empty, and vice versa
- `expiringInMonths` is nullable (`int?`) so omitting it with a tradeName/ingredient enables open-ended future patent search

**Class-level updates:** Added workflow box, tool selection guide entry, and common scenarios for the new tool to the class `<remarks>` documentation.

**README updates:** Updated tool count (5→6), added `search_expiring_patents` to the Tool Safety Annotations table, updated DrugLabelTools.cs description in project structure.

Build: 0 errors, 0 warnings.

---

### 2026-02-27 3:21 PM EST — Add Patent Expiration Tool to Getting Started Documentation

Updated `MedRecProMCP/Templates/McpGettingStarted.html` to document the `search_expiring_patents` MCP tool:

- **Intro paragraph:** Added mention of Orange Book patent expiration search capability
- **Feature grid:** Added "Patent & Generic Availability" tile (7th feature item)
- **Example 5:** New example card — "What drug patents are expiring in the next month" with screenshot (`MCP-Patent-Expiration.PNG`). Swapped with the authentication example so all drug-related examples are grouped together (patent = Example 5, auth = Example 6)
- **Tools table:** Updated count from "five" to "six" tools; added `search_expiring_patents` row to Drug Label Tools table

Also updated `MedRecProMCP/Tools/DrugLabelTools.cs` — strengthened the `[Description]` attribute on `search_expiring_patents` to better emphasize that trade names appear as clickable FDA label links when a cross-referenced SPL label exists, with instructions to preserve those links in output.

Build: 0 errors, 0 warnings.

---

### 2026-03-02 10:12 AM EST — TarpitMiddleware Phase 2: Endpoint Abuse Detection

Extended the existing tarpit system (both MedRecPro and MedRecProStatic) to detect and throttle rate-based abuse on configurable success-returning endpoints. Bots were hammering endpoints returning 200 OK (e.g., `GET /api/` with 2,875 hits, `GET /Home/Index` with 664 hits), so the tarpit now monitors those paths too.

**Design — Second Dictionary, Shared Lifecycle:**
- New `ConcurrentDictionary<string, EndpointAbuseEntry>` keyed by `"{IP}|{normalizedPath}"` — separate from the existing 404 tracker
- Tumbling window rate detection: hits per configurable time window, counter resets when window expires
- Combined `MaxTrackedIps` cap across both dictionaries with merged eviction (oldest from either dictionary)
- Same exponential backoff formula with its own threshold (`EndpointRateThreshold`)

**Key decision:** A 200 on a monitored endpoint does NOT reset the 404 counter — a bot hammering `/api/` is not demonstrating legitimate behavior.

**Files modified (11):**
- `TarpitSettings.cs` — 3 new properties: `MonitoredEndpoints`, `EndpointRateThreshold`, `EndpointWindowSeconds`
- `TarpitService.cs` — `EndpointAbuseEntry` record struct, `_endpointTracker` dictionary, 3 new public methods (`RecordEndpointHit`, `GetEndpointHitCount`, `CalculateEndpointDelay`), modified cleanup/eviction/dispose to sweep both dictionaries
- `TarpitMiddleware.cs` — `getMatchedEndpoint()` helper, restructured `InvokeAsync` success branch for monitored vs non-monitored paths
- `appsettings.json` (both projects) — added 3 new TarpitSettings fields
- `SettingsController.cs` — 3 new fields in `GetFeatures()`
- `TarpitServiceTests.cs` — 12 new endpoint abuse tests
- `TarpitMiddlewareTests.cs` — 8 new middleware endpoint tests

**Verification:** Both projects build with 0 errors. All 46 tarpit tests pass (26 original + 20 new).

---

### 2026-03-02 1:15 PM EST — MCP Endpoint Health Check Workflow + Integrity Protection

Created `.github/workflows/mcp-health-check.yml` — a GitHub Actions workflow that monitors the MedRecPro MCP server endpoints hourly on weekdays (8 AM – 7 PM EST).

**Health checks (4 steps):**
1. MCP server liveness — `GET /mcp/health`, validates `{"status":"running"}`
2. `search_drug_labels` — Anthropic API call with `productNameSearch='aspirin'`
3. `export_drug_label_markdown` — Anthropic API call with `productNameSearch='aspirin'` (Step 1)
4. `search_expiring_patents` — Anthropic API call with `tradeName='Lipitor'`

Uses `claude-haiku-4-5-20251001` via the Anthropic Messages API with `mcp_servers` parameter. User tool endpoints (`get_my_profile`, `get_my_activity`, `get_my_activity_by_date_range`) are excluded.

**Security hardening:**
- `permissions: {}` — zero workflow permissions (only outbound HTTP needed)
- `actions/checkout` pinned to commit SHA (`@11bd71901bbe5b1630ceea73d27597364c9af683`, v4.2.2)
- SHA-256 integrity check — workflow computes its own hash at runtime, compares against `WORKFLOW_INTEGRITY_HASH` GitHub secret; mismatch exits before `ANTHROPIC_API_KEY` is ever exposed to any step
- All API-key steps gated on `steps.integrity.outcome == 'success'`

**Required GitHub secrets:** `ANTHROPIC_API_KEY` (existing), `WORKFLOW_INTEGRITY_HASH` (new — SHA-256 of the workflow file, must be updated after any legitimate edit).

---

### 2026-03-02 2:57 PM EST — MCP Health Check: Cloudflare Worker Proxy & Direct REST Migration

Resolved multiple issues with the MCP health-check GitHub Actions workflow and migrated from the Anthropic API approach to direct REST API calls proxied through a Cloudflare Worker.

**Problem chain:**
1. **Anthropic API auth failure** — MCP transport at `/mcp` requires OAuth 2.1; Anthropic's `mcp_servers` parameter cannot complete a headless OAuth flow, returning "Authentication error while communicating with MCP server"
2. **Bot Fight Mode (free plan)** — Cannot be bypassed or skipped via WAF rules; blocks all GitHub Actions runner traffic (curl) with JavaScript challenges
3. **JSON array response** — API returns arrays, not objects; `jq -r '.error // empty'` crashed with "Cannot index array with string"

**Solutions applied:**
- **Dropped Anthropic API entirely** — Switched all 3 tool checks to direct REST API calls against the public endpoints (`/api/Label/...`, `/api/OrangeBook/...`). Zero cost per run, no auth needed.
- **Created Cloudflare Worker proxy** (`workers/health-proxy/`) — GitHub Actions hits `*.workers.dev` (not subject to medrecpro.com's Bot Fight Mode), Worker validates `X-Health-Token` secret, proxies to origin through Cloudflare's internal network. Path-whitelisted to 4 endpoints only.
- **Fixed jq crash** — Changed error check to `jq -e 'type == "object" and has("error")'` so JSON arrays pass through safely.
- **Added `.wrangler/` to `.gitignore`** — Wrangler cache directory should not be committed.

**Files modified/created:**
- `.github/workflows/mcp-health-check.yml` — All requests now route through `PROXY_BASE_URL` (Worker URL stored as `HEALTH_PROXY_URL` secret)
- `workers/health-proxy/src/index.js` — Worker with token validation, method restriction (GET/HEAD), path whitelist, bot-challenge detection, 10s timeout
- `workers/health-proxy/wrangler.toml` — Worker config with `ORIGIN_URL` var and `HEALTH_CHECK_TOKEN` secret
- `.gitignore` — Added `.wrangler/`

**Removed secrets:** `ANTHROPIC_API_KEY` (no longer needed for this workflow).
**New secrets:** `HEALTH_PROXY_URL` (Worker URL on workers.dev).
**Retained secrets:** `CF_HEALTH_CHECK_TOKEN`, `WORKFLOW_INTEGRITY_HASH`.

---

### 2026-03-03 12:15 PM EST — CodeQL Analysis & HTTP/Cookie Security Hardening

Added CodeQL analysis workflow and hardened HTTP/cookie security across the codebase.

---

### 2026-03-04 — Fix Database Keep-Alive Cascade Failure

Investigated and fixed a cascade failure in `DatabaseKeepAliveService` where a single transient ping failure caused the Azure SQL Serverless database to remain paused indefinitely. The 55-minute ping interval meant one failure led to 110+ minutes of inactivity (well past the 60-minute auto-pause threshold), and the default 15-second connect timeout was too short for the 30-60 second cold resume.

**Root cause chain:** Single transient failure → 55-min wait → DB idle 110 min → auto-paused → 15s connect timeout too short for resume → permanent cascade. Logs confirmed: last successful ping at 9:20 AM EST, 8 consecutive failures through 5:35 PM EST.

**5 fixes applied (4 implemented, 1 deferred):**

1. **EF Core transient retry** (`Program.cs`) — Added `EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 30s)` and `CommandTimeout(60)` to protect ALL application DB operations from transient failures and cold-start timeouts.

2. **Keep-alive retry logic** (`DatabaseKeepAliveService.cs`) — Added configurable retry with escalating delays (10s → 30s → 60s). Extended `SqlConnection` connect timeout to 90 seconds via `SqlConnectionStringBuilder`. Only increments `_consecutiveFailures` after all retries exhausted.

3. **Interval & business hours** (`appsettings.json`) — Reduced ping interval from 55 → 14 minutes (3 consecutive total failures = 42 min, still under 60-min auto-pause). Extended business hours end from 17 → 20 (covers health check window through 7 PM EST).

4. **Health check curl timeouts** (`mcp-health-check.yml`) — Changed all four `--max-time` from 30 → 120 seconds. New workflow integrity hash: `093b65930963d532fd607f0844be5476f267738e67fd34c30f560832d4d0e35b`.

5. **Deferred:** Worker proxy timeout increase (10s → 90s) — user decided the other fixes make this moot.

**README updated** with new keep-alive parameters and EF Core resilience info.

**Tests created:** 16 unit tests in `DatabaseKeepAliveServiceTests.cs` covering constructor validation, config loading with retry settings, config validation/fallback, service lifecycle. All 16 pass.

**Pending:** Update `WORKFLOW_INTEGRITY_HASH` GitHub secret with new hash.

---

### 2026-03-04 1:54 PM EST — SPL Drug Label Broken Image Handling

Implemented a three-layer solution to gracefully handle missing/broken images in SPL drug label HTML rendering. Label imports do not include image files, but the XML references must remain for validation. Previously, the browser showed broken image icons.

**Approach:** Instead of broken icons, broken images are replaced with styled text placeholders showing the image description (if available from alt text) and a "Text-only label, image not available" notice. Captions remain visible for context.

**Three layers:**
1. **XSLT `onerror` handlers** (primary) — Added to all 5 `<img>` tags across 3 template locations in `spl-common.xsl`. Creates placeholder elements immediately on load failure, zero race condition.
2. **JS `hideBrokenImages()` fallback** (secondary) — Added to `spl.js`, runs on page load after 200ms delay. Catches any images missed by onerror using `img.complete && img.naturalWidth === 0`.
3. **CSS `data-broken` attribute rules** (tertiary) — Added to `spl.css` with `!important` as a safety net.

**Encoding fix:** Initial implementation used em dash (`—`) as separator in placeholder text, which was garbled to `â€"` during XSLT→HTML processing. Replaced with ASCII hyphen ` - `.

**Files modified:**
- `Views/Stylesheets/spl-common.xsl` — `onerror` attributes on all 5 `<img>` tags (inline lines 2813/2818, block lines 2841/2846, product line 3964)
- `Views/Stylesheets/spl.js` — New `hideBrokenImages()` function + integrated into `load` event handler
- `Views/Stylesheets/spl.css` — `img[data-broken]` hiding rule + `.spl-image-placeholder` block/inline styles

---

### 2026-03-04 3:07 PM EST — Add Orange Book Patent Search Skill to AI Skills Interface

Added the Orange Book patent search capability to MedRecPro's AI skills architecture, enabling the AI to discover and route queries about patent expiration dates and generic drug availability to the existing `GET /api/OrangeBook/expiring` endpoint.

**Assessment:** Implemented entirely through the skills layer — no new controller or data access code needed since `OrangeBookController.cs` already had the endpoint.

**Files created:**
- `Skills/interfaces/api/orange-book-patents.md` — Interface document with API endpoint spec, parameter guide, response structure, fallback strategy (brand↔generic retry), and result formatting requirements

**Files modified:**
- `Skills/skills.md` — Added "Orange Book Patents > Patent Expiration Search" capability contract and integration note for patent + label content combination
- `Skills/selectors.md` — Added decision tree branch, keyword section (patent/generic/time horizon keywords), priority rule, 3 skill combination entries, interface reference, and help description
- `Service/ClaudeSkillService.cs` — Added `orangeBookPatents` to all 3 internal dictionaries (`_skillConfigKeys`, `_interfaceDocPaths`, `mapAiSkillNamesToInternal`)
- `appsettings.json` — Added `Skill-OrangeBookPatents` config key

**Tests added:**
- `MedRecProTest/ClaudeSkillServiceTests.cs` — 21 MSTest tests covering Orange Book skill registration, cross-validation of all 3 dictionaries, AI-to-internal name mapping via reflection, interface document file existence/content, and regression guards for all 14 existing skills. All 21 tests pass.

---

### 2026-03-05 7:45 AM EST — Tarpit Middleware: Cookie-Based Client Tracking

Fixed a production issue where the tarpit middleware failed to throttle rapidly repeated API calls when client IPs were masked or rotated (Safari iCloud Private Relay, upstream agent IP rotation, Cloudflare inconsistencies). Each request appeared as a new client with hit count = 1, so the tarpit threshold was never reached.

**Root cause:** Client identification was purely IP-based via `getClientIp()` (CF-Connecting-IP → X-Forwarded-For → RemoteIpAddress). When IPs rotate, every request starts fresh.

**Solution:** Added a hybrid cookie + IP identification scheme. A `__tp` tracking cookie (HttpOnly, Secure, SameSite=Strict) is set on every response. On subsequent requests, the cookie value serves as the stable client identifier regardless of IP changes. First request (no cookie yet) falls back to IP. Bots that reject cookies get pure IP-based tracking — identical to previous behavior. The `TarpitService` itself is unchanged since it already operates on opaque string keys.

**Key design decisions:**
- Cookie value is a 32-char hex GUID, validated via compiled regex to reject spoofed/malformed values
- Cookie MaxAge aligned with `StaleEntryTimeoutMinutes` (renewed on every request)
- Client identity resolved BEFORE `await _next()` so the cookie is set before downstream middleware writes the response body
- New `EnableClientTracking` setting (default: true) provides a kill switch
- Log messages now include both `{ClientId}` and `{IP}` for diagnostic correlation

**Files modified (both MedRecPro and MedRecProStatic):**
- `Models/TarpitSettings.cs` — Added `EnableClientTracking` property (bool, default: true)
- `Middleware/TarpitMiddleware.cs` — Added `resolveClientId()`, `appendTrackingCookie()`, restructured `InvokeAsync()` into 3 phases (pre-pipeline identity, pipeline execution, post-pipeline tarpit evaluation)
- `appsettings.json` — Added `"EnableClientTracking": true` to TarpitSettings

**Tests added:**
- `MedRecProTest/TarpitMiddlewareTests.cs` — 7 new tests: NoCookie_UsesIpAndSetsCookie, ValidCookiePresent_UsesCookieNotIp, CookiePersistsAcrossIpChanges_AccumulatesHits, InvalidCookieFormat_FallsBackToIp, ClientTrackingDisabled_UsesIpOnly, CookieWithMonitoredEndpoint_TracksByCookie, CookieWithResetOnSuccess_ResetsByCookieKey. All 53 tarpit tests pass (26 existing middleware + 27 service + 7 new cookie = 0 regressions).

---

### 2026-03-05 — Tarpit Middleware: Endpoint Rate Threshold Tuning

Config-only change driven by Azure App Insights telemetry analysis (last 24 hours). Production data showed bot abuse patterns: `GET /api/` at 3,020 hits from 4 users (5.27ms avg), `GET Home/Index` at 653 hits from 3 users (11.3ms avg). The 404 tarpit was working correctly (probe paths hitting 30s MaxDelayMs cap), but the endpoint rate monitoring was too lenient — bots could reset the 60-second window and get another 20 free hits, allowing ~28,800 free hits/day per identity.

**Changes (both MedRecPro and MedRecProStatic `appsettings.json`):**
- `EndpointRateThreshold`: 20 → 10 (halved free hits before delay kicks in)
- `EndpointWindowSeconds`: 60 → 300 (5-minute window prevents window-reset abuse)

**Impact:** Free throughput reduced ~90% — from 28,800 to 2,880 free hits/day per identity. A bot hitting `/api/` at 1 req/sec triggers delay after 10 seconds, then faces exponential backoff for the remaining 290 seconds of the window. Legitimate users would need 10+ requests to the same monitored endpoint within 5 minutes to trigger — well outside normal browsing patterns.

**Decision: `/Home/Index` remains monitored.** 653 hits from 3 users (~218/user/day) on a marketing landing page is not legitimate behavior. The `Task.Delay` after `_next()` produces the intended UX: browser shows a loading spinner while the response is held server-side.

No code changes required — the middleware and service already support the new values. All 53 tarpit tests pass (tests use their own settings objects, not appsettings.json).

---

### 2026-03-05 12:38 PM EST — Tarpit Middleware: Pre-Pipeline Delay Architecture Fix

Fixed an architectural issue where the tarpit delay ran AFTER `await _next(context)`, meaning the response body was already flushed to the browser before the delay started. Abusive clients (including manual F5 testing) could bypass the delay entirely: the response arrived instantly, `Task.Delay` held the connection open, and pressing F5 or canceling the request triggered `OperationCanceledException` which was swallowed — the client never experienced any slowdown.

**Root cause:** The delay was post-pipeline. By the time `Task.Delay(delayMs, context.RequestAborted)` executed, the controller had already written the response body to Kestrel's output buffer. The browser received content immediately; only the TCP connection lingered.

**Fix:** Moved the delay to BEFORE `await _next(context)`. The middleware now evaluates the client's PRIOR abuse history (both 404 hits and endpoint rate abuse) and applies a delay pre-pipeline. The browser receives nothing until the delay completes. Pressing F5 cancels the delay AND the response — the client gets a blank page/loading spinner and must wait through the delay on the next attempt too.

**Restructured `InvokeAsync` into slim orchestrator calling two new private methods:**
- `applyPrePipelineDelay(context, clientId, clientIp)` — checks existing 404 + endpoint hit counts, takes MAX of both calculated delays, applies `Task.Delay` with `RequestAborted` cancellation
- `recordPostPipelineHits(context, ref clientId, ref clientIp)` — records hits based on actual response status code (404 → record hit, monitored 200 → record endpoint hit, non-monitored 200 + ResetOnSuccess → reset counter)

**Design note:** The delay is based on hits from PRIOR requests, so the request that first crosses the threshold is recorded but not delayed — the NEXT request sees the threshold exceeded and is delayed. The difference is one request late (e.g., delay starts at hit 11 instead of hit 10 with threshold=10).

**`??=` null guard analysis:** Also confirmed that the `clientId ??= getClientIp(context)` lines in `recordPostPipelineHits` are NOT a bug — the `??=` operator only fires when `clientId` is null (Phase 1 exception path). When Phase 1 succeeds, `clientId` already holds the cookie value or IP, and `??=` is a no-op.

**Files modified (both MedRecPro and MedRecProStatic):**
- `Middleware/TarpitMiddleware.cs` — Extracted `applyPrePipelineDelay()` and `recordPostPipelineHits()`, restructured `InvokeAsync` as slim orchestrator

**Tests:** All 53 tarpit tests pass without modification. Existing threshold-crossing tests validate service state and delay formula math (not timing), so they're unaffected by the pre/post-pipeline change.

---

### 2026-03-05 — Fix Tarpit PathBase Mismatch (Azure Virtual Application)

**Problem:** After deploying the pre-pipeline delay fix, production validation showed tarpit behavior works on `https://www.medrecpro.com/home/index` (MedRecProStatic) but NOT on `https://www.medrecpro.com/api/` (MedRecPro). Rapid F5 on `/api/` never triggers any slowdown.

**Root cause — triple failure from Azure Virtual Application path stripping:**

MedRecPro is deployed as an IIS Virtual Application under `/api`. The ASP.NET Core IIS integration module strips the prefix: `context.Request.PathBase` = `/api`, `context.Request.Path` = `/`. Evidence: controller routes use `[Route("[controller]")]` in production (no `/api`), Swagger config comment states *"Do not prefix with '/api' because Azure App Service hosts under '/api'"*.

1. **Endpoint monitoring never matched:** `getMatchedEndpoint` was called with `context.Request.Path` (which is `/`). `"/".StartsWith("/api/")` → false → endpoint abuse tracking skipped.
2. **404 tracking never triggered:** The `app.MapGet("/", ...)` root endpoint returns HTTP 200, so no 404 hit was recorded.
3. **Counter actively reset:** Since `/` didn't match any monitored endpoint and `ResetOnSuccess=true`, every 200 response wiped the client's abuse history via `_tarpitService.ResetClient()`.

MedRecProStatic worked because it's deployed at the root (no virtual application), so `context.Request.Path` = `/Home/Index` matched the monitored endpoint directly.

**Fix:** Reconstruct the full public path using `(context.Request.PathBase + context.Request.Path).Value` when calling `getMatchedEndpoint`. This is idiomatic ASP.NET Core — `PathBase` exists specifically for virtual application scenarios. Applied the 2-line change in both `applyPrePipelineDelay` and `recordPostPipelineHits`, plus updated log messages to show the full path.

**Files modified (both MedRecPro and MedRecProStatic):**
- `Middleware/TarpitMiddleware.cs` — `applyPrePipelineDelay`: use `PathBase + Path` for endpoint matching and logging. `recordPostPipelineHits`: same reconstruction for endpoint matching and 404 logging.

**New tests (3 added to TarpitMiddlewareTests.cs):**
- `InvokeAsync_WithPathBase_MatchesMonitoredEndpoint` — PathBase="/api", Path="/" → matches "/api/" in MonitoredEndpoints
- `InvokeAsync_WithoutPathBase_MatchesOnPathAlone` — Empty PathBase, Path="/Home/Index" → matches directly (MedRecProStatic scenario)
- `InvokeAsync_WithPathBase_AppliesEndpointDelayFromPriorHits` — Verifies delay calculation uses reconstructed path

**Tests:** All 56 tarpit tests pass (53 existing + 3 new). Both solutions build with 0 errors.

---

### 2026-03-06 11:30 AM EST — Add Pharmacologic Class Search MCP Tool

Added `search_by_pharmacologic_class` MCP tool to `MedRecProMCP/Tools/DrugLabelTools.cs`. This tool exposes the existing `GET /api/Label/pharmacologic-class/search` endpoint for AI-powered drug class discovery — translating natural language terms (e.g., "beta blockers", "SSRIs", "statins") to formal FDA pharmacologic class names and returning all matching products grouped by class with clickable FDA label links.

**Key implementation details:**
- Tool calls the `query` parameter for AI-powered terminology matching (recommended) or `classNameSearch` for direct partial matching (fallback)
- Rewrites relative `labelLinks` from the API response to absolute URLs using the same base URL pattern as `ExportDrugLabelMarkdown`
- Comprehensive `[Description]` attribute with boxed tool-selection rules, 12+ sample trigger questions, terminology mapping table, fallback strategy, and mandatory label link presentation requirements
- Updated class-level XML docs: added ASCII workflow box, tool selection guide entry, and common scenario examples
- Build: 0 errors, 0 warnings

---

### 2026-03-12 3:13 PM EST — AI-Enhanced Search by Indication

Implemented the full three-stage AI-enhanced indication search pipeline for MedRecPro. This feature allows users to search for drugs by medical indication/condition (e.g., "diabetes", "high blood pressure") using a combination of keyword pre-filtering and Claude AI semantic matching.

**Architecture — Three-stage pipeline:**
1. **C# keyword pre-filter** — Tokenizes user query, expands ~40 condition synonym mappings, scores reference entries, caps at 50 candidates
2. **Claude AI semantic matching** — Sends filtered candidates to Claude for semantic relevance scoring with confidence levels (high/medium/low)
3. **Claude AI validation** — Fetches actual FDA Indications & Usage sections (LOINC 34067-9) and validates matches against real label text

**Files created:**
- `MedRecPro/Skills/prompts/indication-matching-prompt.md` — Stage 2 prompt template with `{{USER_QUERY}}` and `{{CANDIDATE_LIST}}` placeholders
- `MedRecPro/Skills/prompts/indication-validation-prompt.md` — Stage 3 prompt template with `{{USER_QUERY}}` and `{{VALIDATION_ENTRIES}}` placeholders
- `MedRecProTest/ClaudeSearchServiceIndicationTests.cs` — 25 unit tests covering parsing, pre-filter, AI response parsing, validation, orchestrator edge cases, and DTOs

**Files modified:**
- `MedRecPro/Service/ClaudeSearchService.cs` — Added 8 DTOs (IndicationReferenceEntry, IndicationMatch, IndicationMatchResult, IndicationProductInfo, IndicationValidationEntry, ValidatedIndication, IndicationValidationResult, IndicationSearchResult), 3 interface methods, constants, and 15+ implementation methods including the orchestrator `SearchByIndicationAsync()`
- `MedRecPro/Controllers/LabelController.cs` — Added `GET indication/search` endpoint with query and maxProductsPerIndication parameters
- `MedRecPro/appsettings.json` — Added `Prompt-IndicationMatching` and `Prompt-IndicationValidation` config keys
- `MedRecProMCP/Tools/DrugLabelTools.cs` — Added `search_by_indication` MCP tool with comprehensive description, updated Tool Selection Guide

**Key design decisions:**
- UNII validation: every AI-returned UNII must exist in the candidate list to prevent hallucinated identifiers
- Graceful degradation: if Stage 3 validation fails, all Stage 2 matches are kept unfiltered
- Reference data cached for 8 hours via `PerformanceHelper`
- Synonym expansion covers ~40 common condition mappings (e.g., "high blood pressure" → "hypertension")
- Build: 0 errors across all projects; 25/25 tests passing

---

### 2026-03-12 3:24 PM EST — Refactor SearchByIndicationAsync() into Orchestrator Pattern

Refactored `SearchByIndicationAsync()` from a ~200-line monolithic method into a concise orchestrator backed by 3 new private methods, matching the pattern established by `SearchByUserQueryAsync()` (pharmacologic class search).

**Extracted methods:**
- `lookupProductsForMatchedIndicationsAsync()` — product lookup per matched UNII, indication summary enrichment, validation entry building
- `applyValidationFilterAsync()` — Stage 3 validation against actual FDA label text, filtering rejected UNIIs, attaching validation metadata
- `buildLabelLinks()` — label link dictionary population from product DocumentGuids

**File modified:** `MedRecPro/Service/ClaudeSearchService.cs`

The orchestrator now reads as a clean step sequence: input validation → Stage 1 pre-filter → Stage 2 AI matching → product lookup → Stage 3 validation → label links → finalize. Pure refactor with no behavior changes. Build: 0 errors; 25/25 tests passing.

---

### 2026-03-13 9:46 AM EST — Extract ClaudeSearchService models to separate file

Separated 12 model classes from `Service/ClaudeSearchService.cs` into a dedicated `Models/ClaudeSearchModels.cs` file to improve separation of concerns. Extracted: `PharmacologicClassMatchResult`, `PharmacologicClassSearchResult`, `PharmacologicClassProductInfo`, `ProductExtractionResult`, `IndicationReferenceEntry`, `IndicationMatch`, `IndicationMatchResult`, `IndicationProductInfo`, `IndicationValidationEntry`, `ValidatedIndication`, `IndicationValidationResult`, `IndicationSearchResult`. Models moved from `MedRecPro.Service` namespace to `MedRecPro.Models`. No behavior changes — existing `using MedRecPro.Models;` imports in both the service and controller already resolved the types. Build: 0 errors.

---

### 2026-03-13 10:07 AM EST — Reorganize ClaudeSearchService by intent with public-then-private ordering

Reorganized `Service/ClaudeSearchService.cs` (~2521 lines, ~30 methods) into intent-based regions with public-then-private method ordering. New structure: `#region pharmacologic class search` (4 public, 11 private), `#region indication search` (3 public, 15 private), `#region shared private methods` (3 utility methods: `loadPromptTemplate`, `readPromptFileByPath`, `extractJsonFromResponse`). Each intent region contains nested `#region public methods` and `#region private methods` sub-regions. Used Node.js scripting to precisely extract and reassemble method blocks by mapped line ranges. Build: 0 errors.

---

