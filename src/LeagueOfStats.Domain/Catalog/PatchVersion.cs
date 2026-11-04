using System.Text.RegularExpressions;

namespace LeagueOfStats.Domain.Catalog;

public sealed record PatchVersion
{
    public string Value { get; }
    public PatchVersion(string value)
    {
        if (!Regex.IsMatch(value ?? "", @"\A[0-9]+\.[0-9]+\.[0-9]+\z")) throw new ArgumentException("Invalid Data Dragon version", nameof(value));
        Value = value!;
    }
    public override string ToString() => Value;
}

