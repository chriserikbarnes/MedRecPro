# Claude Code — Global Instructions

---

## Session Rules

> ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
> ┃ 📓 MANDATORY: JOURNAL ENTRY — EVERY CODING SESSION, NO EXCEPTIONS 📓 ┃
> ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
>
> **Before ending ANY session where code was written, modified, or deleted,
> you MUST append a journal entry to `Journal.md`.** This is non-negotiable.
> If the user says "done", "thanks", "that's all", or the conversation ends
> after code changes — write the journal entry FIRST.

### Journal Entry Details

**ALWAYS** after **EVERY** coding session, APPEND a summary entry to `Journal.md` located in the **parent folder of the current repository**.

> ⚠️ **APPEND ORDER: DATE ASCENDING (oldest first, newest last).** New entries
> are ALWAYS added to the **END** of the file. Never insert entries above existing
> ones. The file reads chronologically from top to bottom.

**Resolution rule**: Identify the repo root (the folder containing `.git`), then go up one level. That parent folder holds `Journal.md`.

Example:
| Working repo | Journal location |
|---|---|
| `C:\Users\chris\Documents\Repos\MedRecPro\` | `C:\Users\chris\Documents\Repos\Journal.md` |
| `C:\Users\chris\Documents\Repos\SomeOtherProject\` | `C:\Users\chris\Documents\Repos\Journal.md` |
| `D:\Projects\ClientWork\AppX\` | `D:\Projects\ClientWork\Journal.md` |

If `Journal.md` does not exist at that location, create it.

Each entry MUST include:
- **Timestamp** in EST (e.g., `2026-02-24 3:45 PM EST`)
- **Title** — short descriptive title of the work
- **Description** — what was done, key decisions, and outcomes

**Timestamp rule**: You do NOT have access to the system clock. NEVER fabricate or guess a time — not even an approximate one. Instead, **ASK the user** for the current time before writing the journal entry. If the user is unavailable or does not respond, fall back to `git log -1 --format="%ai"` on the most recent commit as a last resort. Under no circumstances should you invent a timestamp.

Format:
```markdown
---

### 2026-02-24 3:45 PM EST — Title of Work
Description of the work performed, key decisions made, and outcomes.

