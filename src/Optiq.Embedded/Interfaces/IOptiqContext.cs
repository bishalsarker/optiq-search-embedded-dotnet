namespace Optiq.Embedded.Interfaces
{
    public interface IOptiqContext
    {
        void AddSynonymGroup(string mainWord, params string[] synonyms);
        Dictionary<string, string[]> GetAllSynonyms();
        void RemoveSynonymGroup(string word);
        IOptiqSet<TEntity> SetEntity<TEntity>() where TEntity : class;
    }
}
