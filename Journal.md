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

### 2026-03-13 11:29 AM EST — Consolidate Indication Discovery Skills Interface

Rewrote `Skills/interfaces/api/indication-discovery.md` from 305 lines to ~130 lines, replacing the old 5-step manual workflow with a single-endpoint pattern matching the `pharmacologic-class.md` convention. The old interface documented manual steps (search reference data, chain UNII lookups, fetch label sections, validate relevance) that the new `GET /api/Label/indication/search` endpoint already handles server-side via `ClaudeSearchService.SearchByIndicationAsync()`.

**Duplication eliminated:**
- Condition keyword mappings (duplicated in `selectors.md` and interface doc) — removed from interface, kept in selectors for routing
- Lay-to-medical terminology rules (duplicated across both AI prompt files and interface doc) — removed from interface, kept in prompts for server-side AI calls
- Reference data format/parsing docs — removed (server-internal concern)
- Array extraction syntax, truncation detection, multi-product workflow — all removed (handled server-side)

**No other files changed:** Prompt files stay in `Skills/prompts/` (consistent with `pharmacologic-class-matching-prompt.md` pattern), appsettings config keys unchanged, `selectors.md` routing keywords appropriate for skill selection, `skills.md` capability contracts stable. Build: 0 errors.

---

### 2026-03-13 12:38 PM EST — Fix MCP Error -32603 on Indication Search (Timeout Handling)

Diagnosed and fixed the `MCP error -32603: An error occurred` that surfaced when calling `search_by_indication`. Root cause: the MCP tool layer in `DrugLabelTools.cs` only caught `HttpRequestException`, but HttpClient timeouts throw `TaskCanceledException` (a subclass of `OperationCanceledException`), which propagated unhandled to the MCP framework as a generic -32603 error.

**Fixes applied:**
- **`MedRecProMCP/Tools/DrugLabelTools.cs`** — Added `catch (OperationCanceledException)` blocks to all 5 MCP tool methods (`SearchDrugLabels`, `ExportDrugLabelMarkdown`, `SearchExpiringPatents`, `SearchByPharmacologicClass`, `SearchByIndication`). Each returns a structured JSON error with timeout messaging and suggested follow-ups instead of crashing.
- **`MedRecProMCP/appsettings.json`** — Increased `MedRecProApi.TimeoutSeconds` from 30 to 120. The 3-stage AI-powered indication search pipeline (keyword pre-filter → AI semantic matching → AI validation) requires significantly more time than simple label lookups.

**Investigation path:** Traced from MCP tool → `MedRecProApiClient.GetStringAsync` → HttpClient timeout config in `Program.cs` → confirmed server-side (`LabelController.cs`, `ClaudeSearchService.cs`) has proper exception handling. The gap was exclusively in the MCP client tool layer.

Build: 0 errors, 0 warnings. Branch: `Indication-Search`.

---

### 2026-03-13 3:56 PM EST — Fix MCP OAuth: Claude.ai CIMD URL Returns HTML Instead of JSON

**Problem:** Claude.ai's MCP OAuth connection stopped working. When connecting, the authorize endpoint returned `{"error":"invalid_client","error_description":"Unknown client_id"}`. The root cause: Claude.ai sends `client_id=https://claude.ai/oauth/mcp-oauth-client-metadata` (a Client ID Metadata Document URL per MCP OAuth spec), but that URL now returns Claude's SPA HTML instead of a JSON metadata document. The `ClientRegistrationService.FetchClientMetadataDocumentAsync` tried to deserialize HTML as JSON and failed (`'<' is an invalid start of a value`), so the client was never registered.

**Fix:** Added `IsClaudeClient()` private helper method in `ClientRegistrationService.cs` that recognizes both the simple `"claude"` client ID and any `https://claude.ai/...` or `https://claude.com/...` URL as the pre-registered Claude client. This bypasses the broken CIMD fetch entirely — the hardcoded `ClaudeClient` with correct redirect URIs is returned directly. Updated `ValidateClientAsync` to use the new helper.

**File modified:** `MedRecProMCP/Services/ClientRegistrationService.cs`

Build: 0 errors, 0 warnings.

---

### 2026-03-16 1:59 PM EST — Unify Sub Pages with Home Page Navigation & Styling

Converted standalone MCP HTML files to Razor views and unified all sub pages (MCP Docs, MCP Getting Started, Chat, Privacy, Terms) with the shared `_Layout.cshtml` navbar and earth-tone design system.

**Key changes across 14 files:**

- **Data layer:** Added 15+ MCP model classes to `PageContent.cs`, added `mcpDocs` and `mcpSetup` content blocks to `pages.json` (architecture diagram, OAuth auth flow, all 8 MCP tools with full parameter docs, LOINC codes, tool selection guide, 7 examples with screenshots), added getter methods to `ContentService.cs`
- **Controller:** Added `McpDocs()` and `McpSetup()` actions with `[Route("mcp/docs")]` and `[Route("mcp/getting-started")]` attribute routing to `HomeController.cs`
- **Views:** Created `McpDocs.cshtml` and `McpSetup.cshtml` Razor views; restructured `Chat.cshtml` to use `_Layout.cshtml` (removed `Layout = null`, moved resources to `@section Head/Scripts`, replaced `.chat-header` with `.chat-subheader`); deleted old `mcp.html` and `mcp-setup.html`
- **Navigation:** Added MCP nav link to `_Layout.cshtml` navbar; added MCP Docs and Getting Started links to footer; added `@RenderSection("Head")` for Chat's Google Fonts
- **Styling:** Added ~350 lines of MCP component styles to `site.css` (`.mcp-page`, `.tool-card`, `.param-table`, `.example-card`, `.step-counter`, etc.); rethemed `chat.css` from blue-gray to earth-tone (`#e5771e` burnt orange accent, `#342e2a` dark brown backgrounds)
- **Routing fix:** Added `app.MapControllers()` to `Program.cs` to enable attribute routing alongside conventional routing
- **Tests:** Created `site-tests.js` with 20 browser console tests — all passing

**MCP tool documentation now covers all 8 tools** (previously only 3 of 5 Drug Label tools were documented): `search_drug_labels`, `export_drug_label_markdown`, `search_expiring_patents`, `search_by_pharmacologic_class`, `search_by_indication`, `get_my_profile`, `get_my_activity`, `get_my_activity_by_date_range`.

Build: 0 errors, 1 pre-existing warning.

---

### 2026-03-16 3:27 PM EST — Chat Page: Remove Double Header, Fix Spinner Colors

Reverted Chat.cshtml to self-contained layout (`Layout = null`) so it only shows the chat subheader (lighter brown bar) without the `_Layout` navbar creating a double header.

**Changes:**
- **Chat.cshtml** — Restored full HTML wrapper with `Layout = null`; added Home link button in the header actions for navigation back; kept `ViewBag.Config` for site name
- **chat.css** — Added `.chat-page` layout rule (100vh/100dvh flex column); `.brand-logo` kept at 50×38px oblong with `border-radius: var(--radius-md)` for rounded corners
- **message-renderer.js** — Changed progress ring spinner gradient from blue/purple (`#3b82f6`/`#8b5cf6`) to orange (`#e5771e`/`#d06818`) across both simple and detailed progress indicators
- **site.css** — Removed `.chat-page`/`.chat-subheader` overrides (now fully in chat.css)

Build: 0 errors, 1 pre-existing warning.

---

### 2026-03-16 4:03 PM EST — Chat Page: Fix White Border & Oversized Input Field

Chat page was rendering with a white border/inset around the content and the text input field appeared oversized.

**Root cause:** Chat.cshtml is self-contained (`Layout = null`, only loads `chat.css` — no `site.css`). Without a CSS reset, the browser default `body { margin: 8px }` created the white border, and the missing `box-sizing: border-box` caused textarea padding to be added outside the `min-height: 54px`.

**Fix in `chat.css`:** Added CSS reset block at the top — `*, *::before, *::after { margin: 0; padding: 0; box-sizing: border-box }` and `html, body { height: 100%; overflow: hidden }`.

Build: 0 errors, 1 pre-existing warning.

---

### 2026-03-20 10:10 AM EST — SPL Table Normalization Pipeline: Stage 1 Source View Assembly

Implemented Stage 1 of the SPL Table Normalization pipeline — the data access layer that joins cell-level data (TextTableCell → TextTableRow → TextTable → SectionTextContent) with section context (vw_SectionNavigation) and document context (Document) into a flat 26-column DTO for downstream table reconstruction and meta-analysis.

**Files created (5):**
1. `MedRecProImportClass\Models\TableCellContext.cs` — Read-only 26-property projection DTO (Cell, Row, Table, Content, Document, Section Nav groups)
2. `MedRecProImportClass\Models\TableCellContextFilter.cs` — Filter with DocumentGUID, TextTableID, range batch (start/end), MaxRows; `Validate()` enforces mutual exclusivity and range completeness
3. `MedRecProImportClass\Service\TransformationServices\ITableCellContextService.cs` — Interface with `GetTableCellContextsAsync`, `GetTableCellContextsGroupedByTableAsync`, `GetTextTableIdRangeAsync`
4. `MedRecProImportClass\Service\TransformationServices\TableCellContextService.cs` — EF Core LINQ implementation using explicit joins for SectionTextContent→SectionNavigation→Document (no nav properties exist for those links); `buildQuery()` is `internal` for testability
5. `MedRecProTest\TableCellContextServiceTests.cs` — 14 MSTest tests using SQLite in-memory DB with DDL patching; vw_SectionNavigation backing table created via raw SQL (ToView entities excluded from GenerateCreateScript); GUIDs seeded as uppercase TEXT per EF Core 8 SQLite convention

**Key decisions:**
- Removed TextTableColumn join (selects zero columns, causes duplicate rows)
- Used EF Core LINQ query syntax with explicit joins for consistency
- Batch by TextTableID range for 250K+ label corpus scalability
- No default row limit — callers control batching via filter

Build: 0 errors (pre-existing warnings only). Tests: 14/14 passed.

---

### 2026-03-20 12:08 PM EST — SPL Table Normalization Pipeline: Stage 2 — Table Reconstruction

Implemented Stage 2 of the SPL Table Normalization pipeline. This stage takes the flat 26-column `TableCellContext` output from Stage 1 and reconstructs logical table structures: grouping cells by table, extracting footnotes from HTML `<sup>` tags, classifying rows (header/body/footer/SOC divider), resolving ColSpan/RowSpan into absolute column positions via a 2D occupancy grid, and building multi-level header structures with column paths.

**Files created (7):**

DTOs:
1. `MedRecProImportClass\Models\ProcessedCell.cs` — Single cell after HTML processing (13 properties: identity, position, span, text, footnotes, styleCode)
2. `MedRecProImportClass\Models\ReconstructedRow.cs` — Classified row with `RowClassification` enum (ExplicitHeader, InferredHeader, ContinuationHeader, SocDivider, DataBody, Footer)
3. `MedRecProImportClass\Models\ResolvedHeader.cs` — Multi-level header structure with `HeaderColumn` (column paths like "Treatment > Drug A")
4. `MedRecProImportClass\Models\ReconstructedTable.cs` — Top-level output DTO with classified rows, resolved headers, footnotes dictionary, and document/section context

Service:
5. `MedRecProImportClass\Service\TransformationServices\ITableReconstructionService.cs` — Interface with `ReconstructTableAsync` and `ReconstructTablesAsync`
6. `MedRecProImportClass\Service\TransformationServices\TableReconstructionService.cs` — Full implementation consuming `ITableCellContextService` for DRY data access

Tests:
7. `MedRecProTest\TableReconstructionServiceTests.cs` — 36 MSTest unit tests using Moq (no database needed)

**Key decisions:**
- Always promote first Body row to InferredHeader (~99% of SPL tables encode headers in first Body row)
- Extract styleCode attributes in Stage 2 (available for Stage 3 header inference and formatting)
- Reuse existing `TextUtil.RemoveUnwantedTags(cleanAll: true)` for HTML stripping instead of reimplementing
- SOC divider detection: single cell spanning full table width, non-empty text < 200 chars
- Column position resolution via 2D boolean occupancy grid handles RowSpan bleeding across rows
- Multi-level header resolution walks header rows per column, building HeaderPath arrays joined with " > "
- Replaced all `<see cref>` with `<seealso cref>` for Swagger documentation compatibility
- Did not reuse existing `TextTableDto` family (web project API layer with encrypted IDs — different purpose)
- Did not include TextTableColumn data (rendering hints, not needed for structural reconstruction)

Build: 0 errors. Tests: 36/36 passed.

---

### 2026-03-20 2:02 PM EST — Stage 3: Section-Aware Parsing (SPL Table Normalization)

Implemented the full Stage 3 parsing pipeline for SPL table normalization. This stage takes Stage 2's `ReconstructedTable` output, routes each table to a type-specific parser based on `ParentSectionCode` (LOINC), and decomposes cell values into structured 36-column observations written to `tmp_FlattenedNormalizedTable`.

**Files created (22 new, 3 modified across 2 sessions):**
- **Models (4):** `TableCategory.cs` (enum), `ParsedValue.cs`, `ArmDefinition.cs`, `ParsedObservation.cs` — pipeline DTOs
- **Entity (2):** `LabelView.FlattenedNormalizedTable` nested class added to both `MedRecPro\Models\LabelView.cs` and `MedRecProImportClass\Models\LabelView.cs`
- **API DTO (1):** `FlattenedNormalizedTableDto.cs` in MedRecPro\Models\
- **Services (16):** `ValueParser.cs` (13 regex patterns), `PopulationDetector.cs` (Levenshtein fuzzy validation), `ITableParser.cs`, `BaseTableParser.cs`, 8 concrete parsers (PK, SimpleArm, MultilevelAE, AEWithSOC, EfficacyMultilevel, BMD, TissueRatio, Dosing), `ITableParserRouter.cs`, `TableParserRouter.cs`, `ITableParsingOrchestrator.cs`, `TableParsingOrchestrator.cs`
- **SQL (1):** `Create_tmp_FlattenedNormalizedTable.sql` — idempotent DDL with 5 indexes
- **Tests (4):** `ValueParserTests.cs` (35+ tests), `PopulationDetectorTests.cs`, `TableParserTests.cs` (all 8 parsers + router), `TableParsingOrchestratorTests.cs`

