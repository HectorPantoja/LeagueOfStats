using System.Text.RegularExpressions;
using LeagueOfStats.Application;
namespace LeagueOfStats.Infrastructure.Riot;

// Official website adapter, not a Riot JSON API. Keep independent from Data Dragon.
public sealed class RiotPatchNotesSource(HttpClient http) : IPatchNotesSource
{
    public const string IndexUrl = "https://www.leagueoflegends.com/es-mx/news/tags/patch-notes/";
    public async Task<IReadOnlyList<Uri>> ReadLinksAsync(CancellationToken token) => Parse(await http.GetStringAsync(IndexUrl, token));

    public static IReadOnlyList<Uri> Parse(string html)
    {
        var normalized = html.Replace("\\/", "/");
        var links = Regex.Matches(normalized,
            @"/es-mx/news/game-updates/[a-z0-9-]*patch-[0-9]+-[0-9]+-notes/?",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(m => new Uri("https://www.leagueoflegends.com" + m.Value.TrimEnd('/') + "/"))
            .Distinct().ToArray();
        if (links.Length == 0) throw new InvalidDataException("No se encontraron notas; revisar el formato del sitio oficial.");
        return Array.AsReadOnly(links);
    }
}
