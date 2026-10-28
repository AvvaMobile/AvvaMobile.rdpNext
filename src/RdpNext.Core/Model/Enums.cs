namespace RdpNext.Core.Model;

public enum Protocol { Rdp, Ssh }

// SSH'de giriş yöntemi; RDP'de her zaman Password.
public enum SshAuth { Password, Key }

public enum ScreenMode { FitToWindow, FullScreen }

public enum ServerColor { Blue, Teal, Orange, Violet, Green, Black }

// Yalnız görüntü; protokolden türetilir.
public enum ServerIcon { Windows, Ssh }
