namespace RdpNext.Rdp.Tests;

// Mac'te kopyalanan dosyalar sunucuya "file:///" satırları (text/uri-list) olarak verilir.
public class UriListTests
{
    [Fact]
    public void PathsBecomeEncodedFileUris() =>
        Assert.Equal("file:///Users/murat/A%20B.txt\r\nfile:///tmp/%C3%B6rnek%20%C4%9F.pdf\r\n",
            RdpSession.UriList(["/Users/murat/A B.txt", "/tmp/örnek ğ.pdf"]));

    [Fact]
    public void NoFilesGivesNull() => Assert.Null(RdpSession.UriList([]));
}
