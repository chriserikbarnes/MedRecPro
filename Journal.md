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
