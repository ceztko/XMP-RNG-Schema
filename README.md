## What is this project about?

This projects aims to create RELAX NG grammars to validate [XMP](https://en.wikipedia.org/wiki/Extensible_Metadata_Platform) metadata packets in [PDF/A](https://en.wikipedia.org/wiki/PDF/A) compliances.
The current snapshots can be found at the following locations:
- PDF/A-1: [`ISO19005-1-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-1-xmp_packet-rng)
- PDF/A-2 and PDF/A-3: [`ISO19005-2_3-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-2_3-xmp_packet-rng)
- PDF/A-4: [`ISO19005-4-XMP_Packet.rng`](https://gist.github.com/ceztko/7edd48fae7a9b80f2d089dd5f6aab304#file-iso19005-4-xmp_packet-rng)

## Quickstart

- Download and install .NET SDK[1] and Powershell[2] (Linux, macOS, and Windows);
- Run `pwsh generate-grammars.ps1`

[1] https://dotnet.microsoft.com/en-us/download
[2] https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell