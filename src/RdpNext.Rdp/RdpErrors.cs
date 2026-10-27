namespace RdpNext.Rdp;

// FreeRDP hata kodlarını (freerdp_get_last_error, freerdp_error_info) oturum sonu sebebine çevirir.
internal static class RdpErrors
{
    private const uint ConnectClass = 2;

    // ERRCONNECT_AUTHENTICATION_FAILED, LOGON_FAILURE, WRONG_PASSWORD, NO_OR_MISSING_CREDENTIALS
    private static readonly uint[] AuthErrors = [0x09, 0x14, 0x15, 0x1B];

    // ERRINFO_RPC_INITIATED_DISCONNECT_BYUSER, ERRINFO_LOGOFF_BY_USER: kullanıcı Windows içinden çıktı.
    private static readonly uint[] UserEnded = [0x0B, 0x0C];

    public static RdpEndReason Classify(uint lastError, uint errorInfo, bool wasConnected)
    {
        if (UserEnded.Contains(errorInfo))
            return RdpEndReason.UserLogoff;
        if (wasConnected)
            return RdpEndReason.Disconnected;
        return lastError >> 16 == ConnectClass && AuthErrors.Contains(lastError & 0xFFFF)
            ? RdpEndReason.AuthFailed
            : RdpEndReason.ConnectFailed;
    }
}
