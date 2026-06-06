# Optiq

An embedded search engine for .NET. Add full-text search, filters, and facets to any application — no Elasticsearch, no external services, no DevOps.

Backed by **RocksDB** for persistent, crash-safe storage with microsecond read latency.

---

## Why Optiq

Most search solutions require running a separate process, managing a server, and dealing with sync issues between your database and your search index. Optiq runs inside your application. There is no separate process. The index never drifts out of sync because writes to the document store and all index keys happen in a single atomic transaction.

---

## Features

- **Full-text search** — tokenises and indexes any string field. Finds relevant results across multiple fields simultaneously.
- **Fuzzy matching** — tolerates typos. A search for `headpone` finds `headphone`.
- **Synonym groups** — `laptop` finds `notebook`. Synonyms are bidirectional, persistent, and take effect immediately.
- **Exact filters** — filter by string, boolean, or number. Pass an array for OR matching.
- **Range queries** — filter by price range, date range, or any numeric field using `$gte`, `$lte` and similar operators.
- **Facet counts** — every search response includes aggregated value counts for filterable fields, ready to power a filter sidebar.
- **Atomic writes** — document and all index keys are written together in a single `WriteBatch`. The index cannot drift out of sync.
- **Zero infrastructure** — ships as a NuGet package. No server, no Docker container, no configuration files beyond your `appsettings.json`.

---

## Install

```bash
dotnet add package Optiq.Search
```

---

## Quick Start

### 1. Register in Program.cs

```csharp
builder.Services.AddOptiq(options =>
{
    options.UseDb("./search.db");

    options.Index<Product>(x =>
    {
        x.Searchable(p => p.Name);
        x.Searchable(p => p.Description);

        x.Filterable(p => p.Brand);
        x.Filterable(p => p.Category);
        x.Filterable(p => p.IsInStock);

        x.Sortable(p => p.Price);
        x.Sortable(p => p.CreatedAt);
    });
});
```

### 2. Index a document

```csharp
public class ProductService(IOptiqContext context)
{
    public void Add(Product product)
        => context.SetEntity<Product>().Upsert(product.Id, product);
}
```

### 3. Search

```csharp
var result = context
    .SetEntity<Product>()
    .Search(
        keyword: "wireless headphones",
        query:   new OptiqQuery(),
        skip:    0,
        take:    20
    );

// result.Items  → List<Product> for this page
// result.Facets → aggregated filter counts per field
```

---

## Filtering

Filters are passed as a JSON string and use MongoDB-style operators.

**Exact match**
```json
{ "Brand": "Dell" }
```

**Array match** — OR logic, returns any item in the list
```json
{ "Category": ["Computers", "Laptops"] }
```

**Numeric range**
```json
{ "Price": { "$gte": 400, "$lte": 1200 } }
```

**Combined** — multiple keys are AND'd together
```json
{
  "Brand": "Dell",
  "Price": { "$gte": 500 }
}
```

In an ASP.NET controller:

```csharp
[HttpGet("products")]
public ActionResult<OptiqQueryResult<Product>> Search(
    [FromQuery] string? keyword,
    [FromQuery] string? filters,
    [FromQuery] int page     = 1,
    [FromQuery] int pageSize = 20)
{
    var query = string.IsNullOrWhiteSpace(filters)
        ? new OptiqQuery()
        : JsonSerializer.Deserialize<OptiqQuery>(filters)!;

    var result = context
        .SetEntity<Product>()
        .Search(keyword, query, skip: (page - 1) * pageSize, take: pageSize);

    return Ok(result);
}
```

---

## Synonyms

Synonym groups are bidirectional and persist across restarts.

```csharp
// Adding "laptop" also makes "notebook" and "portable" find laptop documents
context.AddSynonymGroup("laptop", "notebook", "portable");

// Remove an entire group by any member
context.RemoveSynonymGroup("laptop");

// Inspect all active mappings
Dictionary<string, string[]> all = context.GetAllSynonyms();
```

---

## How It Works

Optiq encodes query metadata directly into RocksDB key strings, leaving values empty. Because RocksDB stores keys in sorted order, it can jump to any starting point and scan forward in logarithmic time — no full-table scans.

| Key pattern | Used for |
|---|---|
| `{id}` | Primary document store. Value = JSON bytes. |
| `idx:search:{Field}:{word}:{id}` | Full-text posting list. One key per (field, token, document). |
| `idx:filter:{Field}:{value}:{id}` | Exact filter index. Also drives facet counts. |
| `idx:range:{Field}:{paddedNumber}:{id}` | Range index. Numbers are zero-padded so alphabetical order matches numerical order. |
| `syn:{word}` *(synonyms CF)* | Synonym mapping. Value = comma-separated synonym list. |

Every search runs three stages in sequence:

1. **Keyword scan** — tokenise the query, expand with synonyms, collect matching IDs from the search index (OR logic across tokens).
2. **Filter intersection** — apply each filter condition and intersect the result set (AND logic).
3. **Paginate and hydrate** — slice the ID set, then read and deserialise only the documents on the target page.

---

## Contributing

Bug reports, feature requests, and pull requests are welcome.

For significant changes please open an issue first so we can discuss the approach before you invest time writing code.

**Areas where contributions are especially useful:**
- Additional language stop-word lists
- Multi-field sort support
- `ReadOnlySpan<byte>` zero-allocation parsing in the index scanner
- Integration guides for popular .NET frameworks

```bash
git clone https://github.com/bishalsarker/optiq-search-embedded-dotnet
cd optiq
dotnet restore
dotnet build
dotnet test
```

---

## License

MIT — see [LICENSE](LICENSE) for details.
