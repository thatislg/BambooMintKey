[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# TODO: Implement TSF TIP unregistration per 002_05:
# 1. Require Administrator / auto-elevate
# 2. Locate publish\$Runtime\BambooMintKey.dll
# 3. Run regsvr32 /u /s

throw "Script template not yet implemented. See 002_05_DevHarness_and_RegistrationScript.md"
