using Microsoft.Extensions.Configuration.Ini;
using Mono.Options;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace RelaxNgMerger;

public class RNGMerger
{
    public const string DefaultMergedFilename = "Merged_XMP_Packet.rng";
    static readonly XNamespace RngNs = "http://relaxng.org/ns/structure/1.0";
    static readonly XNamespace UiNs = "http://ns.iso.org/iso-16684-2/xmp-schema-ui-info/1.0";

    static int Main(string[] args)
    {
        string? inputPath = null;
        string? outputDir = null;
        RNGSchemaFlags flags = RNGSchemaFlags.None;
        var presets = new List<string>();

        var options = new OptionSet()
        {
            { "i|input=", "The input RELAX NG schema", (i) => inputPath = i },
            { "o|outdir=", "The output directory", (o) => outputDir = o },
            { "p|preset=", "Condition preset for the schema generation", presets.Add },
            { "dropdesc", "Drop descriptions", (_) => flags |= RNGSchemaFlags.DropDescriptions },
            { "deterministic", "Make the schema deterministic", (_) => flags |= RNGSchemaFlags.Deterministic },
            { "withext", "Include schema extensions", (_) => flags |= RNGSchemaFlags.WithExtensions },
        };

        _ = options.Parse(args);
        if (inputPath == null || outputDir == null)
        {
            ShowHelp(options);
            return -1;
        }

        try
        {
            MakeSchemas(inputPath, outputDir, presets, flags);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return -1;
        }
    }

    public static void MakeSchemas(string rngFilePath, string outputPath,
        IReadOnlyList<string> presets, RNGSchemaFlags flags = RNGSchemaFlags.None)
    {
        Console.WriteLine($"Merging RELAX NG schema from: {rngFilePath} ...");
        var mainDoc = XDocument.Load(rngFilePath);
        mainDoc.AddFirst(new XComment(" Generated with https://github.com/ceztko/XMP-RNG-Schema, DO NOT EDIT! "));
        string schemaBaseDir = Path.GetDirectoryName(Path.GetFullPath(rngFilePath))!;

        var namespaces = new Dictionary<string, string>();
        processIncludes(mainDoc.Root ?? throw new Exception("Invalid document"), mainDoc.Root, schemaBaseDir,
            namespaces, new HashSet<string>(), flags.HasFlag(RNGSchemaFlags.DropDescriptions));

        foreach (var (prefix, uri) in namespaces)
        {
            XName nsAttrName = XNamespace.Xmlns + prefix;

            if (mainDoc.Root.Attribute(nsAttrName) == null)
                mainDoc.Root.SetAttributeValue(nsAttrName, uri);
        }

        validateSchema(mainDoc.Root);
        saveDoc(mainDoc, Path.Combine(outputPath, "Merged_XMP_Packet.rng"), skipIndent: true);

        bool wantDeterministic = flags.HasFlag(RNGSchemaFlags.Deterministic);
        bool withExtensions = flags.HasFlag(RNGSchemaFlags.WithExtensions);
        foreach (var preset in presets)
            makeSchema(mainDoc, preset, outputPath, wantDeterministic, withExtensions);
    }

    static void ShowHelp(OptionSet options)
    {
        Console.WriteLine("Usage: RNGMerger [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        options.WriteOptionDescriptions(Console.Out);
    }

    static void processIncludes(XElement rootElement, XElement element, string basePath,
        Dictionary<string, string> namespaces, HashSet<string> includedFiles, bool dropDescriptions)
    {
        if (dropDescriptions)
            removeDescendantElements(element, UiNs);

        // Remove all comments recursively in the included schema
        foreach (var comment in element.DescendantNodes().OfType<XComment>().ToList())
            comment.Remove();

        foreach (var include in element.Elements(RngNs + "include").ToList())
        {
            var href = include.Attribute("href")?.Value;
            if (string.IsNullOrEmpty(href))
            {
                Console.WriteLine("Warning: <rng:include> without href found; skipping.");
                continue;
            }

            var resolvedPath = Path.GetFullPath(Path.Combine(basePath, href));
            if (!includedFiles.Add(resolvedPath))
            {
                Console.WriteLine($"Skipping already included: {resolvedPath}");
                include.Remove();
                continue;
            }

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Included file not found: {resolvedPath}");

            Console.WriteLine($"Including: {resolvedPath}");

            var includedDoc = XDocument.Load(resolvedPath);
            var includedElement = includedDoc.Root!;

            // Recursively process includes in the included file
            processIncludes(rootElement, includedElement, Path.GetDirectoryName(resolvedPath)!,
                namespaces, includedFiles, dropDescriptions);

            // Replace <rng:include> with the children of the included schema
            include.Remove();
            rootElement.Add(includedElement.Elements());

            foreach (var attr in includedElement.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    var prefix = attr.Name.LocalName == "xmlns" ? "" : attr.Name.LocalName;
                    var uri = attr.Value;
                    if (namespaces.TryGetValue(prefix, out var value))
                    {
                        if (uri != value)
                            throw new Exception($"Invalid redefined namespace for prefix \"{prefix}\". It was \"{value}\" and trying to insert {uri}");
                    }
                    else
                    {
                        namespaces[prefix] = uri;
                    }
                }
            }
        }
    }

