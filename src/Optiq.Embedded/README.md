# \# 🟢 Optiq

# 

# \[!\[NuGet Version](https://shields.io)](https://nuget.org)

# \[!\[License](https://shields.io)](LICENSE)

# \[!\[.NET Core](https://shields.io)](https://microsoft.com)

# 

# \*\*Optiq\*\* is an open-source, hyper-fast, zero-infrastructure embedded search and NoSQL document database engine built natively for the .NET ecosystem. 

# 

# By building directly on top of \*\*RocksDB\*\* (the low-latency log-structured merge-tree engine optimized by Meta), Optiq collapses a document store, a structured database, and an Elasticsearch-style search engine into a single, seamless, in-process library. No heavy server infrastructure, no external process management—just microsecond data access.

# 

# \---

# 

# \## ✨ Features \& Capabilities

# 

# \*   📦 \*\*Zero-Infrastructure Footprint:\*\* Runs entirely inside your .NET application process. No SQL Server, PostgreSQL, or Java JVM (Elasticsearch) instances to host or configure.

# \*   🔒 \*\*Zero Index Drift:\*\* Core data records and multi-tier search indices are updated together inside a single atomic write transaction (`WriteBatch`). They succeed or fail together—your index can \*never\* drift out of sync.

# \*   ⚡ \*\*Sub-Millisecond Queries:\*\* Executes complex structured lookups, numeric/date ranges, and full-text searches simultaneously using highly optimized $\\mathcal{O}(\\log N)$ disk prefix scans.

# \*   📊 \*\*Automated Real-Time Facets:\*\* Delivers instant, zero-configuration aggregated counts (navigation sidebars) for all fields marked as filterable, adapting instantly to your active search results.

# \*   📝 \*\*Permanent Runtime Synonyms:\*\* Add, edit, or clear synonym mapping dictionaries at runtime. Changes are written permanently into a system partition and affect searches instantly.

# \*   🪶 \*\*Garbage-Collector Friendly:\*\* Leverages raw byte streams and structural tracking safely behind clean abstractions to eliminate heap allocations under heavy workloads.

# 

# \---

# 

# \## ⚙️ How It Works Internally

# 

# Optiq achieves its speed by taking an unstructured key-value byte store (RocksDB) and applying a strict \*\*Inverted Index Schema\*\* across its storage spaces:

# 

# 1\.  \*\*Document Store:\*\* Every entity (e.g., `Product`) gets a dedicated Column Family. Objects are serialized into JSON bytes and saved under a direct pointer lookup: `Key: \[Id] -> Value: \[JSON Bytes]`.

# 2\.  \*\*Lexicographical Indexing:\*\* RocksDB stores keys in permanent alphabetical order. Optiq encodes structural metadata straight into the \*\*Key string itself\*\*, leaving the value empty to save disk space.

# &#x20;   \*   \*\*Text Search:\*\* Sentences are stripped of punctuation and stop words (\*the, is, and\*), expanded via your live synonyms, and mapped: `idx:search:\[FieldName]:\[Word]:\[Id] -> \[]`.

# &#x20;   \*   \*\*Exact Filters:\*\* Normalized lowercased items map straight to the ID: `idx:filter:\[FieldName]:\[Value]:\[Id] -> \[]`.

# &#x20;   \*   \*\*Ranges (Numbers \& Dates):\*\* Alphabetical bytes don't sort like numbers. Optiq forces numerical fields into fixed-width zero-padded strings (`00000149.99`) and `DateTime` fields into 12-digit Unix Timestamps (`01767225600000`), forcing alphabetical sorting to perfectly match mathematical values: `idx:range:\[FieldName]:\[PaddedString]:\[Id] -> \[]`.

# 3\.  \*\*Prefix Seeks:\*\* When you query, an unmanaged RocksDB Iterator snaps instantly to the exact page block matching your criteria bounds in $\\mathcal{O}(\\log N)$ time, avoiding slow full-table scans.

# 

# \---

# 

# \## 🚀 Getting Started

# 

# \### 1. Install via NuGet

# ```bash

# dotnet add package Optiq

# ```

# 

# \### 2. Register via Dependency Injection (`Program.cs`)

# Configure your data paths, permanent options, and fluent index blueprints easily at application startup:

# 

# ```csharp

# using Microsoft.Extensions.DependencyInjection;

# 

# var builder = WebApplication.CreateBuilder(args);

# 

# builder.Services.AddOptiq(options =>

# {

# &#x20;   // Define the directory path where physical database files will be saved

# &#x20;   options.UseRocksDb("./App\_Data/optiq\_store.db");

