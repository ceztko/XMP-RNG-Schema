#!/usr/bin/env pwsh

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'stop'

dotnet build -c Release "/p:Platform=Any CPU" "$PSScriptRoot/XMP_RNG_Suite/XMP_RNG_Suite.sln"
. "$PSScriptRoot/XMP_RNG_Suite/bin/Release/RNGMerger" --input "$PSScriptRoot/Schemas/XMP_FullPacket.rng" `
    --outdir "$PSScriptRoot" --preset "$PSScriptRoot/Presets/ISO19005-1-XMP_Packet.ini" `
    --preset "$PSScriptRoot/Presets/ISO19005-2_3-XMP_Packet.ini" --preset "$PSScriptRoot/Presets/ISO19005-4-XMP_Packet.ini"