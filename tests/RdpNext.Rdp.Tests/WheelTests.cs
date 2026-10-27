namespace RdpNext.Rdp.Tests;

// RDP tekerlek değeri 9 bitlik ikiye tümleyen; bir çentik 120.
public class WheelTests
{
    [Fact]
    public void WheelUpIsPositive() => Assert.Equal((RdpMouseFlags)0x0278, RdpSession.WheelFlags(120, horizontal: false));

    [Fact]
    public void WheelDownSetsNegative() => Assert.Equal((RdpMouseFlags)0x0388, RdpSession.WheelFlags(-120, horizontal: false));

    [Fact]
    public void HorizontalUsesHWheel() => Assert.Equal((RdpMouseFlags)0x0478, RdpSession.WheelFlags(120, horizontal: true));

    [Fact]
    public void LargeDeltaIsClamped() => Assert.Equal((RdpMouseFlags)0x02FF, RdpSession.WheelFlags(1000, horizontal: false));
}