---
```

> **⚠️ CRITICAL:** Always leave a **blank line before** every `---` separator.
> Without the blank line, Markdown treats the preceding text as a setext H2 heading
> (renders as oversized bold text instead of normal paragraph + horizontal rule).

This file serves as persistent memory across sessions. Always append to the END — never overwrite, never insert above existing entries. Entries must remain in chronological (date ascending) order.

> ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
> ┃ 🚨 NEW ENTRIES GO AT THE VERY BOTTOM OF Journal.md — NOWHERE ELSE 🚨 ┃
> ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
>
> **DO NOT** insert a new entry between existing entries. The ONLY correct
> location for a new journal entry is **after the last `---` separator** at
> the very end of the file. If the file has 300 lines, your new entry starts
> at line 301. Read the last few lines first to confirm you are appending,
> not inserting.

> ⚠️ **FAILURE TO JOURNAL = INCOMPLETE SESSION.** Treat this with the same
> priority as building the code — the session is not done until the journal
> entry is written.

---

## Problem-Solving Framework

Before beginning any implementation, work through these rationalization steps:

### Step 1: UNDERSTAND THE CONTEXT
**What is the core question or task?**
- Identify the primary objective and the problem being solved
- Clarify the expected outcome
- Determine how this fits into the larger system

### Step 2: ANALYZE THE REQUIREMENTS
**What are the specific requirements, key components, or constraints?**
- Functional requirements (what it must do)
- Non-functional requirements (performance, reliability)
- Technical constraints (APIs, databases, patterns to follow)
- Architectural requirements (separation of concerns, feature flags)
- Dependencies and integrations

### Step 3: REASON
**What are the logical steps or processes needed?**
- Sequence of operations
- Data flow and transformations
- Decision points and branching logic
- Error handling and edge cases
- Transaction boundaries

### Step 4: SYNTHESIZE
**How can you combine the information into a coherent solution?**
- Unified approach addressing all requirements
- Clean abstractions and interfaces
- Reusable components where appropriate
- Design following established patterns
- Solution that integrates cleanly with existing code

### Step 5: CONCLUDE
**What is the most helpful/accurate solution?**
- Complete, working implementation
- Code following all standards and conventions
- Proper error handling and logging
- Comprehensive documentation
- Review solution with criticism, then revise based on criticisms
- Verify the build

---

## Code Standards & Documentation

### General Rules

- **DO NOT** apply documentation conventions in RazorLight templates
- **DO NOT** use Regions in RazorLight templates nor JavaScript files
- **Private methods** — begin with a lowercase letter
- **Public methods** — begin with an uppercase letter
- **Controller endpoints ONLY** — use XML Markdown format compatible with Swashbuckle Swagger generation (Swagger supports Markdown within XML remarks/summaries)

### Required Documentation Elements

#### 1. Comment Header + Summary Block

All methods, classes, and properties must have:

```csharp
/**************************************************************/
/// <summary>
/// Brief description of what this method/class/property does.
/// </summary>
/// <remarks>
/// Additional context, usage notes, performance characteristics.
/// </remarks>
/// <example>
/// <code>
/// var result = DoSomething(param1, param2);
/// </code>
/// </example>
/// <param name="param1">Description of first parameter.</param>
/// <returns>Description of what is returned.</returns>
/// <exception cref="ExceptionType">When this exception is thrown.</exception>
/// <seealso cref="RelatedClass"/>
```

#### 2. SeeAlso References

- Always include `<seealso cref="Label"/>` for methods/properties related to the Label entity
- Add `<seealso>` for: related classes, related methods, interface implementations, parent/child relationships, dependencies

#### 3. Region Blocks

- Use `#region implementation` (all lowercase) within methods, classes, and properties
- **Retain** all existing `#region` blocks (public method groupings, logical groupings, commented-out code blocks)

#### 4. Inline Comments

- Add inline comments to explain logic where it can be inferred
- Explain non-obvious operations, performance choices, and edge case handling

#### 5. Property Documentation

- Add `/// <summary>` for properties when usage can be inferred
- Include `<remarks>` for behavioral notes, performance notes, or side effects

### Documentation Completeness Goal

Documentation should answer: **What** does this do? **Why** does it exist? **How** should it be used? **When** should it be used? **What** are the constraints?

### Existing Code Preservation

When modifying existing files:
- Retain all existing documentation
- Preserve all existing `#region` blocks
- Keep commented-out code if it's inside a `#region`
- Match existing style and patterns
- Add documentation only where missing

### Quality Checklist

- [ ] All public methods have `/// <summary>` documentation
- [ ] All private methods have `/// <summary>` documentation
- [ ] All properties have `/// <summary>` where usage can be inferred
- [ ] `/**************************************************************/` precedes all summaries
- [ ] `<seealso>` references added for related classes/methods
- [ ] `<remarks>` explain important details, performance notes, side effects
- [ ] `<example>` demonstrates typical usage where helpful
- [ ] `#region implementation` blocks organize code within methods/classes
- [ ] All existing `#region` blocks retained
- [ ] Private methods lowercase, public methods uppercase
- [ ] Inline comments explain non-obvious logic
- [ ] Error handling is comprehensive with clear messages
- [ ] Logging uses appropriate levels (Debug, Info, Warning, Error)

---

## SQL Server Patterns

### SSMS 21.x Bug: 30-Second Command Timeout
- SSMS 21.x has a known bug where execution times out at exactly 30 seconds
- The setting at Tools → Options → Query Execution → SQL Server → General → Execution time-out may show 0 but still enforce 30s
- **Workaround**: Run long queries in Visual Studio or Azure Data Studio instead
- Use `RAISERROR('...', 0, 1) WITH NOWAIT` instead of `PRINT` for real-time progress in stored procedures

