using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace KeyAsio.Secrets;

public static class SecretUtils
{
    private static bool? _isOfficialBuildCache;

    /// <summary>
    /// Verifies if the current entry assembly is an official build signed by the official private key.
    /// </summary>
    public static bool IsOfficialBuild()
    {
        if (_isOfficialBuildCache.HasValue) return _isOfficialBuildCache.Value;

        // If the public key is empty (Dev environment), it's not an official build.
        if (string.IsNullOrEmpty(global::Secrets.OfficialPublicKey))
        {
            return false;
        }

        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var dllPath = entryAssembly?.Location;
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                _isOfficialBuildCache = false;
                return false;
            }

            byte[] fileBytes = File.ReadAllBytes(dllPath);
            ReadOnlySpan<byte> fileSpan = fileBytes;

            // The signature marker
            var marker = "KEYASIO-SIG-FD452153-5573-41BD-AB59-B4F324297128:"u8;

            // Search for marker from the end
            int markerPos = fileSpan.LastIndexOf(marker);
            if (markerPos == -1)
            {
                _isOfficialBuildCache = false;
                return false;
            }

            // originalContent 指向 marker 之前的所有内容
            var originalContent = fileSpan.Slice(0, markerPos);

            // signaturePart 指向 marker 之后的所有内容
            var signatureOffset = markerPos + marker.Length;
            var signatureSpan = fileSpan.Slice(signatureOffset);

            if (signatureSpan.IsEmpty)
            {
                _isOfficialBuildCache = false;
                return false;
            }

            string signatureBase64 = Encoding.UTF8.GetString(signatureSpan);
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

            // Verify using RSA
            using var rsa = RSA.Create();
            rsa.ImportFromPem(global::Secrets.OfficialPublicKey);

            bool isValid = rsa.VerifyData(
                originalContent,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            _isOfficialBuildCache = isValid;
            return isValid;
        }
        catch
        {
            return false;
        }
    }
}