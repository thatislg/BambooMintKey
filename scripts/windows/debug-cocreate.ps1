# scripts/debug-cocreate.ps1
# Thử CoCreateInstance BambooMintKey COM server để kiểm tra DLL load/IID hỗ trợ.
[CmdletBinding()]
param ()

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Continue"

$Clsid = [Guid]"B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1"
# Phải khớp với Guids.cs trong BambooMintKey.NativeBridge
$IidITfTextInputProcessorEx = [Guid]"6E4E2102-F9CD-433D-B496-303CE03A6507"
$IidITfTextInputProcessor   = [Guid]"AA80E7F7-2021-11D2-93E0-0060B067B86E"
$IidITfThreadMgrEventSink   = [Guid]"AA80E80E-2021-11D2-93E0-0060B067B86E"
$IidITfKeyEventSink         = [Guid]"AA80E7F5-2021-11D2-93E0-0060B067B86E"
$IidIUnknown = [Guid]"00000000-0000-0000-C000-000000000046"

function TryCreate($iid, $name) {
    Write-Host "Trying CoCreateInstance with IID $name ($iid)..." -ForegroundColor Cyan
    try {
        $type = [Type]::GetTypeFromCLSID($Clsid, $true)
        $obj = [Activator]::CreateInstance($type)
        $pUnk = [System.Runtime.InteropServices.Marshal]::GetIUnknownForObject($obj)
        Write-Host "  IUnknown pointer = $pUnk" -ForegroundColor Green
        $ppv = [IntPtr]::Zero
        $hr = [System.Runtime.InteropServices.Marshal]::QueryInterface($pUnk, [ref]$iid, [ref]$ppv)
        $color = if ($hr -eq 0) { "Green" } else { "Red" }
        Write-Host ("  QueryInterface HR = 0x{0:X8}, ppv = {1}" -f $hr, $ppv) -ForegroundColor $color
        if ($ppv -ne [IntPtr]::Zero) {
            [System.Runtime.InteropServices.Marshal]::Release($ppv) | Out-Null
        }
        [System.Runtime.InteropServices.Marshal]::Release($pUnk) | Out-Null
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($obj) | Out-Null
    } catch {
        Write-Host "  FAILED: $_" -ForegroundColor Red
    }
}

TryCreate $IidIUnknown "IUnknown"
TryCreate $IidITfTextInputProcessor "ITfTextInputProcessor"
TryCreate $IidITfTextInputProcessorEx "ITfTextInputProcessorEx"
TryCreate $IidITfThreadMgrEventSink "ITfThreadMgrEventSink"
TryCreate $IidITfKeyEventSink "ITfKeyEventSink"
