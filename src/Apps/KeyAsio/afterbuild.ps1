param([string]$exe_path)

$root = (Get-Item $exe_path).Directory.FullName
$folder_runtimes = Join-Path $root "bin" "runtimes" 

./DotNetDllPathPatcher.ps1 $exe_path

Move-Item $folder_resources $root
Move-Item $folder_runtimes $root
