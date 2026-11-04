using LeagueOfStats.Application;
namespace LeagueOfStats.Presentation.ViewModels;

public sealed class LastMatchViewModel(IReviewRepository reviews, ICatalogRepository catalogs, IAppDiagnostics diagnostics, IImageAssetCache images)
    : ObservableViewModel
{
    private LastMatchCard? card;
    private string status = "Buscando la última partida guardada…";
    public LastMatchCard? Card { get => card; private set => Set(ref card, value); }
    public string Status { get => status; private set => Set(ref status, value); }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshAsync(token);
            await Task.Delay(TimeSpan.FromSeconds(30), token);
        }
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            var next = await Task.Run(async () =>
            {
                var rows = await reviews.ListAsync(token);
                var match = rows.FirstOrDefault(m => m.CanReview);
                if (match is null) return null;
                LeagueOfStats.Domain.Catalog.GameCatalog? catalog = null;
                try { catalog = await catalogs.FindAsync("lan", "es_MX", token); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { diagnostics.Write("last-match.catalog.failed", error: ex); }
                return LastMatchCard.From(match, catalog);
            }, token);
            token.ThrowIfCancellationRequested();
            if (next is null) Card = null;
            else
            {
                if (Card is null || Card.Details != next.Details)
                    Card = next with { ChampionImageUrl = null, Items = next.Items.Select(i => i with { ImageUrl = null }).ToArray(),
                        AssetsNote = "Cargando retrato e iconos oficiales…" };
                Card = await LoadImagesAsync(next, token);
            }
            Status = next is null ? "Todavía no hay partidas cerradas guardadas. La sección se actualizará automáticamente." : "";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Status = "No se pudo actualizar la última partida; se reintentará automáticamente.";
            diagnostics.Write("last-match.load.failed", error: ex);
        }
    }

    private async Task<LastMatchCard> LoadImagesAsync(LastMatchCard source, CancellationToken token)
    {
        Task<string?> Load(string? url) => url is null ? Task.FromResult<string?>(null) : images.GetAsync(url, token);
        var champion = Load(source.ChampionImageUrl);
        var itemTasks = source.Items.Select(async item => item with { ImageUrl = await Load(item.ImageUrl) }).ToArray();
        await Task.WhenAll(itemTasks.Cast<Task>().Append(champion));
        var items = itemTasks.Select(t => t.Result).ToArray();
        var loaded = items.Count(i => i.ImageUrl is not null) + (champion.Result is null ? 0 : 1);
        var expected = items.Length + 1;
        return source with { ChampionImageUrl = champion.Result, Items = items,
            AssetsNote = $"Imágenes disponibles: {loaded}/{expected}. " + source.AssetsNote +
                (loaded < expected ? " Las imágenes pendientes se reintentan automáticamente." : " Disponibles en caché local.") };
    }
}
