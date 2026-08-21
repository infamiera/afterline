using Afterline.Services;

namespace Afterline.Models;

public sealed class SearchCriteria
{
    public string PrimaryTerm { get; init; } = string.Empty;
    public IReadOnlyList<string> AdditionalTerms { get; init; } = Array.Empty<string>();
    public SearchMode Mode { get; init; } = SearchMode.Contains;
    public bool CaseSensitive { get; init; }
    public int ContextLines { get; init; } = 3;
}
