namespace LeagueOfStats.Application;

public sealed class RefreshPatchNotes(IPatchNotesSource source, IPatchNotesRepository repository)
{
    public async Task<string> ExecuteAsync(CancellationToken token)
    {
        var links = await source.ReadLinksAsync(token);
        await repository.SaveAsync(links, token);
        return $"Notas oficiales: {links.Count} publicaciones disponibles";
    }
}
