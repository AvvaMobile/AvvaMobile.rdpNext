# Plan 3a: Sunucu ve klasör ekleme/düzenleme

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sunucu ve klasörleri ekleyip düzenleyen, silen, kopyalayan, değişiklikleri diske ve şifreleri işletim sistemi anahtar zincirine yazan uygulama.

**Architecture:** Düzenleme ekranı ortadaki sekme şeridinde ayrı bir sekmedir (DbGate). Sekmeler ortak `TabViewModel` tabanından türer: oturum sekmesi ve düzenleme sekmeleri. Her değişiklikten sonra `ConnectionStore.Save` çağrılır; şifreler yalnız `ISecretStore`'a yazılır.

**Tech Stack:** Plan 1 ile aynı. macOS Keychain için Security.framework P/Invoke, Windows için advapi32 `CredWrite/CredRead/CredDelete`.

**Spec:** `docs/superpowers/specs/2026-10-27-rdpnext-v1-design.md` ("Düzenleme ve ekleme ekranı" bölümü). Onaylı çizim: `docs/tasarim/duzenle-sekmesi.png`.

## Global Constraints

1. Plan 1'in Global Constraints maddelerinin hepsi geçerli (dotnet yolu, yazı boyutları, metinler `Strings.resx`'te, em dash yok, Türkçe kısa commit).
2. Düzenleme ekranı yalnız sağ tık > Düzenle ile açılır. Tek tık seçer, çift tık bağlanır.
3. "Sunucu ekle" / "Klasör ekle" aynı sekmeyi boş açar; ağaçta seçili klasör (sunucu seçiliyse onun klasörü) önceden seçili gelir, hiçbir şey seçili değilse en üst.
4. Form: alt sekmeler Genel, Gelişmiş; etiketler üstte; satırlar Adres + Port, Kullanıcı + Şifre (göz ikonu), Domain + Klasör, Görünen ad + Renk; altta Bağlan, Kaydet, sağda Sil. Gelişmiş: Ekran (pencereye sığdır / tam ekran).
5. Devralınan değer boş alanda soluk yer tutucu olarak "<değer> (<klasör>'den)" görünür.
6. Şifre dosyaya yazılmaz; anahtar zincirinde servis `rdpNext`, hesap düğüm `Id`'si.
7. Git: `master`'dan `ekleme-duzenleme` dalı, bitince `--no-ff` merge.

## Review Focus

1. Yeni sunucu kaydedilmeden sekme kapatılırsa ağaçta yarım kayıt kalmamalı. Test: Task 4.
2. Port alanına harf ya da 0/70000 girilirse kaydedilmemeli (Kaydet/Bağlan pasif). Test: Task 4.
3. Klasör kendi alt klasörüne taşınamamalı (seçeneklerde görünmemeli). Test: Task 5.
4. Şifre alanı boşaltılıp kaydedilirse anahtar zincirindeki şifre silinmeli. Test: Task 4.
5. Açık oturumu olan sunucu silinirse oturum sekmesi de kapanmalı. Test: Task 6.

---

### Task 1: Devralınan değer bilgisi ve sunucu kopyalama (Core)

**Files:** Modify `src/RdpNext.Core/Credentials/CredentialResolver.cs`, `src/RdpNext.Core/Model/Server.cs`; Test `tests/RdpNext.Core.Tests/CredentialResolverTests.cs`, `tests/RdpNext.Core.Tests/ServerCopyTests.cs`

