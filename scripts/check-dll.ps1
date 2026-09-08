$proc = Get-Process -Id 5308
foreach ($mod in $proc.Modules) {
    if ($mod.ModuleName -eq "BambooMintKey.dll") {
        Write-Host "Found: $($mod.FileName) - Time: $((Get-Item $mod.FileName).LastWriteTime)"
    }
}
