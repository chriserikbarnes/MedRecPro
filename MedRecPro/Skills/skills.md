# MedRecPro Skills - Capability Contracts

This document defines the system's capabilities as stable contracts. Each skill describes **what** the system can do, not **how** it is implemented.

---

## Database Inventory

### Inventory Summary

Provide comprehensive database inventory statistics showing what products, documents, and classifications are available.

Returns aggregated counts across multiple dimensions including total documents, products, labelers, active ingredients, pharmacologic classes, NDCs, marketing categories, and dosage forms. Use this for "what do you have" type questions.

**Inputs**
- Category filter (optional): TOTALS, TOP_LABELERS, TOP_PHARM_CLASSES, TOP_INGREDIENTS, BY_MARKETING_CATEGORY, BY_DOSAGE_FORM

**Outputs**
- Aggregated counts by dimension
- Category groupings with sort order

**Scenarios**
- "What products do you have?"
- "Who are the top manufacturers?"
- "What drug classes do you have?"
- "How many products are in the database?"

---

## Label Operations

### Label Content Retrieval

Retrieve structured content from FDA drug labels by document identifier and section type.

Returns official FDA prescribing information organized by standardized section categories (indications, warnings, dosing, adverse reactions, interactions, contraindications). Enables access to authoritative pharmaceutical documentation for clinical reference.

**Inputs**
- Document identifier (GUID)
- Section category (optional)

**Outputs**
- Formatted section text
- Section metadata and attribution

**Scenarios**
- Viewing complete prescribing information for a known product
- Accessing specific safety sections (warnings, contraindications)
- Retrieving dosing instructions from official labeling

---

### Label Document Search

Find FDA drug label documents by product identifiers, ingredient names, or manufacturer information.

Locates specific pharmaceutical products in the FDA labeling database using various search criteria. Returns document references suitable for detailed content retrieval.

**Inputs**
- Product name, ingredient name, UNII code, NDC, or manufacturer name

**Outputs**
- List of matching documents with identifiers
- Product metadata (name, ingredients, labeler)

**Scenarios**
- Finding a label by brand or generic name
- Locating products by active ingredient
- Searching by national drug code

---

### Related Products Discovery

Find products related to a source product by shared attributes such as active ingredient or application number.

Identifies generic equivalents, alternative formulations, and products sharing regulatory approval. Supports medication substitution and formulary management workflows.

**Inputs**
- Source document identifier
- Relationship type (optional)

**Outputs**
- List of related products with identifiers
- Relationship classification

**Scenarios**
- Finding generic alternatives to a brand product
- Identifying all products under the same FDA application

---

## Pharmacologic Class Discovery

### Pharmacologic Class Search

Find pharmaceutical products by therapeutic or pharmacologic class using intelligent terminology matching.

Matches user queries about drug classes to actual database classification names, then retrieves all products in matched classes. Solves the vocabulary mismatch between common drug class terms and formal FDA classification names.

**Inputs**
- Drug class name (common or formal terminology)

**Outputs**
- Matched pharmacologic class names from database
- Products in each matched class with label links
- Product counts and class summaries

**Scenarios**
- Finding all beta blockers ("what medications are beta blockers")
- Locating ACE inhibitors for hypertension
- Identifying all SSRIs or statins
- Browsing products by therapeutic category

---

### Pharmacologic Class Browse

List all available pharmacologic classes with product counts for browsing and discovery.

Provides an overview of therapeutic classifications in the database. Enables faceted navigation and class-based product exploration.

**Inputs**
- None required (lists all classes)

**Outputs**
- Class names with product counts
- Class hierarchy relationships

**Scenarios**
- Exploring available drug categories
- Understanding classification structure
- Starting point for class-based searches

---

## Indication Discovery

### Condition-Based Product Search

Find pharmaceutical products indicated for treating specific medical conditions, diseases, or symptoms.

Matches therapeutic needs to FDA-approved indications using curated reference data. Returns products with documented efficacy for the queried condition.

**Inputs**
- Medical condition, symptom, or therapeutic category

**Outputs**
- Matching products with indication summaries
- Document references for full label access

**Scenarios**
- Finding antidepressants for depression
- Identifying antihypertensives for blood pressure management
- Locating antihistamines for seasonal allergies

---

### Alternative Product Discovery

Find therapeutic alternatives and generic equivalents for a specified product.

Combines indication matching with related product discovery to identify substitution options. Supports formulary management and cost-effective prescribing.

**Inputs**
- Product name or ingredient

**Outputs**
- Products with same active ingredient
- Products with similar therapeutic indication

---

## Opioid Conversion

### Equianalgesic Dose Calculation

Retrieve FDA-labeled equianalgesic conversion information for opioid medications.

Accesses official dosing guidance and conversion tables from FDA-approved labeling. Provides reference information for opioid rotation decisions.

**Inputs**
- Source opioid medication
- Target opioid medication

**Outputs**
- Conversion factors from official labeling
- Dosing tables and calculation guidance
- Source document references

