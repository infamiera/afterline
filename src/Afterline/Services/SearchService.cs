using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public enum SearchMode
{
    Contains,
    Exact,
    WholeWord,
    Regex
}

public sealed class SearchService
{
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string root,
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        string query = criteria.PrimaryTerm.Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchHit>();

        StringComparison comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Regex? regex = criteria.Mode switch
        {
            SearchMode.Regex => new Regex(query, criteria.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)),
            SearchMode.WholeWord => new Regex($@"\b{Regex.Escape(query)}\b", criteria.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)),
            _ => null
        };

        string[] additionalTerms = criteria.AdditionalTerms
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(criteria.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hits = new List<SearchHit>();
        foreach (string file in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] lines;
            try { lines = await File.ReadAllLinesAsync(file, cancellationToken); }
            catch { continue; }

            // Extended search behaves like an AND filter across the chatlog. This is useful for
            // finding sessions involving two characters, while keeping the primary-term hits readable.
            if (additionalTerms.Length > 0)
            {
                bool containsAll = additionalTerms.All(term => lines.Any(line => line.Contains(term, comparison)));
                if (!containsAll) continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                bool matches = criteria.Mode switch
                {
                    SearchMode.Exact => string.Equals(lines[i].Trim(), query, comparison),
                    SearchMode.Regex or SearchMode.WholeWord => regex!.IsMatch(lines[i]),
                    _ => lines[i].Contains(query, comparison)
                };

                if (!matches) continue;
                int start = Math.Max(0, i - Math.Max(0, criteria.ContextLines));
                int end = Math.Min(lines.Length - 1, i + Math.Max(0, criteria.ContextLines));
                string context = string.Join(Environment.NewLine,
                    Enumerable.Range(start, end - start + 1)
                        .Select(n => $"{n + 1,6}  {lines[n]}"));
                hits.Add(new SearchHit(file, i + 1, lines[i], context));
            }
        }

        return hits;
    }
}
