# Windows x64 için bu Mac'te mingw-w64 ile çapraz derleme (vcpkg community x64-mingw-dynamic-release temelli).
# Homebrew'in mingw-w64'ünde C11 threads.h yok; FreeRDP bu bayrakla __thread kullanır.
set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE dynamic)
set(VCPKG_ENV_PASSTHROUGH_UNTRACKED PATH)

set(VCPKG_CMAKE_SYSTEM_NAME MinGW)
set(VCPKG_POLICY_DLLS_WITHOUT_LIBS enabled)

set(VCPKG_BUILD_TYPE release)
set(VCPKG_C_FLAGS "-D__STDC_NO_THREADS__")
set(VCPKG_CXX_FLAGS "-D__STDC_NO_THREADS__")