    static void validateSchema(XElement root)
    {
        var defines = new Dictionary<string, XElement>();
        foreach (var define in root.Descendants(RngNs + "define"))
        {
            var name = define.Attribute("name")?.Value ?? throw new Exception("Missing name attribute");
            if (string.IsNullOrEmpty(name) || name.Contains(':'))
                throw new Exception($"Invalid element \"{define}\"");

            if (!defines.TryAdd(name, define))
                throw new Exception($"Define \"{name}\" is already present");
        }

        var references = root.Descendants(RngNs + "ref").Select((XElement r) => r.Attribute("name")?.Value ?? throw new Exception("Missing name attribute")).ToHashSet();
        foreach (var reference in references)
        {
            if (string.IsNullOrEmpty(reference) || reference.Contains(':'))
                throw new Exception($"Invalid reference \"{reference}\"");

            if (!defines.ContainsKey(reference))
                throw new Exception($"Reference \"{reference}\" is not defined");
        }

        // Find main <rng:interleave> element
        var interleave = root.Descendants(RngNs + "interleave").First();
        var refs = interleave.Descendants(RngNs + "ref").ToList();
        HashSet<string> properties = new HashSet<string>();
        foreach (var reference in refs)
        {
            var name = reference.Attribute("name")!.Value!;
            var define = defines[name];
            interleave = define.Descendants(RngNs + "interleave").First();
            foreach (var child in interleave.Elements())
            {
                // Ensure the second level interleave elements
                // are <rng:optional> elements with a single child
                List<XElement> children;
                if (child.Name.LocalName != "optional" || (children = child.Elements().ToList()).Count != 1)
                    throw new Exception("Invalid interleaved element");

                var propName = children[0].Attribute("name")!.Value!;
                if (!properties.Add(children[0].Attribute("name")!.Value!))
                    throw new Exception($"Repeated property \"{propName}\"");
            }
        }
    }

    static void makeSchema(XDocument document, string presetPath,
        string outDir, bool wantDeterministic, bool withExtensions)
    {
        if (!File.Exists(presetPath))
            throw new Exception($"Preset not fount at \"{presetPath}\"");

        var filename = $"{Path.GetFileNameWithoutExtension(presetPath)}.rng";

        Console.WriteLine();
        Console.WriteLine($"Processing {filename} schema...");

        var processed = new XDocument(document);
        var props = readPreset(presetPath);
        if (withExtensions)
            props["IncludeExtensions"] = true;

        preprocess(processed.Root!, new BoolDictionaryXsltContext(props));

        var defineMap = new Dictionary<string, XElement>();
        foreach (var define in processed.Root!.Descendants(RngNs + "define"))
        {
            var name = define.Attribute("name")?.Value ?? throw new Exception("Missing name attribute");
            defineMap[name] = define;
        }

        var start = processed.Root!.Descendants(RngNs + "start").First();
        if (wantDeterministic)
            makeDeterministic(start, defineMap);

        Console.WriteLine($"Collecting schema garbage...");
        var visitedDefines = new HashSet<string>();
        collectGarbage(start, visitedDefines, defineMap);

        foreach (var pair in defineMap)
        {
            if (!visitedDefines.Contains(pair.Key))
            {
                logElementRemoval(pair.Value, RemovalReason.Collected);
                pair.Value.Remove();
            }
        }

        var outputPath = Path.Combine(outDir, filename);
        saveDoc(processed, outputPath);
        Console.WriteLine($"Merged schema saved to: {outputPath}");
    }

    /// <summary>
    /// Process condition="$Prop1 and $Prop2 or $Prop3" like expressions
    /// </summary>
    static void preprocess(XElement element, BoolDictionaryXsltContext ctx)
    {
        bool evaluate(string? expr)
        {
            if (string.IsNullOrWhiteSpace(expr))
                return true;            // no @condition == keep

            // Use an empty navigator (we only need variable lookup, no node test)
            var nav = new XmlDocument().CreateNavigator()!;
            object result = nav.Evaluate(expr!, ctx);
            return Convert.ToBoolean(result);
        }

        foreach (var child in element.Elements().ToList())
        {
            var condAttr = child.Attribute("condition");
            bool keep = evaluate(condAttr?.Value);
            if (keep)
            {
                condAttr?.Remove();
                preprocess(child, ctx);   // depth‑first
            }
            else
            {
                logElementRemoval(child, RemovalReason.Excluded);
                child.Remove();   // prune whole subtree
            }
        }
    }

