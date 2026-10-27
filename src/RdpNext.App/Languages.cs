using System.Globalization;

namespace RdpNext.App;

// Arayüz dili: kayıtlı seçim varsa o, yoksa sistem dili; Türkçe dışındaki her sistem dili İngilizce açılır.
public static class Languages
{
    public const string Turkish = "tr";
    public const string English = "en";

    public static CultureInfo Resolve(string? saved, CultureInfo system) => CultureInfo.GetCultureInfo(
        saved is Turkish or English ? saved
        : system.TwoLetterISOLanguageName == Turkish ? Turkish
        : English);

    public static CultureInfo Current => Strings.Strings.Culture ?? CultureInfo.CurrentUICulture;

    // Açılışta, ilk pencere kurulmadan çağrılır; metinler sonradan yenilenmez.
    public static void Apply(CultureInfo culture)
    {
        Strings.Strings.Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    // Değerin geldiği klasör: "Üretim'den" ya da "from Üretim".
    public static string From(string group, CultureInfo culture) => culture.TwoLetterISOLanguageName == Turkish
        ? TurkishText.From(group)
        : string.Format(Strings.Strings.ResourceManager.GetString(nameof(Strings.Strings.InheritedFrom), culture)!, group);
}
