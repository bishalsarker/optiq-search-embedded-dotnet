using System.Linq.Expressions;
using System.Reflection;

namespace Optiq.Embedded.Configurations
{
    public class OptiqOptions
    {
        public string DbPath { get; private set; } = string.Empty;
        public Dictionary<Type, IndexDefinition> IndexDefinitions { get; } = new();

        public void UseDb(string dbPath)
        {
            DbPath = dbPath;
        }

        public void Index<T>(Action<IndexBuilder<T>> configure) where T : class
        {
            var builder = new IndexBuilder<T>();
            configure(builder);
            IndexDefinitions[typeof(T)] = builder.Build();
        }
    }

    public class IndexDefinition
    {
        public List<PropertyInfo> SearchableFields { get; set; } = new();
        public List<PropertyInfo> FilterableFields { get; set; } = new();
        public List<PropertyInfo> SortableFields { get; set; } = new();
    }

    public class IndexBuilder<T>
    {
        private readonly IndexDefinition _definition = new();

        public IndexBuilder<T> Searchable<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            _definition.SearchableFields.Add(GetPropertyInfo(expression));
            return this;
        }

        public IndexBuilder<T> Filterable<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            _definition.FilterableFields.Add(GetPropertyInfo(expression));
            return this;
        }

        public IndexBuilder<T> Sortable<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            _definition.SortableFields.Add(GetPropertyInfo(expression));
            return this;
        }

        public IndexDefinition Build() => _definition;

        private static PropertyInfo GetPropertyInfo<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression member && member.Member is PropertyInfo prop)
            {
                return prop;
            }

            throw new ArgumentException("Expression must point directly to a valid class property.");
        }
    }
}
