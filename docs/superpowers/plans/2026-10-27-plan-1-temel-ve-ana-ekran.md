# Plan 1: Temel ve ana ekran

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bağlantı ağacını diskten okuyup onaylı Termius tarzı ana ekranda gösteren, arama ve sekme açıp kapatmayı yapan uygulama iskeleti (RDP bağlantısı henüz yok, sekmede boş oturum alanı).

**Architecture:** `RdpNext.Core` (UI'sız sınıf kütüphanesi: model, devralma, arama, JSON saklama) ve `RdpNext.App` (Avalonia, MVVM). Görünüm modelleri Core modeline sarılır; ekran yalnız görünüm modellerine bağlanır. RDP motoru Plan 2'de sekmenin içeriğini değiştirerek gelir.

**Tech Stack:** .NET 10, Avalonia 12.1.4 (Fluent tema tabanı + kendi stillerimiz), Avalonia.Fonts.Inter 12.1.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3, Avalonia.Headless.XUnit 12.1.4 (xunit.v3 3.2+ ister).

**Spec:** `docs/superpowers/specs/2026-10-27-rdpnext-v1-design.md`. Onaylı çizim: `docs/tasarim/gorsel-stil.png` ve `docs/tasarim/gorsel-stil.html` (renkler ve ölçüler oradan alınır).

## Plan sırası (bütün iş)

1. **Bu plan:** temel ve ana ekran.
2. RDP motoru: FreeRDP bağlama, ekranı Avalonia'ya çizme, klavye/fare, dinamik çözünürlük, pano, ses, sertifika ve giriş soruları. Deneme sunucusu olarak Docker'da xrdp kullanılabilir (bu Mac'te Docker 29.4 çalışıyor; Homebrew'de FreeRDP 3.32.2, Apache-2.0).
3. Formlar ve yönetim: sunucu/grup formu, sağ tık menüleri, sürükle-bırak, kopyala, sil, tema seçimi, işletim sistemi anahtar zinciri. Ekranlar önce çizilip Murat'a onaylatılır.
4. İçe/dışa aktarma: mRemoteNG `confCons.xml`, şifreli yedek dosyası.
5. Dağıtım ve güncelleme: Mac imza + notarize, Windows kurulum, GitHub sürümlerinden güncelleme.

## Global Constraints

1. `dotnet` her zaman `/usr/local/share/dotnet/dotnet` ile çağrılır (Homebrew'inki değil).
2. Hedef çatı `net10.0`; paket sürümleri `Directory.Packages.props` içinde merkezî.
3. Gövde yazısı en az 15 px, ikincil yazılar (adres, sayaç) en az 14 px.
4. Ekranda gereksiz metin yok: ipucu yazısı, açıklama paragrafı, boş durum anlatımı yok. Boş oturum alanı yazısız.
5. Açık ve koyu tema ikisi de var; varsayılan işletim sistemi ayarını izler (`RequestedThemeVariant = Default`).
6. Arayüz metinleri Türkçe ve koda gömülmez: `src/RdpNext.App/Strings/Strings.resx` içinde durur.
7. Yeni sunucunun varsayılan portu 3389; sunucu kaydı `Protocol` alanı taşır (şimdilik yalnız `Rdp`).
8. Şifreler bağlantı dosyasına asla yazılmaz.
9. Kod yorumlarında, commit mesajlarında ve metinlerde uzun tire (em dash) kullanılmaz. Commit mesajları kısa ve Türkçe, `Co-Authored-By` satırı yok.
10. Git: iş `master`'dan açılan `temel-ana-ekran` dalında yapılır; bitince `master`'a `--no-ff` merge edilir ve dal silinir. Push yalnız Murat isteyince.

## Review Focus

1. Türkçe harfle arama: "musteri" yazınca "Müşteriler", "ist" yazınca "SQL-IST" bulunmalı (ı/İ/I/i ve ş/ü/ğ/ç/ö farkı yok sayılır). Test: Task 2.
2. Bozuk ya da yarım yazılmış bağlantı dosyası bağlantıları silmemeli: dosya yedeklenir, kullanıcıya tek satır gösterilir, eski dosyanın üstüne yazılmaz. Test: Task 3 ve Task 4.
3. Aynı sunucuya ikinci kez bağlanınca ikinci sekme açılmamalı, mevcut sekme seçilmeli. Test: Task 6.
4. Büyük ağaç (5000 sunucu) aramada takılmamalı: süzme 100 ms altında. Test: Task 2.
5. Uzun sunucu adı ya da adresi satırı taşırmamalı, "…" ile kesilmeli. Test: Task 4.

---

### Task 1: Çözüm iskeleti, model ve kimlik bilgisi devralma

**Files:**
- Create: `RdpNext.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore` (bin, obj, `.superpowers/`, `*.user`, `.DS_Store`)
- Create: `src/RdpNext.Core/RdpNext.Core.csproj`, `src/RdpNext.Core/Model/Node.cs`, `Group.cs`, `Server.cs`, `Enums.cs`
- Create: `src/RdpNext.Core/Secrets/ISecretStore.cs`, `InMemorySecretStore.cs`
- Create: `src/RdpNext.Core/Credentials/CredentialResolver.cs`
- Test: `tests/RdpNext.Core.Tests/RdpNext.Core.Tests.csproj`, `tests/RdpNext.Core.Tests/CredentialResolverTests.cs`

**Interfaces:**
- Produces:
  - `enum Protocol { Rdp }`, `enum ScreenMode { FitToWindow, FullScreen }`, `enum ServerColor { Blue, Teal, Orange, Violet, Green }`
  - `abstract class Node { Guid Id { get; init; } = Guid.NewGuid(); string Name { get; set; } = ""; Group? Parent { get; internal set; } }` (`Parent` JSON'a yazılmaz)
  - `sealed class Group : Node { string? Username; string? Domain; bool IsExpanded = true; IReadOnlyList<Node> Children; void Add(Node child); void Remove(Node child); IEnumerable<Server> AllServers(); }` (`Add` çocuğun `Parent`'ını ayarlar, başka gruptaysa oradan çıkarır)
  - `sealed class Server : Node { Protocol Protocol = Rdp; string Host = ""; int Port = 3389; string? Username; string? Domain; ServerColor Color = Blue; ScreenMode Screen = FitToWindow; }`
  - `interface ISecretStore { string? GetPassword(Guid nodeId); void SetPassword(Guid nodeId, string password); void DeletePassword(Guid nodeId); }` ve `InMemorySecretStore : ISecretStore`
  - `sealed record ResolvedCredentials(string? Username, string? Password, string? Domain)`
  - `static class CredentialResolver { static ResolvedCredentials Resolve(Server server, ISecretStore secrets); }`

- [ ] **Step 1: İskeleti kur.** `dotnet new sln --format slnx`, `classlib` (Core) ve test projesi; test projesi `xunit.v3` kullanır (Avalonia.Headless.XUnit 12.1.4 xunit.v3 3.2.2 istiyor, aynı aileyi her iki test projesinde kullan). `Directory.Build.props`: `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`. Dal: `git switch -c temel-ana-ekran`.

- [ ] **Step 2: Başarısız testleri yaz** (`CredentialResolverTests`):

```csharp
[Fact] void ServerOwnValuesWin()            // sunucuda "ali"/"pw1"/"AVVA", grupta başka değerler -> sunucununkiler
[Fact] void EmptyFieldsInheritFromNearestGroup() // kök > "Üretim"(user "admin", domain "AVVA", şifre "g") > "Web"(user null) > sunucu(boş) -> ("admin","g","AVVA")
[Fact] void FieldsInheritIndependently()    // sunucuda yalnız user "ali"; şifre ve domain gruptan
[Fact] void WhitespaceCountsAsEmpty()       // sunucu Username = "  " -> grubunki gelir
[Fact] void NothingAnywhereGivesNulls()     // hiçbir yerde değer yok -> (null, null, null)
[Fact] void AddMovesChildBetweenGroups()    // a.Add(s); b.Add(s) -> s.Parent == b, a.Children boş
```

- [ ] **Step 3: Testlerin kırmızı olduğunu gör.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests` Expected: derleme hatası ya da FAIL.

- [ ] **Step 4: Modeli, `InMemorySecretStore`'u ve `CredentialResolver.Resolve`'u yaz.** Her alan ayrı ayrı yukarı doğru aranır: sunucu, sonra `Parent`, onun `Parent`'ı... İlk boş olmayan değer kazanır. Şifre `secrets.GetPassword(node.Id)` ile okunur.

- [ ] **Step 5: Testler yeşil.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests` Expected: 6 passed.

- [ ] **Step 6: Commit.** `git add -A && git commit -m "Çözüm iskeleti, model ve kimlik devralma"`

### Task 2: Arama (Türkçe harf katlama ve ağaç süzme)

**Files:**
- Create: `src/RdpNext.Core/Search/SearchText.cs`, `src/RdpNext.Core/Search/ConnectionFilter.cs`
- Test: `tests/RdpNext.Core.Tests/SearchTextTests.cs`, `tests/RdpNext.Core.Tests/ConnectionFilterTests.cs`

**Interfaces:**
- Consumes: `Group`, `Server`, `Node` (Task 1)
- Produces:
  - `static class SearchText { static string Fold(string text); static bool Matches(string text, string foldedQuery); }`
  - `sealed record FilterResult(IReadOnlySet<Guid> Visible, Server? FirstMatch)`
  - `static class ConnectionFilter { static FilterResult Apply(Group root, string? query); }`

- [ ] **Step 1: Başarısız testleri yaz:**

```csharp
// SearchTextTests
Assert.Equal("musteriler", SearchText.Fold("Müşteriler"));
Assert.Equal("istanbul", SearchText.Fold("İSTANBUL"));
Assert.Equal("sql-ist", SearchText.Fold("SQL-IST"));
Assert.Equal("iiii", SearchText.Fold("ıIİi"));
Assert.True(SearchText.Matches("SQL-IST", SearchText.Fold("ist")));
Assert.True(SearchText.Matches("Çağrı Merkezi", SearchText.Fold("cagri")));

// ConnectionFilterTests (ağaç: Üretim{web-01 10.0.1.11, sql-01 10.0.1.20}, Test{web-test 10.0.2.11}, Müşteriler{}, kökte tek-sunucu)
[Fact] void EmptyQueryShowsEverythingAndNoFirstMatch()   // Visible tüm düğümler, FirstMatch null ("" ve null ve "   " için)
[Fact] void MatchesServerByName()      // "web" -> web-01, web-test ve ataları Üretim, Test görünür; sql-01 görünmez
[Fact] void MatchesServerByHost()      // "10.0.1.2" -> yalnız sql-01 (+ Üretim)
[Fact] void GroupNameMatchShowsWholeGroup() // "musteri" -> Müşteriler görünür; "uretim" -> Üretim ve iki sunucusu
[Fact] void FirstMatchIsFirstVisibleServerInTreeOrder() // "web" -> web-01
[Fact] void FiveThousandServersFilterUnder100ms() // 50 grup x 100 sunucu; bir ısınma çağrısından sonra Apply(root,"sunucu-49-99") < 100 ms
```

- [ ] **Step 2: Kırmızı.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests` Expected: FAIL.

- [ ] **Step 3: `SearchText.Fold` ve `ConnectionFilter.Apply`'ı yaz.** `Fold`: önce `ı`, `İ`, `I` -> `i`; sonra `Normalize(FormD)`, `NonSpacingMark` karakterlerini at, `ToLowerInvariant()`. `Apply`: sorgu boşsa her şey görünür. Değilse ağaç bir kez derinlik öncelikli gezilir; adı eşleşen grubun tüm alt ağacı görünür; ad ya da adres eşleşen sunucu ve tüm ataları görünür. `FirstMatch` gezinti sırasındaki ilk görünür sunucudur. Kök grubun kendisi her zaman `Visible` içindedir.

- [ ] **Step 4: Yeşil.** Run: aynı komut. Expected: tüm testler passed.

- [ ] **Step 5: Commit.** `git commit -am "Türkçe harf duyarsız arama ve ağaç süzme"` (yeni dosyalar için önce `git add -A`)

### Task 3: Bağlantı dosyası (JSON saklama)

**Files:**
- Create: `src/RdpNext.Core/Storage/ConnectionStore.cs`, `src/RdpNext.Core/Storage/LoadResult.cs`
- Create: `samples/connections.sample.json` (Üretim{web-01 10.0.1.11 Blue, sql-01 10.0.1.20 Teal, api-01 10.0.1.31 Orange}, Test{7 sunucu}, Müşteriler{23 sunucu}; çizimdeki ağaç)
- Test: `tests/RdpNext.Core.Tests/ConnectionStoreTests.cs`

**Interfaces:**
- Consumes: model (Task 1)
- Produces:
  - `sealed record LoadResult(Group Root, string? CorruptBackupPath)`
  - `sealed class ConnectionStore { ConnectionStore(string filePath); static string DefaultPath(); LoadResult Load(); void Save(Group root); }`
  - `DefaultPath()` = `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "rdpNext", "connections.json")` (bu Mac'te `~/Library/Application Support/rdpNext/connections.json`, Windows'ta `%LOCALAPPDATA%\rdpNext\connections.json`)

- [ ] **Step 1: Başarısız testleri yaz** (her test kendi geçici klasöründe):

```csharp
[Fact] void RoundTripKeepsEverything()   // iç içe 3 seviye; Id, Name, Host, Port 3390, Color Violet, Screen FullScreen, Username, Domain, IsExpanded false aynen geri gelir; Parent bağları kurulmuş
[Fact] void PasswordsNeverWritten()      // InMemorySecretStore'a "Gizli123!" yazılmış sunucu kaydedilir -> dosya metninde "Gizli123!" geçmez
[Fact] void FileHasVersionAndKinds()     // JSON kökünde "version": 1; düğümlerde "kind": "group" | "server"; enum'lar metin ("rdp", "fitToWindow", "blue")
[Fact] void MissingFileGivesEmptyRoot()  // dosya yok -> Root.Children boş, CorruptBackupPath null, dosya oluşturulmaz
[Fact] void CorruptFileIsBackedUpNotOverwritten() // dosya "{ yarım" -> Root boş, CorruptBackupPath "connections.corrupt-*.json" var ve içeriği "{ yarım"; connections.json artık yok
[Fact] void SaveIsAtomic()               // Save sonrası klasörde yalnız connections.json var (geçici dosya kalmaz)
[Fact] void SampleFileLoads()            // samples/connections.sample.json -> 3 grup, Üretim'de 3 sunucu
```

- [ ] **Step 2: Kırmızı.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests` Expected: FAIL.

- [ ] **Step 3: `ConnectionStore`'u yaz.** `System.Text.Json`; `Node` üzerinde `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` + `[JsonDerivedType(typeof(Group), "group")]`, `[JsonDerivedType(typeof(Server), "server")]`; camelCase adlar ve `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. Dosya kökü `{ "version": 1, "root": {...} }`. Yükledikten sonra `Parent` bağlarını kur. `Save`: aynı klasörde geçici dosyaya yaz, sonra `File.Move(tmp, path, overwrite: true)`. Okunamayan dosya `connections.corrupt-yyyyMMdd-HHmmss.json` adıyla taşınır.

- [ ] **Step 4: Yeşil.** Run: aynı komut. Expected: tüm testler passed.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Bağlantı dosyası okuma ve yazma"`

### Task 4: Uygulama kabuğu, tema ve sol panel

**Files:**
- Create: `src/RdpNext.App/RdpNext.App.csproj` (WinExe, Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, CommunityToolkit.Mvvm; `AvaloniaUseCompiledBindingsByDefault=true`)
- Create: `src/RdpNext.App/Program.cs`, `App.axaml(.cs)`, `Strings/Strings.resx`
- Create: `src/RdpNext.App/Styles/Tokens.axaml` (Light/Dark `ThemeDictionaries`), `src/RdpNext.App/Styles/Controls.axaml`
- Create: `src/RdpNext.App/ViewModels/TreeNodeViewModel.cs`, `GroupNodeViewModel.cs`, `ServerNodeViewModel.cs`, `TreeViewModel.cs`, `MainWindowViewModel.cs`
- Create: `src/RdpNext.App/Views/MainWindow.axaml(.cs)`, `src/RdpNext.App/Views/SidebarView.axaml(.cs)`
- Test: `tests/RdpNext.App.Tests/RdpNext.App.Tests.csproj` (Avalonia.Headless.XUnit), `TestAppBuilder.cs`, `SidebarTests.cs`

**Interfaces:**
- Consumes: `ConnectionStore`, `LoadResult`, `ISecretStore`, model (Task 1, 3)
- Produces:
  - `abstract partial class TreeNodeViewModel : ObservableObject { Node Model; string Name; [ObservableProperty] bool isVisible = true; }`
  - `partial class GroupNodeViewModel : TreeNodeViewModel { ObservableCollection<TreeNodeViewModel> Children; [ObservableProperty] bool isExpanded; int ServerCount; }` (`ServerCount` alt ağaçtaki tüm sunucular; `IsExpanded` değişince `Group.IsExpanded`'a yazar)
  - `partial class ServerNodeViewModel : TreeNodeViewModel { string Host; ServerColor Color; [ObservableProperty] bool isConnected; Server Server; }`
  - `partial class TreeViewModel : ObservableObject { TreeViewModel(Group root); ObservableCollection<TreeNodeViewModel> Roots; }` (Roots = kökün çocukları; kök gösterilmez)
  - `partial class MainWindowViewModel : ObservableObject { MainWindowViewModel(ConnectionStore store, ISecretStore secrets); TreeViewModel Tree; string? Notice; }`
  - `Notice`: `LoadResult.CorruptBackupPath` doluysa `Strings.CorruptFileNotice` = "Bağlantı dosyası okunamadı, eski dosya yedeklendi: {0}" (tek satır, sol panelin üstünde); değilse null ve görünmez.

- [ ] **Step 1: Başarısız testleri yaz** (`SidebarTests`, `[AvaloniaFact]`, örnek dosyayla kurulan `MainWindow`):

```csharp
[AvaloniaFact] void ShowsGroupsWithServerCounts()   // "Üretim" satırında sayaç "3", "Müşteriler" satırında "23"
[AvaloniaFact] void ShowsServerNameAndHost()        // "web-01" ve "10.0.1.11" metinleri görsel ağaçta var
[AvaloniaFact] void FontSizesMeetMinimums()         // sunucu adı TextBlock FontSize >= 15, adres TextBlock FontSize >= 14
[AvaloniaFact] void LongNamesAreTrimmed()           // 120 karakterlik ad: ad ve adres TextBlock'larında TextTrimming == CharacterEllipsis
[AvaloniaFact] void AddServerButtonPresent()        // "+ Sunucu ekle" butonu var (Plan 3'e kadar işlevsiz)
[AvaloniaFact] void CorruptFileShowsNotice()        // bozuk dosyayla açılınca Notice metni görünür; normal dosyada görünmez
[AvaloniaFact] void ExpandStateComesFromModel()     // IsExpanded=false olan "Test" grubu kapalı açılır
```

- [ ] **Step 2: Kırmızı.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests` Expected: FAIL.

- [ ] **Step 3: Kabuğu ve sol paneli yaz.** Ölçüler ve renkler `docs/tasarim/gorsel-stil.html`'deki `.tw.light` / `.tw.dark` değerlerinden `Tokens.axaml`'a taşınır (ör. açık: pencere `#eef0f5`, panel `#f7f8fb`, kart `#ffffff`, yazı `#1b1e2b`, ikincil `#6b7085`, vurgu `#3d6dfd`; koyu: `#151827`, `#1a1d2e`, `#252839`, `#e8eaf2`, `#8d92a8`, `#4c7dff`). Sunucu renkleri: Blue `#2d6cdf`, Teal `#12a594`, Orange `#e5673a`, Violet `#7b5cf0`, Green `#2fbf71`; grup kutucuğu `#1f4f7a`. Köşeler 10-12 px. Sol panel 310 px: "+ Sunucu ekle" (vurgu rengi, tam genişlik), arama kutusu (büyüteç ikonu, yer tutucu "Ara"), `TreeView`: grup satırı = klasör kutucuğu + ad + sağda sayaç; sunucu satırı = Windows logolu renkli kutucuk (34 px) + ad + altında adres + bağlıysa sağda yeşil nokta. `TreeViewItem.IsVisible` görünüm modelindeki `IsVisible`'a bağlanır. `.WithInterFont()`, pencere `FontFamily` Inter, temel `FontSize` 15. Pencere 1280x800 açılır; istemci alanı başlık çubuğuna uzatılır (sekmeler Task 6'da oraya gelecek), macOS'ta trafik ışıkları için solda boşluk bırakılır.

- [ ] **Step 4: Yeşil.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests` Expected: 7 passed.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Uygulama kabuğu, tema ve sol panel"`

### Task 5: Arama kutusunu ağaca bağlama

**Files:**
- Modify: `src/RdpNext.App/ViewModels/TreeViewModel.cs`, `src/RdpNext.App/Views/SidebarView.axaml(.cs)`
- Test: `tests/RdpNext.App.Tests/TreeViewModelTests.cs`, `tests/RdpNext.App.Tests/SidebarTests.cs`

**Interfaces:**
- Consumes: `ConnectionFilter.Apply`, `FilterResult` (Task 2)
- Produces: `TreeViewModel` üzerinde `[ObservableProperty] string query = ""` ve `ServerNodeViewModel? FirstMatch { get; }`

- [ ] **Step 1: Başarısız testleri yaz:**

```csharp
// TreeViewModelTests (düz [Fact])
[Fact] void QueryHidesNonMatchingServers()   // Query="sql" -> sql-01 IsVisible, web-01 değil, "Test" grubu değil
[Fact] void QueryExpandsVisibleGroups()      // "Test" kapalıyken Query="web-test" -> Test.IsExpanded true
[Fact] void ClearingQueryRestoresModelExpansion() // sonra Query="" -> Test.IsExpanded yine false, her şey görünür
[Fact] void FirstMatchFollowsQuery()         // Query="web" -> FirstMatch.Name == "web-01"; Query="yok-boyle" -> null
// SidebarTests
[AvaloniaFact] void TypingInSearchBoxFilters() // arama kutusuna "musteri" yazılır -> yalnız Müşteriler grubu görünür
```

- [ ] **Step 2: Kırmızı.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests` Expected: FAIL.

- [ ] **Step 3: Yaz.** `OnQueryChanged` içinde `ConnectionFilter.Apply` çağrılır, her düğümün `IsVisible`'ı `Visible` kümesinden ayarlanır. Sorgu doluyken görünür gruplar açılır ama bu `Group.IsExpanded`'a yazılmaz; sorgu boşalınca açıklık modeldeki değere döner.

- [ ] **Step 4: Yeşil.** Run: aynı komut. Expected: tüm testler passed.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Arama kutusu ağacı süzüyor"`

### Task 6: Oturum sekmeleri

**Files:**
- Create: `src/RdpNext.App/ViewModels/SessionTabViewModel.cs`, `SessionsViewModel.cs`, `src/RdpNext.App/Views/TabStripView.axaml(.cs)`, `src/RdpNext.App/Views/SessionPlaceholderView.axaml(.cs)`
- Modify: `MainWindowViewModel.cs`, `MainWindow.axaml`, `SidebarView.axaml(.cs)`
- Test: `tests/RdpNext.App.Tests/SessionsViewModelTests.cs`, `tests/RdpNext.App.Tests/MainWindowTests.cs`

**Interfaces:**
- Consumes: `ServerNodeViewModel`, `TreeViewModel.FirstMatch` (Task 4, 5)
- Produces (Plan 2 bunları kullanır):
  - `partial class SessionTabViewModel : ObservableObject { SessionTabViewModel(ServerNodeViewModel server); ServerNodeViewModel Server; string Title; ServerColor Color; }`
  - `partial class SessionsViewModel : ObservableObject { ObservableCollection<SessionTabViewModel> Tabs; [ObservableProperty] SessionTabViewModel? selected; SessionTabViewModel Open(ServerNodeViewModel server); void Close(SessionTabViewModel tab); void CloseOthers(SessionTabViewModel keep); }`
  - `MainWindowViewModel` üzerine: `SessionsViewModel Sessions`, `IRelayCommand<ServerNodeViewModel> ConnectCommand`, `IRelayCommand ConnectFirstMatchCommand`, `IRelayCommand NewSessionCommand`, `event Action? FocusSearchRequested`

- [ ] **Step 1: Başarısız testleri yaz:**

```csharp
// SessionsViewModelTests
[Fact] void OpenAddsTabSelectsItAndMarksConnected()
[Fact] void OpeningSameServerTwiceSelectsExistingTab() // Tabs.Count == 1, Selected aynı sekme
[Fact] void CloseSelectsLeftNeighbourOrFirst()          // [a,b,c] seçili b kapanır -> a; [a] kapanır -> Selected null
[Fact] void CloseMarksServerDisconnected()
[Fact] void CloseOthersKeepsOnlyGivenTab()              // kalan sekme seçili, diğer sunucular IsConnected false
// MainWindowTests
[AvaloniaFact] void DoubleClickOnServerOpensTab()       // web-01 satırına çift tık -> üst şeritte "web-01" sekmesi
[AvaloniaFact] void EnterInSearchOpensFirstMatch()      // aramaya "sql" + Enter -> "sql-01" sekmesi seçili
[AvaloniaFact] void EnterWithNoMatchDoesNothing()       // "yok-boyle" + Enter -> sekme yok, hata yok
[AvaloniaFact] void PlusTabFocusesSearch()              // "+" tıklanınca arama kutusu odakta
[AvaloniaFact] void EmptySessionAreaHasNoText()         // sekme yokken oturum alanında hiç TextBlock yok
```

- [ ] **Step 2: Kırmızı.** Run: `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests` Expected: FAIL.

- [ ] **Step 3: Yaz.** Sekme şeridi başlık çubuğunda: her sekme küçük renkli kutucuk (26 px) + ad + "×"; seçili sekme kart renginde zemin; sonda "+". Sekmeye sağ tık: "Kapat", "Diğerlerini kapat" (metinler `Strings.resx`'te; "Yeniden bağlan" Plan 2'de). Seçili sekmenin içeriği `SessionPlaceholderView`: yuvarlak köşeli (12 px), 12 px iç boşluklu koyu alan, yazısız. Sunucu satırına çift tık ve arama kutusunda Enter `ConnectCommand` / `ConnectFirstMatchCommand`'a gider.

- [ ] **Step 4: Yeşil.** Run: `/usr/local/share/dotnet/dotnet test` (tüm çözüm) Expected: tüm testler passed.

- [ ] **Step 5: Commit.** `git add -A && git commit -m "Oturum sekmeleri"`

### Task 7: Çizimle karşılaştırma ve merge

**Files:**
- Modify: yalnız bu adımda bulunan görsel farklar için `Styles/*.axaml` ve `Views/*.axaml`

- [ ] **Step 1: Örnek veriyle çalıştır.** `samples/connections.sample.json` dosyasını `~/Library/Application Support/rdpNext/connections.json`'a kopyala (orada dosya varsa önce yedekle), `/usr/local/share/dotnet/dotnet run --project src/RdpNext.App -c Release`. web-01 ve sql-01'i aç (çizimdeki durum).

- [ ] **Step 2: Açık ve koyu temada ekran görüntüsü al** (macOS Görünüm ayarı değiştirilerek; `screencapture -l <pencere id>`), `docs/tasarim/gorsel-stil.png` ile yan yana koy. Yazı boyutu, köşe yarıçapları, renkler, boşluklar ve sekme görünümü çizimle eşleşene kadar düzelt. Görüntüleri Murat'a göster.

- [ ] **Step 3: Tüm testler yeşil.** Run: `/usr/local/share/dotnet/dotnet test` Expected: tüm testler passed, uyarı yok.

- [ ] **Step 4: Commit ve merge.**

```bash
git add -A && git commit -m "Ana ekran çizimle eşleştirildi"
git switch master && git merge --no-ff temel-ana-ekran -m "Temel ve ana ekran" && git branch -d temel-ana-ekran
```