    static void collectGarbage(XElement element, HashSet<string> visitedDefines, Dictionary<string, XElement> defineMap)
    {
        foreach (var reference in element.Descendants(RngNs + "ref"))
        {
            var name = reference.Attribute("name")!.Value!;
            var define = defineMap[name];
            if (!visitedDefines.Add(name))
                continue;

            collectGarbage(define, visitedDefines, defineMap);
        }
    }

    static void logElementRemoval(XElement child, RemovalReason reason)
    {
        var name = child.Attribute("name");
        XElement? reference = null;
        string prefixedName = getPrefixedName(child);
        var reasonString = reason switch {
            RemovalReason.Excluded => "excluded",
            RemovalReason.Collected => "collected",
            _ => throw new NotSupportedException($"Not supported {reason}"),
        };
        if (name == null)
        {
            if (prefixedName == "rng:optional"
                && (reference = child.Descendants(RngNs + "ref").FirstOrDefault()) != null
                && (name = reference.Attribute("name")) != null)
            {
                Console.WriteLine($"Removing {reasonString} \"rng:optional\" element pointing to element with name \"{name.Value}\"");
                return;
            }

            var ancestor = child.Ancestors(RngNs + "define").FirstOrDefault();
            if (ancestor == null
                || (name = ancestor.Attribute("name")) == null)
            {
                throw new Exception($"Unable to find a define ancestor for a \"{prefixedName}\" element");
            }

            Console.WriteLine($"Removing {reasonString} \"{prefixedName}\" element within \"{name?.Value ?? "null"}\" define");
        }
        else
        {
            Console.WriteLine($"Removing {reasonString} \"{prefixedName}\" element with name \"{name?.Value ?? "null"}\"");
        }
    }

    /// <summary>
    /// This erase the top level rng:interleave elements and place a
    /// big choice of zero or more XMP properties
    /// </summary>
    private static void makeDeterministic(XElement start, Dictionary<string, XElement> defineMap)
    {
        var interleave = start.Descendants(RngNs + "interleave").First();
        var zeroOrMore = new XElement(RngNs + "zeroOrMore");
        interleave.Parent!.Add(zeroOrMore);
        var choice = new XElement(RngNs + "choice");
        zeroOrMore.Add(choice);
        var refs = interleave.Descendants(RngNs + "ref").ToList();
        interleave.Remove();
        foreach (var reference in refs)
        {
            var name = reference.Attribute("name")!.Value!;
            var define = defineMap[name];
            interleave = define.Descendants(RngNs + "interleave").First();
            foreach (var child in interleave.Elements())
            {
                Debug.Assert(child.Name.LocalName == "optional");
                choice.Add(child.Elements().First());
            }
        }
    }

    static void saveDoc(XDocument doc, string filepath, bool skipIndent = false)
    {
        using (var writer = XmlWriter.Create(filepath,
            new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineChars = "\n", Indent = !skipIndent }))
        {
            doc.Save(writer);
        }
    }

    static string getPrefixedName(XElement element)
    {
        string? prefix = element.GetPrefixOfNamespace(element.Name.Namespace);
        return string.IsNullOrEmpty(prefix)
            ? element.Name.LocalName
            : $"{prefix}:{element.Name.LocalName}";
    }

    static void removeDescendantElements(XElement element, XNamespace ns)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.Namespace == ns)
            {
                child.Remove();
                continue;
            }

            removeDescendantElements(child, ns);
        }
    }

    static Dictionary<string, bool> readPreset(string filePath)
    {
        var ret = new Dictionary<string, bool>();
        var iniProps = IniStreamConfigurationProvider.Read(
            File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
        foreach (var pair in iniProps)
        {
            _ = bool.TryParse(pair.Value, out bool val);
            ret[pair.Key] = val;
        }

        return ret;
    }

    sealed class BoolDictionaryXsltContext : XsltContext
    {
        private readonly Dictionary<string, bool> _properties;

        public BoolDictionaryXsltContext(Dictionary<string, bool> properties)
            => _properties = properties;

        // No functions or namespaces needed
        public override bool Whitespace => true;
        public override IXsltContextVariable ResolveVariable(string prefix, string name)
            => new DictVar(name, _properties.TryGetValue(name, out bool v) && v);

        public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] argTypes)
            => throw new NotImplementedException();
        public override int CompareDocument(string baseUri, string nextbaseUri) => 0;

        public override bool PreserveWhitespace(XPathNavigator node)
        {
            return false;
        }

        /* Variable implementation */
        private sealed class DictVar : IXsltContextVariable
        {
            private readonly bool _value;
            public DictVar(string name, bool value) => _value = value;
            public bool IsLocal => false;
            public bool IsParam => true;
            public XPathResultType VariableType => XPathResultType.Boolean;
            public object Evaluate(XsltContext xsltContext) => _value;
        }
    }

    enum RemovalReason
    {
        Excluded,
        Collected,
    }
}

[Flags]
public enum RNGSchemaFlags
{
    None = 0,
    DropDescriptions = 1,
    Deterministic = 2,
    WithExtensions = 4,
}
