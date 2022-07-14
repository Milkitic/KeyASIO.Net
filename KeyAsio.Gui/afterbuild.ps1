param([string]$exe_path)

$root = (Get-Item $exe_path).Directory.FullName
$folder_resources = Join-Path $root "bin" "resources" 

./DotNetDllPathPatcher.ps1 $exe_path

Move-Item $folder_resources $root
