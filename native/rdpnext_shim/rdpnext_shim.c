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
#include <winpr/clipboard.h>
#include <freerdp/client/client_cliprdr_file.h>
#include <freerdp/utils/cliprdr_utils.h>
#include <string.h>
#include <stdio.h>
#include <sys/stat.h>
#include <winpr/interlocked.h>
#include <winpr/shell.h>
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
	/* Mac'te kopyalanan dosyalar, "file:///..." satırları (text/uri-list); NULL ise yok. */
	char* local_files;
	wClipboard* system;
	CliprdrFileContext* file;
	/* Sunucudan dosya indirme: en son kopyalanan dosya listesi sırayla, parça parça indirilir. */
	char* clipboard_dir;
	UINT32 requested_format;
	FILEDESCRIPTORW* dl_files;
	UINT32 dl_count;
	UINT32 dl_index;
	UINT64 dl_size;
	UINT64 dl_offset;
	FILE* dl_out;
	char* dl_root;
} rnContext;

/* ---- Pano (cliprdr): metin (CF_UNICODETEXT) ve Mac'ten sunucuya dosya (FileGroupDescriptorW) ---- */

static const char rn_format_files[] = "FileGroupDescriptorW";
static const char rn_mime_uri_list[] = "text/uri-list";

/* FreeRDP'nin dosya bağlamı cliprdr->custom'ı kendine alıyor; bizim bağlam onun içinden okunur. */
static rnContext* rn_clip_owner(CliprdrClientContext* clip)
{
	return (rnContext*)cliprdr_file_context_get_context((CliprdrFileContext*)clip->custom);
}

static UINT rn_clip_send_format_list(rnContext* rc)
{
	CLIPRDR_FORMAT formats[2] = { 0 };
	UINT32 count = 0;
	if (rc->local_files)
	{
		formats[count].formatId = ClipboardGetFormatId(rc->system, rn_format_files);
		formats[count].formatName = (char*)rn_format_files;
		count++;
	}
	if (rc->local_text)
	{
		formats[count].formatId = CF_UNICODETEXT;
		count++;
	}
	CLIPRDR_FORMAT_LIST list = { 0 };
	list.common.msgType = CB_FORMAT_LIST;
	list.numFormats = count;
	list.formats = count ? formats : NULL;
	UINT rv = rc->clip->ClientFormatList(rc->clip, &list);
	if (rv == CHANNEL_RC_OK)
		rv = cliprdr_file_context_notify_new_client_format_list(rc->file);
	return rv;
}

static UINT rn_clip_monitor_ready(CliprdrClientContext* clip, const CLIPRDR_MONITOR_READY* ready)
{
	rnContext* rc = rn_clip_owner(clip);
	CLIPRDR_GENERAL_CAPABILITY_SET general = { 0 };
	general.capabilitySetType = CB_CAPSTYPE_GENERAL;
	general.capabilitySetLength = CB_CAPSTYPE_GENERAL_LEN;
	general.version = CB_CAPS_VERSION_2;
	general.generalFlags = CB_USE_LONG_FORMAT_NAMES | cliprdr_file_context_current_flags(rc->file);
	CLIPRDR_CAPABILITIES caps = { 0 };
	caps.common.msgType = CB_CLIP_CAPS;
	caps.cCapabilitiesSets = 1;
	caps.capabilitySets = (CLIPRDR_CAPABILITY_SET*)&general;
	UINT rv = clip->ClientCapabilities(clip, &caps);
	return rv == CHANNEL_RC_OK ? rn_clip_send_format_list(rc) : rv;
}

