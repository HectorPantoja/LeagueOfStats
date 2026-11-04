using System.Globalization;
using LeagueOfStats.Domain.Catalog;
using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Presentation.ViewModels;

public sealed record LastMatchCard(string Champion, string? ChampionImageUrl, string Result, string Details,
    string Kda, string Farming, string Economy, string Vision, string CaptureNote, string AssetsNote,
    IReadOnlyList<LastMatchItem> Items)
{
    public string InventoryNote => Items.Count == 0 ? "No hay objetos registrados en la última muestra." : "Objetos de la última muestra";

    public static LastMatchCard From(MatchSummary match, GameCatalog? catalog)
    {
        var champion = catalog?.Champions.FirstOrDefault(c =>
            string.Equals(c.Id, match.ChampionId, StringComparison.OrdinalIgnoreCase)) ??
            catalog?.Champions.FirstOrDefault(c => string.Equals(c.Name, match.Champion, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Id, match.Champion, StringComparison.OrdinalIgnoreCase));
        var final = match.Final;
        var items = (final?.Items ?? []).Where(i => i.Id > 0 && i.Count > 0).Select(i =>
        {
            var item = catalog?.Items.FirstOrDefault(c => c.Id == i.Id);
            return new LastMatchItem(i.Id, item?.Name ?? $"Objeto #{i.Id}", i.Count,
                AssetUrl(catalog, "item", item?.Image.Full));
        }).ToArray();
        var time = DateTimeOffset.TryParse(match.EndedUtc ?? match.StartedUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var date)
            ? TimeZoneInfo.ConvertTime(date, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).ToString("dd/MM/yyyy HH:mm") + " · CDMX"
            : "Fecha no disponible";
        var duration = TimeSpan.FromSeconds(Math.Max(0, match.DurationSeconds));
        return new(champion?.Name ?? (string.IsNullOrWhiteSpace(match.Champion) ? "Campeón no disponible" : match.Champion),
            AssetUrl(catalog, "champion", champion?.Image.Full),
            match.Status != "Completed" ? "Captura incompleta" : match.Result switch
            { "Win" => "Victoria", "Lose" => "Derrota", _ => "Finalizada · resultado desconocido" },
            $"{time} · {match.GameMode} / mapa {match.MapNumber} · {match.Position} · {(int)duration.TotalMinutes}:{duration.Seconds:00}",
            final is null ? "K/D/A no disponible" : $"{final.Kills} / {final.Deaths} / {final.Assists} K/D/A",
            final is null ? "CS no disponible" : $"{final.CreepScore} CS · {match.CsPerMinute:0.0}/min",
            final is null ? "Nivel y oro no disponibles" : $"Nivel {final.Level} · {final.CurrentGold:0} oro disponible",
            final?.WardScore is { } ward ? $"Puntuación de visión: {ward:0.0}" : "Puntuación de visión no disponible",
            match.Status == "Completed" && !match.HasGaps && !match.EventsIncomplete && match.FirstGameTime <= 60
                ? "Estadísticas de la última muestra guardada." : "Captura parcial: las estadísticas pueden no representar el final de la partida.",
            catalog is null ? "Sin catálogo local: se muestran los datos disponibles. Sincroniza el catálogo para cargar nombres e imágenes."
                : $"Imágenes del catálogo {catalog.Patch.Version}; el parche exacto de la partida es desconocido.", items);
    }

    private static string? AssetUrl(GameCatalog? catalog, string kind, string? file)
    {
        if (catalog is null || string.IsNullOrWhiteSpace(file)) return null;
        return $"https://ddragon.leagueoflegends.com/cdn/{catalog.Patch.Version.Value}/img/{kind}/{Uri.EscapeDataString(file)}";
    }
}
