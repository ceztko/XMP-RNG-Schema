#!/usr/bin/env pwsh

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'stop'

dotnet build -c Release "/p:Platform=Any CPU" "$PSScriptRoot/RNGMerger/RNGMerger.sln"
. "$PSScriptRoot/RNGMerger/bin/Release/RNGMerger" --input "$PSScriptRoot/Schemas/XMP_FullPacket.rng" --outdir "$PSScriptRoot"