# Pharmacologic Class - API Interface

Search products by therapeutic or pharmacologic class.

---

## Endpoints

### Search by Class Name

```
GET /api/Label/pharmacologic-class/search?classNameSearch={value}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `classNameSearch` | string | Yes | Pharmacologic class name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example Use Cases**:
- Find all beta blockers
- Find all ACE inhibitors
- Find drugs in a specific therapeutic class

**Trigger Phrases**: "find by drug class", "beta blockers", "ACE inhibitors", "drugs in class"

---

### Get Class Summaries

```
GET /api/Label/pharmacologic-class/summaries?pageNumber={n}&pageSize={n}
```

Returns class summaries with product counts. Useful for understanding the distribution of products across therapeutic classes.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### Get Class Hierarchy

```
GET /api/Label/pharmacologic-class/hierarchy
```

Returns the therapeutic class hierarchy tree. Useful for:
- Faceted navigation
- Class browsing interfaces
- Understanding drug class relationships

---

## Example Workflow

**Query**: "What drugs are in the beta blocker class?"

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/pharmacologic-class/search",
      "queryParameters": {
        "classNameSearch": "beta blocker",
        "pageSize": 10
      },
      "description": "Search for products in the beta blocker pharmacologic class",
      "outputMapping": {
        "documentGuids": "documentGUID[]",
        "productNames": "productName[]"
      }
    }
  ]
}
```

---

## Response Fields

| Field | Description |
|-------|-------------|
| `className` | Name of the pharmacologic class |
| `classCode` | Unique code for the class |
| `productName` | Product name |
| `documentGUID` | For building label links |
| `productCount` | Number of products in class (summaries endpoint) |

---

## Common Class Names

| User Query | Search Term |
|------------|-------------|
| "beta blockers" | beta blocker |
| "ACE inhibitors" | ACE inhibitor |
| "calcium channel blockers" | calcium channel blocker |
| "SSRIs" | selective serotonin reuptake inhibitor |
| "statins" | HMG-CoA reductase inhibitor |
| "proton pump inhibitors" | proton pump inhibitor |
| "opioids" | opioid agonist |
| "benzodiazepines" | benzodiazepine |

---

## Integration with Other Skills

Pharmacologic class searches can be combined with:

1. **indicationDiscovery** - Find products by condition, then filter by class
2. **labelContent** - After finding products by class, retrieve specific label sections
3. **equianalgesicConversion** - For opioid class products, check conversion data

---

## Fallback

If `/api/Label/pharmacologic-class/*` returns 404:

```
Try: /api/label/section/PharmacologicClass?pageNumber=1&pageSize=50
```

See [retry-fallback.md](./retry-fallback.md) for full fallback rules.

---

## Related Documents

- [Label Content](./label-content.md) - Section retrieval after class search
- [Indication Discovery](./indication-discovery.md) - Alternative product discovery
- [Retry Fallback](./retry-fallback.md) - Fallback strategies
