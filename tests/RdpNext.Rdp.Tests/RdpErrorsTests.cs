namespace RdpNext.Rdp.Tests;

public class RdpErrorsTests
{
    [Theory]
    [InlineData(0x00020014u)] // logon failure
    [InlineData(0x00020015u)] // wrong password
    [InlineData(0x00020009u)] // authentication failed
    [InlineData(0x0002001Bu)] // no or missing credentials
    public void LogonErrorsAreAuthFailed(uint error) =>
        Assert.Equal(RdpEndReason.AuthFailed, RdpErrors.Classify(error, 0, wasConnected: false));

    [Fact]
    public void OtherConnectErrorIsConnectFailed() =>
        Assert.Equal(RdpEndReason.ConnectFailed, RdpErrors.Classify(0x00020006u, 0, wasConnected: false));

    [Theory]
    [InlineData(0x0Cu)] // kullanıcı Windows'ta sign out
    [InlineData(0x0Bu)] // kullanıcı Windows'ta disconnect
    public void UserLogoffIsUserLogoff(uint errorInfo) =>
        Assert.Equal(RdpEndReason.UserLogoff, RdpErrors.Classify(0, errorInfo, wasConnected: true));

    [Theory]
    [InlineData(0x02u)] // yönetici oturumu kapattı
    [InlineData(0x00u)] // ağ koptu
    public void OtherEndAfterConnectIsDisconnected(uint errorInfo) =>
        Assert.Equal(RdpEndReason.Disconnected, RdpErrors.Classify(0, errorInfo, wasConnected: true));
}