**Interfaces (Produces):**
- `sealed record InheritedValue(string Value, string FromGroup)`
- `sealed record InheritedCredentials(InheritedValue? Username, InheritedValue? Password, InheritedValue? Domain)`
- `static InheritedCredentials CredentialResolver.Inherited(Group? start, ISecretStore secrets)` (start'tan köke doğru, her alan için ilk dolu değer ve geldiği grubun adı; kök grubun adı boşsa atlanmaz ama adı "" olur)
- `Server Server.CopyWithNewId(string name)` (tüm alanlar aynı, yeni `Id`, `Parent` null)

- [ ] Testler: `InheritedReportsSourceGroup` (Üretim > Web; Üretim'de user, Web'de domain -> Username ("admin","Üretim"), Domain ("AVVA","Web")), `InheritedNullStartGivesNulls`, `CopyKeepsFieldsWithNewId`.
- [ ] Kırmızı, yaz, yeşil: `/usr/local/share/dotnet/dotnet test tests/RdpNext.Core.Tests`
- [ ] Commit: "Devralınan değer kaynağı ve sunucu kopyası"

### Task 2: İşletim sistemi anahtar zinciri (Core)

**Files:** Create `src/RdpNext.Core/Secrets/MacKeychainSecretStore.cs`, `WindowsCredentialSecretStore.cs`, `SecretStores.cs`; Test `tests/RdpNext.Core.Tests/MacKeychainSecretStoreTests.cs`

**Interfaces (Produces):**
- `MacKeychainSecretStore(string service = "rdpNext") : ISecretStore` (Security.framework `SecKeychainAddGenericPassword`, `SecKeychainFindGenericPassword`, `SecKeychainItemModifyAttributesAndData`, `SecKeychainItemDelete`, `SecKeychainItemFreeContent`, CoreFoundation `CFRelease`; bulunamadı = -25300)
- `WindowsCredentialSecretStore(string prefix = "rdpNext:") : ISecretStore` (advapi32 `CredWriteW/CredReadW/CredDeleteW/CredFree`, generic, persist local machine)
- `static ISecretStore SecretStores.ForCurrentPlatform()` (macOS -> Mac, Windows -> Windows, diğer -> InMemory)

- [ ] Testler (yalnız macOS'ta koşar, diğerlerinde atlanır; servis adı `rdpNext-test-<guid>`): `SetThenGetReturnsPassword`, `SetTwiceOverwrites`, `MissingGivesNull`, `DeleteRemoves`, `DeleteMissingDoesNotThrow`, `UnicodePasswordRoundTrips` ("Şifre-ğüı€"). Her test kendi kaydını siler.
- [ ] Kırmızı, yaz, yeşil.
- [ ] `App.axaml.cs` `InMemorySecretStore` yerine `SecretStores.ForCurrentPlatform()`.
- [ ] Commit: "Şifreler işletim sistemi anahtar zincirinde"

### Task 3: Sekmelerin ortak tabanı

**Files:** Create `src/RdpNext.App/ViewModels/TabViewModel.cs`; Modify `SessionTabViewModel.cs`, `SessionsViewModel.cs`, `MainWindowViewModel.cs`, `Views/TabStripView.axaml`, `Views/MainWindow.axaml`; Test `tests/RdpNext.App.Tests/SessionsViewModelTests.cs`

**Interfaces (Produces):**
- `abstract partial class TabViewModel : ObservableObject { abstract string Title { get; } virtual void OnClosed() {} }` (`Title` değişince `OnPropertyChanged(nameof(Title))` çağrılabilir)
- `SessionTabViewModel : TabViewModel` (davranış aynı; `OnClosed` sunucunun `IsConnected`'ını false yapar)
- `SessionsViewModel.Tabs : ObservableCollection<TabViewModel>`, `Selected : TabViewModel?`, `Open(ServerNodeViewModel) : SessionTabViewModel`, `OpenTab(TabViewModel tab)` (ekler ve seçer), `Close(TabViewModel)`, `CloseOthers(TabViewModel)`, `T? Find<T>(Func<T,bool> match) where T : TabViewModel`

- [ ] Mevcut testler yeni tiplere uyarlanır, yeni test: `OpenTabAddsAndSelectsAnyTab`.
- [ ] Sekme şeridi: oturum sekmesi renkli kutucuk; düzenleme sekmesi dişli kutucuk + başlık. Oturum alanı `ContentControl` veri şablonları türe göre.
- [ ] Yeşil: `/usr/local/share/dotnet/dotnet test`; commit "Sekmeler ortak tabandan türüyor"

### Task 4: Sunucu düzenleme sekmesi

**Files:** Create `src/RdpNext.App/ViewModels/ServerEditorViewModel.cs`, `FolderOption.cs`, `src/RdpNext.App/Views/ServerEditorView.axaml(.cs)`; Modify `TreeViewModel.cs`, `TreeNodeViewModel.cs`, `MainWindowViewModel.cs`, `Strings.resx`; Test `tests/RdpNext.App.Tests/ServerEditorTests.cs`

**Interfaces (Produces):**
- `sealed record FolderOption(Group? Group, string Label)` (`Group` null = en üst; `Label` iç içe klasörde başında iki boşluk / seviye; en üst etiketi `Strings.TopLevel` = "En üstte")
- `TreeViewModel`: `TreeNodeViewModel? Selected` (gözlemlenir), `IReadOnlyList<FolderOption> FolderOptions(Group? exclude = null)`, `ServerNodeViewModel AddServer(Server server, Group? parent)`, `GroupNodeViewModel AddGroup(Group group, Group? parent)`, `void Move(TreeNodeViewModel node, Group? newParent)`, `void Remove(TreeNodeViewModel node)`, `T Node<T>(Guid id)`
- `TreeNodeViewModel.Refresh()` (Name/Host/Color değişikliklerini bildirir)
- `ServerEditorViewModel : TabViewModel` (ctor: `MainWindowViewModel owner, Server? existing, Group? folder`): `Name, Host, Port (string), Username, Password, Domain, IsPasswordVisible, FolderOption Folder, IReadOnlyList<FolderOption> Folders, ServerColor Color, ScreenMode Screen, bool IsNew, string? UsernameHint, PasswordHint, DomainHint`, komutlar `SaveCommand`, `ConnectCommand`, `DeleteCommand` (Save ve Connect `CanSave` ile: Host dolu ve Port 1-65535). `Title` = "<ad> ayarları"; yeni ise `Strings.NewServer` = "Yeni sunucu".
- `MainWindowViewModel`: `EditServer(ServerNodeViewModel)`, `NewServer()`, `Persist()` (= `Store.Save(Tree.Root)`)

- [ ] Testler (VM, düz Fact): `NewServerSavesToTreeFileAndKeychain` (Host "10.0.1.50", şifre "pw" -> ağaçta düğüm, dosyada sunucu, secrets'ta "pw", dosyada "pw" yok), `EmptyNameDefaultsToHost`, `InvalidPortDisablesSave` ("abc","0","70000"), `EditChangesNameAndMovesFolder`, `ClearingPasswordDeletesSecret`, `ClosingUnsavedNewServerLeavesNothing`, `HintsShowInheritedValues` ("admin (Üretim'den)"), `EditingSameServerTwiceReusesTab`, `ConnectSavesThenOpensSession`.
- [ ] Görünüm: onaylı çizim; alanlar `TextBox`, Klasör `ComboBox`, Renk beş kare, şifrede göz düğmesi (`PasswordChar` değişir). Headless test: `EditorShowsFieldsInApprovedOrder` (etiketler sırası Adres, Port, Kullanıcı, Şifre, Domain, Klasör, Görünen ad, Renk).
- [ ] Yeşil, commit "Sunucu ekleme ve düzenleme sekmesi"

### Task 5: Klasör düzenleme sekmesi

**Files:** Create `src/RdpNext.App/ViewModels/FolderEditorViewModel.cs`, `src/RdpNext.App/Views/FolderEditorView.axaml(.cs)`; Test `tests/RdpNext.App.Tests/FolderEditorTests.cs`

**Interfaces (Produces):** `FolderEditorViewModel : TabViewModel` (ctor `owner, Group? existing, Group? parent`): `Name, FolderOption Parent, Folders, Username, Password, Domain`, `SaveCommand` (Name dolu), `DeleteCommand`; `MainWindowViewModel.EditFolder(GroupNodeViewModel)`, `NewFolder()`.

- [ ] Testler: `NewFolderAtTopLevel`, `NewFolderInsideSelected`, `FolderCannotMoveIntoItself` (seçeneklerde kendisi ve alt klasörleri yok), `FolderCredentialsSaved` (şifre secrets'ta).
- [ ] Yeşil, commit "Klasör ekleme ve düzenleme sekmesi"

### Task 6: Sağ tık menüleri, butonlar, silme onayı, kopyalama

**Files:** Modify `Views/SidebarView.axaml(.cs)`, `MainWindowViewModel.cs`, `Strings.resx`; Create `src/RdpNext.App/Views/ConfirmDialog.axaml(.cs)`, `src/RdpNext.App/Services/IConfirmService.cs`; Test `tests/RdpNext.App.Tests/TreeActionsTests.cs`

**Interfaces (Produces):** `interface IConfirmService { Task<bool> ConfirmAsync(string message, string confirmText); }`; `MainWindowViewModel` komutları: `EditCommand(TreeNodeViewModel)`, `DeleteNodeCommand(TreeNodeViewModel)`, `CopyServerCommand(ServerNodeViewModel)`, `NewServerCommand`, `NewFolderCommand`, `ConnectCommand` (var).

- [ ] Sunucu sağ tık: Bağlan, Düzenle, Kopyala, Sil. Klasör sağ tık: Sunucu ekle, Klasör ekle, Düzenle, Sil.
- [ ] Silme onayı: "<ad> silinsin mi?" / klasörde "<ad> ve içindeki <n> sunucu silinsin mi?", düğmeler Vazgeç, Sil.
- [ ] Testler (sahte `IConfirmService`): `DeleteServerAfterConfirm`, `DeleteCancelledKeepsServer`, `DeleteServerClosesItsTabs` (oturum + düzenleme), `DeleteFolderRemovesChildrenAndSecrets`, `CopyServerAddsCopyAndOpensEditor` (ad "web-01 kopya", aynı klasör, şifre de kopyalanır), `AddButtonsUseSelectedFolder`, headless `RightClickServerShowsMenu`.
- [ ] Yeşil, commit "Sağ tık menüleri, silme ve kopyalama"

### Task 7: Uygulamada deneme ve merge

- [ ] Uygulamayı çalıştır, sunucu ve klasör ekle/düzenle/sil/kopyala; anahtar zincirinde kaydı `security find-generic-password -s rdpNext` ile doğrula (şifreyi ekrana basmadan). Ekran görüntüsünü çizimle karşılaştır.
- [ ] Tüm testler yeşil, merge.

## Uygulamada verilen kararlar (2026-10-27)

1. Sürükle-bırak, menüden "Klasöre taşı" ve tema seçimi bu plana alınmadı; taşıma formdaki Klasör alanıyla yapılıyor.
2. macOS'ta eski `SecKeychain*GenericPassword` API'si kullanıldı (basit, macOS 13+'da çalışıyor). Windows mağazası yazıldı ama Windows'ta denenmedi.
3. Devralınan değerin kaynağı Türkçe ayrılma ekiyle yazılır ("Üretim'den", "Test'ten").
4. Görünen ad boşsa adres kullanılır. Gelişmiş sekmesinde ekran seçimi açılır liste.
5. Silme onayı küçük ayrı pencere: "web-01 silinsin mi?", "Üretim ve içindeki 3 sunucu silinsin mi?"; düğmeler Vazgeç, Sil.
6. Anahtar zincirine erişilemezse uygulama çökmez; sol panelin üstünde "Anahtar zincirine erişilemedi, şifre okunamadı ya da kaydedilmedi." görünür. Şifre yalnız değiştiyse yazılır.
7. İnceleme sonrası düzeltilenler: açık sekmeden klasörü kendi alt klasörüne taşıma (artık engelleniyor), silinen klasörden sonra çökme, arama açıkken silinen sunucuya Enter, anahtar zinciri reddinde çökme, her kaydetmede anahtar zinciri sorusu.
8. Ertelenen küçükler: sunucu adı arama açıkken değişince süzme yenilenmiyor; "web-20" gibi onlarla biten adlarda ek yanlış ("dan"); kapanan oturum sekmesi sunucu düğümüne bağlı kalıyor; devralınan değer ipuçları başka sekmede klasör değişince yenilenmiyor; boş şifre okunurken olası hata; kaydedilmemiş değişiklikle sekme kapatılınca soru sorulmuyor.