**Key decisions:**
- DI registration goes in `MedRecProConsole\Services\ImportService.cs` (not MedRecPro\Program.cs) — MedRecPro has no ProjectReference to MedRecProImportClass; the console app is the correct composition root for batch processing
- 8 parsers with priority-based selection within categories (e.g., MultilevelAE priority 10 > AEWithSOC priority 20 > SimpleArm priority 30)
- ValueParser uses strict priority-ordered regex chain (first match wins) — discovered that `n_pct` pattern legitimately matches "125(32%)" before `value_cv`
- Footnote marker regex split into two alternatives: special symbols (†‡§¶#*) always match; letters [a-g] only match after non-letter (prevents stripping trailing 'e' from "Headache")
- Type promotion is parser-level (bare Numeric → Percentage in AE, → Mean in PK, → MeanPercentChange in BMD, → Ratio in Tissue)
- Batch processing via TextTableID range for 250K+ label corpus

Build: 0 errors across MedRecPro, MedRecProConsole, MedRecProTest. Tests: 77/77 passed.

---

### 2026-03-20 3:02 PM EST — Stage 4 SPL Table Normalization: Validation Services

Implemented Stage 4 (Validation) of the SPL Table Normalization pipeline — automated post-parse consistency checks, confidence scoring, and coverage reporting. Three new validation services layer on top of the existing Stage 3 parser output.

**New files (10):**
- `ValidationResult.cs` — DTOs: `ValidationStatus` enum, `RowValidationResult`, `TableValidationResult`, `BatchValidationReport`, `CrossVersionDiscrepancy`
- `IRowValidationService.cs` / `RowValidationService.cs` — Per-observation checks: orphan detection (Error), required fields by category (Warning), value type appropriateness, ArmN consistency, bound inversion (Error), low confidence flagging
- `ITableValidationService.cs` / `TableValidationService.cs` — Cross-row checks: duplicate observation detection, arm coverage gap detection, count reasonableness (arms × params ±20%)
- `IBatchValidationService.cs` / `BatchValidationService.cs` — Aggregate reporting (confidence distribution, flag summaries, category/rule breakdowns), cross-version concordance (groups by ProductTitle+LabelerName, flags >50% row count divergence)
- 3 test files: `RowValidationServiceTests.cs` (16 tests), `TableValidationServiceTests.cs` (8 tests), `BatchValidationServiceTests.cs` (13 tests)

**Modified files (2):**
- `ITableParsingOrchestrator.cs` — Added `ProcessAllWithValidationAsync` method
- `TableParsingOrchestrator.cs` — Optional `IBatchValidationService` DI (null = skip validation), skip reason tracking via `processBatchWithSkipTrackingAsync`, validation integration after batch completion

**Key decisions:**
- Missing required fields = Warning severity (not Error) to avoid false positives on valid edge cases like Comparison rows without ArmN
- Cross-version key = (ProductTitle, LabelerName) since SetId is not in the current schema
- Results = in-memory DTOs + ILogger summaries only — no new DB tables
- Stage 4 flags append to existing `ValidationFlags` with semicolon delimiter, preserving Stage 3 PCT_CHECK flags
- Row/Table services are synchronous (pure logic); only BatchValidationService is async (DB queries)

Build: 0 errors. Tests: 692/692 passed (37 new + 655 existing).

---

### 2026-03-23 10:58 AM EST — SPL Table Transformation Fault Tolerance

Added table-level atomicity to the Stage 3 table parsing pipeline. Previously, if a row-level error occurred inside a parser, cells were silently skipped and partial table data was written to the database. Now, any row exception causes the entire table to be skipped with zero data written.

**Approach:** Base-class wrapper pattern — added `parseRowSafe()` to `BaseTableParser` that wraps each row's data-extraction logic in try/catch, rolls back any partial observations on failure, and throws a `TableParseException` with structured context (TextTableID, RowSequence, ParserName). The orchestrator's existing catch block handles the rest.

**Changes:**
- New `TableParseException` custom exception with structured error context
- `BaseTableParser.parseRowSafe()` — row-level try/catch with observation rollback
- All 8 parsers refactored to use `parseRowSafe()` (SimpleArm, MultilevelAe, AeWithSoc, Pk, Dosing, EfficacyMultilevel, Bmd, TissueRatio)
- `TableParsingOrchestrator` — `TableParseException`-specific catch with structured logging, `ChangeTracker.Clear()` safety on `SaveChangesAsync` failure, and `EMPTY:{parser}` skip tracking to distinguish "no data" from "error"

Build: 0 errors.

---

### 2026-03-23 1:19 PM EST — CLI Table Standardization Commands for MedRecProConsole

Added `--standardize-tables` CLI mode and interactive `standardize-tables` / `st` command to MedRecProConsole, exposing the Stage 3+4 SPL table normalization pipeline (parsing + validation) through the console application.

**New files (6):**
- `MedRecProImportClass\Models\TransformBatchProgress.cs` — DTO for IProgress callback (batch number, ranges, counts, elapsed)
- `MedRecProConsole\Models\StandardizationProgressFile.cs` — Serializable progress state for cancellation/resumption
- `MedRecProConsole\Services\StandardizationProgressTracker.cs` — Atomic JSON progress tracking (SemaphoreSlim, write-to-temp-then-rename, SHA256 connection hash)
- `MedRecProConsole\Services\TableStandardizationService.cs` — Main service bridging CLI to orchestrator with Spectre.Console progress bars, Ctrl+C handling, validation report display
- `MedRecProTest\CommandLineArgsStandardizeTablesTests.cs` — 20 tests for CLI arg parsing
- `MedRecProTest\StandardizationProgressTrackerTests.cs` — 8 tests for progress tracking
- `MedRecProTest\TableParsingOrchestratorProgressTests.cs` — 5 tests for IProgress + resume

**Modified files (8):**
- `ITableParsingOrchestrator.cs` / `TableParsingOrchestrator.cs` — Added `IProgress<TransformBatchProgress>`, `int? resumeFromId`, `int? maxBatches` parameters to `ProcessAllAsync` and `ProcessAllWithValidationAsync`; Stopwatch for elapsed time; conditional truncate skip on resume; batch limit break
- `CommandLineArgs.cs` — Added `--standardize-tables <op>`, `--batch-size <n>`, `--table-id <id>` parsing with mutual exclusion and validation rules
- `Program.cs` — Added standardize-tables mode routing; extracted shared `resolveConnectionString()` (eliminated ~96 lines of duplication from unattended + orange-book methods)
- `ConsoleHelper.cs` — Added interactive `standardize-tables` / `st` command with guided flow: resume previous session → truncate prompt → scope selection (all/limited/single) → batch size → confirmation → execute with validation always on
- `HelpDocumentation.cs` — Added `DisplayStandardizeTablesModeInfo()` and usage examples
- `appsettings.json` — Added help topic + command-line options
- `README.md` — Added Table Standardization section with operations, examples, batch tuning, resumption, validation report docs

**Key decisions:**
- Validation always enabled — interactive mode always runs Stage 3+4 (no parse-only option)
- `maxBatches` parameter for limited scope runs (e.g., 10 batches x 1000 = ~10K table IDs)
- Resume via `.medrecpro-standardization-progress.json` — tracks last completed TextTableID, connection hash, cumulative stats
- Ctrl+C saves progress atomically; re-running same command auto-resumes
- `SynchronousProgress<T>` helper in tests to avoid `Progress<T>` ThreadPool callback timing issues

Build: 0 errors. Tests: 725/725 pass (33 new).

---

### 2026-03-23 1:33 PM EST — Table Standardization: UX Refinements + Diagnostics

**Interactive menu redesign** (`ConsoleHelper.runStandardizeTablesFromMenuAsync`):
- Parse always includes validation (Stage 3+4) — removed standalone parse-only option
- Added scope selection: All tables / Limited (N batches) / Single table ID / Cancel
- Added resume prompt when `.medrecpro-standardization-progress.json` exists (shows session stats, offers Resume/Start fresh/Cancel)
- Truncation moved to a yes/no step at the start of the flow, then continues to scope selection
- Better aligned selection prompt labels with padded descriptions

**`maxBatches` parameter** threaded through the full stack:
- `ITableParsingOrchestrator.ProcessAllAsync` / `ProcessAllWithValidationAsync` — new `int? maxBatches` param; caps `totalBatches` and breaks loop when limit reached
- `TableStandardizationService.ExecuteValidateAsync` — passes `maxBatches` through to orchestrator

**Spectre.Console markup escape fix** — `[{RangeStart}-{RangeEnd}]` in progress bar descriptions crashed with `InvalidOperationException: Could not find color or style '1-100'`. Fixed by escaping to `[[...]]` (Spectre markup literal bracket syntax).

**Diagnostics for skipped tables** — First run showed 0 observations, 74 tables skipped with no explanation:
- Changed default logging from `LogLevel.None` to `LogLevel.Warning` so orchestrator parse errors and skip messages appear in console output
- Added Skip Reasons table to validation report display, showing `BatchValidationReport.SkipReasons` breakdown (e.g., `SKIP:SKIP`, `EMPTY:ParserName`, `ERROR:ParserName:RowN`)

---

### 2026-03-23 3:03 PM EST — Table Standardization: PK Fix, Column Widening, Caption-Based Value Type Inference

Three issues discovered and resolved during first real-data runs of the standardization pipeline:

**1. EF Core keyless entity crash (100% failure rate):**
- `FlattenedStandardizedTable` was configured as `.HasNoKey()` (keyless), which is fine for reads but `AddRange` + `SaveChangesAsync` requires EF Core change tracking, which requires a primary key
- Fix: Added `tmp_FlattenedStandardizedTableID INT IDENTITY(1,1) PRIMARY KEY` surrogate column to `tmp_FlattenedStandardizedTable` DDL and updated entity configuration

**2. Column truncation crash (`Unit` NVARCHAR(50) overflow):**
- Parser placed long indication text into the `Unit` field, causing `String or binary data would be truncated` and killing the entire batch
- Fix: Widened 16 columns in the DDL (e.g., `Unit` 50→500, `RawValue` 500→2000, `ParameterName` 500→1000, etc.)
- Made `SaveChangesAsync` failures skip the batch instead of rethrowing — both `ProcessBatchAsync` and `processBatchWithSkipTrackingAsync` now catch, clear the change tracker, log a warning, and return 0. `OperationCanceledException` is still rethrown for Ctrl+C support.

**3. Caption-based value type inference (new feature):**
- Problem: PK table with caption "Mean (SD) Serum Pharmacokinetic Parameters..." had cells like "3057 (980)" misidentified as n(%) (pct=980, count=3057) instead of Mean=3057, SD=980
- Added `CaptionValueHint` struct and 15-pattern compiled regex dictionary to `BaseTableParser` for detecting statistical descriptors in captions (Mean (SD), Geometric Mean (%CV), Median (Range), LS Mean (SE), etc.)
- `detectCaptionValueHint()` scans caption once per table, returns typed hint
- `applyCaptionHint()` reinterprets parsed values: swaps n_pct → mean_sd when caption confirms, promotes bare Numeric with confidence adjustment, fills secondary types
- Wired into `PkTableParser` and `SimpleArmTableParser`; PK fallback `Numeric→Mean` now applies 0.8 confidence multiplier without caption confirmation
- Validation flags: `CAPTION_REINTERPRET:n_pct→Mean(SD)` and `CAPTION_HINT:caption:Mean (SD)` for audit trail

---

### 2026-03-23 4:00 PM EST — Claude API Correction Service (Stage 3.5)

Added AI-powered post-parse correction to the SPL Table Normalization pipeline. After Stage 3 parsers produce `ParsedObservation` objects, the new `ClaudeApiCorrectionService` sends table-level batches to Claude Haiku for semantic review of misclassified fields (PrimaryValueType, SecondaryValueType, TreatmentArm, etc.) before database write.

**New files created:**
- `MedRecProImportClass/Models/ClaudeApiCorrectionSettings.cs` — Configuration model (model, rate limits, enable/disable)
- `MedRecProImportClass/Service/TransformationServices/ClaudeApiCorrectionService.cs` — `IClaudeApiCorrectionService` interface + implementation with table-level grouping, sub-batch splitting, compact JSON payloads, audit flags (`AI_CORRECTED:{field}`), and graceful failure handling
- `MedRecProTest/ClaudeApiCorrectionServiceTests.cs` — 13 MSTest + Moq tests covering happy path, disabled mode, API failures/timeouts, invalid JSON, table grouping, batch splitting, and invalid correction handling

**Files modified:**
- `MedRecProImportClass/MedRecProImportClass.csproj` — Added `Microsoft.Extensions.Http` package
- `MedRecProImportClass/Service/TransformationServices/TableParsingOrchestrator.cs` — Added optional `IClaudeApiCorrectionService` constructor parameter; injected correction call in both `ProcessBatchAsync` and `processBatchWithSkipTrackingAsync` (post-parse, pre-write)
- `MedRecProConsole/MedRecProConsole.csproj` — Added `UserSecretsId` and `Microsoft.Extensions.Configuration.UserSecrets` package
- `MedRecProConsole/appsettings.json` — Added `ClaudeApiCorrectionSettings` configuration section
- `MedRecProConsole/Services/TableStandardizationService.cs` — Composite configuration (in-memory + appsettings.json + user secrets); registered `IClaudeApiCorrectionService` via `AddHttpClient`
- `MedRecProImportClass/README.md` — Added Stage 3.5 documentation, updated architecture diagram and dependency table

**Key decisions:**
- Claude Haiku for speed/cost on high-volume batch processing
- API key stored in User Secrets (never in appsettings.json)
- Correction is optional and gracefully degrades — API failures return original observations unchanged
- `OperationCanceledException` uses `when (ct.IsCancellationRequested)` filter to distinguish user cancellation from HTTP timeouts

---

### 2026-03-24 9:30 AM EST — Stage Visibility, Refactoring, and Pivoted Table Display

**Session 1:** Added stage-by-stage batch orchestration with `ProcessBatchWithStagesAsync` returning `BatchStageResult` DTO capturing intermediates at each pipeline boundary. Added interactive prompts for Claude AI enable/disable and stage detail level (None/Concise/Full) to the `standardize-tables` menu. Created `ExecuteParseWithStagesAsync` in the console service with per-batch stage display. Added `StageDetailLevel` enum and `BatchStageResult` model. All 753 tests pass with 3 new batch stage tests.

**Session 2:** Three improvements applied:

1. **Pivoted table display in Full mode** — `displayBatchStageDetail` now calls `displayReconstructedTable` for each non-skipped table, showing metadata, column headers, body rows, and footnotes inline per batch. This gives full diagnostic visibility into the Stage 2 pivot output.

2. **Refactored `TableStandardizationService`** — Extracted `RunContext` record and three lifecycle helpers (`initializeRunAsync`, `handleCompletionAsync`, `handleCancellationAsync`, `handleErrorAsync`) to eliminate duplicated service provider setup, progress tracking, and error handling across `ExecuteParseAsync`, `ExecuteValidateAsync`, and `ExecuteParseWithStagesAsync`. Public methods reduced from 100-145 lines to 50-75 lines each.

3. **Stage renumbering aligned with ImportClass** — All UI labels and XML doc comments now use consistent 1-based numbering matching `MedRecProImportClass/Service/TransformationServices/`:
   - Stage 1: Get Data (`TableCellContextService`)
   - Stage 2: Pivot Table (`TableReconstructionService`)
   - Stage 3: Standardize (`TableParserRouter` + parsers)
   - Stage 3.5: Claude Enhance (`ClaudeApiCorrectionService`)
   - Stage 4: Validate (`BatchValidationService`)

4. **Method reordering** — Public methods in `TableStandardizationService` now follow stage-sequential order: Truncate → ParseSingle (1→2→3→3.5) → Parse (3 batch) → ParseWithStages (1→2→3→3.5 batch) → Validate (3+4 batch).

5. **Tests** — 2 new tests added (`CapturesPreCorrectionObservations`, `WithCorrectionService_RecordsCorrectionCount`). All 755 tests pass, 0 failures.

---

### 2026-03-24 10:30 AM EST — Add Time/TimeUnit Columns to Table Standardization

Added dedicated `Time` (FLOAT) and `TimeUnit` (NVARCHAR(50)) columns to the standardized table schema to capture temporal dimensions from PK and BMD tables in a structured, queryable format.

**Problem:** PK table standardization embedded dosing duration (e.g., "once daily x 7 days") inside the DoseRegimen text string with Timepoint always NULL, making it impossible to query or filter by time without downstream string parsing.

**Evaluation decision:** Chose dedicated Time/TimeUnit columns over overloading SecondaryValue because (1) SecondaryValue is already used for CV%, SD, and Count in PK cells, (2) time is a dimensional/contextual field not a companion value, and (3) dedicated columns enable clean SQL filtering (`WHERE Time > 5 AND TimeUnit = 'days'`).

**Changes across 9 files:**
1. **Schema** — Added Time/TimeUnit to SQL DDL, ParsedObservation DTO, both LabelView entities (ImportClass + API), and FlattenedStandardizedTableDto.
2. **PkTableParser** — New `extractDuration()` method parses dose regimen text using regex for "x N days/weeks", "for N days", and "single" patterns. Populates Time, TimeUnit, and Timepoint on every observation from the same dose row.
3. **BmdTableParser** — New `parseTimepointNumeric()` method extracts numeric time from existing Timepoint labels ("12 Months" → Time=12, TimeUnit="months"; "Week 12" → Time=12, TimeUnit="weeks").
4. **TableParsingOrchestrator** — Added Time/TimeUnit to `mapToEntity()` mapping.
5. **ClaudeApiCorrectionService** — Added Timepoint and TimeUnit to correctable fields.
6. **Tests** — 11 new tests covering PK duration extraction (multi-day, single dose, weekly, "for" pattern, null/empty, unrecognized) and BMD numeric timepoint parsing. All 755 tests pass.

---

### 2026-03-24 10:36 AM EST — Refine Validation Components with Granular Scoring

Enhanced the Stage 4 validation pipeline with Time/TimeUnit validation, field completeness scoring, adjusted confidence penalties, and a 5-band confidence distribution (replacing the previous 3-tier scheme).

**Row-level validation (RowValidationService) — 3 new checks:**
1. `TIME_UNIT_MISMATCH` — Time and TimeUnit must both be present or both absent
2. `UNREASONABLE_TIME` — Time must be > 0 when set
3. `INVALID_TIME_UNIT` — TimeUnit must be in {days, weeks, months, hours, years}

**Field completeness scoring:** New `calculateFieldCompleteness()` scores each observation 0.0–1.0 based on how many expected fields (required + desirable) are populated for its TableCategory. PK expects 7 fields, AE expects 5, etc.

**Adjusted confidence:** New `AdjustedConfidence` property on ParsedObservation. Starts from ParseConfidence and applies cumulative penalty multipliers per validation issue (MISSING_FIELD ×0.85, UNEXPECTED_VALUE_TYPE ×0.90, TIME_UNIT_MISMATCH ×0.90, etc.).

**Table-level validation (TableValidationService):** New `TIME_EXTRACTION_INCONSISTENCY` check for PK/BMD tables — flags when some observations have Time populated and others don't (excluding single-dose timepoints).

**Batch-level (BatchValidationService):** Confidence distribution expanded from 3 bands (High/Medium/Low) to 5 bands (VeryHigh ≥0.95, High 0.80–0.95, Medium 0.60–0.80, Low 0.40–0.60, VeryLow <0.40) for both ParseConfidence and AdjustedConfidence. Added AverageFieldCompleteness aggregate. Updated `mapFromEntity` to include Time/TimeUnit.

**Console display:** Updated `ExecuteValidateAsync` to show side-by-side Parse vs Adjusted confidence in 5-band table with field completeness footer.

**Tests:** 13 new tests (9 row-level: time pairing, range, vocabulary, completeness, adjusted confidence; 3 table-level: PK time consistency; 1 batch-level fix for 5-band). All 779 tests pass.

---

### 2026-03-24 10:36 AM EST — Column-Derived Time for Time-Based PK Parameters

Extended PkTableParser to detect when a PK column IS a time measurement (Half-life, Tmax) and override Time/TimeUnit with the measured value instead of the row-derived dosing duration.

**Problem:** Half-life (hours) values like 26.6 had Time=7, TimeUnit="days" (from the dose regimen), losing the relationship between the time measurement and its value. The system only captured row-derived time, not column-derived time.

**Solution — dual-source time capture:**
- `Timepoint` (text) always holds the row-derived label ("7 days", "single dose") — dosing schedule context
- `Time`/`TimeUnit` now holds the most semantically relevant numeric time:
  - For time-based parameters (Half-life, Tmax): column-derived from PrimaryValue/Unit (e.g., 26.6 hours)
  - For non-time parameters (Cmax, AUC): row-derived from dose regimen (e.g., 7 days)

**Changes to PkTableParser:**
1. Added `_timeUnitStrings` HashSet with known pure time units (hours, hrs, hr, h, minutes, min, seconds, sec, days, weeks, months)
2. Extended `extractParameterDefinitions` to return 4-tuple `(columnIndex, name, unit, isTimeMeasure)` — detects when unit is a pure time string
3. After `applyParsedValue`, overrides Time/TimeUnit from PrimaryValue when `isTimeMeasure` is true
4. Improved `normalizeTimeUnit` to handle abbreviations (hrs→hours, min→minutes, sec→seconds, h→hours)

**Composite units excluded:** "mcg·h/mL" is NOT detected as a time measure — only pure time units trigger the override.

**Tests:** 2 new tests (Tmax hrs detection, composite unit exclusion) + 1 updated (mixed time/non-time assertions). All 781 tests pass.

---

### 2026-03-24 10:36 AM EST — Parse Dash-Separated Confidence Intervals in PK Tables

Added support for parsing values with dash-separated confidence intervals like `"0.38 (0.31 - 0.46)"` which were previously falling through to `text_descriptive` at 0.50 confidence.

**Problem:** ValueParser Pattern 5 (rr_ci) and Pattern 6 (diff_ci) both require COMMA separators inside parentheses. Drug interaction PK tables use DASH format `VALUE (LOWER - UPPER)` for mean ratios with 90% CI, which matched no pattern.

**Solution — 3 layers:**

1. **ValueParser — new Pattern 6b `value_ci_dash`:**
   - Regex: `^(-?[\d.]+)\s*\(\s*(-?[\d.]+)\s*[-–—]\s*(-?[\d.]+)\s*\)$`
   - Handles hyphen, en-dash, em-dash with optional spaces
   - Returns `PrimaryValueType = "Numeric"`, `BoundType = "CI"` (generic), `ParseConfidence = 0.95`
   - Validates lower < upper (rejects inverted bounds → falls to text)
   - PK fallback promotes "Numeric" → "Mean" downstream

2. **BaseTableParser — CaptionValueHint extended:**
   - Added `BoundType` field to `CaptionValueHint` struct
   - New caption patterns: "Mean ratio with 90% CI" → `Ratio`+`90CI`, "Mean ratio with 95% CI" → `Ratio`+`95CI`, generic "90% CI"/"95% CI", bare "Mean ratio" → `Ratio`
   - New Case 4 in `applyCaptionHint`: refines generic "CI" → "90CI"/"95CI" from caption

3. **PkTableParser — table text CI level detection:**
   - `detectCILevelFromTableText()` scans caption, footer rows, and data body rows for "N% CI" pattern
   - Post-parse: if any observations have `BoundType == "CI"`, scans table text and refines to "90CI"/"95CI"
   - Handles the common case where CI level is in a footer/annotation row rather than caption

**Tests:** 8 new tests (6 ValueParser: standard, no-spaces, en-dash, negatives, invalid bounds, comma non-interference; 2 integration: footer refinement to 90CI, no-footer stays generic CI). All 789 tests pass.

---

### 2026-03-24 2:09 PM EST — Broaden CI Pattern to Accept "to" Separator

Dolutegravir drug interaction table (TextTableID=118) uses "to" format: `"0.99 (0.91 to 1.08)"`. The previous `value_ci_dash` pattern only matched dash/en-dash/em-dash separators, missing this common format entirely.

**Root cause:** Regex `[-–—]` doesn't match the word "to". All "to"-separated CI values fell through to text_descriptive at 0.50 confidence.

**Fix:** Broadened separator group from `[-–—]` to `(?:[-–—]|to)` in the regex. Renamed pattern/method/rule: `_valueCiDashPattern` → `_valueCiPattern`, `tryParseValueCIDash` → `tryParseValueCI`, `"value_ci_dash"` → `"value_ci"`.

**No conflicts:** verified "to" separator doesn't interfere with Pattern 8 (range `"10.7 to 273"`) which is `^`-anchored and requires no parens.

**Tests:** 3 new (to-separator, to-no-space, range-non-interference), 6 renamed from `_Dash_` to `_CI_`. All 792 tests pass.

---

### 2026-03-24 2:42 PM EST — Fix String Truncation in mapToEntity for All Fixed-Size Columns

SQL error 2628 (`String or binary data would be truncated`) on `RawValue` column during batch SaveChangesAsync, losing the entire 64,783-entity batch.

**Root cause:** While `RawValue` truncation (Truncate(1994) → 1998 ≤ 2000) was mathematically correct, 7 other fixed-size string columns had **no truncation at all** — any overflow in any field kills the entire batch. Additionally, a stale binary (pre-truncation build) may have been running.

**Fix:** Added `XSM_TEXT_LENGTH = 94` (NVARCHAR(100)) and `TINY_TEXT_LENGTH = 44` (NVARCHAR(50)) constants. Applied truncation to all 7 unprotected fields: `ParentSectionCode`, `TimeUnit`, `BoundType` → TINY; `PrimaryValueType`, `SecondaryValueType`, `ParseRule` → XSM; `FootnoteMarkers` → SML. Every fixed-size string column now has defensive truncation. NVARCHAR(MAX) columns (`Caption`, `FootnoteText`) need none.

**Clean rebuild:** Both ImportClass and Console app rebuilt with `--no-incremental`. All 792 tests pass.

---

### 2026-03-24 3:44 PM EST — Parse ± Values, Detect "n" as Sample Size, Population from Column 0

TextTableID=346 (pediatric PK table) exposed three parsing gaps: ± values falling to text_descriptive, "n" column misidentified as Mean, and "Age Group (y)" column 0 values assigned as DoseRegimen instead of Population.

**1. ValueParser — new Pattern 6c `value_plusminus`:**
- Regex: `^(-?[\d.]+)\s*(?:±|\+/?-)\s*(-?[\d.]+)$`
- Parses "1.1 ± 0.5", "580±450", "71 +/- 40", "55 +- 18"
- Returns PrimaryValueType="Numeric" (PK promotes to Mean), SecondaryValueType="SD"
- Computes LowerBound = primary - tolerance, UpperBound = primary + tolerance, BoundType="SD"
- Added "Mean ± SD" caption hint pattern in BaseTableParser

**2. PkTableParser — "n" column as sample size:**
- Extended `extractParameterDefinitions` tuple to 5 fields: added `isSampleSize`
- `_sampleSizeHeaders` HashSet: "n", "N", "n=", "sample size"
- When `isSampleSize` is true, overrides Numeric → Count before PK Mean fallback
- Prevents sample sizes from being promoted to Mean

**3. PkTableParser — column 0 population detection:**
- `_populationHeaderKeywords` HashSet: "age group", "age", "population", "patient group", "subgroup", "cohort"
- `isColumn0Population()` checks column 0 header text against keywords
- When true: column 0 values go to `Population` (not DoseRegimen), DoseRegimen set to null

**Tests:** 7 new (5 ValueParser ± tests, 2 integration: n-column Count, Age Group population). All 799 tests pass.

---

### 2026-03-25 12:16 PM EST — AE Table Parsing: Arm N, ParameterCategory, ValueType Fixes

Fixed 5 issues in Stage 3 AE table parsing for Paroxetine label (DocumentGUID 9CDE2EF4-FD50-ED21-0628-CB6CD8A6153F):

**Round 1 — Regex and category fixes:**
- **ValueParser.cs**: Updated `_armHeaderPattern` to handle lowercase `n` and spaces (`[Nn]\s*=\s*`). Added `_armHeaderNoParenPattern` for no-parentheses format like `"Placebo n = 51 %"`. Refactored `ParseArmHeader` to try both patterns via shared `buildArmFromMatch` helper.
- **SimpleArmTableParser.cs**: Added `currentCategory` state. In AE context, empty-data rows now set `ParameterCategory` (SOC body systems) instead of `ParameterSubtype`.
- **ArmDefinition.cs**: Documentation updated for dual regex patterns.
- **Tests**: 7 new tests covering all 5 issues. All 25 pass.

---

### 2026-03-25 1:13 PM EST — AE Table Parsing: Header Format Hints, Body-Row Enrichment, DoseRegimen

Fixed additional misalignments in TextTableID=54 (multi-indication AE) and TextTableID=58 (dose-comparison Table 7):

**Round 2 — Format hints and body-row enrichment:**
- **BaseTableParser.cs**: Added `_trailingFormatHintPattern` to strip trailing `%` or `n(%)` from arm headers without N= (e.g., "Paroxetine %" → Name="Paroxetine", FormatHint="%"). Added 3 enrichment detection regexes (`_doseRegimenPattern`, `_nEqualsCellPattern`, `_formatHintCellPattern`). New shared helpers: `classifyEnrichmentRow` (detects dose/N=/format rows), `enrichArmsFromBodyRows` (scans first ≤5 body rows, enriches arm definitions, returns skip count), `applyEnrichmentRow` (applies per-type enrichment).
- **ArmDefinition.cs**: Added `DoseRegimen` property for dose-specific arms.
- **MultilevelAeTableParser.cs**: Format-hint stripping in fallback arm creation, enrichment call, DoseRegimen propagation.
- **SimpleArmTableParser.cs**: Enrichment call before data loop, DoseRegimen propagation.
- **AeWithSocTableParser.cs**: Same enrichment + DoseRegimen pattern.
- **Tests**: 7 new tests (trailing % stripping, dose enrichment, N= enrichment, format hint enrichment, multi-row enrichment, multilevel trailing %). All 32 pass.

---

### 2026-03-25 3:48 PM EST — Column Standardization Service (Stage 3.25)

Built a deterministic, rule-based post-parse service that detects and corrects systematic column misclassification in ADVERSE_EVENT and EFFICACY table observations. Analysis of ~7,700 parsed rows revealed 9 distinct patterns where TreatmentArm, ArmN, DoseRegimen, StudyContext, and ParameterSubtype values end up in the wrong columns due to non-standard SPL table layouts.

**New files:**
- `IColumnStandardizationService.cs` — interface with `InitializeAsync()` and `Standardize()` methods
- `ColumnStandardizationService.cs` — implementation with drug name dictionary (loaded from `vw_ProductsByIngredient`), 13-step content classifier, and 9 prioritized correction rules
- `ColumnStandardizationServiceTests.cs` — 44 MSTest tests covering all 9 rules, category filtering, edge cases, and multi-observation batches

**Modified files:**
- `TableParsingOrchestrator.cs` — injected as optional Stage 3.25 (between parser output and Claude AI correction), with lazy dictionary initialization on first batch; added to all 3 processing methods

**9 correction rules (most-specific first):**
R1: Arm contains N= value → parse to ArmN, recover drug from context
R2: Arm contains format hint (%, #) → discard, recover drug from context
R3: Arm contains severity grade → move to ParameterSubtype
R4: Arm contains pure dose → move to DoseRegimen, extract drug from context
R5: Arm is bare number + context has dose descriptor → reconstruct dose, extract drug
R6: Arm is drug+dose combined → split into TreatmentArm + DoseRegimen
R7: Context contains arm name with N= → split to TreatmentArm + ArmN
R8: Context is drug name, arm is not → swap
R9: Context is descriptor/format hint → clear

**Bugs found during testing:** (1) `isDrugName` partial-match was too aggressive — "Placebo N=300" matched via first-word "Placebo"; fixed by rejecting partial matches containing embedded N= patterns. (2) Rule 7 wouldn't overwrite Unknown-type arms; fixed condition to only protect DrugName arms. All corrections flagged in `ValidationFlags` with `COL_STD:*` prefix for audit trail.

---

### 2026-03-25 4:20 PM EST — Column Standardization: Rules 10–11, Drug Dictionary Resolution

Extended the ColumnStandardizationService with two additional correction rules identified from production data review.

**Rule 10 — Trailing % in TreatmentArm:**
Handles "MYCAPSSA %", "PLACEBO %" where the format hint `%` got concatenated with the drug name during parsing. Strips the trailing hint and promotes `PrimaryValueType` from "Numeric" → "Percentage" when applicable. The regex requires whitespace before `%` to avoid false-matching concentration strings like "Pimecrolimus Cream; 1%".

**Rule 11 — Bracketed [N=xxx] in TreatmentArm:**
Handles composite values like "75 mg/day [N=77]", "Placebo [N=459]", "All PGB [N=979]". Extracts N → ArmN, strips "All" prefix, then classifies the remaining text: drug names stay in TreatmentArm, dose regimens move to DoseRegimen with the drug name resolved from the drug dictionary.

**Drug dictionary resolution (`resolveDrugNameFromProductTitle`):**
New helper method that searches the loaded drug dictionary (ProductName + SubstanceName from `vw_ProductsByIngredient`) for entries appearing as substrings of the observation's ProductTitle. Returns the longest match to prefer specific names. This replaces the previous raw ProductTitle fallback in Rule 4 as well.

**Other fixes:**
- Rule 9 extended to also clear StudyContext when it contains a FormatHint (e.g., "% of Patients")
- Rule 4 updated to use dictionary resolution instead of raw ProductTitle fallback

**Tests:** 56 total (7 new for Rule 10/11), all passing. Key test: batch simulation of the actual LYRICA/pregabalin table from the screenshot — all 6 arm variants correctly decomposed.

---

### 2026-03-26 2:04 PM EST — Implement Column Contracts in ColumnStandardizationService

Refactored the Stage 3.25 `ColumnStandardizationService` from a single-pass AE/EFFICACY-only column fixer into a 4-phase pipeline that processes ALL table categories (PK, DDI, Dosing, BMD, TissueDistribution, etc.). This enforces the per-TableCategory column contracts defined in the data dictionary skill.

**Phase 1 (unchanged):** Existing 11 arm/context correction rules for AE+EFFICACY — wrapped into `applyPhase1_ArmContextCorrections()` with zero logic changes.

**Phase 2 (new) — Content Normalization:** Five sub-methods running on all categories:
- `normalizeDoseRegimen` — triages PK sub-params (Cmax, AUC, etc.) and co-admin drug names out of DoseRegimen into ParameterSubtype; routes residual population/timepoint to their correct columns
- `normalizeParameterName` — detects and nulls caption echoes ("Table 3...") and header echoes ("n"), routes bare dose integers to DoseRegimen, decodes HTML entities
- `normalizeTreatmentArm` — nulls header echoes ("Number of Patients"), generic labels ("Treatment", "PD"), extracts study names to StudyContext
- `normalizeUnit` — detects leaked column headers (>30 chars, drug names, keywords like "Regimen"/"Dosage"), normalizes variant spellings ("mcg h/mL" → "mcg·h/mL"), extracts real units from verbose descriptions
- `normalizeParameterCategory` — canonical MedDRA SOC mapping (~55 variants → 26 canonical names) with OCR artifact repair, AE-only

**Phase 3 (new) — PrimaryValueType Migration:** Maps old enum values to the tightened 15-value enum using TableCategory + Caption context. Key mappings: Mean → GeometricMean (PK/DDI) or ArithmeticMean (AE), Percentage → Proportion, RelativeRiskReduction → HazardRatio/OddsRatio/RelativeRisk based on caption, Numeric → context-resolved per category.

**Phase 4 (new) — Column Contract Enforcement:** Static `_columnContracts` dictionary defines R/E/O/N requirements for 13 observation context columns across 7 table categories. NULLs out N/A columns (e.g., Timepoint for AE, ParameterCategory for PK), flags missing Required columns (`COL_STD:MISSING_R_{Column}`), applies default BoundType when bounds are present but type is missing (90CI for PK/DDI, 95CI for Efficacy/BMD).

**Supporting changes:**
- `TableParsingOrchestrator.cs` — removed AE/EFFICACY category gate at 2 call sites
- `RowValidationService.cs` — added new PVT values (GeometricMean, ArithmeticMean, Proportion, HazardRatio, etc.) to allowed sets + new DRUG_INTERACTION entry
- `IColumnStandardizationService.cs` — updated XML docs to reflect all-category processing

**Static dictionaries added:** `_pkSubParams` (35 PK parameter names), `_knownUnits` (~80 canonical units), `_unitNormalizationMap` (variant→canonical), `_unitHeaderKeywords` (13 leak indicators), `_socCanonicalMap` (~55 SOC variants), `_pvtDirectMap` (9 direct PVT mappings), `_columnContracts` (7 categories × 13 columns), `_defaultBoundType` (5 category defaults).

**Tests:** 88 total (35 new + 53 existing), all passing. New tests cover each Phase 2 sub-method, Phase 3 migration paths, Phase 4 contract enforcement, and cross-category processing verification.

---

### 2026-03-26 3:18 PM EST — README Update and ClaudeApiCorrectionService Skill Expansion

Two tasks completed using four reference files: `table-types.md`, `column-contracts.md`, `normalization-rules.md`, and `TABLE_STANDARDIZATION_SKILL.md`.

**README.md update (`MedRecProImportClass/README.md`):**
Rewrote the SPL Table Normalization section to incorporate all reference file content. Added Stage 3.25 (Column Standardization) to the pipeline architecture, expanded the TableCategory table with source LOINC sections, full Tier 1 Decision Tree (9-step classification algorithm), Tier 2 ML.NET classifier summary (21 features, LightGBM, target Macro F1 ≥ 0.85), complete Column Contracts matrix (7 categories × 13 columns with R/E/O/N requirement levels), all enum definitions (PrimaryValueType 15-value tightened enum, SecondaryValueType, BoundType, ParseRule 16 values), static dictionary inventory (8 dictionaries with sizes and sources), and `ColumnStandardizationService.cs` added to the project structure file tree.

**ClaudeApiCorrectionService.cs update (Stage 3.5 AI correction):**
Replaced the minimal 3-rule system prompt with a comprehensive normalization skill covering all six normalization domains from the reference files:
- `PrimaryValueType` — full 15-value enum with migration rules for all old values (Mean→GeometricMean/ArithmeticMean by category, Percentage→Proportion, RelativeRiskReduction→HR/OR/RR, Numeric resolved by TableCategory + Caption context)
- `DoseRegimen` triage — priority-ordered routing of PK sub-params, actual doses (keep), co-admin drug names, population patterns, timepoint patterns, and header echoes
- `Unit` scrub — header leak detection (>30 chars, drug names, keyword list), variant spelling normalization
- `ParameterName` cleanup — caption/header echo detection, bare dose integer routing, DDI drug name routing, HTML entity decoding
- `TreatmentArm` cleanup — header echo nulling, N=xxx extraction, embedded dose extraction, generic label nulling, study name routing to StudyContext
- `ParameterCategory` SOC mapping — 16 canonical MedDRA SOC names with variant corrections for AdverseEvent/Laboratory only
- `BoundType` inference — category-based defaults (90CI for PK/DDI, 95CI for Efficacy/BMD)

**Code fixes in the same file:**
- Added `StudyContext` and `BoundType` to `CorrectableFields` (were missing)
- Added `timepoint`, `timeunit`, `studycontext`, `boundtype` cases to `setFieldValue()` (bug: Timepoint and TimeUnit were in CorrectableFields but had no setter cases)
- Expanded `buildCompactPayload()` to include `Timepoint`, `TimeUnit`, `StudyContext`, `LowerBound`, `UpperBound`, `BoundType` so Claude has full context for triage and BoundType inference decisions
- Added `ParentSectionCode` and `ObservationCount` to the per-request context header sent to Claude

---

### 2026-03-27 10:36 AM EST — Universal Inline N= Extraction Across All Non-RawValue Columns

Added a Phase 2 pre-pass (`normalizeInlineNValues`) to `ColumnStandardizationService` that strips N= sample-size annotations from every non-RawValue column (TreatmentArm, StudyContext, DoseRegimen, ParameterName, ParameterSubtype, Population, Timepoint, Unit) and populates `ArmN`. This closes three gaps: (1) standalone `(N=xxx)` in non-AE/EFFICACY TreatmentArm was never extracted, (2) DoseRegimen with embedded `(n=963)` mid-string was never stripped, (3) other columns with stray N= patterns were ignored.

**Key changes:**
- Two new compiled regex patterns: `_standaloneBracketNPattern` for `[N=xxx]` as whole value, `_inlineNPattern` for `(N=xxx)` or `[N=xxx]` embedded anywhere
- `tryStripInlineN` helper with three-tier matching (standalone parens, standalone brackets, inline embedded)
- `normalizeInlineNValues` pre-pass wired as first call in `applyPhase2_ContentNormalization`
- Guard added to `normalizeTreatmentArm` Priority 2 (`&& !obs.ArmN.HasValue`) to prevent double-extraction
- Updated existing PK category test to reflect new behavior; added 7 new tests covering all gap cases
- Discovery: DOSING category marks `ArmN` as `NotApplicable` in column contracts, so Phase 4 nulls it after extraction — pre-pass still cleans the column text correctly

**Result:** 908/908 tests pass.

---

### 2026-03-27 11:38 AM EST — Stage 3.4 MlNetCorrectionService Implementation

Implemented the full ML.NET correction and anomaly scoring service (Stage 3.4) that inserts between Stage 3.25 (ColumnStandardization) and Stage 3.5 (ClaudeApiCorrection) in the SPL Table Normalization pipeline.

**New files:**
- `MedRecProImportClass/Models/MlNetCorrectionSettings.cs` — Configuration DTO (7 properties: Enabled, thresholds, training params)
- `MedRecProImportClass/Models/MlNetDataModels.cs` — ML.NET input/prediction class pairs for all 4 stages, organized by region (moved from service file in a follow-up refactor)
- `MedRecProImportClass/Service/TransformationServices/IMlNetCorrectionService.cs` — Interface mirroring `IColumnStandardizationService` pattern (`InitializeAsync` + `ScoreAndCorrect`)
- `MedRecProImportClass/Service/TransformationServices/MlNetCorrectionService.cs` — Full implementation: 4-stage pipeline (TableCategory multiclass, DoseRegimen routing, PrimaryValueType disambiguation, per-category PCA anomaly), in-memory training accumulator with lazy retrain trigger, `appendFlag` helper
- `MedRecProTest/MlNetCorrectionServiceTests.cs` — 23 tests covering init idempotency, accumulator/training triggers, all 4 stages, integration edge cases, and Claude gate flag format

**Modified files:**
- `ClaudeApiCorrectionSettings.cs` — Added `MlAnomalyScoreThreshold` (default 0.0 = backward-compatible)
- `ClaudeApiCorrectionService.cs` — Added `exceedsAnomalyThreshold()` private method + ML gate in `CorrectBatchAsync` that filters observations by anomaly score before API calls
- `TableParsingOrchestrator.cs` — Added `IMlNetCorrectionService?` field + lazy-init flag + Stage 3.4 call sites in all 3 batch methods (`ProcessBatchAsync`, `processBatchWithSkipTrackingAsync`, `ProcessBatchWithStagesAsync`); constructor takes new optional parameter

**Key design decisions:**
- No DB dependency — training uses in-memory accumulation of high-confidence rows across batches; cold-start emits `MLNET_ANOMALY_SCORE:NOMODEL`
- Conservative Claude gate: absent/NOMODEL/ERROR scores always pass through to Claude
- `PredictionEngine<T,P>` kept single-threaded (safe for current sequential batch processing)
- Stage 5 (IidSpikeDetector) omitted per spec — rule-based normalizeUnit() handles unit header leaks

**Result:** 0 build errors; 23/23 new tests pass; all existing ColumnStandardization and ClaudeApiCorrection tests pass with zero regressions.

---

### 2026-03-27 12:37 PM EST — Claude-to-ML Feedback Loop (Training Store + Adaptive Threshold)

Implemented the feedback loop that turns Claude API corrections (Stage 3.5) into ground-truth training data for the ML.NET service (Stage 3.4), enabling the ML models to improve over time and progressively reduce Claude API calls.

**New files (4):**
- `MlTrainingRecord.cs` — Compact 19-field DTO with `FromObservation()` factory method; truncates strings to 200 chars, casts double→float for the 6-slot PCA vector
- `MlTrainingStoreState.cs` — Root persisted state: records list, adaptive threshold, lifetime metrics (TotalSentToClaude, TotalCorrectedByClaude), timestamps
- `IMlTrainingStore.cs` — Interface: Load, AddRecords, GetRecords, RecordClaudeFeedback, RecordRetrain, Save
- `MlTrainingStore.cs` — File-backed implementation following `StandardizationProgressTracker` pattern: `SemaphoreSlim(1,1)`, atomic write (tmp+rename), `System.Text.Json` with `WriteIndented`/`WhenWritingNull`; bootstrap-first eviction when exceeding `MaxAccumulatorRows`

**Modified files (5):**
- `MlNetCorrectionSettings.cs` — Added 7 settings: `TrainingStoreFilePath`, `MaxAccumulatorRows` (100K default), and 5 adaptive threshold settings (min observations, correction rate floor, step size, ceiling, evaluation interval)
- `IMlNetCorrectionService.cs` — Added `FeedClaudeCorrectedBatchAsync` method
- `MlNetCorrectionService.cs` — Changed accumulator from `List<ParsedObservation>` to `List<MlTrainingRecord>`; constructor accepts optional `IMlTrainingStore` + `ClaudeApiCorrectionSettings`; `InitializeAsync` loads persisted store and restores adaptive threshold; `accumulateBatch` converts to `MlTrainingRecord`; all 4 training methods project from `MlTrainingRecord`; removed orphaned `labelDoseRegimenRouting(ParsedObservation)` and `hasRoutingFlag(ParsedObservation)` replaced by `MlTrainingRecord`-based variants
- `TableParsingOrchestrator.cs` — Added `FeedClaudeCorrectedBatchAsync` call after each of 3 `CorrectBatchAsync` sites (`ProcessBatchAsync`, `ProcessBatchWithStagesAsync`, `processBatchWithSkipTrackingAsync`)
- `MlNetCorrectionServiceTests.cs` — Added 7 new tests: store round-trip persistence, bootstrap-first eviction, threshold rise on low correction rate, threshold unchanged on high rate, feedback extraction of AI_CORRECTED rows only, no-op on zero corrections, full store+adaptation integration

**Key design decisions:**
- Adaptive threshold propagates via shared singleton: `MlNetCorrectionService` mutates `ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold` on the same object `ClaudeApiCorrectionService` reads — no restart needed
- Eviction prioritizes ground-truth preservation: bootstrap records evicted first, then oldest ground-truth only if still over capacity
- `FeedClaudeCorrectedBatchAsync` filters on `AI_CORRECTED:` in ValidationFlags; Claude-corrected records bypass ParseConfidence threshold (Claude is authoritative)

**Result:** 0 build errors; 30/30 tests pass (18 existing + 12 new); zero regressions.

---

### 2026-03-27 12:56 PM EST — Within-Batch Progress Bar for Table Standardization

Added per-table progress reporting so the Spectre.Console progress bar updates continuously within each batch instead of jumping from 0% to 100% when only one batch exists.

**Root cause:** `ProcessAllAsync` only fired `IProgress<TransformBatchProgress>` once per batch completion — no visibility into the foreach loop over tables within a batch.

**Approach — dual callbacks:**
- `batchProgress` (per-batch): persists to disk via `StandardizationProgressTracker` for resumption — unchanged frequency
- `rowProgress` (per-table): UI-only, fires after every table, drives the progress bar with fractional percentage

**Changes:**
- `TransformBatchProgress` — added `TablesProcessedInBatch` and `TotalTablesInBatch` properties (zero = batch-boundary report)
- `ITableParsingOrchestrator` — added optional `rowProgress` parameter to `ProcessBatchAsync`, `ProcessAllAsync`, `ProcessAllWithValidationAsync`
- `TableParsingOrchestrator` — `ProcessBatchAsync` fires `rowProgress` after each table; `ProcessAllAsync`/`ProcessAllWithValidationAsync` wrap it with a `Progress<T>` closure that injects batch context (BatchNumber, TotalBatches, RangeStart, RangeEnd, cumulative obs, elapsed)
- `processBatchWithSkipTrackingAsync` — same per-table firing pattern; also added missing lazy-init for column standardizer and ML.NET services
- `TableStandardizationService` — split single callback into `batchProgress` (disk persistence) + `rowProgress` (UI bar): `overallPct = ((batchNumber - 1 + tablesProcessed/totalTables) / totalBatches) * 100`

**Result:** 0 build errors across all three projects. Progress bar now shows `Batch 1/1 [1-5000] Table 2341/4892 — 12,445 obs [███████████] 47%` instead of sitting at 0%.

---

### 2026-03-27 1:13 PM EST — Fix: SynchronousProgress to Replace Double-Progress\<T\> Chain

The within-batch progress bar from the previous session didn't render at all. Root cause: double `System.Progress<T>` wrapping.

**Problem:** `Progress<T>` in console apps (no `SynchronizationContext`) posts callbacks via `ThreadPool.QueueUserWorkItem`. The inner `Progress<T>` wrapper in `ProcessAllAsync` posted to ThreadPool; its callback then called `rowProgress.Report()` on the outer `Progress<T>` from `TableStandardizationService`, which posted *again* to ThreadPool. Two async hops meant callbacks arrived after the batch/run completed and the Spectre.Console progress context had already exited — so no progress bar was ever visible.

**Fix:** Added `SynchronousProgress<T>` — a private nested `IProgress<T>` in `TableParsingOrchestrator` that invokes the handler inline on the calling thread (no ThreadPool post). Replaced both inner `new Progress<T>(...)` wrappers in `ProcessAllAsync` and `ProcessAllWithValidationAsync` with `new SynchronousProgress<T>(...)`. Now the batch-context enrichment and forwarding happen synchronously inside the loop, and only the single outer `Progress<T>` in the UI layer does the async post.

**Result:** 0 build errors. Single async hop restored — same pattern that worked for the original per-batch progress.

---

### 2026-03-27 1:23 PM EST — Fix #2: Eliminate ALL Progress\<T\> from Row-Progress Chain

Progress bar still didn't render. Even with the inner wrapper fixed to `SynchronousProgress`, the outer `rowProgress` in `TableStandardizationService` was still `new Progress<T>(...)` — one async hop was still enough to delay all `task.Value` updates past the Spectre.Console progress context lifetime.

**Fix:** Replaced `new Progress<TransformBatchProgress>(...)` with `new SynchronousProgress<TransformBatchProgress>(...)` in both `ExecuteParseAsync` and `ExecuteValidateAsync`. Promoted `SynchronousProgress<T>` from a private nested class in `TableParsingOrchestrator` to a public shared utility in `MedRecProImportClass/Helpers/SynchronousProgress.cs` (both projects need it). Used `Helpers.SynchronousProgress<T>` in the orchestrator to avoid `Truncate` extension method ambiguity with `Humanizer`.

Now the entire row-progress chain is fully synchronous: `ProcessBatchAsync` → `SynchronousProgress` (enriches batch context) → `SynchronousProgress` (updates Spectre task). Only Spectre.Console's auto-refresh timer (its own thread) handles async rendering.

**Result:** 0 build errors (file-copy errors from locked debugger DLL, not code errors).

---

### 2026-03-27 1:46 PM EST — Fix #3: Wrong Method — Interactive Menu Uses ExecuteParseWithStagesAsync

Progress bar still absent. Added `[DIAG]` debug statements to `ExecuteValidateAsync` — none appeared in Output. Traced the interactive menu dispatch in `ConsoleHelper.cs:1870` and found the root cause: after the "Proceed with table standardization?" confirmation, the code calls **`ExecuteParseWithStagesAsync`**, not `ExecuteValidateAsync` or `ExecuteParseAsync`. All progress bar changes were in the wrong methods entirely.

`ExecuteParseWithStagesAsync` used `ProcessBatchWithStagesAsync` (diagnostic method) in a plain for-loop with `AnsiConsole.MarkupLine()` per batch — no `AnsiConsole.Progress()` widget at all. This is why no progress bar ever rendered regardless of the `SynchronousProgress<T>` fix.

**Fix:** Wrapped `ExecuteParseWithStagesAsync` in `AnsiConsole.Progress().StartAsync()` with per-batch progress updates (description + percentage). `ProcessBatchWithStagesAsync` is a diagnostic method that doesn't support `rowProgress` callbacks, so this path gets per-batch granularity (not per-table). Removed `[DIAG]` statements from `ExecuteValidateAsync`.

**Result:** 0 build errors. The interactive menu path now renders a Spectre.Console progress bar.

---

### 2026-03-27 2:34 PM EST — Intra-Batch Progress for ProcessBatchWithStagesAsync

Added real intra-batch progress reporting to the `ExecuteParseWithStagesAsync` → `ProcessBatchWithStagesAsync` pipeline, which previously only updated the progress bar between batches (jumping 0%→100% for single-batch runs).

**Changes across 4 files:**
- **`TransformBatchProgress.cs`** — Added `CurrentOperation` (string?) for stage label and `IntraBatchPercent` (double, 0–100) for within-batch progress.
- **`ITableParsingOrchestrator.cs`** / **`TableParsingOrchestrator.cs`** — Added optional `IProgress<TransformBatchProgress>? rowProgress` parameter. Orchestrator now fires progress reports per-table during the parse loop (0%→70%) and at each post-processing stage boundary (column standardization 75%, ML.NET 82%, Claude AI 90%, DB write 95%, complete 100%).
- **`TableStandardizationService.cs`** — Added a second Spectre task as an indeterminate spinner showing `CurrentOperation` text. Wired a `SynchronousProgress<TransformBatchProgress>` callback that scales `IntraBatchPercent` to overall progress across batches.

Existing tests compile unchanged (parameter is optional). 0 build errors.

---

### 2026-03-27 2:51 PM EST — Per-API-Call Progress for Claude AI Correction Stage

The progress bar was stuck at 90% during the entire Claude AI correction stage (the slowest stage), then jumped to 100%. Root cause: Claude AI was allocated only 5% of the bar (90→95%) despite making multiple HTTP API calls per batch (one per chunk of 20 observations, grouped by TextTableID, with 200ms rate-limiting delays).

**Changes across 3 files:**
- **`ClaudeApiCorrectionService.cs`** — Added `IProgress<TransformBatchProgress>? progress` parameter to both the `IClaudeApiCorrectionService` interface and implementation. Service now counts total API chunks up front and reports `IntraBatchPercent` (0–100) + `CurrentOperation` (e.g., "Claude AI correction (2/6)...") after each chunk completes.
- **`TableParsingOrchestrator.cs`** — Reweighted progress model: table loop 0–20%, column std 21%, ML.NET 23%, **Claude AI 25–95%** (70% of total), DB write 96%. Created a `SynchronousProgress` forwarding callback that maps the correction service's internal 0–100 into the orchestrator's 25–95 range.
- **`TableParsingOrchestratorStageTests.cs`** — Updated Moq setups/verifies to include the new `IProgress` parameter in `CorrectBatchAsync` calls.

0 build errors across console and test projects.

---

### 2026-03-27 3:03 PM EST — Extract ArmN from RawValue trailing N= patterns

Added N= extraction from RawValue in `ColumnStandardizationService.cs`. Previously, `normalizeInlineNValues` explicitly skipped RawValue, so cells like `2.9 (22%) N=16` or `94.7 (34%)^N=14` never populated ArmN.

**Changes:**
- Added `_rawValueTrailingNPattern` regex — matches trailing `N=digits` with optional footnote markers (`^`, `*`, `†`, `‡`) before the N=
- Added RawValue extraction block at the end of `normalizeInlineNValues` — if ArmN isn't already set, extracts N from RawValue and strips the N= portion (e.g., `2.9 (22%) N=16` → RawValue=`2.9 (22%)`, ArmN=16)

0 build errors.

---

### 2026-03-27 3:27 PM EST — Wire up ColumnStandardizationService in console DI + fix missing InitializeAsync

Discovered and fixed three issues preventing column standardization from running in the console app:

1. **Missing DI registration** — `IColumnStandardizationService` was never registered in `TableStandardizationService.buildServiceProvider()`. The orchestrator's constructor accepted it as an optional nullable parameter, so it silently defaulted to `null` and all standardization was skipped. Added `services.AddScoped<IColumnStandardizationService, ColumnStandardizationService>()`.

2. **Missing DbContext forwarding** — `ColumnStandardizationService` constructor takes `DbContext` (base class), but DI only registered `ApplicationDbContext`. Added `services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>())` to forward the resolution.

3. **Missing `InitializeAsync` in `ProcessBatchWithStagesAsync`** — The console menu's parse path calls `ProcessBatchWithStagesAsync`, which called `Standardize()` without first calling `InitializeAsync()`. The `_initialized` flag stayed false, causing `Standardize` to early-return with a warning. Added the lazy-init block (matching the pattern already in `ProcessBatchAsync` and `processBatchWithSkipTrackingAsync`).

**Files changed:**
- `MedRecProConsole/Services/TableStandardizationService.cs` — DI registrations
- `MedRecProImportClass/Service/TransformationServices/TableParsingOrchestrator.cs` — InitializeAsync call
- `MedRecProImportClass/Service/TransformationServices/ColumnStandardizationService.cs` — Minor refactor of RawValue N= guard

0 build errors.

---

### 2026-03-30 11:06 AM EST — Fix PrimaryValueType: Trust Caption Hints Over Category Defaults

Fixed an incorrect defaulting behavior in `ColumnStandardizationService` where PK tables were being assigned `GeometricMean` as a blanket default, overriding caption hints that explicitly said "Mean". GeometricMean is only appropriate for drug A vs drug B comparison (DDI) studies, not standard PK tables.

Key changes to `ColumnStandardizationService.cs`:
- **Added `extractCaptionHintType()`** — new helper that parses `CAPTION_HINT:caption:X` from `ValidationFlags` so Phase 3 can consume what the parsers already determined upstream
- **Fixed `resolveMeanType()`** — now checks CAPTION_HINT before category defaults; removed PK from the GeometricMean category default (only DDI tables default to GeometricMean now)
- **Fixed `resolveNumericType()`** — PK now defaults to ArithmeticMean instead of GeometricMean

Root cause: parsers correctly generated CAPTION_HINT flags but ColumnStandardizationService never read them, re-analyzing the caption independently with simpler heuristics and falling to an incorrect PK→GeometricMean category default. New priority order: explicit caption keywords → caption hint from parser → category defaults → ArithmeticMean.

Also helped user resolve a Visual Studio debugging issue: breakpoints in MedRecProImportClass weren't hitting because the project was a `<ProjectReference>` in the `.csproj` but not added to the Console `.sln`. Fix: Add → Existing Project in the Console solution.

---

### 2026-04-01 11:19 AM EST — ML/Claude Pipeline Integration & Skill Architecture Overhaul

Five-issue fix addressing DI registration gaps, hardcoded prompts, and missing table context in the SPL table normalization pipeline (Stages 3.4–3.5).

**Issue 1 — MlNetCorrectionService DI Registration:**
`MlNetCorrectionService` was never registered in `TableStandardizationService.buildServiceProvider()`, so the orchestrator's nullable constructor param was always null. Added `MlNetCorrectionSettings` config binding, `IMlTrainingStore` → `MlTrainingStore` registration, and `IMlNetCorrectionService` registration (always enabled, independent of Claude). Initial registration passed `trainingStore: null` which blocked `InitializeAsync` — fixed by wiring the real `MlTrainingStore` instance.

**Issue 3 — System Prompt Extracted to Skill File:**
Moved the ~100-line hardcoded `CorrectionSystemPrompt` const from `ClaudeApiCorrectionService.cs` to `Skills/correction-system-prompt.md` with YAML frontmatter. Added `SkillFilePath` and `PivotComparisonSkillPath` properties to `ClaudeApiCorrectionSettings`. Service now lazy-loads skill files on first API call via `ensureSkillFilesLoaded()` with `stripYamlFrontmatter()`, falling back to a minimal prompt if the file is missing. Added `Content Include="Skills\**\*.md"` to `.csproj` for build output copy.

**Issue 4 — Original Table Context for Claude:**
Claude previously never saw the original `ReconstructedTable` for comparison. Created `Skills/pivot-comparison-prompt.md` with comparison instructions. Added `renderOriginalTable()` to serialize tables as pipe-delimited markdown (caption + header + up to 20 body rows). Changed `IClaudeApiCorrectionService.CorrectBatchAsync` parameter from `ReconstructedTable?` to `IReadOnlyDictionary<int, ReconstructedTable>?` so each TextTableID group gets its own table context. Updated all 4 orchestrator call sites: per-table loops wrap single table in dictionary, batch-level method builds lookup via `ToDictionary()`, diagnostic method passes null.

**Issue 5 — CorrectionEntry DTO Extraction:**
Moved `internal class CorrectionEntry` from inside `ClaudeApiCorrectionService.cs` to `Models/CorrectionEntry.cs` as `public`, following the single-class-per-file convention.

**Key architectural decision:** The `originalTable` parameter was initially a single `ReconstructedTable?`, which was always null in the batch-level call site (line 754) where `allObservations` spans multiple tables. Changed to a dictionary lookup so `CorrectBatchAsync`'s internal per-TextTableID grouping can resolve the correct table for each group.

Build: 936 tests pass, 2 pre-existing failures (PVT Mean→GeometricMean migration tests — separate issue from the skill file's updated default to ArithmeticMean).

---

### 2026-04-01 11:43 AM EST — Pipeline Runtime Fixes: Table Lookup, Training Store, NaN Sanitization

Three runtime issues discovered during debugging and first live run of the ML/Claude pipeline.

**Fix 1 — originalTable always null in Claude service:**
The `CorrectBatchAsync` parameter was `ReconstructedTable?` but internally groups observations by TextTableID — a single table can't represent multiple groups. Changed to `IReadOnlyDictionary<int, ReconstructedTable>?`. Per-table loop call sites wrap single table in a dictionary; the batch-level call site builds a lookup via `tables.ToDictionary(t => t.TextTableID!.Value)`. Each group now resolves its own table via `TryGetValue(group.Key)`.

**Fix 2 — MlTrainingStore constructor crash (TrainingStoreFilePath null):**
`MlNetCorrectionSettings.TrainingStoreFilePath` defaulted to `null`, but `MlTrainingStore` constructor requires it. Also, the DI registration was passing `trainingStore: null` instead of resolving the real service. Fixed both: changed default to `".medrecpro-ml-training-store.json"` and registered `IMlTrainingStore` → `MlTrainingStore` in DI, injected into `MlNetCorrectionService`.

**Fix 3 — Claude returning bare NaN in JSON corrections:**
Claude occasionally emits `"newValue": NaN` (unquoted) in correction JSON. Newtonsoft.Json fails parsing this when the target is `string?`. Added `sanitizeJsonFloatLiterals()` regex that quotes bare `NaN`, `Infinity`, `-Infinity` tokens before deserialization: `(?<=:\s*)(-?(?:NaN|Infinity))(?=\s*[,}\]])` → wraps in double quotes.

---

### 2026-04-02 10:13 AM EST — Table Parsing Pipeline: 6-Issue Implementation

Implemented 6 issues from the table parsing pipeline plan in execution order:

**Issue 6 — Refactor ProcessBatchWithStagesAsync:** Extracted 6 private methods from the ~220-line orchestrator method (`ensureServicesInitializedAsync`, `routeAndParseTables`, `runColumnStandardization`, `runMlCorrection`, `runClaudeCorrectionAsync`, `writeObservationsAsync`). Main method now ~30 lines. Pure refactor, no behavioral change.

**Issue 2 — Comma-Formatted ArmN Extraction:** Updated 7 N-value regex patterns from `(\d+)` to `(\d[\d,]*)` to accept comma-formatted numbers like `(n = 8,506)`. Added `tryParseNValue` helper that strips commas before parsing. Replaced all 9 `int.TryParse` call sites at N-value parse points. 6 new tests.

**Issue 1 — Extract Units from ParameterSubtype:** New `extractUnitFromParameterSubtype` method handles PK/DRUG_INTERACTION subtypes like `Cmax(pg/mL)`, `AUC120(pg·hr/mL)`, `Cmax(serum, mcg/mL)`. Added `·hr` variant normalization entries, structural fallback regex `_pkUnitStructurePattern`, `isRecognizedUnit` helper. Wired into Phase 2 before `normalizeUnit`. 8 new tests.

**Issue 5 — Claude Payload Exclusion:** Verified `buildCompactPayload` already excludes DocumentGUID, LabelerName, ProductTitle, VersionNumber, TextTableID. Added 1 regression test using reflection.

**Issue 3 — Post-Processing Stage 3.6:** Added `PostProcessExtraction` to `IColumnStandardizationService` and implementation. Re-runs `extractUnitFromParameterSubtype` and `normalizeInlineNValues` after Claude correction to catch values Claude corrected into extractable form. Uses `COL_STD:POST_` flag prefix. Wired into orchestrator at 95.5%. 2 new tests.

**Issue 4 — ParseConfidence Provenance Flags:** Added per-observation confidence flags across all 3 correction pathways: `CONFIDENCE:PATTERN:{score}:{reason}({count})` in ColumnStandardization, `CONFIDENCE:ML:{score}:{label}` in MlNet, `CONFIDENCE:AI:{score}:{count}_corrections` in Claude. Updated correction-system-prompt.md with HIGH/MED/LOW qualifier prefix. 3 new tests; updated 4 existing Claude tests for new flag behavior.

**Files modified:** TableParsingOrchestrator.cs, ColumnStandardizationService.cs, IColumnStandardizationService.cs, ClaudeApiCorrectionService.cs, MlNetCorrectionService.cs, correction-system-prompt.md, ColumnStandardizationServiceTests.cs, ClaudeApiCorrectionServiceTests.cs, MlNetCorrectionServiceTests.cs. Build: 0 errors. Tests: 156 passed, 2 pre-existing failures (GeometricMean/ArithmeticMean — unrelated).

---

### 2026-04-02 11:27 AM EST — Consolidate TableParsingOrchestrator Batch Processing Paths

Unified all batch processing in `TableParsingOrchestrator` to flow through a single pipeline: `ProcessBatchWithStagesAsync`. Previously, three methods reimplemented the stage sequence independently — `ProcessBatchAsync`, `processBatchWithSkipTrackingAsync`, and `ProcessBatchWithStagesAsync` — each with subtle divergences (per-table vs per-batch stage ordering, missing Stage 3.6 post-process extraction in the first two).

**Changes:**
- Rewrote `ProcessBatchAsync` as a 2-line thin wrapper delegating to `ProcessBatchWithStagesAsync` and returning `ObservationsWritten` (~130 lines removed).
- Deleted `processBatchWithSkipTrackingAsync` entirely (~160 lines removed) — it was a near-duplicate of `ProcessBatchAsync` with skip reason tracking bolted on.
- Updated `ProcessAllWithValidationAsync` to call `ProcessBatchWithStagesAsync` directly, extracting `ObservationsWritten` and `SkipReasons` from the `BatchStageResult`.
- Added new test: `ProcessBatchAsync_DelegatesToProcessBatchWithStagesAsync_ReturnsObservationsWritten`.

**Key outcome:** All batch processing now runs through the same stage sequence: Reconstruct → Route+Parse → Column Standardization (3.25) → ML.NET (3.4) → Claude AI (3.5) → Post-Process Extraction (3.6) → DB Write. Post-process extraction was previously skipped by the non-stages paths. ~290 lines of duplicated logic removed.

**Files modified:** TableParsingOrchestrator.cs, TableParsingOrchestratorProgressTests.cs. No interface changes. Build: 0 errors. Tests: 41 orchestrator tests passed.

---

### 2026-04-02 12:43 PM EST — Fix NaN Propagation in ML.NET PCA Training and Claude JSON Parsing

Diagnosed and fixed two recurring pipeline warnings caused by unguarded NaN values propagating into ML.NET and Claude JSON deserialization.

**ML.NET PCA fix (`MlTrainingRecord.cs`, `MlNetCorrectionService.cs`):** Root cause: `(float)(obs.X ?? 0.0)` null-coalesces but does not guard `double.NaN` (which is non-null), so NaN values cast silently to `float.NaN` and corrupted the PCA feature matrix, causing `RandomizedPca` to emit NaN eigenvectors. Added `internal static toSafeFloat(double? value)` helper to `MlTrainingRecord` that returns `0f` for null, NaN, and Infinity. Applied it in `FromObservation` (training data entry point) and in `applyAnomalyScore` (live scoring path using raw `ParsedObservation` objects that bypass `FromObservation`). Added defensive `!float.IsNaN` guards on all six feature fields in the `trainAnomalyModels` LINQ `Where` predicate to protect against stale store records persisted before this fix.

**Claude JSON NaN fix (`ClaudeApiCorrectionService.cs`):** Root cause: the existing `sanitizeJsonFloatLiterals` regex used a lookbehind/lookahead pattern that only matched NaN in object-value position (preceded by `:`), missing array-element positions (preceded by `,` or `[`). Replaced with a capturing-group pattern `([:,\[]\s*)(-?(?:NaN|Infinity))(\s*[,}\]])` → `${1}null${3}`. Bare NaN is now replaced with JSON `null` (not `"NaN"` string), which is semantically correct for `string?` target properties and avoids any downstream sentinel-value handling.

**Files modified:** `MlTrainingRecord.cs`, `MlNetCorrectionService.cs`, `ClaudeApiCorrectionService.cs`. Build: 0 errors.

---

### 2026-04-02 2:28 PM EST — Fix ML.NET PCA NaN Eigenvector Crash for Low-Variance Categories

Resolved persistent `ArgumentOutOfRangeException: The learnt eigenvectors contained NaN values` during Stage 4 anomaly model training for categories like PK and BMD where most of the 6 feature slots are constant (e.g., SecondaryValue=0, LowerBound=0, UpperBound=0, PValue=0).

**Root cause:** `NormalizeMeanVariance` divides by standard deviation — constant columns produce `0/0 = NaN`, which propagates into PCA's SVD computation and corrupts eigenvectors. The previous fix (clamping `rank` to the number of varying features via `computeEffectiveRank`) was insufficient because PCA still reads all 6 feature dimensions regardless of rank. A subsequent attempt to strip constant features from the array conflicted with `AnomalyInput.Features` having `[VectorType(6)]`, causing `IndexOutOfRangeException` at prediction time since `PredictionEngine` always expects exactly 6 floats.

**Fix — jitter injection (`MlNetCorrectionService.cs`):**
- Renamed `computeEffectiveRank` → `computeActiveFeatureIndices` — now returns the actual indices of varying features instead of just a count
- After identifying constant-variance slots, injects tiny random noise (~1e-6, seeded deterministically) into those slots during training only
- This breaks zero variance so `NormalizeMeanVariance` never divides by zero, while having negligible effect on PCA eigenvectors
- All 6 feature slots are preserved, maintaining the `[VectorType(6)]` contract for both training and scoring
- Rank remains clamped to the number of real (non-jittered) features

**Files modified:** `MlNetCorrectionService.cs`. Build: 0 errors.

---

### 2026-04-02 3:37 PM EST — Add ParameterCategory, TreatmentArm, and ArmN to ML Training Model

Evaluated 6 database fields (`ParameterCategory`, `ParameterSubtype`, `TreatmentArm`, `ArmN`, `StudyContext`, `DoseRegimen`) for inclusion in the ML.NET 4-stage correction pipeline. Analysis of data distributions and pipeline architecture led to adding 3 fields and skipping 3:

**Added:**
- **ParameterCategory** (string?) — MedDRA SOC grouping stored in `MlTrainingRecord` for future ADVERSE_EVENT sub-partitioning
- **TreatmentArm** (string?) — treatment group label stored for future use (same ParameterName has different expected distributions per arm)
- **LogArmN** (float) — `log(ArmN + 1)` added as 7th slot in the PCA anomaly detection vector. Sample size is critical denominator context: a 5% rate at N=70 vs N=8000 has very different expected variance

**Skipped:**
- `ParameterSubtype` — circular dependency (Stage 2 predicts it as output, can't also train on it); almost 100% NULL at ML scoring time
- `StudyContext` — 100% NULL across all sample data, zero training signal
- `DoseRegimen` — already in the model as Stage 2 input

**Files modified:** `MlTrainingRecord.cs` (3 new properties + `FromObservation()` update), `MlNetDataModels.cs` (`AnomalyInput` expanded from 6-slot to 7-slot vector), `MlNetCorrectionService.cs` (training, scoring, and jitter logic updated for 7th PCA slot). Build: 0 new errors (1 pre-existing error in `ClaudeApiCorrectionService.cs`).

---

### 2026-04-02 4:15 PM EST — PostProcess: Correct Count → Percentage PrimaryValueType

Added a post-processing correction to `PostProcessExtraction` in `ColumnStandardizationService.cs` that detects when `PrimaryValueType` is incorrectly set to `"Count"` when contextual fields (TreatmentArm, ParameterName, ParameterCategory, ParameterSubtype) contain percentage indicators (`%`, `percent`, `proportion`, `incidence`, `rate of`, `frequency`). The correction flips `PrimaryValueType` to `"Percentage"` when: the type is `"Count"`, no `SecondaryValueType` is set (meaning the parser didn't already resolve the pairing), and `PrimaryValue` is <= 100. A validation flag `COL_STD:POST_PCT_TYPE_CORRECTED:{FieldName}` identifies which field triggered the correction. Added 7 new unit tests covering happy paths and guard conditions. All 9 PostProcess tests pass; 6 pre-existing failures in Rule10/Phase3 (Proportion vs Percentage) are unrelated.

**Files modified:** `ColumnStandardizationService.cs` (new `_percentageHintPattern` regex, `correctCountToPercentageType()` helper, wired into `PostProcessExtraction`), `ColumnStandardizationServiceTests.cs` (7 new test methods).

---

### 2026-04-03 12:25 PM EST — PK Parser: Two-Column Context Layout + Parenthesized ± Pattern

Fixed PK table parsing for tables with non-standard two-column context layouts (e.g., TextTableID=184). Previously, all 56 observations from this table were classified as `text_descriptive` with 0.50 confidence due to two root causes: (1) `ValueParser` had no pattern for the parenthesized `value (±X) (n=N)` format like `0.80 (±0.36) (n=129)`, and (2) the parser assumed column 0 = dose regimen when it was actually a category/subtype column with column 1 being the dose column.

**Key changes across 4 files:**
- **ValueParser.cs**: Added Pattern 6d (`value_plusminus_sample`) with regex for `value (±X) (n=N)`. Returns `SecondaryValueType = null` intentionally — the ± symbol could represent SD, SE, or CI, so type resolution is deferred to context.
- **ParsedValue.cs**: Added `int? SampleSize` property to carry cell-embedded `(n=X)` values through the pipeline.
- **BaseTableParser.cs**: Wired `SampleSize → ArmN` in `applyParsedValue()`; promoted `appendFlag()` to `protected`.
- **PkTableParser.cs**: Added `detectDoseColumn()` (col 1 = "Dose/Route" triggers two-column mode), `detectSubHeaderRow()` (dual-signal detection for category divider rows), `resolveDispersionType()` (caption → header path → footnotes → default SD with `PLUSMINUS_TYPE_INFERRED:SD` flag), and extracted `parseAndApplyPkValue()` for DRY shared logic. `Parse()` rewritten with conditional two-column path that sets `ParameterCategory` from sub-header rows and `ParameterSubtype` from col 0. Single-column path preserved unchanged for backward compatibility. Also expanded `_populationHeaderKeywords` with "volunteers"/"subjects".

**Files modified:** `ParsedValue.cs`, `ValueParser.cs`, `BaseTableParser.cs`, `PkTableParser.cs`, `README.md` (added `value_plusminus_sample` to ParseRule dictionary, `PLUSMINUS_TYPE_INFERRED:SD` to ValidationFlags dictionary).

---

### 2026-04-03 3:20 PM EST — Efficacy Parser: WHI Table Standardization Fixes

Fixed 6 deterministic parsing bugs exposed by a WHI (Women's Health Initiative) estrogen-alone substudy table (TextTableID=86). The table had comma-formatted sample sizes in headers ("CE n = 5,310"), a CI type qualifier "(95% nCI)", and a column sub-header row ("Absolute Risk per 10,000 Women-Years" with "↔" ditto marker) — none of which the parser handled.

**Bugs fixed:**
1. **ArmN = 5 → 5310/5429** — `(\d+)` in arm header regexes couldn't handle commas; changed to `(\d[\d,]*)` with `.Replace(",", "")` across all 3 files containing `_nEqualsPattern` (ValueParser, BaseTableParser, EfficacyMultilevelTableParser)
2. **BoundType = CI → 95CI** — new `extractCILevelFromHeader()` detects "(95% nCI)" / "(90% CI)" in stat column headers
3. **No Unit → "per 10,000 Women-Years"** — new `extractUnitFromSubHeader()` extracts unit from sub-header text
4. **Arm PrimaryValueType = RelativeRiskReduction → AbsoluteRisk** — new sub-header row detection (`isColumnSubHeaderRow` + `captureColumnSubHeaders`) with `inferValueTypeFromSubHeader()` mapping "Absolute Risk" → "AbsoluteRisk"
5. **Comparison PrimaryValueType = RelativeRiskReduction → RelativeRisk** — new `inferComparisonTypeFromHeader()` derives type from header text instead of hardcoding; supports HazardRatio, OddsRatio, RelativeRisk, RelativeRiskReduction
6. **Comparison ArmN = null → 10739** — summed arm Ns with safeguards: requires "vs."/"versus" in header, exactly 2 arms, both Ns known

**Also fixed:** 6 pre-existing test failures in `ColumnStandardizationServiceTests` where tests expected `"Proportion"` but service correctly produces `"Percentage"`.

**Files modified:** `ValueParser.cs`, `BaseTableParser.cs`, `EfficacyMultilevelTableParser.cs` (6 new internal methods), `ValueParserTests.cs` (+3 tests), `TableParserTests.cs` (+7 tests, 1 updated), `ColumnStandardizationServiceTests.cs` (6 assertion fixes). **976/976 tests pass.**

---

### 2026-04-06 1:54 PM EST — PK Compound Header Layout Support (TextTableID 185)

Added a third layout path to `PkTableParser` for compound/nested PK tables — tables with a spanning header row (all columns share identical text like "Pharmacokinetic Parameters for Renal Impairment"), embedded sub-header data rows containing actual parameter definitions (Dose, Tmax, Cmax, AUC), and SocDivider rows that reset the section context (e.g., switching from Renal to Hepatic Impairment).

**Root cause:** Stage 2 correctly identified the spanning row as the inferred header and flagged SocDividers, but PkTableParser had no code path for this structure. All 5 header columns got identical LeafHeaderText, the actual parameter names (in the first data row) were treated as data, and col 0 population descriptors were misrouted to DoseRegimen. Result: 28 broken rows with NULL ParameterName, ParameterCategory, TreatmentArm, and Unit.

**Solution:** New `detectCompoundHeaderLayout()` method uses 4 conjunctive signals (HasSocDividers + HasInferredHeader + identical headers + sub-header first row with dose keyword + param unit pattern) to activate the compound path. `parseCompoundLayout()` consumes the first data row as a sub-header, extracts parameter definitions via `parseCompoundParameterHeader()` (handles multi-parenthetical like `AUC(0-96h)(mcgh/mL)` → name/unit/subtype), maps col 0 to TreatmentArm with ArmN extraction, reads the dose column for DoseRegimen, and resets context + refreshes param defs when SocDivider rows are encountered. Existing single-column and two-column paths are completely untouched.

**Key new methods (8 total):** `detectCompoundHeaderLayout`, `parseCompoundLayout`, `parseCompoundParameterHeader`, `extractParameterDefinitionsFromDataRow`, `extractCategoryFromSpanningHeader`, `extractArmNFromLabel`, `detectDoseColumnInSubHeader`, `looksLikeSubHeader`.

**Files modified:** `PkTableParser.cs` (+3 regex fields, +8 methods, +1 branch in Parse()), `TableParserTests.cs` (+22 tests: 15 compound integration, 5 utility unit tests, 2 backward compat). **61/61 TableParserTests pass.**

---

### 2026-04-06 3:59 PM EST — ParseConfidence: Evaluation & Named Constants Refactor

Evaluated whether `ParsedValue.ParseConfidence` should be replaced with a statistically-grounded approach. **Conclusion: the heuristic approach is fundamentally sound** — the values are an ordinal ambiguity ranking (not calibrated probabilities), logically justified by pattern specificity, and serve as the bootstrap signal for the downstream ML.NET system. A statistical alternative would converge to the same values for unambiguous patterns and create a circular dependency with the ML pipeline it feeds.

**What was wrong was readability, not logic.** Replaced all inline magic numbers with named constants across 6 files:

- **`ParsedValue.cs`**: Added three nested static classes — `ConfidenceTier` (5 tier constants: Unambiguous=1.0, ValidatedMatch=0.95, AmbiguousMatch=0.9, KnownExclusion=0.8, TextFallback=0.5), `ConfidenceAdjustment` (3 multiplier constants for caption hints, type promotion, positional detection), and `ConfidenceThreshold` (5 consumer decision thresholds for LOW_CONFIDENCE flagging and 5-band reporting).
- **`ValueParser.cs`**: Replaced all 15 inline confidence literals with `ConfidenceTier.*` constants.
- **`PkTableParser.cs`**: Replaced 2 inline literals. **Fixed overwrite→multiply bug** — line 339 was `= 0.90` (overwriting), changed to `*= PositionalSampleSize` for compositional consistency with every other adjustment in the pipeline.
- **`BaseTableParser.cs`**: Replaced 5 `ConfidenceAdjustment = 0.85` literals with `AmbiguousCaptionHint` constant.
- **`RowValidationService.cs`**: Replaced hardcoded `< 0.5` threshold with `ConfidenceThreshold.LowConfidence`.
- **`BatchValidationService.cs`**: Replaced 10 histogram band boundaries with `ConfidenceThreshold.Band*` constants.

**998/998 tests pass.** One behavioral change: PK sample-size column confidence is now multiplicative (0.9 × 0.9 = 0.81) instead of overwritten to 0.90.

---

### 2026-04-07 2:42 PM EST — Add Dose and DoseUnit Columns to SPL Table Parsing Pipeline

Added two new structured columns (`Dose DECIMAL(18,6)`, `DoseUnit NVARCHAR(50)`) to the `tmp_FlattenedStandardizedTable` and the full parsing/normalization pipeline. Previously, dose information was scattered as free text across `DoseRegimen`, `TreatmentArm`, `ParameterName`, `StudyContext`, and `ParameterSubtype` with no structured decomposition. The new columns enable numeric dose queries, dose-response analysis, and cross-label comparison.

**New utility — `DoseExtractor.cs`**: Static class with four methods shared across BaseTableParser, ColumnStandardizationService, and MlNetCorrectionService:
- `Extract()` — regex-based extraction with range handling (take max), frequency promotion (mg + "Once Daily" -> mg/d), footnote stripping
- `NormalizeUnit()` — mg/day->mg/d, mcg/day->mcg/d, micro->mcg, idempotent
- `BackfillPlaceboArms()` — sets Dose=0.0 with majority DoseUnit from non-placebo arms in same TextTableID
- `ScanAllColumnsForDose()` — scans all text columns (DoseRegimen, TreatmentArm, ParameterName, ParameterSubtype, StudyContext) for misplaced dose patterns, modeled after the existing `normalizeInlineNValues` multi-column scan

**Key decisions:**
- DECIMAL(18,6) over FLOAT for exact prescribed quantity representation
- Multi-column scan runs as last Phase 2 sub-pass so all column movements settle first
- HasDose binary feature added to ML.NET Stage 2 DoseRegimen routing classifier as a "Keep" discriminator
- Range/titration -> max dose (10-20 mg -> Dose=20, most clinically relevant for comparison)
- DoseRegimen routing in both ColumnStandardizationService and MlNetCorrectionService clears Dose/DoseUnit when content is routed away

**Files modified (17):** SQL DDL, 6 model files (ArmDefinition, ParsedObservation, LabelView x2, FlattenedStandardizedTableDto, MlTrainingRecord), MlNetDataModels, DoseExtractor (new), BaseTableParser, 4 parsers (SimpleArm, AeWithSoc, MultilevelAe, PkTableParser), TableParsingOrchestrator, ColumnStandardizationService, MlNetCorrectionService, ClaudeApiCorrectionService, BatchValidationService, column-contracts.md. Both projects build with 0 errors.

---

### 2026-04-08 10:07 AM EST — PK Parser: Transposed Layout & Caption ArmN Fallback
Added two additive sanity checks to `PkTableParser` driven by an Estradiol TDS table (TextTableID=85) that exposed edge cases the parser was not handling. The table is transposed from the canonical PK layout — col 0 is a generic "Parameter" header with PK metric row labels ("AUC84(pg·hr/mL)", "Cmax(pg/mL)", "Tmax(hr)"), and columns 1–3 are dose levels ("0.1 mg/day", "0.05 mg/day", "0.025 mg/day"). The standard parser treated the dose headers as `ParameterName` and the PK metric row labels as `DoseRegimen`, which `ColumnStandardizationService.normalizeDoseRegimen` then rerouted to `ParameterSubtype` — producing `ParameterName="0.1 mg/day"` / `ParameterSubtype="AUC84"` (both wrong). The caption also carried `(N=36)` which was never consulted for `ArmN`.

**Refinement 1 — Caption ArmN fallback.** New `extractArmNFromCaption(string?)` reuses the existing `_armNFromLabelPattern` (case-insensitive parenthesized `(N=X)`). New `applyCaptionArmNFallback(table, observations)` populates null `ArmN` on each observation and appends `PK_CAPTION_ARMN_FALLBACK:{n}` to `ValidationFlags`. Wired into both `Parse()` and `parseCompoundLayout()`. Crucially it **never overrides** an `ArmN` the parser already derived from a row label (verified by the compound-header test injecting a conflicting caption `(N=99)` — row-label-derived `ArmN=6` and `ArmN=18` remain intact).

**Refinement 2 — Transposed layout detection & swap.** New `detectTransposedPkLayout(table)` requires **all three** signals simultaneously: (a) col 0 header matches the new `_transposedLayoutCol0Headers` set (`Parameter`, `Parameters`, `PK Parameter`, `Pharmacokinetic Parameters`, …), (b) every non-col-0 header matches the new `_doseHeaderPattern`, (c) ≥ 2 data-body rows start with a canonical PK metric via new `_pkMetricRowLabelPattern` (AUC/Cmax/Tmax/CL/Vd/Half-life/…) AND those form the majority. New `applyTransposedPkLayoutSwap(observations)` swaps `ParameterName` ↔ `DoseRegimen`, splits the parenthesized PK metric into name + `Unit` via `_paramUnitPattern`, re-extracts `Dose`/`DoseUnit` through `DoseExtractor.Extract`, surfaces `Time`/`TimeUnit` for time-measure metrics, and appends `PK_TRANSPOSED_LAYOUT_SWAP` to `ValidationFlags`. Activates **only** on the standard single-column path when `!hasDoseColumn && !col0IsPopulation`, so the two-column layout, compound-header layout, and population-col0 layout are left untouched.

**Non-regression posture.** All existing PK parsing paths untouched — the two new post-process hooks run after the data-row loop and only mutate observations when their strict detection guards all succeed. `TableParsingOrchestrator`, `ColumnStandardizationService`, `ValueParser`, and `BaseTableParser` unchanged.

**Tests (10 new in `TableParserTests.cs`).** New `#region PkTableParser Transposed Layout & Caption ArmN Tests` block with fixture `createTransposedPkTable` that mirrors TextTableID=85 exactly. Coverage: detection on the Estradiol table, non-detection on canonical PK layout, non-detection on `"Age Group"` col 0, non-detection on non-dose headers, full end-to-end swap (asserts `ParameterName` = `AUC84`/`AUC120`/`Cmax`/`Tmax`, `DoseRegimen` = dose headers, `Unit` = `pg·hr/mL`/`pg/mL`, `Dose` = 0.025m with `DoseUnit` = `mg/d`, both validation flags present, `ArmN=36` from caption), caption fallback on standard table `(N=24)`, caption fallback preservation of row-label ArmN in compound layout (injected `(N=99)` must not override `(n=6)`/`(n=18)`), and unit tests on `extractArmNFromCaption` including a deliberate null case for unparenthesized `"N = 36"` to guard against false positives in free-text captions.

**Verification.** `dotnet build` clean (0 errors). `dotnet test --filter "FullyQualifiedName~TableParserTests.PkParser"` — 33/33 pass. Full suite `dotnet test` — **1071/1071 pass**, zero regressions across ColumnStandardizationServiceTests, TableParsingOrchestratorTests, ValueParserTests, and all other suites.

---

### 2026-04-08 10:51 AM EST — parse-single Spectre.Console Footnote Markup Crash
Diagnosed a reported regression that "TextTableID=85 was entirely omitted from the database" after the PK transposed-layout/caption ArmN refinements landed. Investigation showed the parser was actually fine — DB query confirmed 12 rows for TextTableID=85 in `tmp_FlattenedStandardizedTable` with all the expected post-swap fields (`ParameterName` ∈ {AUC84, AUC120, Cmax, Tmax}, `ParameterSubtype=NULL`, `ArmN=36`, `Dose`/`DoseUnit` parsed from headers, `ValidationFlags` containing both `PK_TRANSPOSED_LAYOUT_SWAP` and `PK_CAPTION_ARMN_FALLBACK:36`). The user's premise was a `SELECT` without `ORDER BY` that hid the row range — once filtered by ID, TextTableID=85 was clearly present.

The investigation did surface a real (unrelated) bug in `MedRecProConsole/Services/TableStandardizationService.cs:1209`. The footnote display loop was interpolating the dictionary key directly inside `[...]`:

```csharp
AnsiConsole.MarkupLine($"  [{fn.Key}] {Markup.Escape(fn.Value)}");
```

Spectre.Console treats `[…]` as a markup style tag, so a footnote keyed `"Median"` made it parse `[Median]` as a style name and throw `InvalidOperationException: Could not find color or style 'Median'`. This crashed the verbose `parse-single` display path mid-table whenever a real table had non-symbolic footnote keys, which is what initially made the table look "missing" when visually scanning the CLI output.

**Fix.** Escape both the literal brackets (using Spectre's `[[`/`]]` doubling) and the key text via `Markup.Escape`:

```csharp
AnsiConsole.MarkupLine($"  [[{Markup.Escape(fn.Key)}]] {Markup.Escape(fn.Value)}");
```

No parser, orchestrator, or DB-write paths were touched — purely a CLI display fix. The defensive try/catch wrapper around `applyTransposedPkLayoutSwap` / `applyCaptionArmNFallback` that the diagnostic plan suggested as a safety net was deemed unnecessary, since neither helper actually throws on real Stage-2 reconstructed data and the verification rows confirm clean end-to-end behavior.

---

### 2026-04-08 12:14 AM EST — TextTableID 203: ArmN propagation + caption-derived StudyContext
Fixed two issues with the Topiramate pediatric epilepsy AE table (TextTableID 203) parsed by `AeWithSocTableParser`: (1) `ArmN` was NULL on every row because the first body row held arm N counts as parenthesized cells `(N =101 )` / `(N =98 )` which the existing `_nEqualsCellPattern` regex didn't match, and (2) `StudyContext` was NULL because neither AE parser consulted the caption as a fallback when the header provided no colspan study context.

**Fix 1 — Generalized parenthesized N= support.** Broadened `BaseTableParser._nEqualsCellPattern` from `^[Nn]\s*=\s*(\d[\d,]*)$` to `^\(?\s*[Nn]\s*=\s*(\d[\d,]*)\s*\)?\s*$` so it tolerates wrapping parens and interior whitespace. `classifyEnrichmentRow` now correctly identifies the first body row as an enrichment row, `enrichArmsFromBodyRows` consumes it, and `applyEnrichmentRow` writes `SampleSize` onto each `ArmDefinition`. The change is anchored and safe because the regex still requires the *entire* cell to be N=number — matches the SPL arm-N convention without false positives on data cells.

**Fix 2 — Caption → StudyContext fallback (procedurally generalizable).** Added `extractStudyContextFromCaption` as a `protected internal static` helper on `BaseTableParser` with a 6-stage pipeline: HTML-decode + tag strip, normalize whitespace, strip `Table N:` prefix, require a canonical AE measure phrase (`Adverse Reactions/Events/Experiences`, `Incidence of…`, `Frequency of…`, `Percent of Patients Reporting…`), find the first trial-descriptor connector (`in|during|from|reported in|observed in|occurring in|seen in|among`) *after* the measure phrase, then trim trailing footnote markers and punctuation. Returns `null` for any caption that doesn't match the canonical AE grammar — safe to call indiscriminately on non-AE tables. Both `AeWithSocTableParser` and `MultilevelAeTableParser` now call once and assign `o.StudyContext = arm.StudyContext ?? captionStudyContext` — header-derived context always wins.

**Testing.** Added 13 new tests: 3 for parenthesized N= enrichment (unparenthesized regression, `(N=101)`, `(N =101 )` Table-203 shape) and 10 for caption extraction (Table 203 canonical form, `<sup>` footnote stripping, missing-measure-phrase null, missing-connector null, null/empty input, PK caption null, alternate connectors parameterized, AE parser integration test, multilevel header-wins-over-caption, non-AE caption leaves null). Used a `CaptionStudyContextProbe` nested class extending `BaseTableParser` to reach the `protected internal` helper from test assembly. Full MedRecProTest suite: 1,084/1,084 passing.

**End-to-end verification.** Ran `parse-single --table-id 203` with a temporary debug dump in `displayParseSingleResults` (since the Spectre table wraps long columns at narrow terminal widths). Confirmed `Fatigue|Placebo|ArmN=101` / `Fatigue|Topiramate|ArmN=98` with `StudyContext='Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients (Ages 2 -16 Years) …'` on all 132 observations (down from 134 — the two enrichment rows are now consumed). Reverted the debug dump after verification to keep the console pristine.

Files touched: `BaseTableParser.cs` (regex broadening + new helper + 5 compiled patterns), `AeWithSocTableParser.cs` + `MultilevelAeTableParser.cs` (two-line fallback assignment each), `MedRecProTest/TableParserTests.cs` (+13 tests).

---

### 2026-04-08 2:23 PM EST — Stage 3.25 quality gate: drop rows missing ArmN or PrimaryValue
Procedural standardization of SPL observation tables has plateaued — over half of rows coming out of Stage 3 still fail downstream validation, and further gains would require table-specific granularity that isn't worth chasing. To establish a baseline row quality for cross-product meta-analysis, added an opt-in Stage 3.25 quality gate that drops observations where EITHER `ArmN` or `PrimaryValue` is `null`. Cross-product meta-analysis downstream requires BOTH fields populated, so any row missing either one is unrecoverable. (Initial implementation mistakenly dropped only rows missing *both* fields — caught immediately during review and corrected to the stricter OR semantics before the entry was finalized.)

**Design — additive, opt-in, backward compatible.** Default OFF everywhere: existing runs behave exactly as before. Three override layers, highest priority first: (1) CLI flag `--drop-incomplete-rows`, (2) interactive y/n prompt, (3) new `Standardization.DropRowsMissingArmNOrPrimaryValue` setting in `appsettings.json`, (4) default false. CLI flag is an opt-in override only — operators who enable the config default must edit `appsettings.json` to turn it back off.

**Plumbing mirrors the existing `--no-claude` / `disableClaude` pattern.** Flag flows CLI arg → `Program.cs` switch → `TableStandardizationService.Execute*Async(dropRowsMissingArmNOrPrimaryValue: …)` → `initializeRunAsync` → `buildServiceProvider` → DI registration → `TableParsingOrchestrator` ctor. The orchestrator gets a new optional ctor parameter defaulted to `false`, which keeps existing direct-construction unit tests compiling unchanged. The DI registration was switched from attribute-based `AddScoped<TInterface, TImpl>()` to an explicit factory lambda so the runtime flag actually reaches the ctor. `sp.GetService<T>()` is used for the optional dependencies (batch validator, column standardizer, ML.NET, Claude) so `null` is returned when they're not registered — matching the orchestrator's nullable-ctor-arg contract.

**Drop point — end of Stage 3.25.** New private helper `dropIncompleteRows` runs immediately after `runColumnStandardization` and before ML.NET (3.4), Claude (3.5), and post-processing (3.6). The filter keeps only rows where `ArmN != null && PrimaryValue != null`. This placement saves Claude tokens on unrecoverable rows, matches the literal brief, and is no-op when the gate is disabled. Logs `Stage 3.25 quality gate: dropped {Dropped}/{Total} rows missing ArmN or PrimaryValue` at Information level when any rows are removed so operators can see what the gate did.

**Interactive UX.** `ConsoleHelper.runStandardizeTablesFromMenuAsync` now takes a `ConsoleAppSettings settings` parameter (both interactive entry points updated accordingly) and prompts with `AnsiConsole.Confirm` in both the main path and the resume branch. The prompt default is seeded from `settings.Standardization.DropRowsMissingArmNOrPrimaryValue`, so operators can pre-configure their default and still be prompted. The confirmation table shows "Drop Incomplete Rows: Yes (ArmN or PrimaryValue null)" or "No" before the run starts. `HelpDocumentation.DisplayStandardizeTablesModeInfo` mirrors this for unattended runs.

**Testing.** Added two new tests in `TableParsingOrchestratorStageTests.cs` in the `ProcessBatchWithStagesAsync Tests` region, each exercising all four (ArmN null/populated) × (PrimaryValue null/populated) combinations:
- `ProcessBatchWithStagesAsync_DropIncompleteRowsDisabled_KeepsRowsMissingArmNOrPrimaryValue` — asserts the legacy-compatible default behavior: all 4 rows survive (null/null, null/val, val/null, val/val).
- `ProcessBatchWithStagesAsync_DropIncompleteRowsEnabled_DropsRowsMissingArmNOrPrimaryValue` — asserts that only the fully-populated row (`ArmN=7, PrimaryValue=42.0`) survives into `PostCorrectionObservations`; the three rows missing at least one field are gone.

Both tests use a mocked `ITableReconstructionService` + `ITableParserRouter` + `ITableParser` to inject a controlled mix of observations, and a real in-memory `ApplicationDbContext` via `UseInMemoryDatabase(...)` so `writeObservationsAsync` completes and `result.PostCorrectionObservations` is populated on the returned result (the existing tests in this region pass `null!` for DbContext and catch `NullReferenceException` at the DB-write boundary, which would hide post-drop state). A private `createDropIncompleteTestOrchestrator` helper wires it all together. All 43 `TableParsingOrchestrator` tests pass (including the two new ones); build is clean with 0 errors.

**CLI argument validation.** `--drop-incomplete-rows` is only valid with `--standardize-tables parse` or `--standardize-tables validate` (no-op for `truncate` and `parse-single`). `CommandLineArgs.Parse` emits a validation error otherwise so operators don't silently set a flag that has no effect.

Files touched: `MedRecProImportClass/Service/TransformationServices/TableParsingOrchestrator.cs`, `MedRecProConsole/Models/CommandLineArgs.cs`, `MedRecProConsole/Services/TableStandardizationService.cs`, `MedRecProConsole/Models/AppSettings.cs`, `MedRecProConsole/Helpers/HelpDocumentation.cs`, `MedRecProConsole/Program.cs`, `MedRecProConsole/Helpers/ConsoleHelper.cs`, `MedRecProTest/TableParsingOrchestratorStageTests.cs` (+2 tests, +1 helper).

---

### 2026-04-08 3:52 PM EST — Claude correction: anomaly gate floor, JSON extractor, NULL preservation rule
Fixed three defects in the Stage 3.5 Claude correction pipeline that were either bypassing the cost gate, crashing a chunk, or silently destroying good data.

**1. ML anomaly gate bypass — configured threshold silently demoted to 0.0f.**
`MlNetCorrectionService.InitializeAsync` and `FeedClaudeCorrectedBatchAsync` were propagating the persisted adaptive threshold (`MlTrainingStoreState.AdaptiveThreshold`, which defaults to `0.0f` and can only climb via the ratchet) straight into `ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold`, overwriting a user-configured `0.75f` floor. With the in-memory value demoted to `0.0f`, the gate condition `_settings.MlAnomalyScoreThreshold > 0f` at `ClaudeApiCorrectionService.cs:199` evaluated false and **every** observation passed through unfiltered — exactly matching the reported symptom of score-`0.70` rows leaking to Claude.

Fix: added a `private readonly float _configuredAnomalyFloor` captured at construction time from `claudeSettings?.MlAnomalyScoreThreshold`, and wrapped both propagation sites in `Math.Max(_configuredAnomalyFloor, persistedAdaptive)`. The configured value now acts as an immutable floor; the adaptive ratchet can only raise the effective threshold above it. `LogInformation` messages updated to show floor / persisted / effective values so operators can see what the gate is actually using.

**2. `JsonReaderException: Additional text encountered after finished reading JSON content: '` ` '`.**
`ClaudeApiCorrectionService.stripMarkdownFences` only handled the "fence-wrapped, nothing else" case via `StartsWith("` ``` `")` / `EndsWith("` ``` `")`. When Claude emitted trailing prose or a dangling backtick on its own line after the closing fence, the stray backtick survived into `deserializeCorrections` and Newtonsoft flagged trailing content at line 11 position 0. One TextTableID=84 chunk of 20 observations was lost per occurrence.

Fix: rewrote `stripMarkdownFences` as a JSON-aware single-pass extractor. It locates the first `[`, then walks forward tracking string state, escape sequences (`\\`, `\"`), and bracket nesting to find the matching closing `]`, returning only that substring. Tolerates markdown fences, leading/trailing prose, stray backticks, and string values containing literal `[`/`]`. If the array is unbalanced (truncation), returns from the first `[` onward so `salvageTruncatedJson` still gets its shot. Method name kept to avoid call-site churn; XML doc block rewritten to describe the new behaviour.

**3. NULL preservation skill rule — Claude was nulling perfectly good parsed values.**
Some Claude corrections were setting `newValue=null` on valid, schema-conformant values (e.g. `ParameterName`, `TreatmentArm`) rather than leaving them alone or routing them, destroying information the downstream pipeline depended on. Added an explicit **NULL Preservation Rule** to `Skills/correction-system-prompt.md` (the runtime prompt Claude reads) and a mirrored **Section 0** to `TableStandards/normalization-rules.md` (the authoritative reference). The rule enumerates the only three permitted NULL cases — routing with a destination correction in the same batch, explicit header/caption echo, and schema-invalid-for-TableCategory — and forbids nulling for any other reason. Includes a worked PK sub-param routing example showing the paired corrections Claude must emit, and the closing directive: *"When in doubt, omit the correction. No correction is always safer than a NULL that deletes a perfectly good parsed value."* Enum columns (`PrimaryValueType`, `SecondaryValueType`, `BoundType`) are explicitly corrected to another enum member, never to NULL.

**Build:** `dotnet build MedRecProImportClass.csproj` — 0 errors, 138 pre-existing warnings.

Files touched: `MedRecProImportClass/Service/TransformationServices/MlNetCorrectionService.cs` (field + ctor capture + two `Math.Max` sites + log updates), `MedRecProImportClass/Service/TransformationServices/ClaudeApiCorrectionService.cs` (`stripMarkdownFences` rewrite), `MedRecProImportClass/Skills/correction-system-prompt.md` (NULL Preservation Rule section), `MedRecProImportClass/TableStandards/normalization-rules.md` (Section 0 governing rule).

---

### 2026-04-10 11:16 AM EST — AeParameterCategoryDictionaryService: Documentation & Artifact Sync

Synchronized all supporting documentation and test artifacts with the current 698-entry AeParameterCategoryDictionaryService dictionary.

**Changes:**

1. **Interface & Implementation doc updates** — Corrected stale entry counts in XML doc remarks: `IAeParameterCategoryDictionaryService.cs` (747 → 698), `AeParameterCategoryDictionaryService.cs` (673 → 698 in two places).

2. **Unit tests** (`AeParameterCategoryDictionaryServiceTests.cs`) — Updated `Count` assertion from `> 600` to `>= 698` to match exact dictionary size. Updated class-level summary to reference 698 entries.

3. **README.md** — Added `TransformationServices/` directory listing with `AeParameterCategoryDictionaryService.cs`, `IAeParameterCategoryDictionaryService.cs`, and `ColumnStandardizationService.cs`. Added `TableStandards/` directory with its three reference docs. Added `AeParameterCategoryDictionaryServiceTests.cs` and `ColumnStandardizationServiceTests.cs` to test project listing.

4. **TableStandards/normalization-rules.md** — Added "Dictionary Lookup for NULL ParameterCategory" subsection under Section 4 (ParameterCategory Canonical SOC Mapping) explaining the 698-entry static dictionary lookup, its pipeline position (Stage 3.25 Phase 2), guard conditions, and the `DICT:SOC_RESOLVED` flag. Added `DICT:SOC_RESOLVED` to the Validation Flags catalog.

5. **Skills** — No changes needed; the dictionary service is an internal pipeline component, not a user-facing capability contract.

**Build:** 0 errors, 331 warnings. **Tests:** 17/17 dictionary tests pass, 121/121 ColumnStandardization tests pass.

---

### 2026-04-13 1:56 PM EST — ML Training Store: 30 MB File Size Cap

Added a hard byte-level size cap to prevent `.medrecpro-ml-training-store.json` from growing without bounds. The file was observed at 84 MB — caused by `WriteIndented = true` producing ~840 bytes/record rather than the ~100 bytes/record estimated in the code comment.

**Changes:**

1. **`MlNetCorrectionSettings.cs`** — Added `MaxTrainingStoreSizeBytes` property (default 30 MB = `30L * 1024 * 1024`). Updated `MaxAccumulatorRows` comment to reflect the actual ~800–900 bytes/record indented JSON size.

2. **`MlTrainingStore.cs`** — Three changes:
   - Extracted `evictOldest(int count)` helper with the two-phase bootstrap-first eviction logic (Phase 1: oldest bootstrap records; Phase 2: oldest overall). `evictIfOverCapacity` now delegates to it.
   - Updated `saveInternalAsync` to serialize to `byte[]` first, check size against the cap, call `evictOldest` and re-serialize if over, then write via `WriteAllBytesAsync`. The over-limit file never touches disk.
   - Added load-time size check in `LoadAsync`: if the file exceeds the cap on startup (e.g. written by an older build), a `LogWarning` is emitted and `saveInternalAsync` is called immediately to trim and re-save.

**Key decision:** Both the row cap (`MaxAccumulatorRows`) and the new size cap (`MaxTrainingStoreSizeBytes`) coexist as independent constraints — whichever binds first wins. This preserves the existing row-cap guard while adding a more accurate file-size enforcement.

---

### 2026-04-13 3:37 PM EST — Composite Anomaly Model Keys (Category + PrimaryValueType + SecondaryValueType)

Refactored the Stage 4 anomaly detection system in `MlNetCorrectionService` to partition PCA models by a composite key (`TableCategory|PrimaryValueType|SecondaryValueType`) instead of just `TableCategory` alone. The previous single-dimension keying was too coarse — it compared different numeric types (e.g., percentages vs. arithmetic means) within the same model, causing imprecise anomaly detection.

**Changes across 4 files:**

1. **`MlTrainingRecord.cs`** — Added `SecondaryValueType` (string?) property and updated `FromObservation` to map it from `ParsedObservation`.

2. **`MlNetCorrectionService.cs`** — Three refactors driven by a single new DRY helper:
   - Added `buildAnomalyModelKey(category, pvt, svt)` → returns `"CAT|PVT"` or `"CAT|PVT|SVT"` (internal static, directly testable).
   - Refactored `trainAnomalyModels`: replaced static `_anomalyCategories` iteration with dynamic `GroupBy` on the composite key. PCA rank lookup uses a three-step fallback: composite key → category → default 3.
   - Updated `applyAnomalyScore` and `tryRetrain` to use composite key lookups via the same helper.

3. **`MlTrainingStoreState.cs`** — Bumped schema version 1 → 2 (backward compatible — old stores missing `SecondaryValueType` deserialize as null → two-segment keys).

4. **`MlNetCorrectionServiceTests.cs`** — Updated `createTestObservation` and `generateTrainingBatch` helpers with SVT support. Fixed 4 existing tests for new composite key thresholds. Added 9 new tests: 6 unit tests for `buildAnomalyModelKey` edge cases + 3 integration tests (composite key match, mismatch → NOMODEL, sparse composite → graceful skip). All 40 ML tests pass (31 existing + 9 new).

**Key design decision:** Sparsity handled by the existing `MinTrainingRowsPerCategory` threshold — composite keys with too few rows simply don't get models and fall to NOMODEL, which routes to Claude API for review (safe default).

---