# rdpNext: beyin fırtınası notları (2026-10-27)

Durum: tasarım konuşması sürüyor, henüz onaylı tasarım yok, kod yazılmadı.
Yeni oturumda `superpowers:brainstorming` ile kaldığı yerden devam edilecek.

## Fikir

mRemoteNG'nin cross-platform, çok daha şık arayüzlü karşılığı.
Asıl dert: herkes bağlantılarını kendi PC'sinde tutuyor, PC giderse hepsi kayboluyor.

## Kararlar

1. İlk sürüm: tek kişilik masaüstü uygulama, Mac + Windows.
2. Akış mRemoteNG ile birebir, arayüz çok daha şık ve modern.
3. Yalnız RDP.
4. Olmazsa olmazlar: sidebar'dan hızlı kullanım, sekmeli oturumlar, hızlı sunucu ekleme, gruplama, kopyalama.
5. Ekran düzeni (taslak A): solda gruplu sunucu ağacı, ağacın üstünde arama kutusu ve "+ Sunucu ekle" butonu, sağ tıkla kopyala / gruba taşı / düzenle, sağda üstte sekmeler ve altında RDP oturumu.
6. mRemoteNG'nin `confCons.xml` dosyasını tek tıkla içe aktarma ilk sürümde var.
7. Sıfırdan yazılacak (mRemoteNG kodu GPL, kullanılmıyor). Repo: `AvvaMobile/AvvaMobile.rdpNext`.
8. Teknoloji: .NET + Avalonia + FreeRDP. Deneme (Avalonia 12.1.4, M4 Mac, 200 sunuculu ağaç + 5 sekme): açılış 0,14 sn (native) / 0,32 sn (normal derleme), RAM ~110 MB, native uygulama ~34 MB.
9. Görsel stil: Termius tadı (yuvarlak köşeli kutular, renkli ikon kutucukları, büyük ve rahat yazı, gereksiz metin yok), düzen taslak A olarak kalır. Açık ve koyu tema ikisi de var; varsayılanı işletim sistemi ayarı (Claude önerisi, itiraz gelmedi). Onaylı çizim: `docs/tasarim/gorsel-stil.png`.
10. İlk sürüm RDP özellikleri: ad, adres, port, kullanıcı, şifre, domain; oturum pencereye sığar ve pencereyle büyür, tam ekran; metin kopyala-yapıştır (dosya kopyalama FreeRDP Mac'te doğrulanacak); ses bu bilgisayarda çalar; kullanıcı/şifre/domain gruptan devralınır. Sonraki sürüme: RD Gateway, yazıcı, çoklu monitör, yerel disk bağlama.
11. Yedek: ilk sürümde tüm bağlantılar (şifreler dahil) şifreli tek dosyaya dışa aktarılır ve içe alınır; dışa aktarırken bir parola girilir, içe alırken o parola sorulur (parola akışı Claude önerisi). Bulut sürümü gelince bunun yerini bulut alır.
12. Mac dağıtımı: Avva Mobile'ın Developer ID Application sertifikası bu Mac'te kurulu, App Store Connect API anahtarı (`~/.appstoreconnect/private_keys`) ve notarytool hazır; imzalama ve notarize lokalde yapılır. Kimlik bilgileri (Issuer ID vb.) repoya yazılmaz, repo herkese açık.
13. Windows dağıtımı: ilk sürüm imzasız çıkar. Kod imzalama sertifikası sonra alınacak (aday: Certum bulut imza, ~108 $/yıl, teyit edilmedi; Azure Artifact Signing Türkiye'ye muhtemelen kapalı).
14. Lisans ve ürün: bu ilk sürüm herkese açık repoda, MIT lisanslı "Community Edition" olarak tamamlanıp verilir. Bulut sürümü sonra ayrı ve ücretli satılır. (Repo bir ara private yapıldı, aynı gün geri public yapıldı; LICENSE yerinde.)
15. Güncelleme: uygulama açılışta bu reponun GitHub sürümlerine bakar; yeni sürüm varsa "Güncelle" gösterir, basınca indirip kurar (indirip kurma kısmı Claude önerisi).

## Sonraki sürüm (ilk sürümde yok)

1. Şifrelerin bulutta saklanması ve ekip paylaşımı. Önce iç ekip, sonra dışarıya SaaS.
2. Yetki: yönetici bağlantıyı ekler, paylaştığı kişiye "düzenleyebilir" ya da "yalnız bağlanır" yetkisi verir.
3. "Yalnız bağlanır" kişi şifreyi hiç görmez: şifre cihaza şifreli iner, yalnız bağlanırken açılıp bağlantıya eklenir (gateway yok).
4. Sunucumuz şifreleri okuyamaz (uçtan uca şifreleme); müşterinin yöneticisi istediğinde hepsini açık hâliyle dışarı alabilir.
5. Ana parola unutulursa kurtarma yolu (öneri: hesap açılışında indirilen kurtarma anahtarı) henüz onaylanmadı.

## Açık konular

Yok. Sıradaki adım: tasarım dokümanı.
