#!/usr/bin/env pwsh
# 文件名: Signer.ps1
# 运行方式: 
#    ./Signer.ps1 generate
#    ./Signer.ps1 sign ./bin/Release/net10.0/KeyAsio.dll ./private.key
#    ./Signer.ps1 verify ./bin/Release/net10.0/KeyAsio.dll ./public.key

# 将参数映射到变量，模拟 args 数组
$Command = $args[0]
$RestArgs = $args[1..($args.Count - 1)]

# 检查参数
if (-not $Command) {
    Write-Host "Usage: ./Signer.ps1 [generate|sign|verify] [args...]"
    exit
}

# 确保 .NET 使用 PowerShell 的当前目录
[System.IO.Directory]::SetCurrentDirectory($PWD)

function Fail($message) {
    Write-Host $message
    exit 1
}

function GenerateKeys {
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        $privateKey = $rsa.ExportRSAPrivateKeyPem()
        $publicKey = $rsa.ExportRSAPublicKeyPem()

        [System.IO.File]::WriteAllText("private.key", $privateKey)
        [System.IO.File]::WriteAllText("public.key", $publicKey)

        Write-Host "✅ Keys generated successfully."
        Write-Host "   private.key -> 🔒 Keep this SECRET in your CI/CD pipeline (GitHub Secrets)."
        Write-Host "   public.key  -> 🌍 Embed this content into your KeyAsio.Secrets class."
    }
    finally {
        $rsa.Dispose()
    }
}

function SignFile($dllPath, $keyPath) {
    if (-not (Test-Path $dllPath)) { throw [System.IO.FileNotFoundException]::new("Dll not found", $dllPath) }
    if (-not (Test-Path $keyPath)) { throw [System.IO.FileNotFoundException]::new("Private key not found", $keyPath) }

    $dllPath = Convert-Path $dllPath
    $keyPath = Convert-Path $keyPath

    $dllBytes = [System.IO.File]::ReadAllBytes($dllPath)
    $privateKey = [System.IO.File]::ReadAllText($keyPath)

    # 签名逻辑
    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportFromPem($privateKey)

        # 使用 SHA256 + Pkcs1 签名
        $signatureBytes = $rsa.SignData($dllBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $signatureBase64 = [Convert]::ToBase64String($signatureBytes)

        # 构造附加数据
        $marker = [System.Text.Encoding]::UTF8.GetBytes("KEYASIO-SIG-FD452153-5573-41BD-AB59-B4F324297128:")
        $sigBytes = [System.Text.Encoding]::UTF8.GetBytes($signatureBase64)

        # 追加到文件末尾
        $fs = [System.IO.FileStream]::new($dllPath, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write)
        try {
            $fs.Write($marker, 0, $marker.Length)
            $fs.Write($sigBytes, 0, $sigBytes.Length)
        }
        finally {
            $fs.Dispose()
        }

        Write-Host "✅ Signed successfully. Signature appended to: $([System.IO.Path]::GetFileName($dllPath))"
    }
    finally {
        $rsa.Dispose()
    }
}

function FindMarkerPosition($data, $pattern) {
    # data and pattern are byte arrays
    # Naive search from end
    for ($i = $data.Length - $pattern.Length; $i -ge 0; $i--) {
        $match = $true
        for ($j = 0; $j -lt $pattern.Length; $j++) {
            if ($data[$i + $j] -ne $pattern[$j]) {
                $match = $false
                break
            }
        }
        if ($match) {
            return $i
        }
    }
    return -1
}

function VerifyFile($dllPath, $keyPath) {
    if (-not (Test-Path $dllPath)) { throw [System.IO.FileNotFoundException]::new("Dll not found", $dllPath) }
    if (-not (Test-Path $keyPath)) { throw [System.IO.FileNotFoundException]::new("Public key not found", $keyPath) }

    $dllPath = Convert-Path $dllPath
    $keyPath = Convert-Path $keyPath

    $fileBytes = [System.IO.File]::ReadAllBytes($dllPath)
    $publicKey = [System.IO.File]::ReadAllText($keyPath)
    $marker = [System.Text.Encoding]::UTF8.GetBytes("KEYASIO-SIG-FD452153-5573-41BD-AB59-B4F324297128:")

    $markerPos = FindMarkerPosition $fileBytes $marker

    if ($markerPos -eq -1) {
        Write-Host "⚠️  No embedded signature found in this file."
        exit 1
    }

    # 分离原始数据和签名
    # PowerShell creates a copy for array slicing usually, but to be precise with types for VerifyData:
    $originalData = [byte[]]::new($markerPos)
    [Array]::Copy($fileBytes, 0, $originalData, 0, $markerPos)
    
    $sigOffset = $markerPos + $marker.Length
    $sigLen = $fileBytes.Length - $sigOffset

    # 获取 Base64 字符串
    $sigBase64 = [System.Text.Encoding]::UTF8.GetString($fileBytes, $sigOffset, $sigLen)
    
    try {
        $sigBytes = [Convert]::FromBase64String($sigBase64)
    }
    catch {
        throw [FormatException]::new("Corrupted signature format.")
    }

    # 验证
    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportFromPem($publicKey)

        $isValid = $rsa.VerifyData($originalData, $sigBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

        if ($isValid) {
            Write-Host "✅ Verification PASSED. The file is authentic."
        }
        else {
            Write-Host "❌ Verification FAILED. The file may have been tampered with."
            exit 1
        }
    }
    finally {
        $rsa.Dispose()
    }
}

try {
    switch ($Command) {
        "generate" {
            GenerateKeys
        }
        "sign" {
            if ($RestArgs.Count -lt 2) { Fail "Usage: sign <PathToDll> <PathToPrivateKey>" }
            SignFile $RestArgs[0] $RestArgs[1]
        }
        "verify" {
            if ($RestArgs.Count -lt 2) { Fail "Usage: verify <PathToDll> <PathToPublicKey>" }
            VerifyFile $RestArgs[0] $RestArgs[1]
        }
        default {
            Fail "Unknown command: $Command"
        }
    }
}
catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
