using LeagueOfStats.Domain.Matches;
namespace LeagueOfStats.Application;

public interface IReviewRepository
{
    Task<IReadOnlyList<MatchSummary>> ListAsync(CancellationToken token);
    Task<(MatchReview Review, IReadOnlyList<MatchSummary> Previous)> LoadAsync(string sessionId, CancellationToken token);
    Task SavePackageAsync(MatchSummary expected, string json, CancellationToken token);
}