### STRING_AGG Optimization Barriers
- STRING_AGG + GROUP BY creates optimization barriers that prevent predicate pushdown through CTEs
- ROW_NUMBER() and UNION ALL compound the problem
- Indexed/materialized views cannot use STRING_AGG, UNION ALL with aggregates, subqueries, ROW_NUMBER(), or outer joins
- **Solution**: Inline CTE logic into stored procedures with predicates injected at the base CTE level, batch by ID ranges

---

## EF Core Patterns

### View-to-Table Swap Pattern
- `entityBuilder.ToView(viewName)` works for both views AND tables — it just means "keyless, read-only, no migrations"
- Changing `[Table("vw_X")]` to `[Table("tmp_X")]` on the entity class is sufficient to swap from view to table
- `.HasNoKey()` remains correct for tables without PKs (just indexes)

---

## MCP Tool Documentation Generator

Transform existing API endpoints and methods into fully-documented MCP (Model Context Protocol) tool endpoints using the `ModelContextProtocol.AspNetCore` package.

### Prerequisites
- `ModelContextProtocol.AspNetCore` NuGet package installed
- Existing method/controller with XML documentation
- Associated DTOs/models with property documentation

### Tool Class Structure

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace {Namespace}.McpTools;

/**************************************************************/
/// <summary>
/// {Tool class summary describing the domain/capability}
/// </summary>
/// <remarks>
/// ## Tool Workflow
/// {ASCII diagram showing tool relationships}
/// </remarks>
[McpServerToolType]
public class {Domain}Tools { }
```

### Tool Method Decoration Pattern

Each MCP tool method MUST include:

```csharp
/**************************************************************/
/// <summary>
/// {One-line action summary starting with a verb}
/// </summary>
[McpServerTool(Name = "{snake_case_tool_name}")]
[Description("""
    {EMOJI} {CATEGORY}: {One-line summary}

    📋 WORKFLOW: {Position in tool chain}
    ├── {Prerequisite or input source}
    ├── Returns: {Key fields returned}
    └── Next: {Downstream tool usage}

    🎯 {USE CASE|SEARCH TIPS|EXAMPLE}: {Contextual guidance}
    """)]
```

### Category Emojis

| Emoji | Category | Use For |
|-------|----------|---------|
| 🔍 | SEARCH | Tools that find/query data |
| 📄 | RETRIEVE | Tools that get complete records |
| 📋 | LIST | Tools that enumerate collections |
| ✏️ | CREATE | Tools that create new records |
| 🔄 | UPDATE | Tools that modify existing records |
| 🗑️ | DELETE | Tools that remove records |
| 📊 | ANALYZE | Tools that compute/aggregate |
| 🔗 | LINK | Tools that relate entities |

### Contextual Sections

| Section | Use When |
|---------|----------|
| `🎯 SEARCH TIPS:` | For search tools — explain query strategies |
| `🎯 USE CASE:` | Explain when to use this tool |
| `🎯 EXAMPLE:` | Provide concrete usage example |
| `🎯 NOTE:` | Important caveats or behaviors |

### Parameter Documentation Pattern

Every parameter MUST have a `[Description]` attribute:

```csharp
// ID/GUID parameters (downstream references)
[Description("{EntityName} identifier from '{source_tool_name}' results. Format: '{format_example}'")]
[Required]
Guid {entityId}

// Search/filter parameters
[Description("{What it filters}. Supports {matching_type} matching. Example: '{example_value}'")]
string? {searchParam} = null

// Pagination parameters
[Description("Page number, 1-based. Default: {default}")]
[Range(1, int.MaxValue)]
int pageNumber = {default}

[Description("Results per page ({min}-{max}). Default: {default}")]
[Range({min}, {max})]
int pageSize = {default}

