using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GameDemo.Rogue
{
    [Serializable]
    public sealed class RoguePersistedSnapshot
    {
        public string versionTag = "1.0.0";
        public long savedAtUnixMs;
        public string crc32;
        public string saltBase64;
        public string ivBase64;
        public string cipherTextBase64;
        public RogueRunSnapshot snapshot; // legacy/plain fallback
    }

    public static class RogueRunPersistence
    {
        public static void Save(string path, RogueRunSnapshot snapshot, string versionTag = "1.0.0")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is empty", nameof(path));
            }

            snapshot = snapshot ?? new RogueRunSnapshot();
            string json = JsonUtility.ToJson(snapshot, true);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] crc = BitConverter.GetBytes(Crc32(plainBytes));
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(crc);
            }

            byte[] salt = RandomBytes(16);
            byte[] iv = RandomBytes(16);
            byte[] cipher = Encrypt(plainBytes, salt, iv);

            var payload = new RoguePersistedSnapshot
            {
                versionTag = versionTag,
                savedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                crc32 = Convert.ToBase64String(crc),
                saltBase64 = Convert.ToBase64String(salt),
                ivBase64 = Convert.ToBase64String(iv),
                cipherTextBase64 = Convert.ToBase64String(cipher),
                snapshot = null
            };

            string wrapperJson = JsonUtility.ToJson(payload, true);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, wrapperJson, Encoding.UTF8);
        }

        public static bool TryLoad(string path, out RogueRunSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "path is empty";
                return false;
            }
            if (!File.Exists(path))
            {
                error = "save file not found";
                return false;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            RoguePersistedSnapshot payload = JsonUtility.FromJson<RoguePersistedSnapshot>(json);
            if (payload == null)
            {
                error = "save payload is invalid";
                return false;
            }

            if (payload.snapshot != null && string.IsNullOrWhiteSpace(payload.cipherTextBase64))
            {
                snapshot = payload.snapshot;
                return true;
            }

            if (string.IsNullOrWhiteSpace(payload.cipherTextBase64) ||
                string.IsNullOrWhiteSpace(payload.saltBase64) ||
                string.IsNullOrWhiteSpace(payload.ivBase64))
            {
                error = "encrypted payload is missing";
                return false;
            }

            byte[] salt = Convert.FromBase64String(payload.saltBase64);
            byte[] iv = Convert.FromBase64String(payload.ivBase64);
            byte[] cipher = Convert.FromBase64String(payload.cipherTextBase64);
            byte[] plainBytes = Decrypt(cipher, salt, iv);

            if (!string.IsNullOrWhiteSpace(payload.crc32))
            {
                byte[] expected = Convert.FromBase64String(payload.crc32);
                byte[] actual = BitConverter.GetBytes(Crc32(plainBytes));
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(actual);
                }

                if (expected.Length != actual.Length)
                {
                    error = "checksum mismatch";
                    return false;
                }

                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i] != actual[i])
                    {
                        error = "checksum mismatch";
                        return false;
                    }
                }
            }

            snapshot = JsonUtility.FromJson<RogueRunSnapshot>(Encoding.UTF8.GetString(plainBytes));
            if (snapshot == null)
            {
                error = "snapshot decode failed";
                return false;
            }

            return true;
        }

        static byte[] Encrypt(byte[] plainBytes, byte[] salt, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey(salt);
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    return Transform(plainBytes, encryptor);
                }
            }
        }

        static byte[] Decrypt(byte[] cipherBytes, byte[] salt, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey(salt);
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    return Transform(cipherBytes, decryptor);
                }
            }
        }

        static byte[] Transform(byte[] data, ICryptoTransform transform)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var crypto = new CryptoStream(output, transform, CryptoStreamMode.Write))
            {
                input.CopyTo(crypto);
                crypto.FlushFinalBlock();
                return output.ToArray();
            }
        }

        static byte[] DeriveKey(byte[] salt)
        {
            string passphrase = Application.identifier + "|GameDemo|RogueSave";
            using (var derive = new Rfc2898DeriveBytes(passphrase, salt, 10000))
            {
                return derive.GetBytes(32);
            }
        }

        static byte[] RandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        static uint Crc32(byte[] data)
        {
            const uint poly = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    uint mask = (uint)-(int)(crc & 1);
                    crc = (crc >> 1) ^ (poly & mask);
                }
            }
            return ~crc;
        }
    }
}
