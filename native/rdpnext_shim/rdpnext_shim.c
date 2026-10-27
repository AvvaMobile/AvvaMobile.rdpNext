#include "rdpnext_shim.h"

#include <freerdp/freerdp.h>
#include <freerdp/client.h>
#include <freerdp/gdi/gdi.h>
#include <freerdp/input.h>
#include <freerdp/graphics.h>
#include <freerdp/scancode.h>
#include <freerdp/codec/color.h>
#include <freerdp/event.h>
#include <freerdp/client/disp.h>
#include <freerdp/channels/disp.h>
#include <freerdp/client/cliprdr.h>
#include <freerdp/channels/cliprdr.h>
#include <winpr/string.h>
#include <string.h>
#include <winpr/synch.h>
#include <stdatomic.h>

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
	DispClientContext* disp;
	/* Sunucu çözünürlük kanalının hazır olduğunu (DisplayControlCaps) bildirdi mi; bildirmeden giden istek düşer. */
	atomic_int disp_ready;
	/* Kanal hazır olmadan gelen son istek (genişlik << 32 | yükseklik); hazır olunca gönderilir. 0 ise bekleyen yok.
	   Kanalın iş parçacığı ile oturumun iş parçacığı arasında atomik değiştirilir; isteği yalnız bir taraf gönderir. */
	atomic_llong pending_size;
	CliprdrClientContext* clip;
	char* local_text;
} rnContext;

/* ---- Metin panosu (cliprdr, CF_UNICODETEXT) ---- */

static UINT rn_clip_send_format_list(rnContext* rc)
{
	CLIPRDR_FORMAT format = { CF_UNICODETEXT, NULL };
	CLIPRDR_FORMAT_LIST list = { 0 };
	list.common.msgType = CB_FORMAT_LIST;
	list.numFormats = rc->local_text ? 1 : 0;
	list.formats = rc->local_text ? &format : NULL;
	return rc->clip->ClientFormatList(rc->clip, &list);
}

static UINT rn_clip_monitor_ready(CliprdrClientContext* clip, const CLIPRDR_MONITOR_READY* ready)
{
	rnContext* rc = (rnContext*)clip->custom;
	CLIPRDR_GENERAL_CAPABILITY_SET general = { 0 };
	general.capabilitySetType = CB_CAPSTYPE_GENERAL;
	general.capabilitySetLength = CB_CAPSTYPE_GENERAL_LEN;
	general.version = CB_CAPS_VERSION_2;
	general.generalFlags = CB_USE_LONG_FORMAT_NAMES;
	CLIPRDR_CAPABILITIES caps = { 0 };
	caps.common.msgType = CB_CLIP_CAPS;
	caps.cCapabilitiesSets = 1;
	caps.capabilitySets = (CLIPRDR_CAPABILITY_SET*)&general;
	UINT rv = clip->ClientCapabilities(clip, &caps);
	return rv == CHANNEL_RC_OK ? rn_clip_send_format_list(rc) : rv;
}

static UINT rn_clip_server_capabilities(CliprdrClientContext* clip, const CLIPRDR_CAPABILITIES* caps)
{
	return CHANNEL_RC_OK;
}

/* Sunucuda kopyalandı: metin varsa hemen istenir. */
static UINT rn_clip_server_format_list(CliprdrClientContext* clip, const CLIPRDR_FORMAT_LIST* list)
{
	CLIPRDR_FORMAT_LIST_RESPONSE response = { 0 };
	response.common.msgType = CB_FORMAT_LIST_RESPONSE;
	response.common.msgFlags = CB_RESPONSE_OK;
	UINT rv = clip->ClientFormatListResponse(clip, &response);
	if (rv != CHANNEL_RC_OK)
		return rv;
	for (UINT32 i = 0; i < list->numFormats; i++)
	{
		if (list->formats[i].formatId != CF_UNICODETEXT)
			continue;
		CLIPRDR_FORMAT_DATA_REQUEST request = { 0 };
		request.common.msgType = CB_FORMAT_DATA_REQUEST;
		request.requestedFormatId = CF_UNICODETEXT;
		return clip->ClientFormatDataRequest(clip, &request);
	}
	rnContext* rc = (rnContext*)clip->custom;
	if (rc->callbacks.clipboard_text)
		rc->callbacks.clipboard_text(rc->user, NULL);
	return CHANNEL_RC_OK;
}

static UINT rn_clip_server_format_list_response(CliprdrClientContext* clip, const CLIPRDR_FORMAT_LIST_RESPONSE* response)
{
	return CHANNEL_RC_OK;
}

static UINT rn_clip_server_format_data_response(CliprdrClientContext* clip, const CLIPRDR_FORMAT_DATA_RESPONSE* response)
{
	rnContext* rc = (rnContext*)clip->custom;
	if (response->common.msgFlags != CB_RESPONSE_OK || !response->requestedFormatData || !rc->callbacks.clipboard_text)
		return CHANNEL_RC_OK;
	char* text = ConvertWCharNToUtf8Alloc((const WCHAR*)response->requestedFormatData, response->common.dataLen / sizeof(WCHAR), NULL);
	if (text)
	{
		rc->callbacks.clipboard_text(rc->user, text);
		free(text);
	}
	return CHANNEL_RC_OK;
}

/* Sunucuda yapıştırıldı: Mac'teki metin gönderilir. */
static UINT rn_clip_server_format_data_request(CliprdrClientContext* clip, const CLIPRDR_FORMAT_DATA_REQUEST* request)
{
	rnContext* rc = (rnContext*)clip->custom;
	CLIPRDR_FORMAT_DATA_RESPONSE response = { 0 };
	response.common.msgType = CB_FORMAT_DATA_RESPONSE;
	size_t size = 0;
	WCHAR* data = NULL;
	if (request->requestedFormatId == CF_UNICODETEXT && rc->local_text)
		data = ConvertUtf8ToWCharAlloc(rc->local_text, &size);
	if (data)
	{
		response.common.msgFlags = CB_RESPONSE_OK;
		response.common.dataLen = (UINT32)((size + 1) * sizeof(WCHAR));
		response.requestedFormatData = (const BYTE*)data;
	}
	else
		response.common.msgFlags = CB_RESPONSE_FAIL;
	UINT rv = clip->ClientFormatDataResponse(clip, &response);
	free(data);
	return rv;
}

static void rn_clip_init(rnContext* rc, CliprdrClientContext* clip)
{
	rc->clip = clip;
	clip->custom = rc;
	clip->MonitorReady = rn_clip_monitor_ready;
	clip->ServerCapabilities = rn_clip_server_capabilities;
	clip->ServerFormatList = rn_clip_server_format_list;
	clip->ServerFormatListResponse = rn_clip_server_format_list_response;
	clip->ServerFormatDataResponse = rn_clip_server_format_data_response;
	clip->ServerFormatDataRequest = rn_clip_server_format_data_request;
}

static BOOL rn_send_layout(rnContext* rc, UINT32 width, UINT32 height)
{
	DISPLAY_CONTROL_MONITOR_LAYOUT layout = { 0 };
	layout.Flags = DISPLAY_CONTROL_MONITOR_PRIMARY;
	layout.Width = width;
	layout.Height = height;
	layout.DesktopScaleFactor = 100;
	layout.DeviceScaleFactor = 100;
	return rc->disp->SendMonitorLayout(rc->disp, 1, &layout) == CHANNEL_RC_OK;
}

static void rn_send_pending(rnContext* rc)
{
	long long size = atomic_exchange(&rc->pending_size, 0);
	if (size)
		rn_send_layout(rc, (UINT32)(size >> 32), (UINT32)(size & 0xFFFFFFFF));
}

/* Sunucu çözünürlük kanalını açtı: bekleyen istek varsa şimdi gönderilir. */
static UINT rn_disp_caps(DispClientContext* disp, UINT32 max_monitors, UINT32 factor_a, UINT32 factor_b)
{
	rnContext* rc = (rnContext*)disp->custom;
	atomic_store(&rc->disp_ready, 1);
	rn_send_pending(rc);
	return CHANNEL_RC_OK;
}

