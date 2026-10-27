#include "rdpnext_shim.h"

#include <freerdp/freerdp.h>
#include <freerdp/client.h>
#include <freerdp/gdi/gdi.h>
#include <freerdp/input.h>
#include <freerdp/graphics.h>
#include <freerdp/scancode.h>
#include <freerdp/codec/color.h>
#include <winpr/synch.h>

typedef struct
{
	rdpPointer pointer;
	uint8_t* bgra;
} rnPointer;

typedef struct
{
	rdpClientContext common;
	rn_callbacks callbacks;
	void* user;
} rnContext;

static void rn_send_frame(rdpContext* context, int x, int y, int w, int h)
{
	rnContext* rc = (rnContext*)context;
	rdpGdi* gdi = context->gdi;
	if (rc->callbacks.frame)
		rc->callbacks.frame(rc->user, gdi->primary_buffer, (int)gdi->width, (int)gdi->height, (int)gdi->stride, x, y, w, h);
}

static BOOL rn_begin_paint(rdpContext* context)
{
	context->gdi->primary->hdc->hwnd->invalid->null = TRUE;
	return TRUE;
}

static BOOL rn_end_paint(rdpContext* context)
{
	HGDI_RGN invalid = context->gdi->primary->hdc->hwnd->invalid;
	if (!invalid->null)
		rn_send_frame(context, invalid->x, invalid->y, invalid->w, invalid->h);
	return TRUE;
}

static BOOL rn_desktop_resize(rdpContext* context)
{
	rdpSettings* settings = context->settings;
	if (!gdi_resize(context->gdi, freerdp_settings_get_uint32(settings, FreeRDP_DesktopWidth),
	                freerdp_settings_get_uint32(settings, FreeRDP_DesktopHeight)))
		return FALSE;
	rn_send_frame(context, 0, 0, (int)context->gdi->width, (int)context->gdi->height);
	return TRUE;
}

/* Sunucunun imleç şekli BGRA'ya çevrilip .NET'e verilir; gizli imleç bgra NULL, genişlik 0. */
static BOOL rn_pointer_new(rdpContext* context, rdpPointer* pointer)
{
	rnPointer* p = (rnPointer*)pointer;
	size_t size = (size_t)pointer->width * pointer->height * 4;
	p->bgra = calloc(1, size ? size : 1);
	if (!p->bgra)
		return FALSE;
	return freerdp_image_copy_from_pointer_data(p->bgra, PIXEL_FORMAT_BGRA32, pointer->width * 4, 0, 0, pointer->width,
	                                            pointer->height, pointer->xorMaskData, pointer->lengthXorMask, pointer->andMaskData,
	                                            pointer->lengthAndMask, pointer->xorBpp, &context->gdi->palette);
}

static void rn_pointer_free(rdpContext* context, rdpPointer* pointer)
{
	rnPointer* p = (rnPointer*)pointer;
	free(p->bgra);
	p->bgra = NULL;
}

static BOOL rn_pointer_set(rdpContext* context, rdpPointer* pointer)
{
	rnContext* rc = (rnContext*)context;
	rnPointer* p = (rnPointer*)pointer;
	if (rc->callbacks.pointer)
		rc->callbacks.pointer(rc->user, p->bgra, (int)pointer->width, (int)pointer->height, (int)pointer->xPos, (int)pointer->yPos);
	return TRUE;
}

static BOOL rn_pointer_set_null(rdpContext* context)
{
	rnContext* rc = (rnContext*)context;
	if (rc->callbacks.pointer)
		rc->callbacks.pointer(rc->user, NULL, 0, 0, 0, 0);
	return TRUE;
}

/* Varsayılan ok: bgra NULL, genişlik -1. */
static BOOL rn_pointer_set_default(rdpContext* context)
{
	rnContext* rc = (rnContext*)context;
	if (rc->callbacks.pointer)
		rc->callbacks.pointer(rc->user, NULL, -1, 0, 0, 0);
	return TRUE;
}

static BOOL rn_pointer_set_position(rdpContext* context, UINT32 x, UINT32 y)
{
	return TRUE;
}

static BOOL rn_pre_connect(freerdp* instance)
{
#ifdef _WIN32
	freerdp_settings_set_uint32(instance->context->settings, FreeRDP_OsMajorType, OSMAJORTYPE_WINDOWS);
#else
	freerdp_settings_set_uint32(instance->context->settings, FreeRDP_OsMajorType, OSMAJORTYPE_MACINTOSH);
#endif
	return TRUE;
}

static BOOL rn_post_connect(freerdp* instance)
{
	if (!gdi_init(instance, PIXEL_FORMAT_BGRA32))
		return FALSE;
	rdpUpdate* update = instance->context->update;
	update->BeginPaint = rn_begin_paint;
	update->EndPaint = rn_end_paint;
	update->DesktopResize = rn_desktop_resize;

	rdpPointer pointer = { 0 };
	pointer.size = sizeof(rnPointer);
	pointer.New = rn_pointer_new;
	pointer.Free = rn_pointer_free;
	pointer.Set = rn_pointer_set;
	pointer.SetNull = rn_pointer_set_null;
	pointer.SetDefault = rn_pointer_set_default;
	pointer.SetPosition = rn_pointer_set_position;
	graphics_register_pointer(instance->context->graphics, &pointer);
	return TRUE;
}

