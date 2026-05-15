#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace OneFileBox.Services;

/// <summary>
/// Cross-platform password encryption service.
/// On Windows: uses DPAPI (CurrentUser scope).
/// On Linux: uses AES-GCM with a machine-specific key derived from machine ID.
/// </summary>
public static class DpapiService
{
    /// <summary>
    /// Encrypts a plaintext string.
    /// Returns a base64-encoded string suitable for JSON storage.
    /// </summary>
    public static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
#if WINDOWS
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                null,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
#else
            return EncryptLinux(plaintext);
#endif
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Password encryption failed");
            return plaintext;
        }
    }

    /// <summary>
    /// Decrypts an encrypted base64 string.
    /// Returns the plaintext on success, or the input as-is if decryption fails.
    /// </summary>
    public static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return encryptedBase64;

        try
        {
#if WINDOWS
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
#else
            return DecryptLinux(encryptedBase64);
#endif
        }
        catch (Exception ex)
        {
            Log.Debug("Password decryption failed (legacy password?): {Msg}", ex.Message);
            return encryptedBase64;
        }
    }

#if !WINDOWS
    private static string EncryptLinux(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var key = GetLinuxKey();
        using var aes = new AesGcm(key, 16);
        var ciphertext = new byte[plaintextBytes.Length];
        var nonce = new byte[12];
        Random.Shared.NextBytes(nonce);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, null);
        // Combine nonce + ciphertext and base64
        var combined = new byte[nonce.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length, ciphertext.Length);
        return Convert.ToBase64String(combined);
    }

    private static string DecryptLinux(string encryptedBase64)
    {
        var combined = Convert.FromBase64String(encryptedBase64);
        var nonce = new byte[12];
        var ciphertext = new byte[combined.Length - 12];
        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, ciphertext, 0, ciphertext.Length);
        var key = GetLinuxKey();
        using var aes = new AesGcm(key, 16);
        var plaintext = new byte[ciphertext.Length];
        aes.Decrypt(nonce, ciphertext, null, plaintext, null);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] GetLinuxKey()
    {
        var machineId = GetMachineId();
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(machineId + "OneFileBox_Salt"));
    }

    private static string GetMachineId()
    {
        try
        {
            return File.ReadAllText("/etc/machine-id").Trim();
        }
        catch
        {
            return Environment.MachineName;
        }
    }
#endif
}
