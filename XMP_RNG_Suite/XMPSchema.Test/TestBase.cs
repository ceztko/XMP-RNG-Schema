using Commons.Xml.Relaxng;
using RelaxNgMerger;
using System.Reflection;
using System.Xml;

namespace XMPSchema.Test;

public class TestBase
{
    static string _ProjectDir;

    static TestBase()
    {
        var info = new DirectoryInfo(Assembly.GetEntryAssembly()!.Location);
        _ProjectDir = info.Parent!.Parent!.Parent!.Parent!.FullName;
        generateSchemasIfNeeded();
    }

    internal TestBase() { }

    public static string ProjectDir => _ProjectDir;

    protected static void ValidateXML(string contentPath, string schemaPath)
    {
        XmlReader instance = new XmlTextReader(contentPath);
        XmlReader grammar = new XmlTextReader(schemaPath);
        using (RelaxngValidatingReader reader = new RelaxngValidatingReader(instance, grammar))
        {
            while (!reader.EOF)
            {
                reader.Read();
            }
        }
    }

    static void generateSchemasIfNeeded()
    {
        if (File.Exists(Path.Combine(_ProjectDir, RNGMerger.DefaultMergedFilename)))
            return;

        RNGMerger.MakeSchemas(Path.Combine(_ProjectDir, "Schemas", "XMP_FullPacket.rng"),
            _ProjectDir, [
                Path.Combine(_ProjectDir, "Presets", "ISO19005-1-XMP_Packet.ini"),
                Path.Combine(_ProjectDir, "Presets", "ISO19005-2_3-XMP_Packet.ini"),
                Path.Combine(_ProjectDir, "Presets", "ISO19005-4-XMP_Packet.ini")
            ]);
    }
}
