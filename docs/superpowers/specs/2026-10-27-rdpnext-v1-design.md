# rdpNext ilk sürüm: tasarım

Tarih: 2026-10-27. Kaynak konuşma: `docs/brainstorm-notlari.md`.

## Neden

Ekipte herkes uzak masaüstü bağlantılarını mRemoteNG ile kendi bilgisayarında tutuyor. İki dert var:

1. mRemoteNG macOS'ta çalışmıyor; Mac kullanan (Murat dahil) kullanamıyor.
2. Bağlantılar tek bir bilgisayarda duruyor, bilgisayar giderse hepsi kayboluyor.

rdpNext, mRemoteNG'nin işlevini aynen yapan, Mac ve Windows'ta çalışan, çok daha şık bir uygulama olacak. İşlev olarak mRemoteNG "gayet güzel"; fark yaratacak şey platform ve görünüm. İleride (bu sürümde değil) bağlantılar ve şifreler buluta taşınıp ekiple paylaşılacak, en sonunda dışarıya SaaS olarak satılacak.

## Ürün ve lisans

Bu sürüm herkese açık repoda (`AvvaMobile/AvvaMobile.rdpNext`), MIT lisanslı "Community Edition" olarak tamamlanıp ücretsiz verilir. Bulut ve ekip sürümü sonra ayrı ve ücretli bir ürün olur; bu repoya bulut kodu konmaz.

## Hedefler

1. Tek kişilik masaüstü uygulaması, macOS ve Windows.
2. Yalnız RDP.
3. Soldaki ağaçtan hızlı kullanım: ara, bul, çift tıkla bağlan.
4. Sekmeli oturumlar: birden çok sunucu aynı pencerede, sekmeler arasında geçiş.
5. Hızlı sunucu ekleme, gruplama, kopyalama (sunucuyu çoğaltma).
6. mRemoteNG'nin `confCons.xml` dosyasını tek tıkla içe aktarma.
7. Tüm bağlantıları şifreli tek bir yedek dosyasına dışa aktarma ve geri içe alma.
8. Hızlı ve hafif: açılış yarım saniyenin altında, boşta RAM ~110 MB civarı (denemede ölçülen değerler, aşağıda).

## Hedef olmayanlar (bu sürümde yok)

