#!/usr/bin/env dotnet-run
// 文件名: Signer.cs
// 运行方式: 
//    dotnet run Signer.cs -- generate
//    dotnet run Signer.cs -- sign ./bin/Release/net10.0/KeyAsio.dll ./private.key
//    dotnet run Signer.cs -- verify ./bin/Release/net10.0/KeyAsio.dll ./public.key

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// 检查参数
if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run Signer.cs -- [generate|sign|verify] [args...]");
    return;
}

var command = args[0];

try
{
    switch (command)
    {
        case "generate":
            GenerateKeys();
            break;
        case "sign":
            if (args.Length < 3) Fail("Usage: sign <PathToDll> <PathToPrivateKey>");
            SignFile(args[1], args[2]);
            break;
        case "verify":
            if (args.Length < 3) Fail("Usage: verify <PathToDll> <PathToPublicKey>");
            VerifyFile(args[1], args[2]);
            break;
        default:
            Fail($"Unknown command: {command}");
            break;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"[ERROR] {ex.Message}");
    Console.ResetColor();
    Environment.Exit(1);
}

// --- 核心逻辑方法 ---

void GenerateKeys()
{
    using var rsa = RSA.Create(2048);
    var privateKey = rsa.ExportRSAPrivateKeyPem();
    var publicKey = rsa.ExportRSAPublicKeyPem();

    File.WriteAllText("private.key", privateKey);
    File.WriteAllText("public.key", publicKey);

    Console.WriteLine("✅ Keys generated successfully.");
    Console.WriteLine("   private.key -> 🔒 Keep this SECRET in your CI/CD pipeline (GitHub Secrets).");
    Console.WriteLine("   public.key  -> 🌍 Embed this content into your KeyAsio.Secrets class.");
}

void SignFile(string dllPath, string keyPath)
{
    if (!File.Exists(dllPath)) throw new FileNotFoundException("Dll not found", dllPath);
    if (!File.Exists(keyPath)) throw new FileNotFoundException("Private key not found", keyPath);

    var dllBytes = File.ReadAllBytes(dllPath);
    var privateKey = File.ReadAllText(keyPath);

    // 签名逻辑
    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKey);

    // 使用 SHA256 + Pkcs1 签名
    var signatureBytes = rsa.SignData(dllBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var signatureBase64 = Convert.ToBase64String(signatureBytes);

    // 构造附加数据
    var marker = Encoding.UTF8.GetBytes("KEYASIO_SIG:");
    var sigBytes = Encoding.UTF8.GetBytes(signatureBase64);

    // 追加到文件末尾
    using (var fs = new FileStream(dllPath, FileMode.Append, FileAccess.Write))
    {
        fs.Write(marker);
        fs.Write(sigBytes);
    }

    Console.WriteLine($"✅ Signed successfully. Signature appended to: {Path.GetFileName(dllPath)}");
}

void VerifyFile(string dllPath, string keyPath)
{
    if (!File.Exists(dllPath)) throw new FileNotFoundException("Dll not found", dllPath);
    if (!File.Exists(keyPath)) throw new FileNotFoundException("Public key not found", keyPath);

    var fileBytes = File.ReadAllBytes(dllPath);
    var publicKey = File.ReadAllText(keyPath);
    var marker = Encoding.UTF8.GetBytes("KEYASIO_SIG:");

    int markerPos = FindMarkerPosition(fileBytes, marker);

    if (markerPos == -1)
    {
        Console.WriteLine("⚠️  No embedded signature found in this file.");
        Environment.Exit(1); 
    }

    // 分离原始数据和签名
    var originalData = fileBytes.AsSpan(0, markerPos);
    
    var sigOffset = markerPos + marker.Length;
    var sigLen = fileBytes.Length - sigOffset;

    // 获取 Base64 字符串
    var sigBase64 = Encoding.UTF8.GetString(fileBytes, sigOffset, sigLen);
    
    byte[] sigBytes;
    try { sigBytes = Convert.FromBase64String(sigBase64); }
    catch { throw new FormatException("Corrupted signature format."); }

    // 验证
    using var rsa = RSA.Create();
    rsa.ImportFromPem(publicKey);

    // 注意：VerifyData 需要原始数据
    // 这里我们用原始数据的 Span 直接验证，无需 new byte[]
    var isValid = rsa.VerifyData(originalData, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    if (isValid)
        Console.WriteLine("✅ Verification PASSED. The file is authentic.");
    else
    {
        Console.WriteLine("❌ Verification FAILED. The file may have been tampered with.");
        Environment.Exit(1);
    }
}

int FindMarkerPosition(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
{
    return data.LastIndexOf(pattern);
}

void Fail(string message)
{
    Console.WriteLine(message);
    Environment.Exit(1);
}