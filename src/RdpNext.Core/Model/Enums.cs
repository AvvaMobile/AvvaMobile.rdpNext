namespace RdpNext.Core.Model;

public enum Protocol { Rdp, Ssh }

// SSH'de giriş yöntemi; RDP'de her zaman Password.
public enum SshAuth { Password, Key }

public enum ScreenMode { FitToWindow, FullScreen }

public enum ServerColor { Blue, Teal, Orange, Violet, Green, Black }

// Yalnız görüntü; protokolden ve RDP sunucusunun işletim sisteminden türetilir.
public enum ServerIcon { Windows, Ssh, Linux }

// RDP sunucusunun işletim sistemi; bağlantıyı değiştirmez, yalnız ikonu belirler.
public enum ServerOs { Windows, Linux }