/* Sunucunun dosya aktarım yetenekleri FreeRDP'nin dosya bağlamına verilir. */
static UINT rn_clip_server_capabilities(CliprdrClientContext* clip, const CLIPRDR_CAPABILITIES* caps)
{
	rnContext* rc = rn_clip_owner(clip);
	if (!cliprdr_file_context_remote_set_flags(rc->file, 0))
		return ERROR_INTERNAL_ERROR;
	const BYTE* ptr = (const BYTE*)caps->capabilitySets;
	for (UINT32 i = 0; i < caps->cCapabilitiesSets; i++)
	{
		const CLIPRDR_CAPABILITY_SET* set = (const CLIPRDR_CAPABILITY_SET*)ptr;
		if (set->capabilitySetType == CB_CAPSTYPE_GENERAL
		    && !cliprdr_file_context_remote_set_flags(rc->file, ((const CLIPRDR_GENERAL_CAPABILITY_SET*)set)->generalFlags))
			return ERROR_INTERNAL_ERROR;
		ptr += set->capabilitySetLength;
	}
	return CHANNEL_RC_OK;
}

/* ---- Sunucudan Mac'e dosya: FileGroupDescriptorW listesi alınır, içerikler parça parça indirilir ---- */

#define RN_CHUNK (1024 * 1024)

static void rn_dl_reset(rnContext* rc)
{
	if (rc->dl_out)
		fclose(rc->dl_out);
	rc->dl_out = NULL;
	free(rc->dl_files);
	rc->dl_files = NULL;
	rc->dl_count = rc->dl_index = 0;
	rc->dl_size = rc->dl_offset = 0;
	free(rc->dl_root);
	rc->dl_root = NULL;
}

/* Sunucudaki göreli yol ("klasör\\dosya") indirme klasöründe güvenli bir yola çevrilir; ".." kabul edilmez. */
static char* rn_dl_path(rnContext* rc, const FILEDESCRIPTORW* d)
{
	char* name = ConvertWCharNToUtf8Alloc(d->cFileName, ARRAYSIZE(d->cFileName), NULL);
	if (!name || !*name || strstr(name, ".."))
	{
		free(name);
		return NULL;
	}
	for (char* p = name; *p; p++)
		if (*p == '\\')
			*p = '/';
	size_t len = strlen(rc->dl_root) + 1 + strlen(name) + 1;
	char* path = malloc(len);
	if (path)
		snprintf(path, len, "%s/%s", rc->dl_root, name);
	free(name);
	return path;
}

static void rn_mkdirs(const char* path)
{
	char* copy = _strdup(path);
	if (!copy)
		return;
	for (char* p = copy + 1; *p; p++)
	{
		if (*p != '/')
			continue;
		*p = '\0';
		mkdir(copy, 0700);
		*p = '/';
	}
	mkdir(copy, 0700);
	free(copy);
}

static UINT rn_dl_request(rnContext* rc, UINT32 flags)
{
	CLIPRDR_FILE_CONTENTS_REQUEST request = { 0 };
	request.common.msgType = CB_FILECONTENTS_REQUEST;
	request.streamId = rc->dl_index;
	request.listIndex = rc->dl_index;
	request.dwFlags = flags;
	if (flags == FILECONTENTS_SIZE)
		request.cbRequested = 8;
	else
	{
		request.nPositionLow = (UINT32)(rc->dl_offset & 0xFFFFFFFF);
		request.nPositionHigh = (UINT32)(rc->dl_offset >> 32);
		UINT64 left = rc->dl_size - rc->dl_offset;
		request.cbRequested = left < RN_CHUNK ? (UINT32)left : RN_CHUNK;
	}
	return rc->clip->ClientFileContentsRequest(rc->clip, &request);
}

/* İndirme bitti: en üstteki öğelerin (göreli yolunda '\\' olmayanlar) tam yolları bildirilir. */
static void rn_dl_finish(rnContext* rc)
{
	size_t cap = 1, len = 0;
	char* list = calloc(1, cap);
	for (UINT32 i = 0; list && i < rc->dl_count; i++)
	{
		const FILEDESCRIPTORW* d = &rc->dl_files[i];
		BOOL top = TRUE;
		for (size_t k = 0; k < ARRAYSIZE(d->cFileName) && d->cFileName[k]; k++)
			if (d->cFileName[k] == '\\')
				top = FALSE;
		char* path = top ? rn_dl_path(rc, d) : NULL;
		if (!path)
			continue;
		size_t add = strlen(path) + 1;
		char* grown = realloc(list, cap + add);
		if (grown)
		{
			list = grown;
			cap += add;
			memcpy(list + len, path, add - 1);
			len += add - 1;
			list[len++] = '\n';
			list[len] = '\0';
		}
		free(path);
	}
	if (list && len && rc->callbacks.clipboard_files)
		rc->callbacks.clipboard_files(rc->user, list);
	free(list);
	rn_dl_reset(rc);
}

