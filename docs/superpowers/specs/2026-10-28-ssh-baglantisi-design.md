# SSH bağlantısı: tasarım

Tarih: 2026-10-28. Üst doküman: `2026-10-27-rdpnext-v1-design.md` (hedef olmayanlar, madde 1: "SSH ilk sürümden sonra eklenecek").

## Neden

rdpNext bugün yalnız Windows sunuculara (RDP) bağlanıyor. Ekipteki Linux sunucular için ayrı bir SSH uygulaması açmak gerekiyor. Murat'ın kararı: AnyDesk'ten önce SSH gelir. Amaç, aynı ağaçtan, aynı sekmelerle Linux sunuculara da bağlanmak.

## Hedefler (Murat'ın kararları)

1. Sunucu ekleme/düzenleme ekranının en üstünde bağlantı tipi seçilir: Windows ya da SSH.
2. Formun geri kalanı seçilen tipe göre değişir.
3. Ağaçtaki ve sekmedeki ikon tipe göre kendiliğinden gelir. İkon seçici kalkar.
4. SSH sunucusuna bağlanınca uygulamanın içinde, sağda, RDP oturumu gibi ayrı bir sekmede terminal açılır.
5. İlk sürümde yalnız terminal: renkler, vim/htop gibi tam ekran araçlar, kopyala-yapıştır çalışır.

## Hedef olmayanlar

1. SFTP dosya paneli, dosya aktarımı (Murat: "şimdilik sadece terminal").
2. Port yönlendirme (tunnel), jump host, ssh-agent, `~/.ssh/config` okuma.
3. Ekran bölme (split), birden çok sunucuya aynı anda komut yazma.
4. Linux'a RDP ile bağlanma seçeneği. Windows tipi = RDP, SSH tipi = SSH.

## Başarısızlık sayılacak şeyler

1. Terminalin vim, htop, nano gibi araçlarda bozuk çizmesi ya da Türkçe karakterleri yanlış göstermesi/göndermesi.
2. Şifre ya da anahtar parolasının diske düz metin yazılması ya da log'a düşmesi.
3. Bir SSH oturumunun kopması ya da hatası uygulamayı ya da diğer sekmeleri etkilemesi; bağlanırken arayüzün donması.
4. Sunucu anahtarı değiştiğinde sessizce bağlanması (araya girme saldırısına açık olur).
5. Mevcut RDP sunucularının bu değişiklikten sonra farklı davranması.

## Form

Çizim: `docs/tasarim/ssh-formu.html` (Murat onayladı: "aynen böyle"). Satırlar:

1. Bağlantı tipi (Windows / SSH, yan yana iki seçenekli düğme, ikonlu) | Renk
2. Görünen ad | Klasör
3. Adres | Port
4. Windows seçiliyse bugünkü alanlar aynen: Kullanıcı | Şifre, Domain. Gelişmiş sekmesi (ekran ayarı) açık.
5. SSH seçiliyse: Kullanıcı | Giriş yöntemi (Şifre / Anahtar dosyası).
   1. Şifre seçiliyse: Şifre alanı.
   2. Anahtar dosyası seçiliyse: Anahtar dosyası (yol + "Seç…" düğmesi) | Anahtar parolası (boş bırakılabilir).
   3. Domain yok. Gelişmiş sekmesi soluk ve tıklanamaz.
6. Port: tip değişince, port hâlâ öbür tipin varsayılanıysa (3389 ↔ 22) kendiliğinden değişir; kullanıcı elle başka port yazdıysa dokunulmaz.

Ekrandaki metinler uygulamanın geri kalanı gibi İngilizcedir (`Strings.resx`). Önerilen etiketler: "Connection type", "Windows", "SSH", "Sign-in method", "Password", "Key file", "Choose…", "Key passphrase".

## Davranış

