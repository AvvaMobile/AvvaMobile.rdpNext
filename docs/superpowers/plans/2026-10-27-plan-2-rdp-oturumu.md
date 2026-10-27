# Plan 2: RDP oturumu

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sekmeye çift tıklayınca sunucunun masaüstü o sekmede açılır; tasarımdaki sekiz madde (dinamik çözünürlük, tam ekran, metin panosu, dosya panosu denemesi, ses, sertifika sorusu, kopunca yeniden bağlan, şifre sorusu) macOS'ta çalışır, Windows x64 paketi en sonda hazırlanır.

**Architecture:** FreeRDP 3'ü saran projeye ait ince bir C katmanı (`native/rdpnext_shim`) FreeRDP yapılarını gizler ve düz C fonksiyonları sunar. Yeni `RdpNext.Rdp` kütüphanesi bunu P/Invoke ile çağırır; her oturum kendi iş parçacığında olay döngüsünü döndürür, dışarıya `IRdpSession` arayüzü verir. Uygulama tarafında `SessionTabViewModel` oturumun durum makinesidir, `SessionView` görüntüyü `WriteableBitmap` ile çizer. Testler `IRdpSession`'ın sahtesiyle koşar; gerçek bağlantı elle denenir.

**Tech Stack:** .NET 10, Avalonia 12.1.4, FreeRDP 3.32.2 (macOS: Homebrew; Windows: vcpkg `x64-mingw-dynamic` ile bu Mac'te çapraz derleme), clang, xunit v3, Avalonia.Headless.

**Spec:** `docs/superpowers/specs/2026-10-27-rdp-oturumu-design.md` (üst doküman: `docs/superpowers/specs/2026-10-27-rdpnext-v1-design.md`, "RDP oturumu" bölümü). Denemenin kaynağı (atılacak, ama başlangıç noktası): scratchpad'deki `rdpspike/shim/rdpshim.c` ve `rdpspike/app/Program.cs`; içerikleri Task 1'de repoya taşınır.

## Global Constraints

1. `dotnet` her zaman `/usr/local/share/dotnet/dotnet`. `TreatWarningsAsErrors` açık.
2. Arayüz yalnız İngilizce; her metin `src/RdpNext.App/Strings/Strings.resx`'te. Kod yorumları Türkçe, çevredeki kod gibi.
3. Hiçbir yerde em dash yok (kod yorumu, commit, metin).
4. Git: her task `master`'dan kendi dalında; bitince `master`'a `--no-ff` merge, dal silinir. Commit mesajı kısa ve Türkçe; `Co-Authored-By` ve `Claude-Session` satırı yok. CI yok.
5. Şifre diske ve log'a yazılmaz; yalnız `ISecretStore`'dan okunur, C katmanına bellekte verilir.
6. Homebrew kurulumlarında `openssl@3` bağlı kalmalı: her kurulumdan sonra `ls -l /opt/homebrew/opt/openssl` openssl@3'ü göstermeli; göstermiyorsa `brew unlink openssl@4 && brew link --overwrite openssl@3`.
7. Yeni ekranlar (sertifika sorusu, şifre sorusu, bağlanıyor/koptu durumu, ağaçtaki durum) Task 0'da çizilip Murat'a onaylatılmadan kodlanmaz.
8. Her task sonunda çalışan uygulama kapatılır, Debug derlenir, yeniden açılır (`src/RdpNext.App/bin/Debug/net10.0/rdpNext`).
9. Gerçek sunucu testleri yalnız arsaVev Web Server'a (141.98.205.90:63389, ağaçtaki "Web Server"). Yanlış şifre en fazla bir kez denenir; sunucuya dosya yazan denemeden önce Murat'a sorulur.

## Review Focus

1. Bağlanırken ya da bağlıyken sekme kapatılırsa iş parçacığı durmalı, native bellek bir kez serbest kalmalı, uygulama çökmemeli. Test: Task 1 `DisposeWhileConnectingDoesNotCrash`, Task 2 `ClosingTabDisposesSession`.
2. Açık oturumu olan sunucu silinirse oturum kapanmalı. Test: Task 2 `DeletingServerDisposesSession`.
3. Pencere çok küçültülürse ya da simge durumuna alınırsa sunucuya geçersiz boyut gitmemeli (RDP: genişlik 200..8192 ve çift, yükseklik 200..8192). Test: Task 4 `ResizeIsClampedAndEven`.
4. Uygulama kapanırken açık oturumların hepsi kapatılmalı. Test: Task 2 `ShutdownDisposesAllSessions`.
5. Bağlanırken aynı sunucuya tekrar çift tıklanırsa ikinci oturum açılmamalı. Test: Task 2 `DoubleConnectReusesSession`.

---

### Task 0: Çizimler ve onay

**Files:** Create `docs/tasarim/rdp-oturumu.html`, `docs/tasarim/rdp-oturumu.png`

- [ ] `gorsel-stil.html` ve `duzenle-sekmesi.html` ile aynı stil ve tokenlarla tek sayfada, koyu ve açık hâliyle: **a** sertifika sorusu (sunucu adı, subject, issuer, parmak izi; Trust / Cancel), **b** kullanıcı adı ve şifre sorusu (User, Password, Domain, "Save" kutusu; Connect / Cancel), **c** oturum alanında "Connecting" durumu, **d** oturum alanında kopma durumu ve "Reconnect" düğmesi, **e** kenar çubuğu ağacında bağlanıyor, bağlı ve kopmuş sunucu satırı.
- [ ] PNG'ye çevir (`qlmanage -t -s 1600`), Murat'a göster, onay al. Değişiklik isterse düzelt, tekrar sor.
- [ ] Onaylanan metinleri bu plana not et (Task 6, 7, 8 bu metinleri kullanır).
- [ ] Commit: "RDP oturumu çizimleri"

### Task 1: C katmanı ve `RdpNext.Rdp` temeli (bağlan, görüntü, fare, kapat)

**Files:** Create `native/rdpnext_shim/rdpnext_shim.c`, `native/rdpnext_shim/rdpnext_shim.h`, `native/build-mac.sh`, `src/RdpNext.Rdp/RdpNext.Rdp.csproj`, `src/RdpNext.Rdp/NativeMethods.cs`, `src/RdpNext.Rdp/IRdpSession.cs`, `src/RdpNext.Rdp/RdpSession.cs`, `tests/RdpNext.Rdp.Tests/RdpNext.Rdp.Tests.csproj`, `tests/RdpNext.Rdp.Tests/RdpSessionTests.cs`; Modify `RdpNext.slnx`, `src/RdpNext.App/RdpNext.App.csproj` (proje referansı)

**Interfaces (Produces):**
- C: `rdpContext* rn_create(const rn_callbacks* cb, void* user)`, `uint32_t rn_connect(rdpContext*, const rn_options*)` (0 = bağlandı, aksi FreeRDP hata kodu), `int rn_pump(rdpContext*, int timeout_ms)` (1 devam, 0 bitti), `uint32_t rn_end_reason(rdpContext*)` (`freerdp_error_info`), `void rn_mouse(rdpContext*, uint16_t flags, uint16_t x, uint16_t y)`, `void rn_free(rdpContext*)`. Tüm metinler UTF-8. `rn_callbacks` alan sırası sabit: `frame`, `verify_certificate`, `pointer`, `clipboard_text` (sonraki task'lar doldurur, şimdilik NULL olabilir).
- `build-mac.sh`: `native/out/osx-arm64/librdpnext_shim.dylib` üretir (clang, `-I$(brew --prefix)/include/freerdp3 -I.../winpr3 -lfreerdp3 -lfreerdp-client3 -lwinpr3`). `RdpNext.Rdp.csproj` macOS'ta derlemeden önce bu script'i `Inputs=native/rdpnext_shim/*` `Outputs=dylib` ile çalıştırır ve dylib'i çıktıya kopyalar.
- `sealed record RdpConnectOptions(string Host, int Port, string Username, string? Domain, string Password, int Width, int Height)`
- `readonly record struct RdpFrame(IntPtr Buffer, int Width, int Height, int Stride, int X, int Y, int W, int H)` (Buffer yalnız olay süresince geçerli; BGRA32)
- `enum RdpEndReason { UserLogoff, Disconnected, AuthFailed, CertificateRejected, ConnectFailed }`
- `[Flags] enum RdpMouseFlags : ushort` (FreeRDP `PTR_FLAGS_*` değerleri: Move 0x0800, Down 0x8000, Button1 0x1000, Button2 0x2000, Button3 0x4000, Wheel 0x0200, HWheel 0x0400, WheelNegative 0x0100)
- `interface IRdpSession : IDisposable` ile `void Start(RdpConnectOptions o)` (hemen döner), `event Action? Connected`, `event Action<RdpFrame>? Frame` (oturum iş parçacığından), `event Action<RdpEndReason, string?>? Ended`, `void SendMouse(RdpMouseFlags f, int x, int y)`. Sonraki task'lar bu arayüze üye ekler.
- `sealed class RdpSession : IRdpSession`: `Start` bir arka plan iş parçacığı açar, `rn_connect` + `rn_pump(100)` döngüsü; `Send*` çağrıları `ConcurrentQueue`'ya girer ve döngüde boşaltılır; `Dispose` döngüyü durdurur, iş parçacığını bekler (en fazla 5 sn), `rn_free`'yi bir kez çağırır. Hata kodu `ERRCONNECT_LOGON_FAILURE`/`ERRCONNECT_AUTHENTICATION_FAILED` ise `AuthFailed`; `ERRINFO_LOGOFF_BY_USER` (0x0C) ya da `ERRINFO_RPC_INITIATED_DISCONNECT_BYUSER` (0x0B) ise `UserLogoff`.

- [ ] Testler (`RdpSessionTests`, native kütüphane yoksa atlanır): `ConnectToClosedPortEndsWithConnectFailed` (127.0.0.1:1, 10 sn içinde `Ended(ConnectFailed, _)`), `DisposeWhileConnectingDoesNotCrash` (10.255.255.1:3389'a Start, 200 ms sonra Dispose, 6 sn içinde döner), `DisposeTwiceIsSafe`.
- [ ] Kırmızı: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Rdp.Tests`
- [ ] Yaz (denemedeki `rdpshim.c` başlangıç noktası; görüntü `gdi_init(PIXEL_FORMAT_BGRA32)` + `BeginPaint/EndPaint/DesktopResize`), yeşil.
- [ ] Commit: "FreeRDP C katmanı ve oturum sınıfı"

### Task 2: Oturum sekmesi ve görüntü

**Files:** Create `src/RdpNext.App/Views/SessionView.axaml(.cs)`, `src/RdpNext.App/ViewModels/SessionScreen.cs`, `tests/RdpNext.App.Tests/FakeRdpSession.cs`, `tests/RdpNext.App.Tests/SessionTabTests.cs`; Modify `SessionTabViewModel.cs`, `SessionsViewModel.cs`, `MainWindowViewModel.cs`, `ServerNodeViewModel.cs`, `MainWindow.axaml` (`SessionPlaceholderView` yerine `SessionView`), `App.axaml.cs`, `tests/RdpNext.App.Tests/TestWindows.cs`; Delete `SessionPlaceholderView.axaml(.cs)`

**Interfaces:**
- Consumes: Task 1 `IRdpSession`, `RdpConnectOptions`, `RdpFrame`, `RdpEndReason`.
- Produces: `MainWindowViewModel(..., Func<IRdpSession>? rdp = null)` (varsayılan `() => new RdpSession()`); `enum SessionState { Connecting, Connected, Disconnected }`; `SessionTabViewModel.State`, `SessionTabViewModel.Screen` (`SessionScreen`), `SessionTabViewModel.Session` (`IRdpSession?`), `void SessionTabViewModel.Connect()`; `ServerNodeViewModel.State` (`SessionState?`, sekme yoksa null; `IsConnected` bunun yerine geçer); `SessionScreen.CopyFrame(RdpFrame f)` (değişen dikdörtgeni `WriteableBitmap`'e kopyalar, boyut değişince bitmap'i yeniden kurar, UI'ya tek "yenile" gönderir) ve `event Action? Invalidated`; `void MainWindowViewModel.Shutdown()` (tüm oturum sekmelerini kapatır; `MainWindow.Closing`'den çağrılır); `FakeRdpSession` (çağrıları kaydeder, `RaiseConnected()`, `RaiseEnded(...)`, `RaiseFrame(...)` sunar).
- Bağlanma: `CredentialResolver.Resolve` ile kullanıcı, şifre, domain; boyut `SessionView`'in o anki mantıksal boyutu (görünmeden önce 1280x800). Kullanıcı ya da şifre yoksa şimdilik `Disconnected` (Task 7 soruya çevirir).

- [ ] Testler (`SessionTabTests`): `ConnectStartsSessionWithResolvedCredentials` (Üretim'deki admin/AVVA ve anahtar zincirindeki şifre `RdpConnectOptions`'a gider), `StateFollowsSession` (Connecting → Connected → Disconnected; ağaçtaki `State` aynı), `ClosingTabDisposesSession`, `DeletingServerDisposesSession`, `ShutdownDisposesAllSessions`, `DoubleConnectReusesSession` (Connecting iken ikinci `ConnectCommand` aynı sekme, `Start` bir kez), `FrameUpdatesScreen` (2x2 BGRA kare → `Screen.Bitmap` 2x2).
- [ ] Mevcut `IsConnected` testlerini `State` ile güncelle.
- [ ] Kırmızı, yaz, yeşil: `/usr/local/share/dotnet/dotnet test`
- [ ] Elle: uygulamada Web Server'a çift tık, masaüstü sekmede görünür; sekme değiştirip geri gelince görüntü durur.
- [ ] Commit: "Oturum sekmesinde RDP görüntüsü"

### Task 3: Klavye, fare, imleç

**Files:** Create `src/RdpNext.App/Rdp/KeyMap.cs`, `tests/RdpNext.App.Tests/KeyMapTests.cs`; Modify `rdpnext_shim.c/.h` (`rn_key`, `rn_unicode`, pointer geri çağrısı, `rn_set_keyboard_layout`), `NativeMethods.cs`, `IRdpSession.cs`, `RdpSession.cs`, `SessionView.axaml.cs`, `FakeRdpSession.cs`

**Interfaces:**
- Produces: `IRdpSession.SendKey(ushort scancode, bool extended, bool down)`, `IRdpSession.SendWheel(int delta, bool horizontal, int x, int y)`, `event Action<RdpPointer?>? PointerChanged` (`sealed record RdpPointer(byte[] Bgra, int Width, int Height, int HotX, int HotY)`, null = gizli); `static (ushort Code, bool Extended)? KeyMap.Scancode(PhysicalKey key)`; `static IReadOnlyList<(ushort, bool, bool)>? KeyMap.MacShortcut(PhysicalKey key, KeyModifiers mods)` (Cmd+C/V/X/A/Z için Ctrl basılı sırası, diğerleri null).
- Klavye: `PhysicalKey`'den scancode (düzenden bağımsız). Klavye düzeni bağlanırken Mac'in giriş kaynağından: Türkçe Q → `0x0000041F`, diğer her şey `0x00000409`. Cmd tek başına sunucuya gitmez; Cmd+C/V/X/A/Z Ctrl olarak gider, diğer Cmd kısayolları uygulamada kalır.
- Fare: sol/sağ/orta tuş, tekerlek (dikey ve yatay), koordinat oturum alanına göre. İmleç: sunucunun imleç şekli `Cursor`'a çevrilir.

- [ ] Testler (`KeyMapTests`): `LettersMapToScancodes` (A → 0x1E, Z → 0x2C, Q → 0x10), `ArrowsAreExtended` (Left → (0x4B, true)), `EnterAndNumpadEnterDiffer` (0x1C false / 0x1C true), `CmdCSendsCtrlC` (sıra: Ctrl down 0x1D, C down 0x2E, C up, Ctrl up), `CmdQIsNotSent` (null). Oturum görünümü testi: `KeyDownSendsScancodeToSession` (FakeRdpSession'a gider).
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: Web Server'da Notepad'de Türkçe karakterler (ğüşıöç İ), Cmd+A/C/V, sağ tık menüsü, tekerlek, imleç şekli metin üstünde I-beam.
- [ ] Commit: "Oturumda klavye, fare ve imleç"

### Task 4: Dinamik çözünürlük

**Files:** Modify `rdpnext_shim.c/.h` (`disp` kanalı: kanal bağlanınca bağlamı sakla, `rn_resize`), `NativeMethods.cs`, `IRdpSession.cs`, `RdpSession.cs`, `SessionTabViewModel.cs`, `SessionView.axaml(.cs)`; Test `tests/RdpNext.App.Tests/SessionTabTests.cs`

**Interfaces:**
- Produces: `IRdpSession.Resize(int width, int height)`; `void SessionTabViewModel.ViewSizeChanged(double width, double height)` (son değişiklikten 300 ms sonra tek istek; `TimeProvider` ile test edilir).
- Kanallar: PreConnect'te `freerdp_client_load_addins`, `SupportDisplayControl`, `SupportGraphicsPipeline`, `DynamicResolutionUpdate` açık; `ChannelConnected` olayında `disp` bağlamı saklanır, `rdpgfx` için `gdi_graphics_pipeline_init`. Sunucu `disp` desteklemiyorsa görüntü `Stretch=Uniform` ile alana ölçeklenir.

- [ ] Testler: `ResizeIsDebounced` (100 ms arayla 3 boyut → 300 ms sonra yalnız sonuncusu bir kez), `ResizeIsClampedAndEven` (1001x150 → 1000x200; 9000x9000 → 8192x8192), `ResizeBeforeConnectedIsIgnored`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: pencereyi büyüt/küçült, sunucu masaüstü yeni boyuta geçer.
- [ ] Commit: "Dinamik çözünürlük"

### Task 5: Tam ekran

**Files:** Modify `MainWindowViewModel.cs`, `MainWindow.axaml(.cs)`, `SessionTabViewModel.cs`, `TabStripView.axaml` (sağ tık menüsü); Test `tests/RdpNext.App.Tests/FullScreenTests.cs`

**Interfaces:**
- Produces: `MainWindowViewModel.FullScreenSession` (`SessionTabViewModel?`), `ToggleFullScreenCommand`. Doluyken pencere `WindowState.FullScreen`, kenar çubuğu ve sekme şeridi gizli, yalnız o oturum görünür. Kısayol: macOS Ctrl+Cmd+F, Windows Ctrl+Alt+Enter; ikisi de aynı komut, oturum klavyeye sahipken de uygulamada yakalanır. Sunucunun ekran ayarı `ScreenMode.FullScreen` ise bağlanınca doğrudan tam ekran.

- [ ] Testler: `ToggleHidesSidebarAndTabs`, `ToggleTwiceRestoresWindowState` (önceki `Maximized` geri gelir), `FullScreenServerOpensFullScreen`, `ClosingFullScreenSessionLeavesFullScreen`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Commit: "Oturum tam ekran"

### Task 6: Sertifika sorusu

**Files:** Create `src/RdpNext.App/Views/CertificateDialog.axaml(.cs)`, `src/RdpNext.App/Services/ICertificatePrompt.cs`; Modify `rdpnext_shim.c` (`VerifyCertificateEx`/`VerifyChangedCertificateEx` → geri çağrı, 1 = kalıcı kabul, 0 = ret), `RdpSession.cs`, `IRdpSession.cs`, `src/RdpNext.Core/Model/Server.cs`, `SessionTabViewModel.cs`, `Strings.resx`; Test `tests/RdpNext.App.Tests/CertificateTests.cs`, `tests/RdpNext.Core.Tests/ConnectionStoreTests.cs`

**Interfaces:**
- Produces: `sealed record RdpCertificate(string Subject, string Issuer, string Fingerprint, bool Changed)`; `IRdpSession.VerifyCertificate` (`Func<RdpCertificate, Task<bool>>?`, oturum iş parçacığı sonucu bekler; null ise ret); `Server.CertificateFingerprint` (`string?`, connections.json'a yazılır, `CopyWithNewId` kopyalamaz); `interface ICertificatePrompt { Task<bool> AskAsync(string serverName, RdpCertificate cert); }`.
- Kural: kayıtlı parmak izi ile aynıysa sormadan kabul; değilse Task 0'daki pencereyle sor; "Trust" → parmak izi sunucuya yazılır ve kaydedilir; "Cancel" → `Ended(CertificateRejected)`, sekme kapanır.

- [ ] Testler: `UnknownCertificateAsks`, `TrustedCertificateDoesNotAsk`, `ChangedCertificateAsksAgain`, `TrustSavesFingerprint` (dosyada), `CancelClosesTab`, Core: `RoundTripKeepsEverything` içine `CertificateFingerprint`, `CopyDoesNotCopyFingerprint`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: Web Server'ın parmak izini sil, bağlan: soru gelir; Trust, tekrar bağlan: sormaz.
- [ ] Commit: "Sertifika sorusu ve güvenilen sertifikalar"

### Task 7: Kullanıcı adı ve şifre sorusu

**Files:** Create `src/RdpNext.App/Views/CredentialsDialog.axaml(.cs)`, `src/RdpNext.App/Services/ICredentialsPrompt.cs`; Modify `SessionTabViewModel.cs`, `Strings.resx`; Test `tests/RdpNext.App.Tests/CredentialsPromptTests.cs`

**Interfaces:**
- Produces: `sealed record CredentialsAnswer(string Username, string Password, string? Domain, bool Save)`; `interface ICredentialsPrompt { Task<CredentialsAnswer?> AskAsync(string serverName, string? username, string? domain); }` (null = Cancel).
- Kural: kullanıcı ya da şifre çözülemezse bağlanmadan önce sorulur; `Ended(AuthFailed)` gelirse bir kez sorulur ve aynı sekmede yeniden bağlanılır. "Save" işaretliyse kullanıcı ve domain sunucuya, şifre `ISecretStore`'a sunucunun `Id`'siyle yazılır ve kaydedilir. Cancel → sekme kapanır.

- [ ] Testler: `MissingPasswordAsksBeforeConnect`, `AuthFailedAsksAndReconnects` (ikinci `Start` yeni bilgilerle), `SaveWritesServerAndSecret`, `NoSaveLeavesServerUnchanged`, `CancelClosesTab`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: Web Server'ın şifresini geçici olarak boşalt (önce değeri bir kenara al), bağlan: soru gelir, Save'siz gir, bağlanır; şifreyi geri koy.
- [ ] Commit: "Kullanıcı adı ve şifre sorusu"

### Task 8: Kopma ve yeniden bağlanma

**Files:** Modify `rdpnext_shim.c` (`AutoReconnectionEnabled`, `AutoReconnectMaxRetries` = 5, kopunca `client_auto_reconnect_ex`), `SessionTabViewModel.cs`, `SessionView.axaml`, `TabStripView.axaml` (sağ tık: "Reconnect"), `MainWindowViewModel.cs`, `Strings.resx`; Test `tests/RdpNext.App.Tests/SessionTabTests.cs`

**Interfaces:**
- Produces: `SessionTabViewModel.ReconnectCommand` (eski oturumu Dispose eder, yenisini aynı sekmede başlatır); `MainWindowViewModel.ReconnectTabCommand(TabViewModel)`.
- Kural: `UserLogoff` → sekme kapanır. Diğer kopmalar → sekme açık, son görüntü soluk kalır, üstünde Task 0'daki kopma durumu ve "Reconnect".

- [ ] Testler: `UserLogoffClosesTab`, `DisconnectKeepsTabAndShowsReconnect`, `ReconnectStartsNewSessionInSameTab`, `TabMenuHasReconnectOnlyForSessions`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: Wi-Fi'ı kısa kapat-aç (otomatik döner), uzun kapat (Reconnect çıkar); Windows'ta Sign out (sekme kapanır).
- [ ] Commit: "Kopunca yeniden bağlan"

### Task 9: Metin panosu

**Files:** Modify `rdpnext_shim.c/.h` (`cliprdr` kanalı: sunucu format listesi, veri isteği ve cevabı; `CF_UNICODETEXT`; başvuru: FreeRDP kaynağında `client/SDL/SDL3/sdl_clip.cpp`), `NativeMethods.cs`, `IRdpSession.cs`, `RdpSession.cs`, `SessionTabViewModel.cs`, `SessionView.axaml.cs`; Test `tests/RdpNext.App.Tests/ClipboardTests.cs`

**Interfaces:**
- Produces: `IRdpSession.SetLocalClipboardText(string text)`, `event Action<string>? RemoteClipboardText`.
- Kural: sunucuda kopyalanan metin Mac panosuna yazılır; Mac'te kopyalanan metin oturum odak aldığında sunucuya duyurulur.

- [ ] Testler: `RemoteTextGoesToLocalClipboard` (FakeRdpSession olayı → `TopLevel.Clipboard` metni), `FocusSendsLocalText`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Elle: iki yönde Türkçe karakterli metin kopyala-yapıştır.
- [ ] Commit: "Metin panosu iki yönde"

### Task 10: Ses

**Files:** Modify `rdpnext_shim.c` (`AudioPlayback` açık, `rdpsnd` statik kanalı yüklenir; Mac'te `mac` arka ucu, Windows'ta `winmm`)

- [ ] Elle: Web Server'da ses çal (Windows ses testi), Mac'ten duyulur. Otomatik test yok; bağlanma testleri yeşil kalmalı.
- [ ] Commit: "Sunucu sesi"

### Task 11: Dosya panosu denemesi

**Files:** Modify `rdpnext_shim.c` (`cliprdr_file_context`), `RdpSession.cs`, `SessionView.axaml.cs`

- [ ] Mac Finder'da kopyalanan bir dosyanın sunucu masaüstüne yapıştırılması ve tersi denenir. Sunucuya yazmadan önce Murat'a sorulur.
- [ ] Çalışırsa: yalnız bu yolun test edilebilir parçası (format listesinde dosya varken doğru formatın duyurulması) için bir birim testi, commit "Dosya panosu".
- [ ] Bir task'tan uzun sürer ya da çalışmazsa: kod geri alınır, Murat'a tek satırla söylenir, spec'e "bu sürümde yok" yazılır, commit "Dosya panosu bu sürümde yok".

### Task 12: Windows x64 paketi

**Files:** Create `native/build-win.sh`, `native/README.md` (iki platformun derleme adımları); Modify `RdpNext.Rdp.csproj` (Windows'ta `rdpnext_shim.dll` ve FreeRDP dll'lerini çıktıya koy)

- [ ] `brew install mingw-w64` ve vcpkg ile `vcpkg install freerdp:x64-mingw-dynamic` (Global Constraints 6'daki openssl kontrolü). Olmazsa FreeRDP'yi kaynaktan mingw ile CMake'le derle.
- [ ] `build-win.sh`: `native/out/win-x64/rdpnext_shim.dll` ve FreeRDP, winpr, OpenSSL dll'leri.
- [ ] Doğrula: `x86_64-w64-mingw32-objdump -p native/out/win-x64/rdpnext_shim.dll | grep "DLL Name"` listesindeki her dll ya klasörde ya Windows sistem dll'i.
- [ ] `dotnet publish src/RdpNext.App -c Release -r win-x64 --self-contained` + native dll'ler → `~/Downloads/rdpNext-win-x64/` (tek klasör).
- [ ] Commit: "Windows x64 paketi"

### Task 13: Son kontrol

- [ ] Tam test: `/usr/local/share/dotnet/dotnet test` yeşil.
- [ ] Mac'te sekiz maddenin elle kontrolü (Task 2-11'deki "Elle" adımları), sonuç listesi.
- [ ] Murat'a: Mac'te denemesi için hazır olduğunu ve `~/Downloads/rdpNext-win-x64/` klasörünün Windows'ta denenmeye hazır olduğunu tek mesajda söyle (PushNotification).