// Enum/option parameters
[Description("{What it controls}. Options: {Option1}={meaning}, {Option2}={meaning}. Default: {default}")]
{EnumType} {param} = {EnumType}.{Default}

// Boolean flags
[Description("{What it enables/disables when true}. Default: {default}")]
bool {flagName} = {default}

// Date/time parameters
[Description("{What date this represents}. Format: ISO 8601 (yyyy-MM-dd). Example: '2024-01-15'")]
DateTime? {dateName} = null
```

### Return Type Documentation

```csharp
/**************************************************************/
/// <summary>
/// Result from {tool_name} tool.
/// </summary>
/// <remarks>
/// ## Field Usage
/// - {FieldName}: {Description} → Use with '{downstream_tool}'
/// </remarks>
public class {ToolName}ResultDto
{
    [Description("{Brief description} - {downstream usage hint}")]
    public {type} {FieldName} { get; set; }
}
```

### Workflow Documentation Patterns

**Entry Point Tool** (no prerequisites):
```
📋 WORKFLOW: Start here when {use case}.
├── Returns: {Field1}, {Field2}, {Field3}
├── Next: Use {Field1} → '{tool_a}' for {purpose}
└── Next: Use {Field2} → '{tool_b}' for {purpose}
```

**Middle Chain Tool** (has prerequisites and next steps):
```
📋 WORKFLOW: {Brief description of purpose}
├── PREREQUISITE: Get {InputField} from '{source_tool}'
├── Returns: {Field1}, {Field2}
└── Next: Use {Field1} → '{downstream_tool}' for {purpose}
```

**Terminal Tool** (has prerequisites, no downstream):
```
📋 PREREQUISITE: Get {InputField} from '{source_tool}'.