1. Kimlik devralma: SSH sunucusunda da kullanıcı ve şifre bugünkü gibi klasörden devralınır (Claude kararı). Domain SSH'de kullanılmaz. Anahtar dosyası ve anahtar parolası devralınmaz, sunucuya özeldir (Claude kararı).
2. Gizli bilgiler: SSH şifresi ve anahtar parolası bugünkü şifreler gibi anahtar zincirinde (`ISecretStore`) durur, `connections.json`'a yazılmaz. Bugün düğüm başına tek gizli bilgi var; sunucu yalnız seçili giriş yönteminin gizli bilgisini tutar, yöntem değişince öbürü silinir (Claude kararı). Anahtar dosyasının kendisi kopyalanmaz; yalnız yolu saklanır.
3. Sunucu anahtarı (host key): ilk bağlantıda parmak izi gösterilip "güven" sorulur; "güven" denirse sunucuya bağlı saklanır (RDP sertifikasındaki `CertificateFingerprint` mantığı). `~/.ssh/known_hosts` kullanılmaz (Claude kararı; RDP'deki "kendi verimizde tut" kararıyla aynı).
   1. Anahtar sonra değişirse bağlanılmaz; oturum alanında RDP sertifika sorusuyla aynı kartta, eski ve yeni parmak iziyle açık bir uyarı ve "Trust new key" düğmesi çıkar. Basılmadıkça bağlanılmaz (Claude kararı).
4. Şifre girişinde şifre yoksa ya da sunucu reddederse: RDP'deki kullanıcı adı/şifre sorusu aynen kullanılır ("Kaydet" kutusuyla).
5. Anahtar girişinde (Claude kararları):
   1. Anahtar parolalıysa ve kayıtlı parola yoksa ya da yanlışsa: aynı soru kartı, tek alanla ("Key passphrase") ve "Kaydet" kutusuyla açılır.
   2. Anahtar dosyası bulunamazsa ya da sunucu anahtarı reddederse: şifreye geri düşülmez; hata yazısı ve "Reconnect" gösterilir.
   3. Anahtar girişindeki sunucu klasörden şifre devralmaz; kullanıcı adını devralır.
6. Kopma: sunucuda `exit` yazılıp oturum kapanırsa sekme kapanır (RDP'de sign out gibi). Ağ kopması gibi diğer durumlarda sekme açık kalır, "Reconnect" görünür. Ağaçtaki yeşil nokta ve sekme sağ tık "Reconnect" RDP ile aynı çalışır.
7. Terminal boyutu: sekme ya da pencere boyutu değişince sunucuya yeni satır/sütun sayısı bildirilir.
8. Kopyala-yapıştır (Mac): fare ile seçilen metin Cmd+C ile kopyalanır, Cmd+V yapıştırır. Ctrl+C sunucuya gider (komutu keser). Windows'ta Ctrl+Shift+C / Ctrl+Shift+V (Claude kararı, Windows Terminal'deki gibi).
9. Görünüm: sabit genişlikli yazı, uygulamanın açık/koyu temasına uyar (Claude kararı).
10. Tam ekran: RDP'deki tam ekran kısayolu SSH sekmesinde de çalışır.

## Veri

1. `Protocol` enum'una `Ssh` eklenir; tip seçimi bu alanı yazar.
2. Sunucuya SSH için yeni alanlar: giriş yöntemi, anahtar dosyası yolu, sunucu anahtarı parmak izi.
3. İkon artık protokolden türetilir (Rdp → Windows logosu, Ssh → terminal işareti). `ServerIcon` alanı ve ikon seçici kalkar; penguen ikonu kullanılmaz.
4. Eski kayıtlar: `connections.json`'daki bütün mevcut sunucular RDP'dir ve öyle kalır. Daha önce Linux ya da SSH ikonu seçilmiş bir RDP sunucusu artık Windows logosuyla görünür. Bu dosya gerçek veridir; şema değişikliğinden önce yedeği alınır ve eski dosya yeni sürümle sorunsuz açılır.
   1. Eski rdpNext sürümü SSH içeren yeni dosyayı açarsa dosyayı bozuk sayıp kenara alır ve ağacı boş açar (`ConnectionStore.Load`). Uygulama henüz dağıtılmadı, eski sürüm yalnız bu Mac'teki geliştirme derlemeleri; bu yüzden geriye uyum yazılmaz, yalnız yedek alınır (Claude kararı).
5. mRemoteNG içe aktarma henüz yazılmadı; yazılınca `confCons.xml`'deki SSH2 bağlantıları SSH tipi olarak alınır. Bu işin parçası değil.

## Nasıl (Claude kararları)

1. SSH bağlantısı: SSH.NET (NuGet `SSH.NET`, MIT). Şifre ve özel anahtar (OpenSSH biçimi, ed25519/RSA/ECDSA) girişini destekler; terminal için `ShellStream` "xterm-256color" türünde açılır.
2. Terminal çizimi: Avalonia 12 ile çalışan hazır bir terminal kontrolü (XTerm.NET tabanlı adaylar: Iciclecreek.Avalonia.Terminal, AvaloniaTerminal). Plan aşamasının ilk adımı kısa bir denemedir: aday kontrolle SSH.NET birleştirilip bu Mac'ten gerçek bir Linux sunucusunda vim, htop ve Türkçe karakter denenir. Hiçbiri yeterli değilse XTerm.NET motoru üzerine projenin kendi Avalonia çizimi yazılır.
3. Her SSH oturumu kendi arka plan işinde okur/yazar; arayüz iş parçacığı beklemez (RDP'deki kural).
4. Oturum sekmesi altyapısı (sekme, durumlar, Reconnect, ağaçtaki nokta) RDP ile ortak kullanılır; yalnız içerik alanı terminal olur.
5. Windows'ta da aynı kod çalışır (saf .NET, yerel kütüphane yok). Windows testi RDP'deki sırayla en sonda Murat'ın makinesinde yapılır.

## Test

1. Geliştirme ve otomatik testler bu Mac'teki Docker'da çalışan yerel bir OpenSSH kapsayıcısına karşı yapılır (Docker 29.4 kurulu). Sunucu anahtarı değişimi, şifreli/şifresiz anahtarlar (ed25519, RSA, ECDSA), `exit` ve ağ kopması, Türkçe karakter, vim/htop burada denenir (Claude kararı).
2. Gerçek sunucuda son kontrol: Avva Mobile sunucusu. Yalnız bağlanılır ve okuma yapılır (ls, htop, vim açıp kaydetmeden çıkma); kurulum, ayar ya da dosya değişikliği yapılmaz. Yanlış şifre denemesi orada yapılmaz (fail2ban IP'yi engelleyebilir), o Docker'da denenir. Sunucu anahtarı onayı yalnız bu Mac'te saklanır, sunucuya bir şey yazılmaz.

## Builder'a kalanlar

1. Terminal kontrolünün seçimi (denemeye göre).
2. `Server` sınıfına yeni alanların adları ve JSON biçimi.
3. Bağlantı tipi düğmesinin Avalonia'daki yapısı (mevcut `subtab`/`swatches` stilleriyle).
