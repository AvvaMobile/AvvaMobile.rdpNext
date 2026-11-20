#!/bin/sh
# Mac (Apple Silicon) paketi: rdpNext.app, Developer ID imzası, notarize, zip.
# Kullanım: tools/paket/paketle-mac.sh 0.1.0   Çıktı: dist/rdpNext-<sürüm>-mac-arm64.zip
# FreeRDP vcpkg'den (native/triplets/arm64-osx-rdpnext) gelir; Homebrew'e bağımlılık yoktur.
set -e
VERSION="$1"; [ -n "$VERSION" ] || { echo "sürüm gerekli"; exit 1; }
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOTNET=/usr/local/share/dotnet/dotnet
V="${VCPKG_ROOT:-$HOME/vcpkg}/installed/arm64-osx-rdpnext"
IDENTITY="${MAC_SIGN_IDENTITY:-Developer ID Application: AVVA MOBILE KURUMSAL COZUMLER YAZILIM VE DANISMANLIK TICARET LIMITED SIRKETI (GG34Q2R372)}"
KEY_ID="${ASC_KEY_ID:-YT5K6RVKGB}"
ISSUER="${ASC_ISSUER_ID:-69a6de72-168d-47e3-e053-5b8c7c11a4d1}"
KEY="$HOME/.appstoreconnect/private_keys/AuthKey_$KEY_ID.p8"
WORK="$ROOT/dist/mac"
APP="$WORK/rdpNext.app"
ZIP="$ROOT/dist/rdpNext-$VERSION-mac-arm64.zip"

rm -rf "$WORK" "$ZIP"; mkdir -p "$WORK"

# .NET uygulaması
"$DOTNET" publish "$ROOT/src/RdpNext.App/RdpNext.App.csproj" -c Release -r osx-arm64 --self-contained true \
  -p:Version="$VERSION" -o "$WORK/publish"

# Bundle iskeleti
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$WORK/publish/." "$APP/Contents/MacOS/"
rm -f "$APP/Contents/MacOS/"*.pdb
sed "s/__VERSION__/$VERSION/g" "$ROOT/tools/paket/Info.plist" > "$APP/Contents/Info.plist"

# Simge
ICONSET="$WORK/rdpNext.iconset"; mkdir -p "$ICONSET"
PNG="$ROOT/src/RdpNext.App/Assets/rdpNext.png"
for s in 16 32 128 256; do
  sips -z $s $s "$PNG" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  sips -z $((s*2)) $((s*2)) "$PNG" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
cp "$PNG" "$ICONSET/icon_512x512.png"
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/rdpNext.icns"

# C katmanı: vcpkg FreeRDP'ye karşı, macOS 13 hedefli; FreeRDP dylib'leri yanında aranır.
clang -shared -fPIC -O2 -fvisibility=hidden -Wno-deprecated-declarations -mmacosx-version-min=13.0 \
  -I"$V/include/freerdp3" -I"$V/include/winpr3" -I"$V/include" -L"$V/lib" \
  -lfreerdp3 -lfreerdp-client3 -lwinpr3 \
  -install_name @rpath/librdpnext_shim.dylib -Wl,-rpath,@loader_path \
  -o "$APP/Contents/MacOS/librdpnext_shim.dylib" "$ROOT/native/rdpnext_shim/rdpnext_shim.c"

# vcpkg dylib'leri: yalnız gerçek dosyalar (install name'ler bu adları gösterir)
find "$V/lib" -maxdepth 1 -name "*.dylib" -type f -exec cp {} "$APP/Contents/MacOS/" \;
for f in "$APP"/Contents/MacOS/*.dylib; do
  install_name_tool -add_rpath @loader_path "$f" 2>/dev/null || true
done

# Homebrew'e bağlı bir şey kalmasın
if otool -L "$APP"/Contents/MacOS/*.dylib | grep -q /opt/homebrew; then
  echo "HATA: Homebrew bağımlılığı kaldı"; otool -L "$APP"/Contents/MacOS/*.dylib | grep /opt/homebrew; exit 1
fi

# İmza: önce içteki dosyalar; ana çalıştırılabilir dosya en son, bundle ile birlikte imzalanır.
find "$APP/Contents/MacOS" -type f ! -path "$APP/Contents/MacOS/rdpNext" | while read -r f; do
  codesign --force --timestamp --options runtime --entitlements "$ROOT/tools/paket/rdpNext.entitlements" -s "$IDENTITY" "$f"
done
codesign --force --timestamp --options runtime --entitlements "$ROOT/tools/paket/rdpNext.entitlements" -s "$IDENTITY" "$APP"
codesign --verify --deep --strict "$APP"

# Notarize ve staple
ditto -c -k --keepParent "$APP" "$WORK/notarize.zip"
xcrun notarytool submit "$WORK/notarize.zip" --key "$KEY" --key-id "$KEY_ID" --issuer "$ISSUER" --wait
xcrun stapler staple "$APP"
spctl --assess --type execute -v "$APP"

ditto -c -k --keepParent "$APP" "$ZIP"
echo "$ZIP"
