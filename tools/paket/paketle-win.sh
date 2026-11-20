#!/bin/sh
# Windows x64 paketi (imzasız): dist/rdpNext-<sürüm>-win-x64.zip
# Kullanım: tools/paket/paketle-win.sh 0.1.0   Önce native/build-win.sh ile DLL'ler hazır olmalı.
set -e
VERSION="$1"; [ -n "$VERSION" ] || { echo "sürüm gerekli"; exit 1; }
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="$ROOT/dist/win"
OUT="$WORK/rdpNext"
ZIP="$ROOT/dist/rdpNext-$VERSION-win-x64.zip"
rm -rf "$WORK" "$ZIP"; mkdir -p "$WORK"
/usr/local/share/dotnet/dotnet publish "$ROOT/src/RdpNext.App/RdpNext.App.csproj" -c Release -r win-x64 --self-contained true \
  -p:Version="$VERSION" -o "$OUT"
# Mac'te derlenince Mac C katmanı da kopyalanıyor; Windows paketinde yeri yok.
rm -f "$OUT"/*.dylib "$OUT"/*.pdb
[ -f "$OUT/rdpnext_shim.dll" ] || { echo "HATA: rdpnext_shim.dll yok"; exit 1; }
(cd "$WORK" && zip -qr "$ZIP" rdpNext)
echo "$ZIP"
