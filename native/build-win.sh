#!/bin/sh
# C katmanını Windows x64 için mingw-w64 ile çapraz derler: native/out/win-x64/rdpnext_shim.dll
# FreeRDP DLL'leri vcpkg'nin x64-mingw-rdpnext triplet'iyle (native/triplets) derlenip aynı klasöre konur.
set -e
cd "$(dirname "$0")"
V="${VCPKG_ROOT:-$HOME/vcpkg}/installed/x64-mingw-rdpnext"
mkdir -p out/win-x64
x86_64-w64-mingw32-gcc -shared -O2 -fvisibility=hidden -Wno-deprecated-declarations -D__STDC_NO_THREADS__ \
  -I"$V/include/freerdp3" -I"$V/include/winpr3" -I"$V/include" -L"$V/lib" \
  -o out/win-x64/rdpnext_shim.dll rdpnext_shim/rdpnext_shim.c \
  -lfreerdp-client3 -lfreerdp3 -lwinpr3
