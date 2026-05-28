Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. Find the NDK
# ---------------------------------------------------------------------------

$ndkRoot = $null

$unityEditorBase = "C:\Program Files\Unity\Hub\Editor"
if (Test-Path $unityEditorBase) {
    foreach ($ed in (Get-ChildItem $unityEditorBase -Directory | Sort-Object Name -Descending)) {
        $c = Join-Path $ed.FullName "Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
        if (Test-Path $c) { $ndkRoot = $c; break }
    }
}

if (-not $ndkRoot) {
    $sdkNdk = "$env:LOCALAPPDATA\Android\Sdk\ndk"
    if (Test-Path $sdkNdk) {
        $ndkRoot = (Get-ChildItem $sdkNdk -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
    }
}

if (-not $ndkRoot) {
    Write-Error "Could not locate Android NDK. Install Android build support in Unity Hub."
}
Write-Host "NDK: $ndkRoot"

# ---------------------------------------------------------------------------
# 2. Find clang
# ---------------------------------------------------------------------------

$toolchain = Join-Path $ndkRoot "toolchains\llvm\prebuilt\windows-x86_64\bin"
Write-Host "Toolchain bin: $toolchain"

$useTargetFlag = $false

$clangExes = Get-ChildItem $toolchain -Filter "aarch64-linux-android*-clang.exe" -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending
$clang = if ($clangExes) { $clangExes[0].FullName } else { $null }

if (-not $clang) {
    $bare = Join-Path $toolchain "clang.exe"
    if (Test-Path $bare) {
        $clang = $bare
        $useTargetFlag = $true
        Write-Host "Using bare clang.exe with --target flag"
    } else {
        Write-Host "Executables in toolchain bin:"
        Get-ChildItem $toolchain -Filter "*.exe" | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name)" }
        Write-Error "No aarch64 clang found. See listing above."
    }
}
Write-Host "Clang: $clang"

# ---------------------------------------------------------------------------
# 3. Download SQLite amalgamation
# ---------------------------------------------------------------------------

$workDir = Join-Path $env:TEMP "sqlite_arm64_build"
$srcDir  = Join-Path $workDir "src"
New-Item $srcDir -ItemType Directory -Force | Out-Null

$sqliteSrc = Join-Path $srcDir "sqlite3.c"
if (-not (Test-Path $sqliteSrc)) {
    $zipPath = Join-Path $workDir "sqlite.zip"
    Write-Host "Downloading SQLite amalgamation..."
    Invoke-WebRequest -Uri "https://www.sqlite.org/2024/sqlite-amalgamation-3470200.zip" -OutFile $zipPath -UseBasicParsing
    Expand-Archive -Path $zipPath -DestinationPath $workDir -Force
    $extracted = Get-ChildItem $workDir -Directory -Filter "sqlite-amalgamation-*" | Select-Object -First 1
    Copy-Item (Join-Path $extracted.FullName "*") $srcDir -Force
    Write-Host "SQLite source ready."
}

# ---------------------------------------------------------------------------
# 4. Compile
# ---------------------------------------------------------------------------

$outSo = Join-Path $workDir "libsqlite3.so"

# NDK sysroot — required when using bare clang.exe so it finds Android headers/libs
$sysroot = Join-Path (Split-Path $toolchain -Parent) "sysroot"

$args = [System.Collections.Generic.List[string]]::new()
if ($useTargetFlag) { $args.Add("--target=aarch64-linux-android21") }
if (Test-Path $sysroot) {
    $args.Add("--sysroot=$sysroot")
    Write-Host "Sysroot: $sysroot"
} else {
    Write-Warning "Sysroot not found at $sysroot - build may fail to resolve Android libs"
}
$args.Add("-shared")
$args.Add("-fPIC")
$args.Add("-O2")
$args.Add("-o"); $args.Add($outSo)
$args.Add($sqliteSrc)
$args.Add("-Wl,-z,max-page-size=16384")
$args.Add("-DSQLITE_ENABLE_COLUMN_METADATA")
$args.Add("-DSQLITE_ENABLE_FTS3")
$args.Add("-DSQLITE_ENABLE_FTS5")
$args.Add("-DSQLITE_ENABLE_JSON1")
$args.Add("-DSQLITE_ENABLE_RTREE")
$args.Add("-DSQLITE_ENABLE_UNLOCK_NOTIFY")
$args.Add("-DNDEBUG")
# Android requires explicit linking against libm (math) and libdl (dynamic loading)
# SQLite uses log() for query planning and dlopen internally on some platforms
$args.Add("-lm")
$args.Add("-ldl")

Write-Host "Compiling libsqlite3.so for arm64-v8a (16KB page alignment)..."
$result = & $clang $args.ToArray() 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Clang failed: $result"
}
Write-Host "Compilation succeeded."

# ---------------------------------------------------------------------------
# 5. Verify page alignment using PowerShell binary read (no Python needed)
# ---------------------------------------------------------------------------

$bytes = [System.IO.File]::ReadAllBytes($outSo)

# ELF64 header: e_phoff at offset 32 (8 bytes), e_phentsize at 54 (2 bytes), e_phnum at 56 (2 bytes)
$e_phoff     = [System.BitConverter]::ToUInt64($bytes, 32)
$e_phentsize = [System.BitConverter]::ToUInt16($bytes, 54)
$e_phnum     = [System.BitConverter]::ToUInt16($bytes, 56)

$minAlign = [UInt64]::MaxValue
for ($i = 0; $i -lt $e_phnum; $i++) {
    $off    = $e_phoff + $i * $e_phentsize
    $p_type = [System.BitConverter]::ToUInt32($bytes, $off)
    if ($p_type -eq 1) {   # PT_LOAD
        $p_align = [System.BitConverter]::ToUInt64($bytes, $off + 48)
        if ($p_align -lt $minAlign) { $minAlign = $p_align }
    }
}

Write-Host "Minimum PT_LOAD alignment: $minAlign (0x$($minAlign.ToString('X')))"
if ($minAlign -ge 16384) {
    Write-Host "PASS: library is 16KB-aligned."
} else {
    Write-Warning "Alignment $minAlign is below 16384 - the linker flag may not have taken effect."
}

# ---------------------------------------------------------------------------
# 6. Copy into the Unity project
# ---------------------------------------------------------------------------

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginDest = Join-Path $scriptDir "SpaceGame\Assets\Plugins\Android\libs\arm64-v8a\libsqlite3.so"

Copy-Item $outSo $pluginDest -Force
Write-Host ""
Write-Host "SUCCESS: $pluginDest"
Write-Host "Rebuild the Unity project and run on the emulator."
