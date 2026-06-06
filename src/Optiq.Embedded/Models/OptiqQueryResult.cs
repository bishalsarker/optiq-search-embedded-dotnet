namespace Optiq.Embedded.Models
{
    public class FacetResult
    {
        public string FieldName { get; set; } = string.Empty;
        public Dictionary<string, int> Counts { get; set; } = new();
    }

    public class OptiqQueryResult<TEntity>
    {
        public List<TEntity> Items { get; set; } = new();
        public List<FacetResult> Facets { get; set; } = new();
    }
}
