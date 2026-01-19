param([string]$exe_path)

$root = (Get-Item $exe_path).Directory.FullName
# $folder_resources = Join-Path $root "bin" "resources" 
# $folder_runtimes = Join-Path $root "bin" "runtimes" 

./DotNetDllPathPatcher.ps1 $exe_path

$bin_bat = Join-Path $root "bin" "CompatibleRun.bat"
if (Test-Path $bin_bat) {
    Move-Item $bin_bat $root
}

$shortcut_bat = Join-Path $root "bin" "Create-Shortcut.bat"
if (Test-Path $shortcut_bat) {
    Move-Item $shortcut_bat $root
}

# Move-Item $folder_resources $root
# Move-Item $folder_runtimes $root
