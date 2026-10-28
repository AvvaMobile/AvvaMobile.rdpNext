# Plan 4: SSH bağlantısı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sunucu formunda Windows / SSH bağlantı tipi seçilir; SSH sunucusuna çift tıklayınca sağda, RDP sekmesi gibi bir sekmede uygulama içi terminal açılır.

**Architecture:** Yeni `RdpNext.Ssh` projesi SSH.NET'i `IRdpSession` benzeri bir `ISshSession` arkasına saklar. `SessionTabViewModel`'in protokolden bağımsız kısmı (durum, şifre sorusu, Reconnect, olaylar) `ConnectionTabViewModel` tabanına çıkar; RDP sekmesi ve yeni `SshTabViewModel` ondan türer. Terminal çizimini hazır bir Avalonia terminal kontrolü yapar; kontrol ilk görevdeki denemeyle seçilir.

**Tech Stack:** .NET 10, Avalonia 12.1.4, CommunityToolkit.Mvvm, SSH.NET 2026.0.0, terminal kontrolü (aday `SvcSystems.UI.Terminal`, XTerm.NET tabanlı; Task 1 karar verir), xUnit v3, Docker (yerel OpenSSH test sunucusu).

**Spec:** `docs/superpowers/specs/2026-10-28-ssh-baglantisi-design.md`. Onaylı çizim: `docs/tasarim/ssh-formu.html`.

## Global Constraints

