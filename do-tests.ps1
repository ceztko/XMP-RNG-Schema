#!/usr/bin/env pwsh

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'stop'

dotnet build -c Release "/p:Platform=Any CPU" "$PSScriptRoot/XMP_RNG_Suite/XMP_RNG_Suite.sln"
. "$PSScriptRoot/XMP_RNG_Suite/bin/Release/XMPSchema.Test"