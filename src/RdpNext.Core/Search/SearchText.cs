using System.Globalization;
using System.Text;

namespace RdpNext.Core.Search;

public static class SearchText
{
    // Büyük/küçük harf, ı/İ/I/i ve ş, ğ, ü gibi işaretli harf farkları yok sayılır.
    public static string Fold(string text)
    {
        var decomposed = text.Replace('ı', 'i').Replace('İ', 'i').Replace('I', 'i').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }

    public static bool Matches(string text, string foldedQuery) =>
        Fold(text).Contains(foldedQuery, StringComparison.Ordinal);
}
