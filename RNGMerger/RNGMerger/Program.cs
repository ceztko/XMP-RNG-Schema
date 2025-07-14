using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace RelaxNgMerger;

class Program
{
    static readonly XNamespace RngNs = "http://relaxng.org/ns/structure/1.0";
    static readonly HashSet<string> IncludedFiles = new(StringComparer.OrdinalIgnoreCase);

    static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: RelaxNgMerger -i <input.rng> -o <outputDir>");
            return 1;
        }

        string? inputPath = null;
        string? outputDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-i" || args[i] == "--input")
                inputPath = args[++i];
            else if (args[i] == "-o" || args[i] == "--outdir")
                outputDir = args[++i];
        }

        if (inputPath == null || outputDir == null)
        {
            Console.Error.WriteLine("Error: Both --input and --outdir must be specified.");
            return -1;
        }

        try
        {
            Console.WriteLine($"Merging RELAX NG grammar from: {inputPath} ...");
            var mainDoc = XDocument.Load(inputPath);
            string baseDir = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;

            processIncludes(mainDoc.Root!, baseDir);
            validateGrammar(mainDoc.Root!);

            Console.WriteLine();
            Console.WriteLine($"Processing PDF/A-1 grammar...");

            var properties = new Dictionary<string, bool>
            {
                ["IsPDFA1"] = true,
                ["IsPDFA1OrGreater"] = true,
            };
            makeGrammar(mainDoc, properties, outputDir, "ISO19005-1-XMP_Packet.rng");

            Console.WriteLine();
            Console.WriteLine($"Processing PDF/A-2 and PDF/A-3 grammar...");

            properties["IsPDFA1"] = false;
            properties["IsPDFA2"] = true;
            properties["IsPDFA2OrGreater"] = true;
            properties["IsPDFA3"] = true;
            properties["IsPDFA3OrGreater"] = true;
            makeGrammar(mainDoc, properties, outputDir, "ISO19005-2_3-XMP_Packet.rng");

            Console.WriteLine();
            Console.WriteLine($"Processing PDF/A-4 grammar...");

            properties["IsPDFA1"] = false;
            properties["IsPDFA2"] = false;
            properties["IsPDFA3"] = false;
            properties["IsPDFA4"] = true;
            properties["IsPDFA4OrGreater"] = true;
            makeGrammar(mainDoc, properties, outputDir, "ISO19005-4-XMP_Packet.rng");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return -1;
        }
    }

    static void processIncludes(XElement element, string basePath)
    {
        foreach (var include in element.Descendants(RngNs + "include").ToList())
        {
            var href = include.Attribute("href")?.Value;
            if (string.IsNullOrEmpty(href))
            {
                Console.WriteLine("Warning: <rng:include> without href found; skipping.");
                continue;
            }

            var resolvedPath = Path.GetFullPath(Path.Combine(basePath, href));

            if (!IncludedFiles.Add(resolvedPath))
            {
                Console.WriteLine($"Skipping already included: {resolvedPath}");
                include.Remove();
                continue;
            }

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Included file not found: {resolvedPath}");

            Console.WriteLine($"Including: {resolvedPath}");

            var includedDoc = XDocument.Load(resolvedPath);
            var includedRoot = includedDoc.Root!;

            // Remove all comments recursively in the included grammar
            includedRoot.DescendantNodes()
                .OfType<XComment>()
                .ToList()
                .ForEach(c => c.Remove());

            // Recursively process includes in the included file
            processIncludes(includedRoot, Path.GetDirectoryName(resolvedPath)!);

            // Replace <rng:include> with the children of the included grammar
            include.ReplaceWith(includedRoot.Elements());
        }
    }

    static void validateGrammar(XElement element)
    {
        var defines = new HashSet<string>();
        foreach (var define in element.Descendants(RngNs + "define").Select((XElement r) => r.Attribute("name")?.Value ?? throw new Exception("Missing name attribute")))
        {
            if (string.IsNullOrEmpty(define) || define.Contains(':'))
                throw new Exception($"Invalid element \"{define}\"");

            if (!defines.Add(define))
                throw new Exception($"Element \"{define}\" is already defined");
        }

        var references = element.Descendants(RngNs + "ref").Select((XElement r) => r.Attribute("name")?.Value ?? throw new Exception("Missing name attribute")).ToHashSet();
        foreach (var reference in references)
        {
            if (string.IsNullOrEmpty(reference) || reference.Contains(':'))
                throw new Exception($"Invalid reference \"{reference}\"");

            if (!defines.Contains(reference))
                throw new Exception($"Reference \"{reference}\" is not defined");
        }
    }

    static void makeGrammar(XDocument document, Dictionary<string, bool> properties,
        string outDir, string filename)
    {
        var processed = new XDocument(document);
        preprocess(processed.Root!, new BoolDictionaryXsltContext(properties));

        Console.WriteLine($"Collecting grammar garbage...");
        collectGarbage(processed);
        var outputPath = Path.Combine(outDir, filename);
        using (var writer = XmlWriter.Create(outputPath,
            new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineChars = "\n", Indent = true }))
        {
            processed.Save(writer);
        }

        Console.WriteLine($"Merged grammar saved to: {outputPath}");
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

    /// <summary>
    /// Remove unreferenced defines
    /// </summary>
    static void collectGarbage(XDocument document)
    {
        var defineMap = new Dictionary<string, XElement>();
        foreach (var define in document.Root!.Descendants(RngNs + "define"))
        {
            var name = define.Attribute("name")?.Value ?? throw new Exception("Missing name attribute");
            defineMap[name] = define;
        }

        var start = document.Root!.Descendants(RngNs + "start").FirstOrDefault() ??
            throw new Exception("Missing start element");
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

    private static void logElementRemoval(XElement child, RemovalReason reason)
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

    static string getPrefixedName(XElement element)
    {
        string? prefix = element.GetPrefixOfNamespace(element.Name.Namespace);
        return string.IsNullOrEmpty(prefix)
            ? element.Name.LocalName
            : $"{prefix}:{element.Name.LocalName}";
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