/* Sıradaki öğeye geçer: klasörse oluşturulur, dosyaysa açılıp ilk parça (ya da boyut) istenir. */
static UINT rn_dl_next(rnContext* rc)
{
	for (; rc->dl_index < rc->dl_count; rc->dl_index++)
	{
		const FILEDESCRIPTORW* d = &rc->dl_files[rc->dl_index];
		char* path = rn_dl_path(rc, d);
		if (!path)
			continue;
		if (d->dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
		{
			rn_mkdirs(path);
			free(path);
			continue;
		}
		char* slash = strrchr(path, '/');
		if (slash)
		{
			*slash = '\0';
			rn_mkdirs(path);
			*slash = '/';
		}
		rc->dl_out = fopen(path, "wb");
		free(path);
		if (!rc->dl_out)
			continue;
		rc->dl_offset = 0;
		if (d->dwFlags & FD_FILESIZE)
		{
			rc->dl_size = ((UINT64)d->nFileSizeHigh << 32) | d->nFileSizeLow;
			if (rc->dl_size == 0)
			{
				fclose(rc->dl_out);
				rc->dl_out = NULL;
				continue;
			}
			return rn_dl_request(rc, FILECONTENTS_RANGE);
		}
		return rn_dl_request(rc, FILECONTENTS_SIZE);
	}
	rn_dl_finish(rc);
	return CHANNEL_RC_OK;
}

static void rn_dl_start(rnContext* rc, const BYTE* data, UINT32 length)
{
	rn_dl_reset(rc);
	if (!rc->clipboard_dir || cliprdr_parse_file_list(data, length, &rc->dl_files, &rc->dl_count) != 0 || rc->dl_count == 0)
	{
		rn_dl_reset(rc);
		return;
	}
	/* Her kopya kendi alt klasörüne iner; aynı adlı dosyalar birbirini ezmez. */
	static volatile LONG counter = 0;
	size_t len = strlen(rc->clipboard_dir) + 32;
	rc->dl_root = malloc(len);
	if (!rc->dl_root)
	{
		rn_dl_reset(rc);
		return;
	}
	snprintf(rc->dl_root, len, "%s/%ld", rc->clipboard_dir, (long)InterlockedIncrement(&counter));
	rn_mkdirs(rc->dl_root);
	rn_dl_next(rc);
}

static UINT rn_clip_server_file_contents_response(CliprdrClientContext* clip, const CLIPRDR_FILE_CONTENTS_RESPONSE* response)
{
	rnContext* rc = rn_clip_owner(clip);
	if (!rc->dl_files || response->streamId != rc->dl_index || !rc->dl_out)
		return CHANNEL_RC_OK;
	if (response->common.msgFlags != CB_RESPONSE_OK)
	{
		rn_dl_reset(rc);
		return CHANNEL_RC_OK;
	}
	if (rc->dl_size == 0 && rc->dl_offset == 0 && response->cbRequested == 8 && !(rc->dl_files[rc->dl_index].dwFlags & FD_FILESIZE))
	{
		/* FILECONTENTS_SIZE cevabı: 64 bit boyut. */
		UINT64 size = 0;
		memcpy(&size, response->requestedData, 8);
		rc->dl_size = size;
		if (size == 0)
		{
			fclose(rc->dl_out);
			rc->dl_out = NULL;
			rc->dl_index++;
			return rn_dl_next(rc);
		}
		return rn_dl_request(rc, FILECONTENTS_RANGE);
	}
	if (response->cbRequested && fwrite(response->requestedData, 1, response->cbRequested, rc->dl_out) != response->cbRequested)
	{
		rn_dl_reset(rc);
		return CHANNEL_RC_OK;
	}
	rc->dl_offset += response->cbRequested;
	if (response->cbRequested && rc->dl_offset < rc->dl_size)
		return rn_dl_request(rc, FILECONTENTS_RANGE);
	fclose(rc->dl_out);
	rc->dl_out = NULL;
	rc->dl_index++;
	return rn_dl_next(rc);
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
	rnContext* rc = rn_clip_owner(clip);
	/* Dosya varsa dosya listesi istenir (Explorer dosya kopyalarken yanında metin de verebilir), yoksa metin. */
	UINT32 wanted = 0;
	for (UINT32 i = 0; i < list->numFormats; i++)
	{
		if (list->formats[i].formatName && strcmp(list->formats[i].formatName, rn_format_files) == 0)
			wanted = list->formats[i].formatId;
		else if (!wanted && list->formats[i].formatId == CF_UNICODETEXT)
			wanted = CF_UNICODETEXT;
	}
	if (wanted)
	{
		rc->requested_format = wanted;
		CLIPRDR_FORMAT_DATA_REQUEST request = { 0 };
		request.common.msgType = CB_FORMAT_DATA_REQUEST;
		request.requestedFormatId = wanted;
		return clip->ClientFormatDataRequest(clip, &request);
	}
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
	rnContext* rc = rn_clip_owner(clip);
	if (response->common.msgFlags != CB_RESPONSE_OK || !response->requestedFormatData)
		return CHANNEL_RC_OK;
	if (rc->requested_format != CF_UNICODETEXT)
	{
		/* Dosya listesi geldi: Mac'teki eski metin sunucuya geri duyurulmasın diye önce haber verilir, sonra indirilir. */
		if (rc->callbacks.clipboard_text)
			rc->callbacks.clipboard_text(rc->user, NULL);
		rn_dl_start(rc, response->requestedFormatData, response->common.dataLen);
		return CHANNEL_RC_OK;
	}
	if (!rc->callbacks.clipboard_text)
		return CHANNEL_RC_OK;
	char* text = ConvertWCharNToUtf8Alloc((const WCHAR*)response->requestedFormatData, response->common.dataLen / sizeof(WCHAR), NULL);
	if (text)
	{
		rc->callbacks.clipboard_text(rc->user, text);
		free(text);
	}
	return CHANNEL_RC_OK;
}

/* Mac'teki dosyaların listesi sunucunun anladığı paketlenmiş dosya listesine (CLIPRDR_FILELIST) çevrilir.
   Dosya içerikleri sonra sunucu isteyince FreeRDP'nin dosya bağlamı tarafından okunup gönderilir. */
static BYTE* rn_clip_file_list(rnContext* rc, UINT32* length)
{
	*length = 0;
	size_t uri_len = strlen(rc->local_files);
	if (!cliprdr_file_context_update_client_data(rc->file, rc->local_files, uri_len))
		return NULL;
	UINT32 uri_id = ClipboardGetFormatId(rc->system, rn_mime_uri_list);
	UINT32 files_id = ClipboardGetFormatId(rc->system, rn_format_files);
	BYTE* result = NULL;
	ClipboardLock(rc->system);
	if (ClipboardSetData(rc->system, uri_id, rc->local_files, (UINT32)uri_len))
	{
		UINT32 size = 0;
		FILEDESCRIPTORW* descriptors = (FILEDESCRIPTORW*)ClipboardGetData(rc->system, files_id, &size);
		if (descriptors)
		{
			if (cliprdr_serialize_file_list_ex(cliprdr_file_context_remote_get_flags(rc->file), descriptors,
			                                   size / sizeof(FILEDESCRIPTORW), &result, length) != 0)
			{
				result = NULL;
				*length = 0;
			}
			free(descriptors);
		}
	}
	ClipboardEmpty(rc->system);
	ClipboardUnlock(rc->system);
	return result;
}

/* Sunucuda yapıştırıldı: Mac'teki metin ya da dosya listesi gönderilir. */
static UINT rn_clip_server_format_data_request(CliprdrClientContext* clip, const CLIPRDR_FORMAT_DATA_REQUEST* request)
{
	rnContext* rc = rn_clip_owner(clip);
	CLIPRDR_FORMAT_DATA_RESPONSE response = { 0 };
	response.common.msgType = CB_FORMAT_DATA_RESPONSE;
	BYTE* data = NULL;
	UINT32 length = 0;
	if (request->requestedFormatId == CF_UNICODETEXT && rc->local_text)
	{
		size_t size = 0;
		data = (BYTE*)ConvertUtf8ToWCharAlloc(rc->local_text, &size);
		length = data ? (UINT32)((size + 1) * sizeof(WCHAR)) : 0;
	}
	else if (rc->local_files && request->requestedFormatId == ClipboardGetFormatId(rc->system, rn_format_files))
		data = rn_clip_file_list(rc, &length);
	response.common.msgFlags = data ? CB_RESPONSE_OK : CB_RESPONSE_FAIL;
	response.common.dataLen = length;
	response.requestedFormatData = data;
	UINT rv = clip->ClientFormatDataResponse(clip, &response);
	free(data);
	return rv;
}

static void rn_clip_init(rnContext* rc, CliprdrClientContext* clip)
{
	rc->clip = clip;
	clip->MonitorReady = rn_clip_monitor_ready;
	clip->ServerCapabilities = rn_clip_server_capabilities;
	clip->ServerFormatList = rn_clip_server_format_list;
	clip->ServerFormatListResponse = rn_clip_server_format_list_response;
	clip->ServerFormatDataResponse = rn_clip_server_format_data_response;
	clip->ServerFormatDataRequest = rn_clip_server_format_data_request;
	/* Dosya içeriği istekleri (FileContentsRequest) ve kilitleme FreeRDP'nin dosya bağlamında; custom'ı o alır. */
	cliprdr_file_context_init(rc->file, clip);
	/* FUSE yok (macOS): sunucudan dosya içeriğini kendimiz istiyor ve diske yazıyoruz. */
	clip->ServerFileContentsResponse = rn_clip_server_file_contents_response;
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
	{
		cliprdr_file_context_uninit(rc->file, rc->clip);
		rc->clip = NULL;
	}
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
	rc->system = ClipboardCreate();
	rc->file = cliprdr_file_context_new(rc);
	/* Dosya aktarım yeteneği sunucuya bildirilsin (Mac'ten sunucuya dosya). */
	cliprdr_file_context_set_locally_available(rc->file, TRUE);
	return context;
}

uint32_t rn_connect(rdpContext* ctx, const rn_options* o)
{
	rdpSettings* s = ctx->settings;
	rnContext* rc = (rnContext*)ctx;
	free(rc->clipboard_dir);
	rc->clipboard_dir = o->clipboard_dir && *o->clipboard_dir ? _strdup(o->clipboard_dir) : NULL;
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

void rn_set_clipboard(rdpContext* ctx, const char* text, const char* uri_list)
{
	rnContext* rc = (rnContext*)ctx;
	free(rc->local_text);
	free(rc->local_files);
	rc->local_text = text && *text ? _strdup(text) : NULL;
	rc->local_files = uri_list && *uri_list ? _strdup(uri_list) : NULL;
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
	rnContext* rc = (rnContext*)ctx;
	free(rc->local_text);
	free(rc->local_files);
	rn_dl_reset(rc);
	free(rc->clipboard_dir);
	CliprdrFileContext* file = rc->file;
	wClipboard* system = rc->system;
	freerdp_disconnect(ctx->instance);
	freerdp_client_context_free(ctx);
	cliprdr_file_context_free(file);
	ClipboardDestroy(system);
}