/* disp kanalının bağlamı saklanır; diğer kanallar (rdpgfx vb.) FreeRDP'nin ortak işleyicisine gider. */
static void rn_channel_connected(void* context, const ChannelConnectedEventArgs* e)
{
	rnContext* rc = (rnContext*)context;
	if (strcmp(e->name, DISP_DVC_CHANNEL_NAME) == 0)
	{
		rc->disp = (DispClientContext*)e->pInterface;
		rc->disp->custom = rc;
		rc->disp->DisplayControlCaps = rn_disp_caps;
	}
	else if (strcmp(e->name, CLIPRDR_SVC_CHANNEL_NAME) == 0)
		rn_clip_init(rc, (CliprdrClientContext*)e->pInterface);
	else
		freerdp_client_OnChannelConnectedEventHandler(context, e);
}

static void rn_channel_disconnected(void* context, const ChannelDisconnectedEventArgs* e)
{
	rnContext* rc = (rnContext*)context;
	if (strcmp(e->name, DISP_DVC_CHANNEL_NAME) == 0)
	{
		rc->disp = NULL;
		atomic_store(&rc->disp_ready, 0);
	}
	else if (strcmp(e->name, CLIPRDR_SVC_CHANNEL_NAME) == 0)
		rc->clip = NULL;
	else
		freerdp_client_OnChannelDisconnectedEventHandler(context, e);
}

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
	PubSub_SubscribeChannelConnected(instance->context->pubSub, rn_channel_connected);
	PubSub_SubscribeChannelDisconnected(instance->context->pubSub, rn_channel_disconnected);
	return freerdp_client_load_channels(instance);
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
	/* 2: yalnız bu oturum için kabul. FreeRDP'nin kendi sertifika deposuna yazılmaz; güvenilenler rdpNext'te tutulur. */
	return rc->callbacks.verify_certificate(rc->user, subject, issuer, fingerprint, changed) ? 2 : 0;
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
	freerdp_settings_set_bool(s, FreeRDP_SupportDisplayControl, TRUE);
	freerdp_settings_set_bool(s, FreeRDP_DynamicResolutionUpdate, TRUE);
	freerdp_settings_set_bool(s, FreeRDP_SupportGraphicsPipeline, TRUE);
	freerdp_settings_set_bool(s, FreeRDP_RedirectClipboard, TRUE);
	/* Sunucunun sesi bu bilgisayarda çalar (rdpsnd; Mac'te mac, Windows'ta winmm arka ucu). */
	freerdp_settings_set_bool(s, FreeRDP_AudioPlayback, TRUE);
	/* Ağ kopunca FreeRDP önce kendisi birkaç kez dener; olmazsa oturum biter ve Reconnect çıkar. */
	freerdp_settings_set_bool(s, FreeRDP_AutoReconnectionEnabled, TRUE);
	freerdp_settings_set_uint32(s, FreeRDP_AutoReconnectMaxRetries, 5);
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
		return client_auto_reconnect_ex(ctx->instance, NULL) ? 1 : 0;
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

int rn_resize(rdpContext* ctx, int width, int height)
{
	rnContext* rc = (rnContext*)ctx;
	/* En son istenen boyut saklanır; kanal hazırsa hemen, değilse hazır olunca gönderilir. */
	atomic_store(&rc->pending_size, ((long long)width << 32) | (UINT32)height);
	if (!rc->disp || !atomic_load(&rc->disp_ready))
		return 0;
	rn_send_pending(rc);
	return 1;
}

void rn_set_clipboard_text(rdpContext* ctx, const char* text)
{
	rnContext* rc = (rnContext*)ctx;
	free(rc->local_text);
	rc->local_text = text && *text ? _strdup(text) : NULL;
	if (rc->clip)
		rn_clip_send_format_list(rc);
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
	free(((rnContext*)ctx)->local_text);
	freerdp_disconnect(ctx->instance);
	freerdp_client_context_free(ctx);
}