1. Önceki planların kuralları geçerli: dotnet yolu `/usr/local/share/dotnet/dotnet`, gövde yazısı en az 15 px, ekran metinleri `Strings.resx`'te İngilizce, em dash yok, commit mesajı kısa Türkçe ve `Co-Authored-By`/`Claude-Session` satırı yok.
2. Dal: `ssh-baglantisi` (master'dan açıldı). Bitince master'a `--no-ff` merge.
3. Windows tipi = RDP (port 3389), SSH tipi = SSH (port 22). Linux'a RDP yok.
4. SSH şifresi ve anahtar parolası anahtar zincirinde, düğümün tek gizli bilgi yuvasında (`ISecretStore.GetPassword(server.Id)`): sunucu yalnız seçili giriş yönteminin gizli bilgisini tutar. `connections.json`'a yazılmaz, log'a düşmez.
5. Sunucu anahtarı `~/.ssh/known_hosts`'a değil, sunucu kaydına (`HostKeyFingerprint`) yazılır.
6. `connections.json` gerçek veridir: yeni sürümü ilk kez çalıştırmadan önce `ConnectionStore.DefaultPath()`'teki dosya yanına `connections.before-ssh.json` olarak kopyalanır.
7. Avva Mobile sunucusunda yalnız okuma: bağlan, `ls`, `htop`, vim aç ve kaydetmeden çık. Yanlış şifre denemesi yalnız Docker'da.
8. Yeni ekran metinleri (önerilen, spec): "Connection type", "Windows", "SSH", "Sign-in method", "Password", "Key file", "Choose…", "Key passphrase", "Trust new key".

## Review Focus

1. Eski `connections.json`'da `"icon": "linux"` ya da `"icon": "ssh"` olan RDP sunucusu yeni sürümde açılmalı, ağaç boş gelmemeli, sunucu RDP kalmalı. Test: Task 2 `OldFileWithLinuxIconLoadsAsRdp`.
2. Sunucunun anahtarı değişince sessizce bağlanılmamalı; kart eski ve yeni parmak izini gösterip "Trust new key" beklemeli. Test: Task 5 `ChangedHostKeyAsksAndDoesNotConnect`.
3. Kullanıcı formda tipi SSH'den Windows'a çevirip kaydederse eski anahtar yolu ve sunucu anahtarı ortada kalmamalı, port elle değiştirilmediyse 3389'a dönmeli. Test: Task 3 `SwitchingBackToWindowsRestoresRdpDefaults`.
4. Terminal açıkken pencere küçültülüp büyütülünce sunucuya yeni satır/sütun gitmeli (vim ekranı kaymamalı). Test: Task 6 `ResizeSendsColumnsAndRows`, Task 7 elle.
5. Türkçe karakter (ğüşıöç İ) iki yönde bozulmadan gitmeli. Test: Task 4 `TurkishCharactersRoundTrip` (Docker), Task 7 elle.

---

### Task 1: Deneme: terminal kontrolü + SSH.NET (atılacak kod)

**Files:** Create `tests/RdpNext.Ssh.Tests/server/Dockerfile`, `tests/RdpNext.Ssh.Tests/server/start.sh` (kalıcı); deneme uygulaması scratchpad'de, repoya girmez.

**Interfaces (Produces):**
- Docker imajı `rdpnext-ssh-test`: Alpine + openssh + bash + vim + htop, kullanıcı `deneme` / şifre `deneme123`, `LANG=C.UTF-8`.
- `start.sh [port]`: imajı kurar (yoksa), kapsayıcıyı `rdpnext-ssh-test` adıyla verilen portta (varsayılan 2222) açar, `~/.ssh/authorized_keys`'i `tests/RdpNext.Ssh.Tests/server/keys/*.pub`'tan doldurur. Anahtarlar yoksa `ssh-keygen` ile üretir: `id_ed25519`, `id_rsa`, `id_ecdsa` (parolasız), `id_ed25519_pw` (parola `anahtar123`). `keys/` `.gitignore`'da.
- Kararın kaydı: bu planın "Task 1 sonucu" satırı (aşağıda) seçilen paket ve sürümle doldurulur.

- [ ] `start.sh` yaz, çalıştır; `ssh -p 2222 deneme@127.0.0.1` ile şifreyle ve `-i keys/id_ed25519` ile girildiğini doğrula.
- [ ] Scratchpad'de tek pencereli Avalonia 12.1.4 deneme uygulaması: `SvcSystems.UI.Terminal` kontrolü + SSH.NET `ShellStream` ("xterm-256color"). Çıktı `Feed`, kullanıcı girdisi `UserInput` ile.
- [ ] Elle dene: `ls --color`, `htop`, `vim` (aç, yaz, `:q!`), `echo ğüşıöçİ` ve klavyeden Türkçe yazma, pencereyi büyütüp küçültünce `stty size`, seçip Cmd+C, Cmd+V.
- [ ] Hepsi geçerse paket seçilir. Geçmezse sırayla `Iciclecreek.Avalonia.Terminal`, sonra `Terminal.Avalonia` denenir; hiçbiri olmazsa durulur ve Murat'a "XTerm.NET üstüne kendi çizimimizi yazalım mı" diye sorulur (plan büyür).
- [ ] Task 1 sonucu satırını doldur, commit: "SSH test sunucusu ve terminal denemesi sonucu"

Task 1 sonucu (2026-10-28): `SvcSystems.UI.Terminal` 2.3.0 (MIT, Avalonia 12.1.4 ve XTerm.NET 3.0.0'a bağlı). Docker sunucusunda renkler, htop, vim (alternatif ekran), Türkçe yazma ve gösterme, pencere boyutu (`SizeChanged` → `ShellStream.ChangeWindowSize`, `stty size` doğru) ve Cmd+V çalıştı. API: `TerminalControl { Model, FontFamily, FontSize, CaretBrush, SelectionBrush, CopySelectionAsync(), PasteFromClipboardAsync(), SelectedText }`, `TerminalControlModel(TerminalOptions { TermName })`, `Feed(byte[], int)` (UI iş parçacığında), `event UserInput (e.Data: ReadOnlyMemory<byte>)`, `event SizeChanged (e.Cols, e.Rows)`, `Terminal.Cols/Rows`, tema için `Application.Styles`'a `TerminalTheme`, renk paleti `SvcSystems.UI.TerminalColor0..255` kaynakları. SSH.NET `HostKeyReceived` parmak izini "SHA256:" öneki olmadan veriyor; önek biz ekleriz. Cmd+C denenemedi (sahte tuş basımı durduruldu), Task 6'da headless testle doğrulanır.

### Task 2: Model: protokol, giriş yöntemi, ikon (Core)

**Files:** Modify `src/RdpNext.Core/Model/Enums.cs`, `src/RdpNext.Core/Model/Server.cs`, `src/RdpNext.Core/Credentials/CredentialResolver.cs`; Test `tests/RdpNext.Core.Tests/ConnectionStoreTests.cs`, `ServerCopyTests.cs`, `CredentialResolverTests.cs`

**Interfaces (Produces):**
- `enum Protocol { Rdp, Ssh }`
- `enum SshAuth { Password, Key }`
- `enum ServerIcon { Windows, Ssh }` (Linux kalkar; yalnız görüntü)
- `Server`: `SshAuth Auth { get; set; } = SshAuth.Password`, `string? KeyFile`, `string? HostKeyFingerprint`, `[JsonIgnore] ServerIcon Icon => Protocol == Protocol.Ssh ? ServerIcon.Ssh : ServerIcon.Windows` (setter yok). `CopyWithNewId` yeni alanları da kopyalar, `HostKeyFingerprint` hariç (kopya yeni sunucudur, `CertificateFingerprint` gibi).
- `CredentialResolver.Resolve(Server, ISecretStore)`: SSH sunucusunda `Domain` her zaman null; `Auth == Key` ise `Password` yalnız sunucunun kendi yuvasından gelir (klasörden devralınmaz). Kullanıcı adı her durumda devralınır.

- [ ] Testler:
  - `OldFileWithLinuxIconLoadsAsRdp`: elle yazılmış eski JSON (`"version":1`, sunucuda `"icon":"linux"`, `protocol` yok) yüklenir; `Load().CorruptBackupPath` null, sunucu `Protocol.Rdp`, `Icon == ServerIcon.Windows`.
  - `SshServerRoundTrips`: `Protocol.Ssh`, `Auth.Key`, `KeyFile "~/.ssh/id_ed25519"`, `HostKeyFingerprint "SHA256:abc"`, Port 22 kaydedilip okunur, aynı gelir; dosyada `"icon"` anahtarı yok.
  - `CopyKeepsSshFieldsButNotHostKey`.
  - `SshKeyAuthDoesNotInheritPassword`: klasörde şifre "klasor", SSH+Key sunucusunun kendi yuvası boş → `Password` null; kendi yuvasında "anahtar123" → o gelir.
  - `SshIgnoresDomain`: klasörde domain "AVVA", SSH sunucusunda `Domain` null.
  - Mevcut testlerdeki `ServerIcon.Linux`/`Icon =` atamaları kaldırılır (artık türetilmiş).
- [ ] Kırmızı, yaz, yeşil: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests`
- [ ] Commit: "SSH için sunucu modeli, ikon protokolden"

### Task 3: Sunucu formu: bağlantı tipi ve SSH alanları

**Files:** Modify `src/RdpNext.App/ViewModels/ServerEditorViewModel.cs`, `src/RdpNext.App/Views/ServerEditorView.axaml`, `src/RdpNext.App/Strings/Strings.resx`, `src/RdpNext.App/Views/ServerIconView.axaml(.cs)` (Linux çizimi silinir); Test `tests/RdpNext.App.Tests/ServerEditorTests.cs`, `MainWindowTests.cs`, `FormLayoutTests.cs`

**Interfaces:**
- Consumes: Task 2 modeli; `IFileDialogService.OpenAsync()`.
- Produces (`ServerEditorViewModel`): `Protocol Protocol` (observable), `bool IsSsh`, `bool IsRdp`, `SshAuth Auth`, `bool IsKeyAuth`, `bool IsPasswordAuth`, `string KeyFile`, `IAsyncRelayCommand ChooseKeyFileCommand`. `Icon`, `Icons` kalkar. `Password` alanı SSH+Key'de anahtar parolası olarak kullanılır (aynı yuva).

Davranış (spec "Form"):
1. Satırlar: Bağlantı tipi (iki seçenekli, ikonlu; `subtab` benzeri yuvarlak düğme grubu, çizimdeki gibi) | Renk; Görünen ad | Klasör; Adres | Port; sonra tipe göre alanlar.
2. RDP: Kullanıcı | Şifre, Domain; Gelişmiş sekmesi açık.
3. SSH: Kullanıcı | Giriş yöntemi (Password / Key file); Password → Şifre alanı; Key file → Key file (yol + "Choose…") | Key passphrase. Domain gizli; Gelişmiş alt sekmesi `IsEnabled=false` ve soluk; SSH seçilince `ShowAdvanced` false olur.
4. Tip değişince port "3389" ise "22" olur, "22" ise "3389" olur; başka değerse dokunulmaz.
5. Kaydederken RDP ise `Auth = Password`, `KeyFile = null`, `HostKeyFingerprint = null`; SSH ise `Domain = null`.
6. Giriş yöntemi değişip kaydedilince yuvadaki eski gizli bilgi silinir (Password→Key geçişinde şifre, tersinde anahtar parolası); yeni alan doluysa yazılır.

- [ ] Testler (VM, düz Fact):
  - `NewServerDefaultsToWindows` (Protocol Rdp, Port "3389").
  - `ChoosingSshSetsPort22AndHidesDomain` (`IsSsh`, Port "22").
  - `CustomPortSurvivesTypeChange` (Port "2200" → SSH → "2200").
  - `SwitchingBackToWindowsRestoresRdpDefaults` (kayıtlı SSH+Key sunucusu, `KeyFile` dolu, `HostKeyFingerprint "SHA256:x"` → Windows seç, kaydet → Port 3389, `KeyFile` null, `HostKeyFingerprint` null, Protocol Rdp).
  - `SshKeyServerSavesPathAndPassphrase` (`KeyFile "/k/id"`, Password "anahtar123" → dosyada yol, yuvada "anahtar123", dosyada "anahtar123" yok).
  - `ChangingAuthMethodDeletesOldSecret` (şifreli SSH sunucusu Key'e çevrilip parola boş kaydedilince yuva boş).
  - `ChooseKeyFileFillsPath` (sahte `IFileDialogService.OpenAsync` → "/k/id_ed25519").
  - Görünüm (headless): `SshFormShowsKeyFieldsOnlyForKeyAuth`, `AdvancedTabDisabledForSsh`; `MainWindowTests`'teki Linux ikon testi "SSH sunucusu ağaçta ve sekmede SSH çizimiyle görünür" olarak değişir.
- [ ] Kırmızı, yaz, yeşil: `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests --filter "FullyQualifiedName~ServerEditor|FullyQualifiedName~MainWindow|FullyQualifiedName~FormLayout"`
- [ ] Uygulamayı çalıştırıp formu çizimle karşılaştır (açık ve koyu tema), ekran görüntüsünü Murat'a göster.
- [ ] Commit: "Sunucu formunda bağlantı tipi ve SSH alanları"

### Task 4: SSH oturum katmanı (`RdpNext.Ssh`)

**Files:** Create `src/RdpNext.Ssh/RdpNext.Ssh.csproj`, `ISshSession.cs`, `SshSession.cs`; `tests/RdpNext.Ssh.Tests/RdpNext.Ssh.Tests.csproj`, `SshServerFixture.cs`, `SshSessionTests.cs`; Modify `RdpNext.slnx`, `Directory.Packages.props` (`SSH.NET` 2026.0.0)

**Interfaces (Produces):**
```csharp
public sealed record SshConnectOptions(string Host, int Port, string Username, string? Password,
    string? KeyFile, string? KeyPassphrase, int Columns, int Rows);
public sealed record SshHostKey(string Algorithm, string Fingerprint); // Fingerprint: "SHA256:<base64>"
public enum SshEndReason { Exited, Disconnected, AuthFailed, PassphraseNeeded, KeyUnusable, HostKeyRejected, ConnectFailed }
public interface ISshSession : IDisposable
{
    event Action? Connected;
    event Action<byte[]>? Output;            // oturum iş parçacığından
    event Action<SshEndReason, string?>? Ended;
    Func<SshHostKey, Task<bool>>? VerifyHostKey { get; set; } // null: reddet
    void Start(SshConnectOptions options);    // hemen döner
    void Send(byte[] data);
    void Resize(int columns, int rows);
}
```
- `SshSession`: SSH.NET `SshClient`; `HostKeyReceived`'da `VerifyHostKey` beklenir (`e.CanTrust`). `KeyFile` doluysa `PrivateKeyFile(path, passphrase)` ile yalnız anahtar girişi; boşsa yalnız şifre. `ShellStream("xterm-256color", cols, rows, 0, 0, 4096)`. Okuma döngüsü arka planda; akış kapanınca `Exited`. Hata eşlemesi: `SshAuthenticationException` → `AuthFailed`; parolalı anahtar ve parola yok/yanlış (`SshPassPhraseNullOrEmptyException`, `SshException` "passphrase") → `PassphraseNeeded`; dosya yok ya da okunamıyor, sunucu anahtarı reddetti → `KeyUnusable`; host key reddedildi → `HostKeyRejected`; soket hatası → `ConnectFailed`; bağlıyken kopma → `Disconnected`. `Resize` → `ShellStream.ChangeWindowSize`. Şifre ve parola hiçbir mesaja yazılmaz.

- [ ] Testler (`SshServerFixture` Task 1'deki `start.sh`'ı 2222'de çalıştırır; Docker yoksa `Assert.SkipWhen`):
  - `ConnectsWithPasswordAndRunsCommand` (`echo merhaba\n` gönder → çıktıda "merhaba").
  - `ConnectsWithEachKeyType` (ed25519, rsa, ecdsa; Theory).
  - `EncryptedKeyWithoutPassphraseEndsWithPassphraseNeeded`; `EncryptedKeyWithWrongPassphrase...` aynı; doğru parola bağlanır.
  - `MissingKeyFileEndsWithKeyUnusable`.
  - `WrongPasswordEndsWithAuthFailed` (yalnız Docker).
  - `HostKeyAskedWithSha256Fingerprint`; `RejectedHostKeyEndsWithHostKeyRejected`.
  - `ExitEndsWithExited` (`exit\n`).
  - `ContainerStopEndsWithDisconnected` (bağlıyken `docker restart rdpnext-ssh-test`).
  - `TurkishCharactersRoundTrip` (`printf 'ğüşıöçİ\n'` → UTF-8 çıktıda aynı dize).
  - `ResizeChangesStty` (`Resize(100, 30)` sonra `stty size` → "30 100").
- [ ] Kırmızı, yaz, yeşil: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Ssh.Tests`
- [ ] Commit: "SSH oturum katmanı ve Docker testleri"

### Task 5: Ortak oturum sekmesi tabanı ve SSH sekmesi (ViewModel)

**Files:** Create `src/RdpNext.App/ViewModels/ConnectionTabViewModel.cs`, `SshTabViewModel.cs`; Modify `SessionTabViewModel.cs`, `SessionsViewModel.cs`, `MainWindowViewModel.cs`, `src/RdpNext.App/RdpNext.App.csproj` (Ssh projesine referans); Create `tests/RdpNext.App.Tests/FakeSshSession.cs`, `SshTabTests.cs`

**Interfaces:**
- Consumes: Task 4 `ISshSession`, Task 2 modeli.
- Produces:
  - `abstract class ConnectionTabViewModel : TabViewModel`: `ServerNodeViewModel Server`, `SessionState State`, `bool ShowConnecting`, `bool ShowDisconnected`, `bool IsAskingCredentials`, `PromptUsername`, `PromptPassword`, `PromptSave`, `SignInTitle`, `ConnectingTitle`, `SignInCommand`, `CancelSignInCommand`, `ReconnectCommand`, `event CloseRequested`, `event ServerChanged`, `Title/Color/Icon/CanReconnect` override'ları, `OnClosed` (Server bağını çözer, `State = null`). Soyut: `protected abstract void StartWith(string username, string password, string? domain)`, `protected abstract void ReconnectCore()`. `SessionTabViewModel` (RDP, adı aynı kalır) bundan türer; domain, sertifika, pano, imleç, boyut onda kalır. Bu adım saf yeniden düzenlemedir: mevcut bütün App testleri değişmeden geçmeli.
  - `SessionsViewModel.Windows : ObservableCollection<ConnectionTabViewModel>`; `Open(ServerNodeViewModel)` dönüşü `ConnectionTabViewModel`, protokole göre `SessionTabViewModel` ya da `SshTabViewModel` kurar. `MainWindowViewModel` ve `MainWindow.axaml.cs`'teki `SessionTabViewModel` kullanımları gerektiği yerde tabana döner; `MainWindowViewModel` kurucusuna `Func<ISshSession>? ssh = null`.
  - `SshTabViewModel : ConnectionTabViewModel`: `void Connect(Func<ISshSession> newSession, ResolvedCredentials credentials, int columns, int rows)`, `event Action<byte[]> Output`, `void Send(byte[] data)`, `void TerminalSizeChanged(int columns, int rows)`, `SshHostKey? HostKeyPrompt`, `string? PreviousHostKey` (değiştiyse eski parmak izi), `TrustHostKeyCommand`, `CancelHostKeyCommand`, `bool IsAskingPassphrase`, `string PromptPassphrase`, `SubmitPassphraseCommand`, `event Action<string> PassphraseSaved`.

Davranış (spec "Davranış"):
1. Şifre girişinde kullanıcı ya da şifre yoksa ya da `AuthFailed`: mevcut sign-in kartı (domain alanı SSH'de gizli).
2. Anahtar girişinde kullanıcı yoksa sign-in kartı yalnız kullanıcıyla; `PassphraseNeeded`: tek alanlı parola kartı ("Save" ile, kaydedilirse sunucunun yuvasına); `KeyUnusable`: `Disconnected` + hata yazısı, şifreye düşülmez.
3. Host key: kayıtlıyla aynıysa sorulmaz; kayıt yoksa kart (Trust); kayıt farklıysa kart eski ve yeni parmak iziyle, düğme "Trust new key". Güvenilince `HostKeyFingerprint` yazılır, `ServerChanged`.
4. `Exited` → `CloseRequested`; `HostKeyRejected` → `CloseRequested`; diğerleri → `Disconnected` (Reconnect).
5. Olaylar arayüz iş parçacığına aktarılır; eski oturumdan gelen olay yok sayılır (RDP'deki `OnUi` kuralı).

- [ ] Önce yeniden düzenleme: `ConnectionTabViewModel` çıkar, `/usr/local/share/dotnet/dotnet test tests/RdpNext.App.Tests` tamamı yeşil. Commit: "Oturum sekmesinin ortak tabanı"
- [ ] `SshTabTests` (FakeSshSession ile): `PasswordServerConnectsWithResolvedCredentials`, `MissingPasswordAsksCredentials`, `AuthFailedAsksAgain`, `KeyServerPassesKeyFileAndPassphrase`, `PassphraseNeededAsksPassphraseAndSaves`, `KeyUnusableShowsDisconnectedWithoutPasswordFallback`, `FirstHostKeyAsksAndTrustSaves`, `SameHostKeyDoesNotAsk`, `ChangedHostKeyAsksAndDoesNotConnect` (`PreviousHostKey` dolu, cevap gelene kadar `Connected` yok), `ExitClosesTab`, `DropShowsReconnectAndReconnectReuses`, `TreeDotFollowsState`, `OutputFromOldSessionIgnored`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Commit: "SSH oturum sekmesi"

### Task 6: Terminal görünümü

**Files:** Create `src/RdpNext.App/Views/TerminalView.axaml(.cs)`; Modify `MainWindow.axaml` (DataTemplate `vm:SshTabViewModel`), `SessionWindow.axaml(.cs)` (tam ekranda SSH de), `SessionView.axaml` kart stilleri paylaşım için gerekiyorsa `Styles/Controls.axaml`'a taşınır, `Strings.resx`, `Directory.Packages.props` (Task 1'deki paket); Test `tests/RdpNext.App.Tests/TerminalViewTests.cs`

**Interfaces:**
- Consumes: Task 5 `SshTabViewModel`, Task 1'de seçilen kontrol.

Davranış:
1. Sekme seçilince terminal odak alır. Çıktı `Output` → kontrolün besleme API'si; kontrol girdisi → `Send`.
2. Boyut: kontrolün sütun/satır sayısı değişince `TerminalSizeChanged`; ilk bağlanma o anki boyutla.
3. Mac: Cmd+C seçili metni panoya, Cmd+V panodaki metni `Send`; Ctrl+C sunucuya gider. Windows: Ctrl+Shift+C / Ctrl+Shift+V.
4. Renkler: uygulamanın açık/koyu temasına göre zemin ve yazı (`Tokens.axaml` fırçaları), yazı tipi sabit genişlikli (macOS'ta Menlo, Windows'ta Cascadia Mono, yoksa Consolas), 14 px.
5. Kartlar (bağlanıyor, koptu + Reconnect, sign-in, parola, host key) RDP kartlarıyla aynı stil ve yer.

- [ ] Testler (headless): `OutputReachesTerminal`, `TypingSendsBytes` ("ls\r"), `ResizeSendsColumnsAndRows`, `CmdVPastesClipboard`, `HostKeyCardShowsOldAndNewFingerprint`, `PassphraseCardHasSingleField`.
- [ ] Kırmızı, yaz, yeşil.
- [ ] Commit: "Uygulama içi terminal"

### Task 7: Uçtan uca deneme ve teslim

**Files:** yok (yalnız çalışma ve doğrulama); gerekirse küçük düzeltmeler ayrı commit.

- [ ] Global Constraints 6'daki yedeği al.
- [ ] Tam test: `/usr/local/share/dotnet/dotnet test RdpNext.slnx` (Docker açık) tamamı yeşil.
- [ ] Uygulamayı derleyip aç; Docker sunucusuna formdan SSH sunucusu ekle (şifreyle ve parolalı anahtarla): ilk host key sorusu, vim, htop, Türkçe yazma, pencere boyutu, Cmd+C/V, tam ekran, `exit` sekmeyi kapatır, `docker restart` → Reconnect, ağaçta yeşil nokta.
- [ ] Docker kapsayıcısını yeniden kur (yeni host key) → "Trust new key" kartı geliyor, basmadan bağlanmıyor.
- [ ] Var olan bir RDP sunucusuna bağlan, eskisi gibi çalıştığını gör (Review Focus 1).
- [ ] Avva Mobile sunucusuna yalnız okuma denemesi (Global Constraints 7); bağlantı bilgisini o an Murat'tan iste.
- [ ] Windows denemesi bu planda yok; RDP'nin Windows işiyle (`rdp-windows` dalı) birlikte en sonda Murat'ın makinesinde yapılır.
- [ ] Ekran görüntüleri ile Murat'a göster; onay gelince master'a `--no-ff` merge, dal silinir.