📦 RETURNS: {Detailed description of complete data returned}
• {Section/field 1}
• {Section/field 2}
```

### ASCII Workflow Diagrams

Include in class-level `<remarks>`:
```
/// {entry_tool_1} ──┬──► {KeyField} ──► {middle_tool} ──► {KeyField} ──► {terminal_tool}
///                  │
/// {entry_tool_2} ──┘
```

### Tool Naming Conventions

| Pattern | Examples |
|---------|----------|
| `search_{entities}` | `search_products`, `search_labels` |
| `get_{entity}` | `get_product`, `get_label_document` |
| `list_{entities}` | `list_orders`, `list_products_by_category` |
| `create_{entity}` | `create_order`, `create_comment` |
| `update_{entity}` | `update_profile`, `update_order_status` |
| `delete_{entity}` | `delete_comment`, `delete_draft` |
| `{verb}_{entity}` | `validate_address`, `calculate_shipping` |

### Parameter Naming (camelCase)

| Type | Pattern | Examples |
|------|---------|----------|
| ID/GUID | `{entity}Id`, `{entity}Guid` | `productId`, `documentGuid` |
| Search | `{field}Search`, `{field}Query` | `nameSearch`, `ingredientQuery` |
| Filter | `{field}Filter`, `{field}` | `statusFilter`, `category` |
| Pagination | `pageNumber`, `pageSize` | Always use these exact names |
| Flags | `include{Feature}`, `is{State}` | `includeDetails`, `isActive` |

### Validation Attributes Reference

| Scenario | Attribute | Example |
|----------|-----------|---------|
| Required parameter | `[Required]` | `[Required] Guid id` |
| Numeric range | `[Range(min, max)]` | `[Range(1, 100)] int pageSize` |
| String length | `[StringLength(max)]` | `[StringLength(500)] string query` |
| Minimum length | `[MinLength(min)]` | `[MinLength(3)] string search` |
| Pattern/format | `[RegularExpression]` | `[RegularExpression(@"^\d{4}$")]` |

### MCP Tool Quality Checklist

- [ ] Tool name is snake_case, verb_noun pattern
- [ ] `[McpServerTool]` has explicit Name property
- [ ] `[Description]` starts with category emoji and one-line summary
- [ ] Workflow section shows prerequisites and next steps
- [ ] Every parameter has `[Description]` with concrete examples
- [ ] Required parameters have `[Required]` attribute
- [ ] Numeric parameters have `[Range]` validation
- [ ] ID/GUID parameters reference their source tool by name
- [ ] DTO properties have `[Description]` indicating downstream usage
- [ ] No orphan tools — every tool connects to the workflow
- [ ] Entry points and terminal tools are clearly identifiable

### Anti-Patterns

❌ Vague: `[Description("Gets data")]`
✅ Specific: `[Description("🔍 SEARCH: Find products by brand name, generic name, or UNII code")]`

❌ Missing workflow: `[Description("Returns a document by ID")]` — Where does the ID come from?
✅ Clear workflow: Include `📋 PREREQUISITE: Get documentGuid from 'search_documents'`

❌ No examples: `[Description("The UNII code")]`
✅ With examples: `[Description("UNII code for exact match. Example: 'R16CO5Y76E' (aspirin)")]`

---

## Skills Architecture — Best Practices

### Primary Goals
1. Skills go into **capability contracts**, not workflows
2. Eliminate endpoint, HTTP, and routing logic from skills
3. Enforce strict size and depth limits
4. Produce a structure supporting: AI skill selection, human understanding, API evolution, regulatory review

### Three Required Document Types

#### 1. `skills.md` — Capability Contracts (PRIMARY)
- Describes *what the system can do*
- No implementation details
- Stable over time

#### 2. `selectors.md` — Skill Selection & Routing Rules
- Keyword matching, priority rules, decision trees, guardrails

#### 3. `/interfaces/*` — Implementation Mappings
- API endpoints, section codes, step-by-step workflows, token optimization rules

### Skill Content Template (Required Order)

Each skill MUST contain:
1. **One-sentence declarative summary** — starts with action verb, describes capability not method
2. **Short purpose paragraph** — why the capability exists, what problem it solves
3. **Inputs** — conceptual only (no formats, no endpoints)
4. **Outputs** — observable outcomes only
5. **Optional scenarios** — max 3 bullets, no step ordering, no conditional logic

### Prohibited in Skills

Skills MUST NOT include:
- API routes or HTTP verbs
- LOINC codes or section codes
- Step numbers or procedural steps
- "CRITICAL", "REQUIRED", or enforcement language
- Token optimization notes
- Training-data disclaimers
- Admin/auth checks

### Sub-Skill Extraction Rules

Move content OUT of skills if it contains: multi-step workflows, fallback logic, performance optimizations, error handling, "if X fails then Y", data-sourcing rules, enforcement checklists.

**Maximum nesting depth: 2**
```
skills.md → skills/label.md → skills/label/indication-discovery.md
```

### Selector Rules (`selectors.md`)

Contains: keyword lists, priority rules, decision trees, fallback loading, admin role checks.
Selectors may reference skills by name and interface documents by link.
Selectors MUST NOT define capabilities or describe system behavior beyond routing.

### Interface Mappings (`/interfaces/api/*.md`)

Contains: API paths, HTTP verbs, required parameters, output mappings, section codes, token size notes, step-by-step execution plans.
Each interface doc maps **one skill → one or more endpoints**.

### Preservation Requirements

You MUST: preserve every real capability, preserve all critical constraints (relocate properly), merge overlapping/redundant skills.
You MAY: shorten wording, normalize terminology, remove duplication, replace long rules with references.

### Skills Validation Checklist

- [ ] No skill mentions an API endpoint (those go in interfaces/api)
- [ ] No skill includes procedural steps
- [ ] All "CRITICAL" rules are relocated to selectors or interfaces
- [ ] Selection logic exists only in selectors.md
- [ ] Endpoint logic exists only in interfaces docs
- [ ] Each skill stands alone as a stable capability contract
