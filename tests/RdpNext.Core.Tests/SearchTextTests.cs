using RdpNext.Core.Search;

namespace RdpNext.Core.Tests;

public class SearchTextTests
{
    [Theory]
    [InlineData("Müşteriler", "musteriler")]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("SQL-IST", "sql-ist")]
    [InlineData("ıIİi", "iiii")]
    public void FoldRemovesCaseAndTurkishMarks(string text, string expected) =>
        Assert.Equal(expected, SearchText.Fold(text));

    [Theory]
    [InlineData("SQL-IST", "ist")]
    [InlineData("Çağrı Merkezi", "cagri")]
    public void MatchesIgnoresTurkishLetterDifferences(string text, string query) =>
        Assert.True(SearchText.Matches(text, SearchText.Fold(query)));
}
