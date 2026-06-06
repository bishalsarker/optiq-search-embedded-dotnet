using Optiq.Embedded.Configurations;
using Optiq.Embedded.Interfaces;
using Optiq.Embedded.Models;
using Optiq.Embedded.Utilities;
using RocksDbSharp;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Optiq.Embedded.Persistence
{
    public class RocksDbSet<TEntity> : IOptiqSet<TEntity> where TEntity : class
    {
        private readonly RocksDb _db;
        private readonly ColumnFamilyHandle _dataCf;
        private readonly IndexDefinition? _indexDef;
        private readonly ColumnFamilyHandle _synonymCf;

        public RocksDbSet(RocksDb db, string cfName, IndexDefinition? indexDef, ColumnFamilyHandle synonymCf)
        {
            _db = db;
            _dataCf = db.GetColumnFamily(cfName);
            _indexDef = indexDef;
            _synonymCf = synonymCf;
        }

        public OptiqQueryResult<TEntity> Search(string? keyword, OptiqQuery query, int skip, int take)
        {
            var result = new OptiqQueryResult<TEntity>();

            var finalMatchedIds = new HashSet<string>();
            var textSearchIds = new HashSet<string>();
            bool isFirstOperation = true;

            // ========================================================================
            // STAGE 1: TEXT KEYWORD SCANNING (Combines tokens with OR logic)
            // ========================================================================
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                if (_indexDef != null && _indexDef.SearchableFields.Count > 0)
                {
                    var originalTokens = Tokenizer.Tokenize(keyword);
                    if (originalTokens.Count > 0)
                    {
                        var tokensToQuery = new HashSet<string>();

                        foreach (var token in originalTokens)
                        {
                            tokensToQuery.Add(token);
                            var dynamicSynonyms = GetLiveSynonymsForWord(token);
                            tokensToQuery.UnionWith(dynamicSynonyms);
                        }

                        foreach (var token in tokensToQuery)
                        {
                            // MatchByWord returns a HashSet<string> of found document IDs
                            var matchedIds = MatchByWord(token);
                            textSearchIds.UnionWith(matchedIds); // Accumulate all keyword variations
                        }

                        finalMatchedIds = textSearchIds;
                        isFirstOperation = false;

                        // Short-circuit: If a keyword was typed but nothing found, exit immediately
                        if (finalMatchedIds.Count == 0) return new OptiqQueryResult<TEntity>();
                    }
                }
            }

            if (_indexDef?.FilterableFields?.Count > 0)
            {
                foreach (var field in _indexDef.FilterableFields)
                {
                    var facet = new FacetResult
                    {
                        FieldName = field.Name
                    };

                    string prefix = $"idx:filter:{field.Name}:";

                    using var iterator = _db.NewIterator(_dataCf);

                    iterator.Seek(Encoding.UTF8.GetBytes(prefix));

                    while (iterator.Valid())
                    {
                        string key = Encoding.UTF8.GetString(iterator.Key());

                        // left the current field block
                        if (!key.StartsWith(prefix))
                            break;

                        // idx:filter:Brand:Dell:prod_001
                        string[] parts = key.Split(':');

                        if (parts.Length >= 5)
                        {
                            string facetValue = parts[3];
                            string entityId = parts[4];

                            // only count entities that matched search
                            if (finalMatchedIds.Contains(entityId))
                            {
                                if (facet.Counts.TryGetValue(facetValue, out int count))
                                {
                                    facet.Counts[facetValue] = count + 1;
                                }
                                else
                                {
                                    facet.Counts[facetValue] = 1;
                                }
                            }
                        }

                        iterator.Next();
                    }

                    if (facet.Counts.Count > 0)
                    {
                        result.Facets.Add(facet);
                    }
                }
            }

            // ========================================================================
            // STAGE 2: FILTER PROCESSING (Intersects using strict AND behavior)
            // ========================================================================
            var filterDefinitions = QueryBuilder.BuildQuery(query);

            if (filterDefinitions != null && filterDefinitions.Count > 0)
            {
                foreach (var filterDefinition in filterDefinitions)
                {
                    HashSet<string> currentFilterHits = new();

                    // A. Exact Equality Lookups
                    if (filterDefinition is EqualFilter eqFilter)
                    {
                        currentFilterHits = ScanExactFilter(eqFilter.Field, eqFilter.Value?.ToString() ?? string.Empty);
                    }
                    // B. Conditional Range/Numeric Lookups (Min and Max packaged together)
                    else if (filterDefinition is RangeFilter rangeFilter)
                    {
                        currentFilterHits = ScanRangeFilter(rangeFilter.Field, rangeFilter.MinBound, rangeFilter.MaxBound);
                    }
                    else
                    {
                        continue;
                    }

                    // Apply strict logical AND combination rules
                    if (isFirstOperation)
                    {
                        finalMatchedIds = currentFilterHits;
                        isFirstOperation = false;
                    }
                    else
                    {
                        // Ensures items MUST match both text constraints AND property filters
                        finalMatchedIds.IntersectWith(currentFilterHits);
                    }

                    // Short-circuit: If intersections narrow matches down to 0, stop disk operations
                    if (finalMatchedIds.Count == 0) return new OptiqQueryResult<TEntity>();
                }
            }

            // If no text keyword was passed and no filters were provided, return an empty payload
            if (isFirstOperation) return new OptiqQueryResult<TEntity>();

            // ========================================================================
            // STAGE 3: UNIQUE PAGINATION & DISK DATA HYDRATION
            // ========================================================================
            // HashSet inherently guarantees unique items. We apply skip and take now.
            result.Items = finalMatchedIds
                .Skip(skip)
                .Take(take)
                .Select(id => Find(id)) // Only executes heavy JSON deserialization for the target page slice
                .Where(entity => entity != null)
                .Cast<TEntity>()
                .ToList();

            return result;
        }

        public void Upsert(string id, TEntity entity)
        {
            using var batch = new WriteBatch();

            // 1. UPDATE TRACKING: Check if this item already exists in the database
            TEntity? oldEntity = Find(id);
            if (oldEntity != null)
            {
                // If it exists, clear its old structural indexes first to prevent dangling search keys
                ClearIndexesForEntity(batch, id, oldEntity);
            }

            byte[] idBytes = Encoding.UTF8.GetBytes(id);
            byte[] valueBytes = JsonSerializer.SerializeToUtf8Bytes(entity);
            batch.Put(idBytes, valueBytes, _dataCf);

            if (_indexDef != null)
            {
                GenerateIndexes(batch, id, entity);
            }

            _db.Write(batch);
        }

        public void Remove(string id)
        {
            TEntity? existingEntity = Find(id);
            if (existingEntity == null) return; // Nothing to clear

            using var batch = new WriteBatch();

            // 1. Clean up all secondary lookup keys mapping to this item
            ClearIndexesForEntity(batch, id, existingEntity);

            // 2. Delete the actual master record
            byte[] idBytes = Encoding.UTF8.GetBytes(id);
            batch.Delete(idBytes, _dataCf);

            _db.Write(batch);
        }

        public TEntity? Find(string id)
        {
            byte[] idBytes = Encoding.UTF8.GetBytes(id);
            byte[] valueBytes = _db.Get(idBytes, _dataCf);
            return valueBytes == null ? null : JsonSerializer.Deserialize<TEntity>(valueBytes);
        }

        private HashSet<string> ScanRangeFilter(string fieldName, object? min, object? max)
        {
            var ids = new HashSet<string>();

            // 1. Reflectively discover the underlying C# property type for this specific field
            PropertyInfo? prop = typeof(TEntity).GetProperty(fieldName);
            Type propertyType = prop?.PropertyType ?? typeof(double); // Fallback to double if untracked

            // 2. Format the objects into your strict lexically sortable strings
            // If min or max is null, we leave the boundary open-ended
            string? minStr = min != null ? FormatPropertyForSorting(min, propertyType) : null;
            string? maxStr = max != null ? FormatPropertyForSorting(max, propertyType) : null;

            // 3. SEEK BOUNDARY: If min bound string is present, snap to it. Else, start from the field root.
            string startRangeStr = minStr != null
                ? $"idx:range:{fieldName}:{minStr}:"
                : $"idx:range:{fieldName}:";

            byte[] startBytes = Encoding.UTF8.GetBytes(startRangeStr);

            // 4. CEILING BOUNDARY: If max bound string is present, prepare the alphabetical terminator string
            string? endRangeStr = maxStr != null
                ? $"idx:range:{fieldName}:{maxStr}:"
                : null;

            using var iterator = _db.NewIterator(_dataCf);
            iterator.Seek(startBytes);

            while (iterator.Valid())
            {
                string currentKey = Encoding.UTF8.GetString(iterator.Key());

                // Safety Guard: Stop immediately if we wander completely out of this field's index space
                if (!currentKey.StartsWith($"idx:range:{fieldName}:"))
                    break;

                // Bounded Ceiling Check: Break out the exact millisecond we cross past our max bound alphabetically
                if (endRangeStr != null && string.Compare(currentKey, endRangeStr) > 0)
                {
                    break;
                }

                // Extract the unique entity ID from the end of the key string
                ids.Add(currentKey.Split(':')[^1]);
                iterator.Next();
            }

            return ids;
        }

        private HashSet<string> ScanExactFilter(string fieldName, string value)
        {
            var ids = new HashSet<string>();
            string prefix = $"idx:filter:{fieldName}:{value}:";
            byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix);

            using var iterator = _db.NewIterator(_dataCf);
            iterator.Seek(prefixBytes);

            while (iterator.Valid())
            {
                string key = Encoding.UTF8.GetString(iterator.Key());
                if (!key.StartsWith(prefix)) break;

                ids.Add(key.Split(':')[^1]);
                iterator.Next();
            }
            return ids;
        }

        private HashSet<string> GetLiveSynonymsForWord(string cleanWord)
        {
            var synonyms = new HashSet<string>();
            byte[] bytes = _db.Get(Encoding.UTF8.GetBytes($"syn:{cleanWord}"), _synonymCf);

            if (bytes != null)
            {
                string[] items = Encoding.UTF8.GetString(bytes).Split(',');
                foreach (var item in items) synonyms.Add(item.Trim());
            }
            return synonyms;
        }

        private void GenerateIndexes(WriteBatch batch, string id, TEntity entity)
        {
            foreach (var prop in _indexDef!.SearchableFields)
            {
                var text = prop.GetValue(entity)?.ToString()?.ToLower();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var words = Tokenizer.Tokenize(text);

                foreach (var word in words)
                {
                    string indexKey = $"idx:search:{prop.Name}:{word}:{id}";
                    batch.Put(Encoding.UTF8.GetBytes(indexKey), Array.Empty<byte>(), _dataCf);

                    var dynamicSynonyms = GetLiveSynonymsForWord(word);
                    foreach (var syn in dynamicSynonyms)
                    {
                        batch.Put(Encoding.UTF8.GetBytes($"idx:search:{prop.Name}:{syn}:{id}"), Array.Empty<byte>(), _dataCf);
                    }
                }
            }

            foreach (var prop in _indexDef.FilterableFields)
            {
                var val = prop.GetValue(entity)?.ToString();
                if (val == null) continue;

                string indexKey = $"idx:filter:{prop.Name}:{val}:{id}";
                batch.Put(Encoding.UTF8.GetBytes(indexKey), Array.Empty<byte>(), _dataCf);
            }

            if (_indexDef.SortableFields != null)
            {
                foreach (var prop in _indexDef.SortableFields)
                {
                    var value = prop.GetValue(entity);
                    if (value == null) continue;

                    // Format numbers to fixed-width strings for flawless alphabetical sorting
                    string formattedValue = FormatPropertyForSorting(value, prop.PropertyType);

                    // Key layout: idx:range:Price:000000149.99:prod_101
                    string indexKey = $"idx:range:{prop.Name}:{formattedValue}:{id}";
                    batch.Put(Encoding.UTF8.GetBytes(indexKey), Array.Empty<byte>(), _dataCf);
                }
            }
        }

        private string FormatPropertyForSorting(object value, Type propertyType)
        {
            // Handle Nullable wrapper types safely (extract underlying Type)
            Type underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            // 1. Handle Native DateTime Objects
            if (value is DateTime dateVal)
            {
                long unixTimestampMs = new DateTimeOffset(dateVal.ToUniversalTime()).ToUnixTimeMilliseconds();
                return unixTimestampMs.ToString("000000000000"); // 12-digit integer padding
            }

            // 2. Handle Whole Numbers / Integers (Timestamps, Quantity, Counts)
            if (underlyingType == typeof(long) || underlyingType == typeof(int) || underlyingType == typeof(short))
            {
                long longVal = Convert.ToInt64(value);
                return longVal.ToString("000000000000"); // Zero-padded clean whole integer
            }

            // 3. Handle Floating-Point Decimals (Price, Cost, Weight, Ratings)
            if (underlyingType == typeof(double) || underlyingType == typeof(decimal) || underlyingType == typeof(float))
            {
                double doubleVal = Convert.ToDouble(value);
                return doubleVal.ToString("000000000.00"); // 9-digit padding with 2 decimal precision slots
            }

            // Fallback default
            return value.ToString()?.Trim().ToLower() ?? string.Empty;
        }

        private void ClearIndexesForEntity(WriteBatch batch, string id, TEntity oldEntity)
        {
            if (_indexDef == null) return;

            // Strip previous Searchable keys
            foreach (var prop in _indexDef.SearchableFields)
            {
                var rawText = prop.GetValue(oldEntity)?.ToString();
                if (string.IsNullOrWhiteSpace(rawText)) continue;

                var words = Tokenizer.Tokenize(rawText);
                var uniqueWords = new HashSet<string>(words);

                foreach (var word in uniqueWords)
                {
                    string indexKey = $"idx:search:{prop.Name}:{word}:{id}";
                    batch.Delete(Encoding.UTF8.GetBytes(indexKey), _dataCf);
                }
            }

            // Strip previous Filterable keys
            foreach (var prop in _indexDef.FilterableFields)
            {
                var rawValue = prop.GetValue(oldEntity)?.ToString();
                if (string.IsNullOrWhiteSpace(rawValue)) continue;

                string indexKey = $"idx:filter:{prop.Name}:{rawValue.Trim().ToLower()}:{id}";
                batch.Delete(Encoding.UTF8.GetBytes(indexKey), _dataCf);
            }
        }

        private static int MaxAllowedDistance(int length)
        {
            if (length <= 3) return 0;
            if (length <= 6) return 1;
            return 2;
        }

        private HashSet<string> MatchByWord(string token)
        {
            var matchedIds = new HashSet<string>();
            int allowedTypos = MaxAllowedDistance(token.Length);

            foreach (var prop in _indexDef.SearchableFields)
            {
                var propMatchedIds = SearchProperty(prop.Name, token, allowedTypos);
                matchedIds.UnionWith(propMatchedIds);
            }

            return matchedIds;
        }

        private HashSet<string> SearchProperty(string propertyName, string token, int allowedTypos)
        {
            var matched = new HashSet<string>();

            string rootPrefix = $"idx:search:{propertyName}:{token}";
            string propertyPrefix = $"idx:search:{propertyName}:";

            using var iterator = _db.NewIterator(_dataCf);

            iterator.Seek(Encoding.UTF8.GetBytes(rootPrefix));

            while (iterator.Valid())
            {
                string currentKey = Encoding.UTF8.GetString(iterator.Key());

                if (!currentKey.StartsWith(propertyPrefix))
                    break;

                var matchedId = ProcessKey(currentKey, token, allowedTypos);

                if (!string.IsNullOrEmpty(matchedId))
                {
                    matched.Add(matchedId);
                }

                iterator.Next();
            }

            return matched;
        }

        private string ProcessKey(string currentKey, string token, int allowedTypos)
        {
            string[] parts = currentKey.Split(':');

            if (parts.Length < 5)
                return string.Empty;

            string indexedWord = parts[3];
            string entityId = parts[4];

            if (!IsFuzzyMatch(token, allowedTypos, indexedWord))
                return string.Empty;

            return entityId;
        }

        private static bool IsFuzzyMatch(string token, int allowedTypos, string indexedWord)
        {
            bool isMatch = false;
            if (indexedWord == token)
            {
                isMatch = true;
            }
            else if (allowedTypos > 0 && Math.Abs(indexedWord.Length - token.Length) <= allowedTypos)
            {
                int distance = Typo.GetLevenshteinDistance(token, indexedWord);
                if (distance <= allowedTypos) isMatch = true;
            }

            return isMatch;
        }
    }
}