# 

# &#x20;   // Map your structural entity indices using type-safe C# expressions

# &#x20;   options.Index<Product>(x =>

# &#x20;   {

# &#x20;       x.Searchable(p => p.Name);

# &#x20;       x.Searchable(p => p.Description);

# 

# &#x20;       x.Filterable(p => p.Brand);

# &#x20;       x.Filterable(p => p.Category);

# 

# &#x20;       x.Sortable(p => p.Price);

# &#x20;       x.Sortable(p => p.CreatedAt);

# &#x20;   });

# });

# ```

# 

# \### 3. Expose the Unified Web API Endpoint

# Accept MongoDB-style dynamic search queries directly out of standard HTTP query parameters. By binding to `Dictionary<string, JsonElement>`, the endpoint parses complex operator arrays and child parameters safely:

# 

# ```csharp

# using Microsoft.AspNetCore.Mvc;

# using System.Text.Json;

# 

# \[ApiController]

# \[Route("api/\[controller]")]

# public class ProductsController : ControllerBase

# {

# &#x20;   private readonly IOptiqContext \_context;

# 

# &#x20;   public ProductsController(IOptiqContext context) => \_context = context;

# 

# &#x20;   \[HttpGet("query")]

# &#x20;   public ActionResult<OptiqQueryResult<Product>> GetProducts(

# &#x20;       \[FromQuery] string? filters,

# &#x20;       \[FromQuery] int pageNumber = 1,

# &#x20;       \[FromQuery] int pageSize = 10)

# &#x20;   {

# &#x20;       var productSet = \_context.SetEntity<Product>();

# &#x20;       var optiqQuery = new OptiqQuery();

# 

# &#x20;       if (!string.IsNullOrWhiteSpace(filters))

# &#x20;       {

# &#x20;           var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(filters);

# &#x20;           if (parsed != null) optiqQuery = new OptiqQuery(parsed);

# &#x20;       }

# 

# &#x20;       int skip = (pageNumber - 1) \* pageSize;

# &#x20;       

# &#x20;       // Query returns both your paginated Items and automated Facet sidebar metrics together!

# &#x20;       OptiqQueryResult<Product> response = productSet.Query(optiqQuery, skip, pageSize);

# &#x20;       return Ok(response);

# &#x20;   }

# }

# ```

# 

# \---

# 

# \## 📡 Web API Query JSON Syntax

# 

# Optiq evaluates separate parameters as standard logical \*\*`AND`\*\* constraints at the root level.

# 

# \### Direct Exact Match

# ```json

# { "Brand": "Dell" }

# ```

# 

# \### Array Set Match (Implicit "IN" filter)

# Passing a standard bracketed array `\[]` automatically instructs the engine to accept any matching items in the list:

# ```json

# { "Category": \["Computers", "Desktops"] }

# ```

# 

# \### Bounded Numeric \& Timestamp Ranges

# Combine operators (`$gt`, `$gte`, `$lt`, `$lte`) inside a single property block. Optiq resolves open or closed ranges within a single high-performance disk point-seek operation:

# ```json

# {

# &#x20; "Price": { "\\(gte": 400.00, "\\)lte": 1200.00 },

# &#x20; "CreatedAt": { "\\$gte": 1767225600000 }

# }

# ```

# 

# \---

# 

# \## 🗃️ Permanent Runtime Synonyms

# 

# Manage keyword synonym mappings dynamically through your data access layer. These updates persist permanently across application restarts without requiring configuration asset rebuilds:

# 

# ```csharp

# IOptiqContext context = serviceProvider.GetRequiredService<IOptiqContext>();

# 

# // Add a permanent, bidirectional synonym relationship grouping to the database

# context.AddSynonymGroup("laptop", "notebook", "computer");

# 

# // Wipe out a relationship grouping cleanly

# context.RemoveSynonymGroup("laptop");

# 

# // Read all active mappings

# Dictionary<string, string\[]> currentSynonyms = context.GetAllSynonyms();

# ```

# 

# \---

# 

# \## 🤝 Contributing

# 

# Contributions are welcome! If you find a bug or have an optimization idea (such as zero-allocation `ReadOnlySpan<byte>` parsing enhancements or multi-field sorting pipelines), feel free to open an issue or submit a pull request.

# 

# 1\. Fork the Project

# 2\. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)

# 3\. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)

# 4\. Push to the Branch (`git push origin feature/AmazingFeature`)

# 5\. Open a Pull Request

# 

# \---

# 

# \## 📄 License

# 

# Distributed under the MIT License. See `LICENSE` file for more details.