static void rn_post_disconnect(freerdp* instance)
{
	gdi_free(instance);
}

static DWORD rn_ask_certificate(freerdp* instance, const char* subject, const char* issuer, const char* fingerprint, int changed)
{
	rnContext* rc = (rnContext*)instance->context;
	if (!rc->callbacks.verify_certificate)
		return 2; /* bu oturum için kabul */
	return rc->callbacks.verify_certificate(rc->user, subject, issuer, fingerprint, changed) ? 1 : 0;
}

static DWORD rn_verify_certificate(freerdp* instance, const char* host, UINT16 port, const char* common_name, const char* subject,
                                   const char* issuer, const char* fingerprint, DWORD flags)
{
	return rn_ask_certificate(instance, subject, issuer, fingerprint, 0);
}

static DWORD rn_verify_changed_certificate(freerdp* instance, const char* host, UINT16 port, const char* common_name,
                                           const char* subject, const char* issuer, const char* fingerprint,
                                           const char* old_subject, const char* old_issuer, const char* old_fingerprint, DWORD flags)
{
	return rn_ask_certificate(instance, subject, issuer, fingerprint, 1);
}

static BOOL rn_client_new(freerdp* instance, rdpContext* context)
{
	instance->PreConnect = rn_pre_connect;
	instance->PostConnect = rn_post_connect;
	instance->PostDisconnect = rn_post_disconnect;
	instance->VerifyCertificateEx = rn_verify_certificate;
	instance->VerifyChangedCertificateEx = rn_verify_changed_certificate;
	return TRUE;
}

rdpContext* rn_create(const rn_callbacks* callbacks, void* user)
{
	RDP_CLIENT_ENTRY_POINTS entry = { 0 };
	entry.Version = RDP_CLIENT_INTERFACE_VERSION;
	entry.Size = sizeof(entry);
	entry.ContextSize = sizeof(rnContext);
	entry.ClientNew = rn_client_new;
	rdpContext* context = freerdp_client_context_new(&entry);
	if (!context)
		return NULL;
	rnContext* rc = (rnContext*)context;
	if (callbacks)
		rc->callbacks = *callbacks;
	rc->user = user;
	return context;
}

uint32_t rn_connect(rdpContext* ctx, const rn_options* o)
{
	rdpSettings* s = ctx->settings;
	freerdp_settings_set_string(s, FreeRDP_ServerHostname, o->host);
	freerdp_settings_set_uint32(s, FreeRDP_ServerPort, (UINT32)o->port);
	freerdp_settings_set_string(s, FreeRDP_Username, o->username);
	if (o->domain && *o->domain)
		freerdp_settings_set_string(s, FreeRDP_Domain, o->domain);
	freerdp_settings_set_string(s, FreeRDP_Password, o->password);
	freerdp_settings_set_uint32(s, FreeRDP_DesktopWidth, (UINT32)o->width);
	freerdp_settings_set_uint32(s, FreeRDP_DesktopHeight, (UINT32)o->height);
	freerdp_settings_set_uint32(s, FreeRDP_ColorDepth, 32);
	freerdp_settings_set_bool(s, FreeRDP_SoftwareGdi, TRUE);
	if (freerdp_connect(ctx->instance))
		return 0;
	uint32_t error = freerdp_get_last_error(ctx);
	return error ? error : 1;
}

int rn_pump(rdpContext* ctx, int timeout_ms)
{
	HANDLE handles[MAXIMUM_WAIT_OBJECTS] = { 0 };
	DWORD count = freerdp_get_event_handles(ctx, handles, ARRAYSIZE(handles));
	if (count == 0)
		return 0;
	WaitForMultipleObjects(count, handles, FALSE, (DWORD)timeout_ms);
	if (!freerdp_check_event_handles(ctx))
		return 0;
	return freerdp_shall_disconnect_context(ctx) ? 0 : 1;
}

uint32_t rn_end_reason(rdpContext* ctx)
{
	return freerdp_error_info(ctx->instance);
}

uint32_t rn_last_error(rdpContext* ctx)
{
	return freerdp_get_last_error(ctx);
}

void rn_abort(rdpContext* ctx)
{
	freerdp_abort_connect_context(ctx);
}

void rn_mouse(rdpContext* ctx, uint16_t flags, uint16_t x, uint16_t y)
{
	freerdp_input_send_mouse_event(ctx->input, flags, x, y);
}

void rn_key(rdpContext* ctx, uint16_t code, int extended, int down)
{
	freerdp_input_send_keyboard_event_ex(ctx->input, down ? TRUE : FALSE, FALSE, MAKE_RDP_SCANCODE(code, extended));
}

void rn_unicode(rdpContext* ctx, uint16_t code, int down)
{
	freerdp_input_send_unicode_keyboard_event(ctx->input, down ? 0 : KBD_FLAGS_RELEASE, code);
}

void rn_free(rdpContext* ctx)
{
	freerdp_disconnect(ctx->instance);
	freerdp_client_context_free(ctx);
}