1. SSH, VNC ya da RDP dışındaki protokoller. SSH ilk sürümden sonra eklenecek; bu yüzden sunucu kaydı baştan bir protokol alanı taşır (şimdilik yalnız RDP).
2. Bulut, hesap, ekip paylaşımı, yetki. Bunlar sonraki sürüm; ama veri modeli ileride buluta taşınmayı zorlaştırmamalı (bkz. "Sonraki sürüme hazırlık").
3. RD Gateway, yazıcı yönlendirme, çoklu monitör, yerel diski sunucuya bağlama.
4. Linux.
5. Windows kod imzası (ilk sürüm Windows'ta imzasız çıkar).

## Başarısızlık sayılacak şeyler (çalışsa bile)

1. Sıradan bir yönetim paneli gibi görünmesi. Murat'ın ilk üç çizime tepkisi "hepsi çok kötü, çok sıradan" oldu; onay alan çizim Termius tadındaki `docs/tasarim/gorsel-stil.png`.
2. Küçük yazılar. Gövde yazısı en az 15 px; ikincil yazılar (IP, sayaç) en az 14 px.
3. Gereksiz metin: açıklama paragrafları, ipucu yazıları, boş durum için uzun anlatımlar, etiket kalabalığı. Ekranda yalnız işe yarayan şey durur.
4. Hantallık: yavaş açılış, takılan ağaç ya da sekme geçişi.

## Teknoloji (karar)

1. .NET 10 + Avalonia 12 + FreeRDP.
2. Neden Avalonia (Electron değil): RDP görüntüsünü doğrudan çiziyor, daha hafif; ekip .NET biliyor.
3. Deneme ölçümü (Avalonia 12.1.4, Apple M4, 200 sunuculu ağaç + 5 sekme, kod imzası yok): ilk kare 0,14 sn (NativeAOT) / 0,32 sn (ReadyToRun), Activity Monitor belleği ~110 MB, NativeAOT uygulama boyutu ~34 MB. Bilgisayar açıldıktan sonraki ilk açılış 0,8 sn (AOT) / 2,2 sn (R2R).
4. FreeRDP her iki platformda da kullanılır (Windows'ta Microsoft'un ActiveX denetimi değil), böylece davranış tek kod yolundan gelir.
5. Bu makinede `dotnet` olarak `/usr/local/share/dotnet/dotnet` kullanılır (Homebrew'inki değil).

## Ekran ve görünüm

Onaylı çizim: `docs/tasarim/gorsel-stil.png` (kaynak HTML: `docs/tasarim/gorsel-stil.html`). Aynı ekranın açık ve koyu hâli.

1. Pencerenin üst şeridi: (Mac'te trafik ışıkları), ardından oturum sekmeleri. Her sekme: küçük renkli ikon kutucuğu + sunucu adı + kapatma (×). Sekme şeridinde "+" yok (Murat kaldırttı; ileride hızlı bağlan gibi bir işle geri gelebilir). Şeridin boş yerine çift tık pencereyi ekrana sığdırır, tekrar çift tık eski boyuta döndürür.
2. Sol panel (~310 px):
   1. En üstte "+ Sunucu ekle" butonu (vurgu rengi, tam genişlik).
   2. Altında arama kutusu. Yazdıkça ağaç ad ve adrese göre süzülür; Enter ilk sonuca bağlanır.
   3. Gruplu ağaç. Grup satırı: aç/kapa oku, klasör kutucuğu, grup adı, sağda sunucu sayısı. Sunucu satırı: renkli ikon kutucuğu, ad, altında adres; bağlı olanın sağında yeşil nokta. Seçili satır yuvarlak köşeli zeminle vurgulanır.
   4. Gruplar iç içe olabilir (mRemoteNG'de klasörler iç içe; içe aktarma bunu korumalı).
3. Sağ alan: seçili sekmenin RDP oturumu, yuvarlak köşeli bir alan içinde. Hiç sekme yokken bu alan boş durur (yazı yok; uygulamanın simgesi soluk durabilir).
4. Görsel dil: Termius tadı. Yuvarlak köşeler (10-12 px), renkli ikon kutucukları, hap şeklinde butonlar, ferah boşluklar, Inter ya da sistem yazı tipi.
5. Açık ve koyu tema ikisi de var. Varsayılan işletim sisteminin ayarını izler; kullanıcı elle de seçebilir.

## Etkileşimler

1. Tek tık: yalnız seçer. Çift tık (ya da Enter): sunucuya bağlan, yeni sekmede aç. Zaten açıksa o sekmeye geç.
2. Sunucuya sağ tık: Bağlan, Düzenle, Kopyala, Gruba taşı, Sil.
3. Gruba sağ tık: Sunucu ekle, Alt grup ekle, Düzenle, Sil.
4. Ağaçta sürükle-bırak ile sunucuyu ya da grubu başka gruba taşıma.
5. "Kopyala": sunucunun tüm ayarlarıyla aynı gruba bir kopyasını ekler ve düzenleme ekranını açar.
6. Sekmeye sağ tık: Kapat, Diğerlerini kapat, Yeniden bağlan.
7. Silmede onay sorulur.

## Düzenleme ve ekleme ekranı (Murat onayı, 2026-10-27)

Onaylı çizim: `docs/tasarim/duzenle-sekmesi.png` (kaynak: `docs/tasarim/duzenle-sekmesi.html`). DbGate'in bağlantı ekranı örnek alındı.

1. Ortadaki alanda ayrı bir sekme olarak açılır; sekme şeridinde dişli ikonu ve "<ad> ayarları" başlığıyla durur. Sağ panel ya da üstte açılan pencere değil.
2. Açılış: sunucuya ya da klasöre sağ tık > Düzenle. Tek tık ve çift tık bu ekranı açmaz.
3. Sol paneldeki "Sunucu ekle" ve "Klasör ekle" butonları aynı sekmeyi boş açar; seçili klasör önceden seçili gelir.
4. Üstte alt sekmeler: Genel, Gelişmiş. Etiketler alanların üstünde, satırlar ikişerli: Adres + Port, Kullanıcı + Şifre (göz ikonuyla), Domain + Klasör, Görünen ad + Renk. Gelişmiş: ekran ayarı (pencereye sığdır / tam ekran).
5. Gruptan devralınan değer alanda soluk görünür ve sağında "<klasör>'den" yazar.
6. Altta: Bağlan, Kaydet; sağda Sil.
7. Klasör ekranı aynı düzende: Ad, Üst klasör, Kullanıcı, Şifre, Domain.

## Sunucu ve grup bilgileri

Sunucu:

1. Ad (ağaçta ve sekmede görünen).
2. Adres (host ya da IP).
3. Port (varsayılan 3389).
4. Kullanıcı adı, şifre, domain.
5. İkon rengi (ikon kutucuğunun rengi; birkaç hazır renkten seçilir).
6. Ekran: "pencereye sığdır" (varsayılan) ya da tam ekran.

Grup:

1. Ad.
2. Kullanıcı adı, şifre, domain (isteğe bağlı).

Devralma: sunucunun kullanıcı adı, şifre ya da domain alanı boşsa en yakın üst gruptakini kullanır (mRemoteNG'deki gibi). Düzenleme ekranında boş alan, devraldığı değeri soluk gösterir.

## RDP oturumu

1. Oturum, sekmedeki alana sığar; pencere büyüyüp küçüldükçe sunucudaki çözünürlük de değişir (dinamik çözünürlük).
2. Tam ekran: sekmeden tam ekrana geçilir, aynı kısayolla geri dönülür.
3. Metin kopyala-yapıştır iki yönde çalışır.
4. Dosya kopyala-yapıştırın macOS'ta FreeRDP ile çalışıp çalışmadığı doğrulanmadı. Çalışıyorsa açılır; çalışmıyorsa bu sürümde yok sayılır ve Murat'a söylenir.
5. Sunucunun sesi bu bilgisayarda çalar.
6. Sertifika uyarısı: sunucunun sertifikası ilk bağlantıda doğrulanamazsa kullanıcıya sorulur; "güven" denirse o sunucu için hatırlanır.
7. Bağlantı koparsa sekme kapanmaz; "Yeniden bağlan" seçeneği gösterilir.
8. Kullanılabilir kullanıcı adı ya da şifre yoksa (sunucuda da üst gruplarda da boş) ya da sunucu girişi reddederse, bağlanırken kullanıcı adı ve şifre sorulur; "Kaydet" kutusu işaretliyse sunucuya yazılır. (Claude kararı)

## Veri saklama

1. Bağlantı ağacı (gruplar, sunucular, ayarlar) kullanıcının uygulama verisi klasöründe tek bir dosyada tutulur.
2. Şifreler bu dosyada düz metin durmaz; işletim sisteminin anahtar zincirinde (macOS Keychain, Windows Credential Manager) saklanır.

## mRemoteNG içe aktarma

1. Kullanıcı `confCons.xml` dosyasını seçer; klasör yapısı gruplar olarak, RDP bağlantıları sunucular olarak gelir. RDP dışındaki bağlantılar atlanır ve sonunda kaç tanesinin atlandığı tek satırla söylenir.
2. Şifreler çözülüp anahtar zincirine yazılır. Dosya özel bir ana parolayla korunuyorsa kullanıcıdan istenir.
3. mRemoteNG'deki devralma (inheritance) ayarları bizim devralma kuralına çevrilir.
4. İçe aktarılanlar ağaçta ayrı bir üst grup altına konur, mevcut bağlantılar bozulmaz.

## Yedek dışa ve içe aktarma

1. Dışa aktar: tüm ağaç ve şifreler, kullanıcının o an girdiği bir parolayla şifrelenmiş tek dosyaya yazılır.
2. İçe al: dosya seçilir, parola sorulur; içerik ayrı bir üst grup altına eklenir.
3. Parola unutulursa dosya açılamaz; bu, dışa aktarma ekranında tek satırla söylenir.

## Dağıtım

Hedef platformlar (Claude kararı): macOS 13 ve üstü, yalnız Apple Silicon (arm64); Windows 10 ve 11, x64.

1. macOS: Avva Mobile'ın Developer ID Application sertifikasıyla imzalanır ve notarize edilir; bunların hepsi bu Mac'te kurulu. Kimlik bilgileri repoya yazılmaz.
2. Windows: imzasız kurulum dosyası.
3. Güncelleme: uygulama açılışta bu reponun GitHub sürümlerine (releases) bakar. Yeni sürüm varsa bir "Güncelle" seçeneği çıkar (yeri çizimle onaylanacak); basınca indirip kurar ve yeniden başlar. (İndirip kurma kısmı Claude kararı)
4. Build, imzalama ve release lokalde yapılır; GitHub Actions ya da CI yok. Release `gh release` ile elle oluşturulur.

## Sonraki sürüme hazırlık

1. Her sunucu ve grubun kalıcı, benzersiz bir kimliği olur (ileride bulutla eşleştirme için).
2. Şifre saklama tek bir arayüzün arkasında durur; ileride buluttan gelen şifreli şifreler aynı yoldan bağlantıya eklenebilsin.

## Dil

Arayüz Türkçe. Metinler koda gömülmez, çeviriye hazır kaynak dosyasında durur; İngilizce ileride eklenir. (Claude kararı)

## Ana ekran dışındaki ekranlar

Onaylı çizim yalnız ana ekranı kapsıyor. Sunucu ve grup formu, ayarlar (tema seçimi dahil) ve bu ekranlara nereden girileceği, mRemoteNG içe aktarma, yedek dışa ve içe aktarma, sertifika ve giriş soruları: her biri kodlanmadan önce tarayıcıda çizilip Murat'a onaylatılır. Plan bu onay adımlarını içerir. (Claude kararı)

## Builder'a kalanlar

1. FreeRDP'nin .NET'e nasıl bağlanacağı, ekranın Avalonia'ya nasıl çizileceği, FreeRDP kütüphanelerinin uygulamayla nasıl paketleneceği.
2. NativeAOT ya da ReadyToRun seçimi (ölçümde AOT daha hızlı açıldı, bellek benzer).
3. Veri dosyasının biçimi ve şifreleme ayrıntıları.
4. Kısayol tuşları.
