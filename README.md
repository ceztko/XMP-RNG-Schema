## What is this project about?

This projects aims to create RELAX NG schemas to validate [XMP](https://en.wikipedia.org/wiki/Extensible_Metadata_Platform) metadata packets in PDF and [PDF/A](https://en.wikipedia.org/wiki/PDF/A) compliances.
The source is a custom RELAX NG schema with simple extensions to allow to customize the template for different flavors (eg. different the PDF/A parts/revisions). The main one is a `condition` attribute
that can be used to select nodes using a XSLT compatible expression, so for example the `pdf:Trapped` property below:

```
<rng:optional condition="$IsPDFA4OrGreater">
    <rng:ref name="pdf.Trapped"/>
</rng:optional>
```

... is enabled for a PDF/A-4 schema, while all the `pdfaExtension` properties below:

```
<rng:interleave>
    <!-- ... -->
    <rng:ref name="XMP_Properties-pdfaExtension" condition="$IsPDFA1 or $IsPDFA2 or $IsPDFA3"/>
</rng:interleave>
```

... are enabled for PDF/A-1, PDF/A-2 and PDF/A-3 schemas. The schemas are then garbage collected so only recursivelly referenced nodes from the `<rng:start>` element are preserved in the final generated schema. The current snapshots targeting PDF/A compliances can be found at the following locations:

- PDF/A-1: [`ISO19005-1-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-1-xmp_packet-rng)
- PDF/A-2 and PDF/A-3: [`ISO19005-2_3-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-2_3-xmp_packet-rng)
- PDF/A-4: [`ISO19005-4-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-4-xmp_packet-rng)

The schemas are meant to be used to actually validate XMP packets following the normalization procedure as explained in chapter 5 _"Canonical serialization of XMP"_ of [ISO 16684-2:2014](https://www.iso.org/standard/57422.html).

## Quickstart

If you want to look at the template development follows the steps below:

- Download and install .NET SDK[^1] and Powershell[^2] (Linux, macOS, and Windows);
- Run `pwsh generate-schemas.ps1`

[^1]: https://dotnet.microsoft.com/en-us/download
[^2]: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
