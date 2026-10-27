#!/bin/sh
# C katmanını Homebrew'deki FreeRDP 3'e karşı derler: native/out/osx-arm64/librdpnext_shim.dylib
set -e
cd "$(dirname "$0")"
BREW="$(brew --prefix)"
mkdir -p out/osx-arm64
clang -shared -fPIC -O2 -fvisibility=hidden -Wno-deprecated-declarations \
  -I"$BREW/include/freerdp3" -I"$BREW/include/winpr3" \
  -L"$BREW/lib" -lfreerdp3 -lfreerdp-client3 -lwinpr3 \
  -o out/osx-arm64/librdpnext_shim.dylib rdpnext_shim/rdpnext_shim.c
