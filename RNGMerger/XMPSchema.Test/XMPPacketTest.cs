namespace XMPSchema.Test;

[TestClass]
public sealed class XMPPacketTest : TestBase
{
    [TestMethod]
    public void XMPPacketTest1()
    {
        ValidateXML(
            Path.Combine(ProjectDir, "Test", "XMPPacket1.xml"),
            Path.Combine(ProjectDir, "ISO19005-4-XMP_Packet.rng"));
    }

    [TestMethod]
    public void XMPPacketTest2()
    {
        try
        {
            ValidateXML(
            Path.Combine(ProjectDir, "Test", "XMPPacket1.xml"),
            Path.Combine(ProjectDir, "ISO19005-1-XMP_Packet.rng"));
        }
        catch
        {
            return;
        }

        throw new Exception("It should fail");
    }
}
