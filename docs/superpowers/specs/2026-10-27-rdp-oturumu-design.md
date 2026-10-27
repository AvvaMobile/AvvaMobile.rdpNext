# RDP oturumu: tasarım

Tarih: 2026-10-27. Üst doküman: `2026-10-27-rdpnext-v1-design.md` ("RDP oturumu" bölümü). Bu doküman o bölümün nasıl yapılacağını anlatır.

## Neden

Uygulamanın ağacı, sekmeleri, düzenleme ekranları ve ayarları hazır; ama sekmeye çift tıklayınca açılan alan hâlâ boş bir yer tutucu. rdpNext'in var olma sebebi Mac'te RDP; bu adım uygulamayı gerçekten kullanılır yapar.

Murat'ın kararları: üst dokümandaki sekiz oturum maddesinin hepsi tek seferde yapılır, parça parça teslim yok. Aynı turda hem macOS'ta hem Windows'ta çalışır.

## Hedefler

Üst dokümandaki sekiz madde, aynen:

1. Oturum sekmedeki alana sığar; pencere büyüyüp küçüldükçe sunucudaki çözünürlük de değişir (dinamik çözünürlük).
2. Tam ekran: sekmeden tam ekrana geçilir, aynı kısayolla geri dönülür.
3. Metin kopyala-yapıştır iki yönde çalışır.
4. Dosya kopyala-yapıştır: macOS'ta çalışıp çalışmadığı denenir. Çalışıyorsa açılır; çalışmıyorsa bu sürümde yok sayılır ve Murat'a söylenir.
5. Sunucunun sesi bu bilgisayarda çalar.
6. Sertifika uyarısı: sertifika ilk bağlantıda doğrulanamazsa kullanıcıya sorulur; "güven" denirse o sunucu için hatırlanır.
7. Bağlantı koparsa sekme kapanmaz; "Yeniden bağlan" seçeneği gösterilir.
8. Kullanılabilir kullanıcı adı ya da şifre yoksa ya da sunucu girişi reddederse kullanıcı adı ve şifre sorulur; "Kaydet" işaretliyse sunucuya yazılır.

Bunların çalışması için gereken ama listede adı geçmeyenler de bu işe dahildir: klavye (Türkçe Mac klavyesi dahil), fare (sağ tık, tekerlek), sunucunun imleç şekli, aynı anda birden çok oturum.

## Hedef olmayanlar

1. Görüntünün Retina ekranda tam keskin olması. Murat: "bir tık bulanık olması hiç sorun değil." Oturum mantıksal çözünürlükte (pencerenin nokta boyutu) açılır.
2. Üst dokümanda zaten dışarıda bırakılanlar: RD Gateway, yazıcı yönlendirme, çoklu monitör, yerel diski sunucuya bağlama.
3. Uygulamanın paketlenip dağıtılması (imza, notarize, kurulum dosyası, güncelleme). Bu, üst dokümandaki "Dağıtım" işinin parçasıdır, ayrı plan. Ama Windows'ta çalışabilmek için FreeRDP ve C katmanının Windows x64 için derlenip uygulamanın yanına konması bu işe dahildir.

## Başarısızlık sayılacak şeyler

