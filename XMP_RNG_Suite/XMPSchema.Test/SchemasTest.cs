namespace XMPSchema.Test;

[TestClass]
public sealed class SchemasTest : TestBase
{
    [TestMethod]
    public void TestPDFA1_Schema()
    {
        ValidateXML(
            Path.Combine(ProjectDir, "ISO19005-1-XMP_Packet.rng"),
            Path.Combine(ProjectDir, "Resources", "relaxng.rng"));
    }

    [TestMethod]
    public void TestPDFA2_3_Schema()
    {
        ValidateXML(
            Path.Combine(ProjectDir, "ISO19005-2_3-XMP_Packet.rng"),
            Path.Combine(ProjectDir, "Resources", "relaxng.rng"));
    }

    [TestMethod]
    public void TestPDFA4_Schema()
    {
        ValidateXML(
            Path.Combine(ProjectDir, "ISO19005-4-XMP_Packet.rng"),
            Path.Combine(ProjectDir, "Resources", "relaxng.rng"));
    }
}
