using Commons.Xml.Relaxng;
using System.Reflection;
using System.Xml;

namespace XMPSchema.Test;

public class TestBase
{
    internal TestBase() { }

    public static string ProjectDir
    {
        get
        {
            var info = new DirectoryInfo(Assembly.GetEntryAssembly()!.Location);
            return info.Parent!.Parent!.Parent!.Parent!.FullName;
        }
    }

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
}