1. Bir oturumun hatası ya da kopması uygulamayı çökertmesi ya da diğer sekmeleri etkilemesi.
2. Bağlanırken ya da oturum çalışırken arayüzün donması.
3. Şifrenin diske düz metin yazılması ya da log'a düşmesi.
4. Klavyenin yanlış karakter basması (özellikle Türkçe karakterler ve Mac'teki Cmd tuşu).

## Denemede kanıtlananlar (2026-10-27)

Atılacak bir denemeyle arsaVev'deki Web Server'a (141.98.205.90:63389) Mac'ten bağlanıldı ve masaüstü Avalonia penceresinde canlı göründü. Kullanılan yol:

1. FreeRDP 3.32.2, Homebrew'den (`brew install freerdp`). İçinde `disp`, `cliprdr` (dosya bağlamı dahil), `rdpsnd` (Mac ses arka ucu dahil), `rdpgfx`, `drdynvc` kanalları gömülü geliyor.
2. FreeRDP'yi saran küçük bir C katmanı (`rdpClientContext` + `gdi_init(BGRA32)` + `EndPaint` geri çağrısı + olay döngüsü). .NET bunu P/Invoke ile çağırıyor.
3. Görüntü `WriteableBitmap`'e değişen dikdörtgen kopyalanarak çiziliyor, UI iş parçacığına yalnız "yenile" gönderiliyor.
4. Şifre uygulamanın kendi anahtar zinciri kaydından (`MacKeychainSecretStore`) okundu.

## Nasıl (kararlar)

Bunlar Claude kararıdır; Murat yalnız sonucu (sekiz madde) ve denemedeki yolu onayladı.

1. FreeRDP C'den doğrudan .NET'e bağlanmaz; arada projenin kendi ince C katmanı durur (denemedeki gibi). FreeRDP yapılarının bellek düzenine .NET'ten dokunulmaz; .NET yalnız bu katmanın düz fonksiyonlarını çağırır. Katman repoda durur ve lokalde derlenir (CI yok).
2. Her oturum kendi arka plan iş parçacığında FreeRDP olay döngüsünü çalıştırır. Klavye, fare ve pano istekleri bu iş parçacığına kuyrukla iletilir; FreeRDP nesnesine iki iş parçacığından aynı anda dokunulmaz.
3. Görüntü: sunucu ekranı mantıksal çözünürlükte, `WriteableBitmap` üzerinden çizilir.
4. Dinamik çözünürlük `disp` kanalıyla yapılır; pencere boyutu değişirken istek kısa bir bekleme ile tek seferde gönderilir. Sunucu desteklemiyorsa görüntü alana ölçeklenir.
5. Sertifika: FreeRDP'nin kendi sertifika deposu kullanılmaz. "Güven" denen sertifikanın parmak izi rdpNext'in kendi verisinde sunucuya bağlı saklanır. Parmak izi sonra değişirse yeniden sorulur.
6. Mac klavyesi: Cmd+C, Cmd+V, Cmd+X, Cmd+A, Cmd+Z sunucuya Ctrl ile gönderilir (Microsoft'un Mac uygulamasındaki varsayılan gibi). Diğer Cmd kısayolları uygulamada kalır.
7. Kopan bağlantı: sekmede oturum alanının ortasında kısa bir durum yazısı ve "Reconnect" düğmesi. Sekmenin sağ tık menüsüne de "Reconnect" eklenir (üst doküman, madde 73).
8. Oturum sekmesi, kenar çubuğundaki "bağlı" yeşil noktası ile uyumlu kalır: bağlanırken, bağlıyken ve koptuğunda durum ağaçta da doğru görünür. Ağaçtaki görünüm çizimle onaylanır (aşağıda).
9. Tam ekran: macOS'un kendi tam ekranı; ekranda yalnız oturum kalır, kenar çubuğu ve sekme şeridi gizlenir. Aynı kısayol geri döndürür; başka oturuma geçmek için önce tam ekrandan çıkılır. Sunucunun Advanced sekmesindeki ekran ayarı "Full screen" ise bağlanınca doğrudan tam ekran açılır, "Fit to window" ise sekmede açılır.
10. Kopma çeşitleri: kullanıcı Windows içinde oturumu kapatırsa (sign out) sekme kapanır, mstsc'deki gibi. Ağ kopması, yönetici tarafından atılma ya da başka girişin oturumu alması gibi diğer durumlarda sekme açık kalır ve "Reconnect" görünür. Ağ kopmasında önce FreeRDP'nin kendi otomatik yeniden bağlanması kısa süre denenir, olmazsa "Reconnect" çıkar.
11. Windows: aynı C katmanı Windows x64 için de derlenir; FreeRDP Windows'ta Homebrew yerine kaynaktan ya da vcpkg ile derlenir. Mac'e özel parçalar (Cmd eşlemesi, Mac ses arka ucu) Windows'ta karşılıklarıyla değişir: Cmd eşlemesi yok, ses Windows arka ucuyla çalar. Dosya kopyala-yapıştır kuralı Windows için de aynıdır: denenir, çalışmıyorsa bu sürümde yok sayılır ve Murat'a söylenir.
12. Sıra (Murat'ın kararı): önce her şey bu Mac'te çalışır ve test edilir. Windows en sonda gelir: Murat'ın RDP ile girilen bir Windows makinesi var, Windows testini en sonda Murat orada yapar. O makineye SSH yok; bu yüzden Windows için gereken her şey (FreeRDP, C katmanı, uygulama) bu Mac'te Windows x64 için derlenir ve Murat'ın kopyalayıp çalıştıracağı tek bir klasör hâlinde `~/Downloads`'a konur.
13. Testler: arsaVev Web Server gerçek bir sunucu. Hesabı kilitlememek için yanlış şifre denemesi en fazla bir kez yapılır; şifre sorusu asıl olarak "şifre yok" yolundan denenir. Sunucuya dosya yapıştırmak gibi yazma gerektiren denemeler o an Murat'a sorularak yapılır.

## Çizim gerektirenler

Üst dokümanın kuralı geçerli: yeni ekranlar kodlanmadan önce çizilip Murat'a onaylatılır. Bu işte çizilecek olanlar:

1. Sertifika sorusu.
2. Kullanıcı adı ve şifre sorusu ("Kaydet" kutusuyla).
3. Bağlanıyor ve koptu durumları (oturum alanında).
4. Bu üç durumun kenar çubuğundaki ağaçta nasıl göründüğü.

## Builder'a kalanlar

1. C katmanının fonksiyon listesi ve .NET tarafındaki sınıf yapısı.
2. Klavye eşlemesi (Avalonia tuşu → RDP scancode), Türkçe Mac düzeninin doğru gitmesi için Unicode giriş mi scancode mu kullanılacağı.
3. Tam ekran kısayolu.
4. Dosya kopyala-yapıştır denemesinin nasıl yapılacağı ve ne zaman bırakılacağı.
5. Test yaklaşımı: oturum mantığı sahte bir oturum arayüzüyle birim testinde; gerçek bağlantı arsaVev Web Server'a karşı elle.
