using Optiq.Embedded.Configurations;
using Optiq.Embedded.Interfaces;
using RocksDbSharp;
using System.Text;

namespace Optiq.Embedded.Persistence
{
    internal class RocksDbContext : IOptiqContext
    {
        private readonly RocksDb _db;
        private readonly OptiqOptions _options;
        private readonly ColumnFamilyHandle _synonymCf;

        public RocksDbContext(OptiqOptions options)
        {
            
            
            _options = options;

            var dbOptions = new DbOptions().SetCreateIfMissing(true);
            var cfOptions = new ColumnFamilyOptions();
            var cfCollection = new ColumnFamilies();

            // 1. Step 1: Discover what EXACTLY exists physically on disk right now
            if (Directory.Exists(options.DbPath) && Directory.GetFiles(options.DbPath).Length > 0)
            {
                try
                {
                    // Inspect the on-disk manifest file
                    var existingCfs = RocksDb.ListColumnFamilies(dbOptions, options.DbPath);
                    foreach (var cf in existingCfs)
                    {
                        cfCollection.Add(cf, cfOptions);
                    }
                }
                catch (RocksDbException)
                {
                    // Fallback if the folder exists but has no valid active manifest
                    cfCollection.Add("default", cfOptions);
                }
            }

            // 2. Ensure the mandatory "default" handle is present for new databases
            if (!cfCollection.Select(x => x.Name).Contains("default"))
            {
                cfCollection.Add("default", cfOptions);
            }

            // 3. Open RocksDB strictly with what already exists on disk
            _db = RocksDb.Open(dbOptions, options.DbPath, cfCollection);

            try
            {
                _synonymCf = _db.GetColumnFamily("_synonyms");
            }
            catch (KeyNotFoundException)
            {
                // If the folder is old and missing the synonyms partition, this natively registers it on disk
                _synonymCf = _db.CreateColumnFamily(cfOptions, "_synonyms");
            }

            // 4. Step 2: Now that the engine is running, safely create any missing code-defined columns!
            foreach (Type entityType in _options.IndexDefinitions.Keys)
            {
                string cfName = entityType.Name; // e.g., "Product"

                try
                {
                    // Test if it's already mapped in the database instance
                    _db.GetColumnFamily(cfName);
                }
                catch (KeyNotFoundException)
                {
                    // If it's missing, this native call safely spins it up dynamically
                    _db.CreateColumnFamily(cfOptions, cfName);
                }
            }
        }

        public IOptiqSet<TEntity> SetEntity<TEntity>() where TEntity : class
        {
            string cfName = typeof(TEntity).Name;

            _options.IndexDefinitions.TryGetValue(typeof(TEntity), out var indexDef);

            return new RocksDbSet<TEntity>(_db, cfName, indexDef, _synonymCf);
        }

        public void AddSynonymGroup(string mainWord, params string[] synonyms)
        {
            if (string.IsNullOrWhiteSpace(mainWord) || synonyms.Length == 0) return;

            string cleanMain = mainWord.Trim().ToLower();
            var cleanSyns = synonyms.Select(s => s.Trim().ToLower()).Where(s => s != cleanMain).Distinct().ToList();

            using var batch = new WriteBatch();

            // 1. Map main word to all variants: syn:laptop -> notebook,computer
            string mainValue = string.Join(",", cleanSyns);
            batch.Put(Encoding.UTF8.GetBytes($"syn:{cleanMain}"), Encoding.UTF8.GetBytes(mainValue), _synonymCf);

            // 2. Bidirectional reverse map variants back to the group: syn:notebook -> laptop,computer
            foreach (var syn in cleanSyns)
            {
                var alternates = cleanSyns.Where(s => s != syn).Append(cleanMain);
                string synValue = string.Join(",", alternates);
                batch.Put(Encoding.UTF8.GetBytes($"syn:{syn}"), Encoding.UTF8.GetBytes(synValue), _synonymCf);
            }

            _db.Write(batch);
        }

        public void RemoveSynonymGroup(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            string cleanWord = word.Trim().ToLower();

            // Read the variants mapped to this word to clear their cross-references
            byte[] existingBytes = _db.Get(Encoding.UTF8.GetBytes($"syn:{cleanWord}"), _synonymCf);
            if (existingBytes == null) return;

            string[] variants = Encoding.UTF8.GetString(existingBytes).Split(',');

            using var batch = new WriteBatch();

            // Delete the main node
            batch.Delete(Encoding.UTF8.GetBytes($"syn:{cleanWord}"), _synonymCf);

            // Delete all bidirectional child links
            foreach (var variant in variants)
            {
                batch.Delete(Encoding.UTF8.GetBytes($"syn:{variant.Trim()}"), _synonymCf);
            }

            _db.Write(batch);
        }

        public Dictionary<string, string[]> GetAllSynonyms()
        {
            var map = new Dictionary<string, string[]>();
            using var iterator = _db.NewIterator(_synonymCf);
            iterator.Seek(Encoding.UTF8.GetBytes("syn:"));

            while (iterator.Valid())
            {
                string key = Encoding.UTF8.GetString(iterator.Key());
                if (!key.StartsWith("syn:")) break;

                string word = key.Replace("syn:", "");
                string[] values = Encoding.UTF8.GetString(iterator.Value()).Split(',');

                map[word] = values;
                iterator.Next();
            }
            return map;
        }
    }
}
