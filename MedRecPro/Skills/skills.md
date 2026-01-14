# MedRecPro Skills - Capability Contracts

This document defines the system's capabilities as stable contracts. Each skill describes **what** the system can do, not **how** it is implemented.

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

### User Activity Monitoring

View application logs, user actions, and system events with filtering capabilities.

Provides audit trail access for compliance and troubleshooting. Supports filtering by severity level, user, time range, and event category.

**Inputs**
- Filter criteria (level, user, date range, category)

**Outputs**
- Log entries matching criteria
- Statistical summaries

**Scenarios**
- Reviewing error logs for troubleshooting
- Auditing specific user activity
- Monitoring system health

---

### Endpoint Performance Analysis

View API response time statistics and performance metrics by controller and endpoint.

Enables performance monitoring and capacity planning. Identifies slow endpoints and usage patterns.

**Inputs**
- Controller or endpoint filter (optional)
- Time range (optional)

**Outputs**
- Response time statistics
- Request volume metrics

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

For implementation details, routing logic, and API specifications, see:
- [Skill Selectors](./selectors.md) - Routing and selection rules
- [Interface Mappings](./interfaces/) - API endpoint specifications
