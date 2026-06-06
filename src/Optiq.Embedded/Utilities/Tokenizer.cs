namespace Optiq.Embedded.Utilities
{
    internal static class Tokenizer
    {
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { 
            // Your original words
            "the", "a", "an", "is", "with", "for", "of", "and", "in",

            // Pronouns
            "i", "me", "my", "myself", "we", "our", "ours", "ourselves", "you", "your", "yours",
            "yourself", "yourselves", "he", "him", "his", "himself", "she", "her", "hers",
            "herself", "it", "its", "itself", "they", "them", "their", "theirs", "themselves",
            "what", "which", "who", "whom", "this", "that", "these", "those",

            // Verbs & Auxiliaries
            "am", "are", "was", "were", "be", "been", "being", "have", "has", "had", "having",
            "do", "does", "did", "doing", "can", "could", "should", "would", "will", "may", "might",

            // Prepositions & Conjunctions
            "to", "from", "up", "down", "on", "off", "over", "under", "again", "further",
            "then", "once", "here", "there", "when", "where", "why", "how", "all", "any",
            "both", "each", "few", "more", "most", "other", "some", "such", "no", "nor",
            "not", "only", "own", "same", "so", "than", "too", "very", "s", "t", "just",
            "don", "shouldn", "now", "at", "by", "about", "against", "between", "into",
            "through", "during", "before", "after", "above", "below", "out"
        };

        private static readonly char[] Delimiters = new[]
        {
            ' ', '.', ',', '!', '?', ';', ':', '-', '_', '(', ')', '[', ']', '"', '\'', '\r', '\n', '\t'
        };

        public static HashSet<string> Tokenize(string rawText)
        {
            var cleanTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawText)) return cleanTokens;

            string[] words = rawText.ToLower().Split(Delimiters, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                string cleanWord = word.Trim();
                if (cleanWord.Length <= 1 || StopWords.Contains(cleanWord))
                {
                    continue;
                }

                cleanTokens.Add(cleanWord);
            }

            return cleanTokens;
        }
    }
}
