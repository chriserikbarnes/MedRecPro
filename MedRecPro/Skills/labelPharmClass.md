### Pharmacologic Class Search

Search by therapeutic/pharmacologic class.

#### Search by Pharmacologic Class
```
GET /api/Label/pharmacologic-class/search?classNameSearch={value}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `classNameSearch` | string | Yes | Pharmacologic class name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: Find all beta blockers, ACE inhibitors

**Trigger Phrases**: "find by drug class", "beta blockers", "ACE inhibitors", "drugs in class"

#### Pharmacologic Class Summaries
```
GET /api/Label/pharmacologic-class/summaries?pageNumber={n}&pageSize={n}
```

Get class summaries with product counts.

#### Pharmacologic Class Hierarchy
```
GET /api/Label/pharmacologic-class/hierarchy
```

Get the therapeutic class hierarchy tree (useful for faceted navigation).

---
