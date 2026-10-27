using System.Globalization;

namespace RdpNext.App;

public static class TurkishText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // Rakamla biten adlar okunuşlarına göre ek alır: "web-01" -> bir -> "'den".
    private static readonly string[] DigitWords = ["sıfır", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"];

    // Ayrılma hâli: "Üretim" -> "Üretim'den", "Test" -> "Test'ten", "Ankara" -> "Ankara'dan".
    public static string From(string name)
    {
        var word = name.ToLower(Turkish);
        if (word.Length > 0 && char.IsAsciiDigit(word[^1]))
            word = DigitWords[word[^1] - '0'];
        var lastVowel = word.LastOrDefault(c => "aıoueiöü".Contains(c));
        var vowel = "aıou".Contains(lastVowel) && lastVowel != '\0' ? 'a' : 'e';
        var consonant = word.Length > 0 && "çfhkpsşt".Contains(word[^1]) ? 't' : 'd';
        return $"{name}'{consonant}{vowel}n";
    }
}
