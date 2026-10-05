using BioAnalyzer.App.Contracts.Services;
using BioAnalyzer.App.Models;
using Microsoft.AspNetCore.Components;

namespace BioAnalyzer.App.Components;

public partial class AbstractPopup(ISearchService searchService) : ComponentBase
{
    private readonly Dictionary<string, LiteratureAbstract> _abstractCache = new(StringComparer.OrdinalIgnoreCase);
    [Parameter]
    public LiteratureReference? LiteratureReference { get; set; }

    [Parameter]
    public EventCallback PopupClosed { get; set; }
    
    private LiteratureReference? _literatureReference;
    private string? _activePmcId;
    
    public LiteratureAbstract? Abstract { get; set; }

    
    protected override async Task OnParametersSetAsync()
    {
        _literatureReference = LiteratureReference;
        if (_literatureReference == null)
        {
            _activePmcId = null;
            Abstract = null;
            return;
        }

        var pmcId = _literatureReference.PmcId;
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            _activePmcId = null;
            Abstract = null;
            return;
        }

        if (_abstractCache.TryGetValue(pmcId, out var cachedAbstract))
        {
            _activePmcId = pmcId;
            Abstract = cachedAbstract;
            return;
        }

        if (string.Equals(_activePmcId, pmcId, StringComparison.Ordinal))
        {
            return;
        }

        _activePmcId = pmcId;
        var articleAbstract = await searchService.GetAbstract(pmcId).ConfigureAwait(false);
        Abstract = articleAbstract;
        _abstractCache[pmcId] = articleAbstract;
    }
    
    public async Task Close()
    {
        Abstract = null;
        _literatureReference = null;
        _activePmcId = null;
        await PopupClosed.InvokeAsync();
    }
}