**Scenarios**
- Converting morphine to hydromorphone
- Switching from transdermal fentanyl to oral morphine
- Methadone to buprenorphine transition guidance

---

## System Administration

### Application Log Viewing

View in-memory application logs with comprehensive filtering capabilities.

Provides real-time access to application logs stored in memory. Supports filtering by severity level (Trace, Debug, Information, Warning, Error, Critical), log category, user, and date range. Logs are retained for a configurable period (default 60 minutes).

**Inputs**
- Severity level filter (optional): Trace, Debug, Information, Warning, Error, Critical
- Category filter (optional): Logger category name (e.g., "ClaudeApiService", "Controller")
- User filter (optional): Filter by specific user ID
- Date range filter (optional): Start and end timestamps
- Pagination: Page number and page size

**Outputs**
- Log entries with message, level, timestamp, category, user context
- Exception details when applicable
- Pagination metadata

**Scenarios**
- "Show me the logs" - View recent application logs
- "Show me error logs" - Filter by Error level
- "Show logs from ClaudeApiService" - Filter by category
- "What errors happened in the last hour" - Filter by date and level
- "How many logs do we have" - Get log statistics
- "What log categories are available" - List categories

---

### User Activity Monitoring

View activity audit logs for specific users showing their actions in the system.

Provides detailed audit trail for compliance and troubleshooting. Tracks API calls, login events, and data modifications. Requires user lookup first to obtain encrypted user ID.

**Inputs**
- User identifier (email or encrypted user ID)
- Date range filter (optional): Up to 365 days
- Pagination: Page number and page size

**Outputs**
- Activity entries with type (Read, Create, Update, Delete, Login)
- Request details (path, method, controller, action)
- Performance data (execution time, response status)
- Client context (IP address, user agent)

**Scenarios**
- "What did chris.erik.barnes@gmail.com do today" - User activity audit
- "Show activity for [user] between dates" - Date-filtered audit
- "What actions has [user] performed" - General activity review
- "Show login history for [user]" - Authentication audit

---

### Endpoint Performance Analysis

View API response time statistics and performance metrics by controller and endpoint.

Enables performance monitoring and capacity planning. Analyzes activity logs to compute response time statistics. Identifies slow endpoints and usage patterns.

**Inputs**
- Controller name (required): e.g., "Settings", "Label", "Users", "Ai"
- Action name (optional): Specific method name for detailed analysis
- Analysis limit (optional): Number of recent activities to analyze

**Outputs**
- Response time statistics (average, min, max)
- Request volume metrics
- Date range of analyzed data

**Scenarios**
- "How is the Settings controller performing" - Controller-level stats
- "What is the response time for GetLogs" - Specific endpoint stats
- "Which endpoints are slowest" - Performance comparison
- "API performance report" - Overall system performance

---

### Cache Management

Clear or invalidate application caches to ensure data freshness.

Supports manual cache invalidation after data updates or configuration changes. Requires administrative authorization.

**Inputs**
- Cache scope (optional)

**Outputs**
- Confirmation of cache operation

---

## Authentication

### Session Management

Handle user authentication flows including OAuth sign-in, sign-out, and session validation.

Manages user identity and authorization state. Supports OAuth providers for single sign-on.

**Inputs**
- Authentication action (sign-in, sign-out, validate)
- Provider (for sign-in)

**Outputs**
- Authentication status
- User profile information

---

## Data Rescue

### Narrative Text Extraction

Extract structured information from unstructured label narrative text when dedicated data fields are empty.

Searches label prose sections for data that exists in narrative form rather than structured tables. Fallback strategy for incomplete structured data.

**Inputs**
- Document identifier
- Target data type (e.g., inactive ingredients)

**Outputs**
- Extracted data from narrative text
- Source section attribution

**Scenarios**
- Finding inactive ingredients listed in Description section
- Extracting physical characteristics from narrative
- Locating storage conditions in non-standard locations

---

## Skill Integration Notes

Skills may be combined for complex queries:

- **Condition search + Label content**: Find products for a condition, then retrieve detailed safety information
- **Product search + Related products**: Identify a product, then find generic alternatives
- **Label content + Rescue workflow**: Attempt structured retrieval, fall back to narrative extraction
- **Pharmacologic class search + Label content**: Find products by drug class, then retrieve detailed prescribing information
- **Pharmacologic class browse + Class search**: Browse available classes, then search for products in selected class

---

## CRITICAL: Response Requirements

### Label Links Are Mandatory

**Every response that retrieves product data MUST include label links.**

**A response without label links is INCOMPLETE.**

### Section Retrieval

**Recommended approach**: Omit `sectionCode` parameter to get ALL available sections. This avoids errors from non-existent section codes.

---

For implementation details, routing logic, and API specifications, see:
- [Skill Selectors](./selectors.md) - Routing and selection rules
- [Interface Mappings](./interfaces/) - API endpoint specifications
