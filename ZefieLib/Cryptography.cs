using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZefieLib
{
    public class Cryptography
    {       
        //Preconfigured Encryption Parameters
        public static readonly int BlockBitSize = 128;
        public static readonly int KeyBitSize = 256;

        //Preconfigured Password Key Derivation Parameters
        public static readonly int SaltBitSize = 64;
        public static readonly int Iterations = 10000;
        public static readonly int MinPasswordLength = 12;

        /// <summary>
        /// Generates random bytes via RNGCryptoServiceProvider
        /// </summary>
        /// <param name="length">Number of bytes to generate</param>
        /// <returns>Random binary data</returns>
        public static byte[] GenerateCryptoBytes(int length = 4)
        {
            byte[] data = new byte[length];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(data);
            }
            return data;
        }
        /// <summary>
        /// Generates a random number via RNGCryptoServiceProvider
        /// </summary>
        /// <param name="min">Minimum number to generate</param>
        /// <param name="max">Maximum number to generate</param>
        /// <returns>A random number between <paramref name="min"/> and <paramref name="max"/></returns>
        public static int GenerateCryptoNumber(int min = 0, int max = Int32.MaxValue)
        {
            if (min > max) throw new ArgumentOutOfRangeException("min should not be greater than max");
            if (min == max) return min;
            long diff = (long)max - min;

            while (true)
            {
                byte[] uint32Buffer = GenerateCryptoBytes();
                uint rand = BitConverter.ToUInt32(uint32Buffer, 0);
                const long maxv = (1 + (long)int.MaxValue);
                long remainder = maxv % diff;
                if (rand < maxv - remainder)
                {
                    return (int)(min + (rand % diff));
                }
            }
        }
        /// <summary>
        /// Generates a random string via RNGCryptoServiceProvider
        /// </summary>
        /// <param name="length">Number of characters</param>
        /// <param name="chars">Characters to use in generation</param>
        /// <returns>A random string of characters</returns>
        public static string GenerateCryptoString(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        {
            char[] AvailableCharacters = chars.ToCharArray();
            char[] identifier = new char[length];
            byte[] randomData = GenerateCryptoBytes(length);

            for (int idx = 0; idx < identifier.Length; idx++)
            {
                int pos = randomData[idx] % AvailableCharacters.Length;
                identifier[idx] = AvailableCharacters[pos];
            }

            return new string(identifier);
        }
        /// <summary>
        /// Generates a random string via RNGCryptoServiceProvider
        /// </summary>
        /// <param name="length">Number of characters</param>
        /// <param name="append">Characters to append to the default set of characters</param>
        /// <returns>A random string of characters</returns>
        public static string GenerateCryptoString(int length, char[] append)
        {
            return Cryptography.GenerateCryptoString(length, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" + new String(append));
        }
        /// <summary>
        /// Generates a hexadecimal string via RNGCryptoServiceProvider
        /// </summary>
        /// <param name="bits">length in bits</param>
        /// <returns>A hexadecimal string</returns>
        public static string GenerateHash(int bits = 256)
        {
            return Cryptography.GenerateCryptoString((bits / 8), "0123456789ABCDEF");
        }
        /// <summary>
        /// Generates a key using getCryptoBytes
        /// </summary>
        /// <param name="bits">length in bits</param>
        /// <returns>Random binary data</returns>
        public static byte[] GenerateCryptoKey(int bits = 256)
        {
            return Cryptography.GenerateCryptoBytes(bits / 8);
        }
        /// <summary>
        /// Encrypts a string via AES
        /// </summary>
        /// <param name="toEncrypt">String to encrypt</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        /// <returns>Base64 encoded encrypted data</returns>
        public static string Encrypt(string toEncrypt, byte[] cryptKey, byte[] authKey, byte[] nonSecretPayload = null)
        {
            if (string.IsNullOrEmpty(toEncrypt))
                throw new ArgumentException("What are we encrypting?", "toEncrypt");

            var plainText = Encoding.UTF8.GetBytes(toEncrypt);
            var cipherText = Encrypt(plainText, cryptKey, authKey, nonSecretPayload);
            return Convert.ToBase64String(cipherText);
        }
        /// <summary>
        /// Encrypts data via AES
        /// </summary>
        /// <param name="toEncrypt">Data to encrypt</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        /// <returns>Encrypted binary data</returns>
        public static byte[] Encrypt(byte[] toEncrypt, byte[] cryptKey, byte[] authKey, byte[] nonSecretPayload = null)
        {
            //User Error Checks
            if (cryptKey == null || cryptKey.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KeyBitSize), "cryptKey");

            if (authKey == null || authKey.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KeyBitSize), "authKey");

            if (toEncrypt == null || toEncrypt.Length < 1)
                throw new ArgumentException("What are we encrypting?", "toEncrypt");

            //non-secret payload optional
            nonSecretPayload = nonSecretPayload ?? new byte[] { };

            byte[] cipherText;
            byte[] iv;

            using (var aes = new AesManaged
            {
                KeySize = KeyBitSize,
                BlockSize = BlockBitSize,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            })
            {

                //Use random IV
                aes.GenerateIV();
                iv = aes.IV;

                using (var encrypter = aes.CreateEncryptor(cryptKey, iv))
                using (var cipherStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(cipherStream, encrypter, CryptoStreamMode.Write))
                    using (var binaryWriter = new BinaryWriter(cryptoStream))
                    {
                        //Encrypt Data
                        binaryWriter.Write(toEncrypt);
                    }

                    cipherText = cipherStream.ToArray();
                }

            }

            //Assemble encrypted message and add authentication
            using (var hmac = new HMACSHA256(authKey))
            using (var encryptedStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(encryptedStream))
                {
                    //Prepend non-secret payload if any
                    binaryWriter.Write(nonSecretPayload);
                    //Prepend IV
                    binaryWriter.Write(iv);
                    //Write Ciphertext
                    binaryWriter.Write(cipherText);
                    binaryWriter.Flush();

                    //Authenticate all data
                    var tag = hmac.ComputeHash(encryptedStream.ToArray());
                    //Postpend tag
                    binaryWriter.Write(tag);
                }
                return encryptedStream.ToArray();
            }

        }
        /// <summary>
        /// Encrypts a string via AES
        /// </summary>
        /// <param name="toEncrypt">String to encrypt</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        /// <returns>Base64 encoded encrypted data</returns>
        public static string Encrypt(string toEncrypt, string password, byte[] nonSecretPayload = null)
        {
            if (string.IsNullOrEmpty(toEncrypt))
                throw new ArgumentException("What are we encrpyting?", "toEncrypt");

            var plainText = Encoding.UTF8.GetBytes(toEncrypt);
            var cipherText = Encrypt(plainText, password, nonSecretPayload);
            return Convert.ToBase64String(cipherText);
        }
        /// <summary>
        /// Encrypts data via AES
        /// </summary>
        /// <param name="toEncrypt">Data to encrypt</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        /// <returns>Encrypted binary data</returns>
        public static byte[] Encrypt(byte[] toEncrypt, string password, byte[] nonSecretPayload = null)
        {
            nonSecretPayload = nonSecretPayload ?? new byte[] { };

            //User Error Checks
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException(String.Format("Must have a password of at least {0} characters!", MinPasswordLength), "password");

            if (toEncrypt == null || toEncrypt.Length == 0)
                throw new ArgumentException("What are we encrypting?", "secretMessage");

            if (password.Length < MinPasswordLength)
            {
                while (password.Length < MinPasswordLength)
                    password += "\0";
            }


            var payload = new byte[((SaltBitSize / 8) * 2) + nonSecretPayload.Length];

            Array.Copy(nonSecretPayload, payload, nonSecretPayload.Length);
            int payloadIndex = nonSecretPayload.Length;

            byte[] cryptKey;
            byte[] authKey;
            //Use Random Salt to prevent pre-generated weak password attacks.
            using (var generator = new Rfc2898DeriveBytes(password, SaltBitSize / 8, Iterations))
            {
                var salt = generator.Salt;

                //Generate Keys
                cryptKey = generator.GetBytes(KeyBitSize / 8);

                //Create Non Secret Payload
                Array.Copy(salt, 0, payload, payloadIndex, salt.Length);
                payloadIndex += salt.Length;
            }

            //Deriving separate key, might be less efficient than using HKDF, 
            //but now compatible with RNEncryptor which had a very similar wireformat and requires less code than HKDF.
            using (var generator = new Rfc2898DeriveBytes(password, SaltBitSize / 8, Iterations))
            {
                var salt = generator.Salt;

                //Generate Keys
                authKey = generator.GetBytes(KeyBitSize / 8);

                //Create Rest of Non Secret Payload
                Array.Copy(salt, 0, payload, payloadIndex, salt.Length);
            }

            return Encrypt(toEncrypt, cryptKey, authKey, payload);
        }
        /// <summary>
        /// Decrypts an AES encrypted string
        /// </summary>
        /// <param name="toDecrypt">Base64 encoded encrypted data</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted string</returns>
        public static string Decrypt(string toDecrypt, byte[] cryptKey, byte[] authKey, int nonSecretPayloadLength = 0)
        {
            if (string.IsNullOrWhiteSpace(toDecrypt))
                throw new ArgumentException("What are we decrypting?", "encryptedMessage");

            var cipherText = Convert.FromBase64String(toDecrypt);
            var plainText = Decrypt(cipherText, cryptKey, authKey, nonSecretPayloadLength);
            return Encoding.UTF8.GetString(plainText);
        }
        /// <summary>
        /// Decrypts AES encrypted binary data
        /// </summary>
        /// <param name="toDecrypt">Data to decrypt</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted binary data</returns>
        public static byte[] Decrypt(byte[] toDecrypt, byte[] cryptKey, byte[] authKey, int nonSecretPayloadLength = 0)
        {

            //Basic Usage Error Checks
            if (cryptKey == null || cryptKey.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("CryptKey needs to be {0} bit!", KeyBitSize), "cryptKey");

            if (authKey == null || authKey.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("AuthKey needs to be {0} bit!", KeyBitSize), "authKey");

            if (toDecrypt == null || toDecrypt.Length == 0)
                throw new ArgumentException("What are we decrypting?", "encryptedMessage");

            using (var hmac = new HMACSHA256(authKey))
            {
                var sentTag = new byte[hmac.HashSize / 8];
                //Calculate Tag
                var calcTag = hmac.ComputeHash(toDecrypt, 0, toDecrypt.Length - sentTag.Length);
                var ivLength = (BlockBitSize / 8);

                //if message length is to small just return null
                if (toDecrypt.Length < sentTag.Length + nonSecretPayloadLength + ivLength)
                    return null;

                //Grab Sent Tag
                Array.Copy(toDecrypt, toDecrypt.Length - sentTag.Length, sentTag, 0, sentTag.Length);

                //Compare Tag with constant time comparison
                var compare = 0;
                for (var i = 0; i < sentTag.Length; i++)
                    compare |= sentTag[i] ^ calcTag[i];

                //if message doesn't authenticate return null
                if (compare != 0)
                    return null;

                using (var aes = new AesManaged
                {
                    KeySize = KeyBitSize,
                    BlockSize = BlockBitSize,
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7
                })
                {

                    //Grab IV from message
                    var iv = new byte[ivLength];
                    Array.Copy(toDecrypt, nonSecretPayloadLength, iv, 0, iv.Length);

                    using (var decrypter = aes.CreateDecryptor(cryptKey, iv))
                    using (var plainTextStream = new MemoryStream())
                    {
                        using (var decrypterStream = new CryptoStream(plainTextStream, decrypter, CryptoStreamMode.Write))
                        using (var binaryWriter = new BinaryWriter(decrypterStream))
                        {
                            //Decrypt Cipher Text from Message
                            binaryWriter.Write(
                                toDecrypt,
                                nonSecretPayloadLength + iv.Length,
                                toDecrypt.Length - nonSecretPayloadLength - iv.Length - sentTag.Length
                            );
                        }
                        //Return Plain Text
                        return plainTextStream.ToArray();
                    }
                }
            }
        }
        /// <summary>
        /// Decrypts an AES encrypted string
        /// </summary>
        /// <param name="toDecrypt">Base64 encoded encrypted data</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted string</returns>
        public static string Decrypt(string toDecrypt, string password, int nonSecretPayloadLength = 0)
        {
            if (string.IsNullOrWhiteSpace(toDecrypt))
                throw new ArgumentException("What are we decrypting?", "encryptedMessage");

            var cipherText = Convert.FromBase64String(toDecrypt);
            var plainText = Decrypt(cipherText, password, nonSecretPayloadLength);
            return Encoding.UTF8.GetString(plainText);
        }
        /// <summary>
        /// Decrypts AES encrypted binary data
        /// </summary>
        /// <param name="toDecrypt">Base64 encoded encrypted data</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted binary data</returns>
        public static byte[] Decrypt(byte[] toDecrypt, string password, int nonSecretPayloadLength = 0)
        {
            //User Error Checks
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException(String.Format("Must have a password of at least {0} characters!", MinPasswordLength), "password");

            if (toDecrypt == null || toDecrypt.Length == 0)
                throw new ArgumentException("What are we decrypting?!", "encryptedMessage");

            if (password.Length < MinPasswordLength)
            {
                while (password.Length < MinPasswordLength)
                    password += "\0";
            }

            var cryptSalt = new byte[SaltBitSize / 8];
            var authSalt = new byte[SaltBitSize / 8];

            //Grab Salt from Non-Secret Payload
            Array.Copy(toDecrypt, nonSecretPayloadLength, cryptSalt, 0, cryptSalt.Length);
            Array.Copy(toDecrypt, nonSecretPayloadLength + cryptSalt.Length, authSalt, 0, authSalt.Length);

            byte[] cryptKey;
            byte[] authKey;

            //Generate crypt key
            using (var generator = new Rfc2898DeriveBytes(password, cryptSalt, Iterations))
            {
                cryptKey = generator.GetBytes(KeyBitSize / 8);
            }
            //Generate auth key
            using (var generator = new Rfc2898DeriveBytes(password, authSalt, Iterations))
            {
                authKey = generator.GetBytes(KeyBitSize / 8);
            }

            return Decrypt(toDecrypt, cryptKey, authKey, cryptSalt.Length + authSalt.Length + nonSecretPayloadLength);
        }
        /// <summary>
        /// Encrypts binary data and saves it to a file
        /// </summary>
        /// <param name="filename">Output file</param>
        /// <param name="toEncrypt">Data to encrypt</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        public static void EncryptToFile(string filename, byte[] toEncrypt, byte[] cryptKey, byte[] authKey, byte[] nonSecretPayload = null)
        {
            byte[] data = Encrypt(toEncrypt, cryptKey, authKey, nonSecretPayload);
            FileStream f = System.IO.File.OpenWrite(filename);
            f.Write(data, 0, data.Length);
            f.Close();
        }
        /// <summary>
        /// Encrypts binary data and saves it to a file
        /// </summary>
        /// <param name="filename">Output file</param>
        /// <param name="toEncrypt">Data to encrypt</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        public static void EncryptToFile(string filename, byte[] toEncrypt, string password, byte[] nonSecretPayload = null)
        {
            byte[] data = Encrypt(toEncrypt, password, nonSecretPayload);
            FileStream f = System.IO.File.OpenWrite(filename);
            f.Write(data, 0, data.Length);
            f.Close();
        }
        /// <summary>
        /// Encrypts a string via AES and saves it to a file
        /// </summary>
        /// <param name="filename">Output file</param>
        /// <param name="toEncrypt">String to encrypt</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayload">Salt payload</param>
        public static void EncryptToFile(string filename, string toEncrypt, string password, byte[] nonSecretPayload = null)
        {
            byte[] data = Convert.FromBase64String(Encrypt(toEncrypt, password, nonSecretPayload));
            FileStream f = System.IO.File.OpenWrite(filename);
            f.Write(data, 0, data.Length);
            f.Close();
        }
        /// <summary>
        /// Encrypts a string via AES and saves it to a file
        /// </summary>
        /// <param name="filename">Outfile file</param>
        /// <param name="toEncrypt">String to encrypt</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        public static void EncryptToFile(string filename, string toEncrypt, byte[] cryptKey, byte[] authKey, byte[] nonSecretPayload = null)
        {
            EncryptToFile(filename, Encoding.UTF8.GetBytes(toEncrypt), cryptKey, authKey, nonSecretPayload);
        }
        /// <summary>
        /// Decrypts data from an AES encrypted file
        /// </summary>
        /// <param name="filename">Input file</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptFromFile(string filename, byte[] cryptKey, byte[] authKey, int nonSecretPayloadLength = 0)
        {
            FileStream f = System.IO.File.OpenRead(filename);
            byte[] data = new byte[f.Length];
            f.Read(data, 0, (int)f.Length);
            f.Close();
            return Decrypt(data, cryptKey, authKey, nonSecretPayloadLength);
        }
        /// <summary>
        /// Decrypts data from an AES encrypted file
        /// </summary>
        /// <param name="filename">Input file</param>
        /// <param name="cryptKey">First encryption key</param>
        /// <param name="authKey">Second encryption key</param>
        /// <param name="password">Encryption key string</param>
        /// <param name="nonSecretPayloadLength">Salt payload</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptFromFile(string filename, string password, int nonSecretPayloadLength = 0)
        {
            FileStream f = System.IO.File.OpenRead(filename);
            byte[] data = new byte[f.Length];
            f.Read(data, 0, (int)f.Length);
            f.Close();
            return Decrypt(data, password, nonSecretPayloadLength);
        }
        public class Hash
        {            
            /// <summary>
            /// Computes a SHA512 hash
            /// </summary>
            /// <param name="data">Data to hash</param>
            /// <returns>Hexadecimal SHA512 hash string</returns>
            public static string SHA512(byte[] data)
            {
                using (SHA512 shaM = new SHA512Managed())
                    return ZefieLib.Data.ByteToHex(shaM.ComputeHash(data));
            }
            /// <summary>
            /// Computes a SHA512 hash
            /// </summary>
            /// <param name="text">String or filename of file hash</param>
            /// <returns>Hexadecimal SHA512 hash string</returns>
            public static string SHA512(string s)
            {
                if (System.IO.File.Exists(s))
                {
                    int offset = 0;
                    byte[] block = new byte[ZefieLib.Data.BlockSize];
                    byte[] hash;
                    using (var f = new BufferedStream(new FileStream(s, FileMode.Open, FileAccess.Read)))
                    {
                        using (SHA512 shaM = new SHA512Managed())
                        {
                            // For each block:
                            while (offset + block.Length < f.Length)
                            {
                                f.Position = offset;
                                f.Read(block, 0, ZefieLib.Data.BlockSize);
                                offset += shaM.TransformBlock(block, 0, block.Length, null, 0);
                            }
                            int remain = (int)(f.Length - (long)offset);
                            block = new byte[remain];
                            f.Position = offset;
                            f.Read(block, 0, remain);
                            shaM.TransformFinalBlock(block, 0, block.Length);
                            hash = shaM.Hash;
                        }
                    }
                    return ZefieLib.Data.ByteToHex(hash);
                }
                else
                    return SHA512(Encoding.UTF8.GetBytes(s));
            }
            /// <summary>
            /// Computes a SHA384 hash
            /// </summary>
            /// <param name="data">Data to hash</param>
            /// <returns>Hexadecimal SHA384 hash string</returns>
            public static string SHA384(byte[] data)
            {
                using (SHA384 shaM = new SHA384Managed())
                    return ZefieLib.Data.ByteToHex(shaM.ComputeHash(data));
            }
            /// <summary>
            /// Computes a SHA384 hash
            /// </summary>
            /// <param name="text">String or filename of file hash</param>
            /// <returns>Hexadecimal SHA384 hash string</returns>
            public static string SHA384(string s)
            {
                if (System.IO.File.Exists(s))
                {
                    int offset = 0;
                    byte[] block = new byte[ZefieLib.Data.BlockSize];
                    byte[] hash;
                    using (var f = new BufferedStream(new FileStream(s, FileMode.Open, FileAccess.Read)))
                    {
                        using (SHA384 shaM = new SHA384Managed())
                        {
                            // For each block:
                            while (offset + block.Length < f.Length)
                            {
                                f.Position = offset;
                                f.Read(block, 0, ZefieLib.Data.BlockSize);
                                offset += shaM.TransformBlock(block, 0, block.Length, null, 0);
                            }
                            int remain = (int)(f.Length - (long)offset);
                            block = new byte[remain];
                            f.Position = offset;
                            f.Read(block, 0, remain);
                            shaM.TransformFinalBlock(block, 0, block.Length);
                            hash = shaM.Hash;
                        }
                    }
                    return ZefieLib.Data.ByteToHex(hash);
                }
                else
                    return SHA384(Encoding.UTF8.GetBytes(s));
            }
            /// <summary>
            /// Computes a SHA256 hash
            /// </summary>
            /// <param name="data">Data to hash</param>
            /// <returns>Hexadecimal SHA256 hash string</returns>
            public static string SHA256(byte[] data)
            {
                using (SHA256 shaM = new SHA256Managed())
                    return ZefieLib.Data.ByteToHex(shaM.ComputeHash(data));
            }
            /// <summary>
            /// Computes a SHA256 hash
            /// </summary>
            /// <param name="text">String or filename of file hash</param>
            /// <returns>Hexadecimal SHA256 hash string</returns>
            public static string SHA256(string s)
            {
                if (System.IO.File.Exists(s))
                {
                    int offset = 0;
                    byte[] block = new byte[ZefieLib.Data.BlockSize];
                    byte[] hash;
                    using (var f = new BufferedStream(new FileStream(s, FileMode.Open, FileAccess.Read)))
                    {
                        using (SHA256 shaM = new SHA256Managed())
                        {
                            // For each block:
                            while (offset + block.Length < f.Length)
                            {
                                f.Position = offset;
                                f.Read(block, 0, ZefieLib.Data.BlockSize);
                                offset += shaM.TransformBlock(block, 0, block.Length, null, 0);
                            }
                            int remain = (int)(f.Length - (long)offset);
                            block = new byte[remain];
                            f.Position = offset;
                            f.Read(block, 0, remain);
                            shaM.TransformFinalBlock(block, 0, block.Length);
                            hash = shaM.Hash;
                        }
                    }
                    return ZefieLib.Data.ByteToHex(hash);
                }
                else
                    return SHA256(Encoding.UTF8.GetBytes(s));
            }
            /// <summary>
            /// Computes a SHA1 hash
            /// </summary>
            /// <param name="data">Data to hash</param>
            /// <returns>Hexadecimal SHA1 hash string</returns>
            public static string SHA1(byte[] data)
            {
                using (SHA1 shaM = new SHA1Managed())
                    return ZefieLib.Data.ByteToHex(shaM.ComputeHash(data));
            }
            /// <summary>
            /// Computes a SHA1 hash
            /// </summary>
            /// <param name="text">String or filename of file hash</param>
            /// <returns>Hexadecimal SHA1 hash string</returns>
            public static string SHA1(string s)
            {
                if (System.IO.File.Exists(s))
                {
                    int offset = 0;
                    byte[] block = new byte[ZefieLib.Data.BlockSize];
                    byte[] hash;
                    using (var f = new BufferedStream(new FileStream(s, FileMode.Open, FileAccess.Read)))
                    {
                        using (SHA1 shaM = new SHA1Managed())
                        {
                            // For each block:
                            while (offset + block.Length < f.Length)
                            {
                                f.Position = offset;
                                f.Read(block, 0, ZefieLib.Data.BlockSize);
                                offset += shaM.TransformBlock(block, 0, block.Length, null, 0);
                            }
                            int remain = (int)(f.Length - (long)offset);
                            block = new byte[remain];
                            f.Position = offset;
                            f.Read(block, 0, remain);
                            shaM.TransformFinalBlock(block, 0, block.Length);
                            hash = shaM.Hash;
                        }
                    }
                    return ZefieLib.Data.ByteToHex(hash);
                }
                else
                    return SHA1(Encoding.UTF8.GetBytes(s));
            }
            /// <summary>
            /// Computes a MD5 hash
            /// </summary>
            /// <param name="data">Data to hash</param>
            /// <returns>Hexadecimal MD5 hash string</returns>
            public static string MD5(byte[] data)
            {
                using (MD5 md5 = new MD5CryptoServiceProvider())
                    return ZefieLib.Data.ByteToHex(md5.ComputeHash(data));
            }
            /// <summary>
            /// Computes a MD5 hash
            /// </summary>
            /// <param name="text">String or filename of file hash</param>
            /// <returns>Hexadecimal MD5 hash string</returns>
            public static string MD5(string s)
            {
                if (System.IO.File.Exists(s))
                {
                    int offset = 0;
                    byte[] block = new byte[ZefieLib.Data.BlockSize];
                    byte[] hash;
                    using (var f = new BufferedStream(new FileStream(s, FileMode.Open, FileAccess.Read)))
                    {
                        using (MD5 md5 = new MD5CryptoServiceProvider())
                        {
                            // For each block:
                            while (offset + block.Length < f.Length)
                            {
                                f.Position = offset;
                                f.Read(block, 0, ZefieLib.Data.BlockSize);
                                offset += md5.TransformBlock(block, 0, block.Length, null, 0);
                            }
                            int remain = (int)(f.Length - (long)offset);
                            block = new byte[remain];
                            f.Position = offset;
                            f.Read(block, 0, remain);
                            md5.TransformFinalBlock(block, 0, block.Length);
                            hash = md5.Hash;
                        }
                    } 
                    return ZefieLib.Data.ByteToHex(hash);
                }
                else
                    return MD5(Encoding.UTF8.GetBytes(s));
            }

        }
    }
}
