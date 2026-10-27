/* rdpNext ile FreeRDP 3 arasındaki ince katman. .NET yalnız buradaki düz fonksiyonları çağırır,
   FreeRDP yapılarına dokunmaz. Tüm metinler UTF-8. */
#ifndef RDPNEXT_SHIM_H
#define RDPNEXT_SHIM_H

#include <stdint.h>

#ifdef _WIN32
#define RN_API __declspec(dllexport)
#else
#define RN_API __attribute__((visibility("default")))
#endif

typedef struct rdp_context rdpContext;

/* Alan sırası .NET tarafındaki RnCallbacks ile aynı; yeni alan yalnız sona eklenir. */
typedef struct
{
	/* Değişen dikdörtgen; buf yalnız çağrı süresince geçerli, BGRA32. */
	void (*frame)(void* user, const uint8_t* buf, int width, int height, int stride, int x, int y, int w, int h);
	/* 1 kabul, 0 ret. NULL ise bu oturum için kabul edilir. */
	int (*verify_certificate)(void* user, const char* subject, const char* issuer, const char* fingerprint, int changed);
	/* bgra NULL ise imleç gizli. */
	void (*pointer)(void* user, const uint8_t* bgra, int width, int height, int hot_x, int hot_y);
	void (*clipboard_text)(void* user, const char* text);
} rn_callbacks;

typedef struct
{
	const char* host;
	int port;
	const char* username;
	const char* domain;
	const char* password;
	int width;
	int height;
} rn_options;

RN_API rdpContext* rn_create(const rn_callbacks* callbacks, void* user);
/* 0: bağlandı, aksi hâlde freerdp_get_last_error kodu. */
RN_API uint32_t rn_connect(rdpContext* ctx, const rn_options* options);
/* 1: devam, 0: bağlantı bitti. */
RN_API int rn_pump(rdpContext* ctx, int timeout_ms);
/* Sunucunun bildirdiği kopma sebebi (ERRINFO_*). */
RN_API uint32_t rn_end_reason(rdpContext* ctx);
RN_API uint32_t rn_last_error(rdpContext* ctx);
/* Başka iş parçacığından çağrılabilir: süren bağlanmayı ya da bekleyen pump'ı keser. */
RN_API void rn_abort(rdpContext* ctx);
RN_API void rn_mouse(rdpContext* ctx, uint16_t flags, uint16_t x, uint16_t y);
RN_API void rn_key(rdpContext* ctx, uint16_t code, int extended, int down);
RN_API void rn_unicode(rdpContext* ctx, uint16_t code, int down);
RN_API void rn_free(rdpContext* ctx);

#endif
