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

