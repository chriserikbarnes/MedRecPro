
using System.Text;
using System.Security.Cryptography;

namespace MedRecPro.Helpers
{
    /// <summary>
    /// Provides encryption for the users logon credential
    /// </summary>
    /// 

    public class StringCipher
    {

        #region FIPS compliance info
        /* NOTE:
        * 
        * C# Under Windows has basically 3 encryption providers that "support" 
        * AES: RijndaelManaged, AesManaged, AesCryptoServiceProvider.
        * 
        * RijndaelManaged implements the full Rijnadael Algorithm (All Options) 
        * and so it is a super-set of AES capabilities; however, it is not 
        * certified FIPS compliant (because it is capable of doing things not in 
        * the FIPS-approved AES specification, like having block size other than 128 bits)
        * 
        * AesManaged is nothing more than a decorator/wrapper over RijndaelManaged that 
        * restrict it to a block-size of 128 bits, but, because RijndaelManaged is 
        * not FIPS approved, neither is AesManaged
        * 
        * AesCryptoServiceProvider is a C# wrapper over the C-library on Windows 
        * for AES that IS FIPS approved; however, in CFB Mode, it only supports 
        * 8|16|24|32|40|48|56|64 bits for the FeedbackSize (I can find no documentation 
        * that says that FIPS is restricted thusly, so, it's questionable how 
        * AesCryptoServiceProvider passsed the FIPS certification 
        * 
        * If FIPS mode is turned on on Windows, then RijndaelManaged (and thereby AesManaged) 
        * will throw and exception saying they are not FIPS compliant when you attempt to 
        * instantiate them.
        * 
        * Some things require AES-128 with CFB of 128-bits FeedbackSize (e.g. SNMPv3 AES according the the RFC).
        * 
        * So, if you are in an environment where the following is true:
        * 
        *      You need AES-128 with CFB-128 (SNMPv3 for example)
        *      You need to do the Crypto from C# without using Non-Microsoft Libs
        *      You need to have FIPS mode turned on on the OS (Gov't requirements for example)
        * 
        * Then, your ONLY option is to use RijndaelManaged AND use 
        * the "<configuration> <runtime> <enforceFIPSPolicy enabled="false"/> <runtime> </configuration>" 
        * in the Application.exe.config to turn-off FIPS forced compliance for that particular application.
        * 
        * https://stackoverflow.com/questions/939040/when-will-c-sharp-aes-algorithm-be-fips-compliant
        */
        #endregion

        #region vars/properties
        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;
        #endregion

        /******************************************************/
        /// <summary>
        /// Returns an encrypted value for the passed string.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="passPhrase"></param>
        /// <returns></returns>
        /// <remarks>According to online sources, FIPS compliance requires
        /// a 128 bit block size. 
        /// 
        /// https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp/10177020#10177020
        /// https://stackoverflow.com/questions/939040/when-will-c-sharp-aes-algorithm-be-fips-compliant
        /// </remarks>
        public static string Encrypt(string plainText, string passPhrase)
        {
            #region implementation
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = Generate128BitsOfRandomEntropy();
            var ivStringBytes = Generate128BitsOfRandomEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new AesCryptoServiceProvider())
                {
                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();

                                // Create the final bytes as a concatenation of the random 
                                //salt bytes, the random iv bytes and the cipher bytes.
                                var cipherTextBytes = saltStringBytes;

                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();

                                memoryStream.Close();
                                cryptoStream.Close();

                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a clear text result from the passed encrypted string.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="passPhrase"></param>
        /// <returns></returns>
        /// <remarks>
        /// https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp/10177020#10177020
        /// https://stackoverflow.com/questions/939040/when-will-c-sharp-aes-algorithm-be-fips-compliant
        /// </remarks>
        protected internal string Decrypt(string cipherText, string passPhrase)
        {
            #region implementation
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
            // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
            // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                string text;
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new AesCryptoServiceProvider())
                {
                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            using (var streamReader = new StreamReader(cryptoStream, Encoding.UTF8))
                            {
                                try
                                {
                                    text = streamReader.ReadToEnd();
                                }
                                catch (System.Security.Cryptography.CryptographicException e)
                                {

                                    ErrorHelper.AddErrorMsg("StringCipher.Decrypt: " + e.Message);
                                    text = "Can't decode because the secret was wrong";
                                    throw new Exception(text);
                                }

                                return text;
                            }
                        }
                    }
                }
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Adds randomness to the the encrypted value.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp/10177020#10177020
        /// https://stackoverflow.com/questions/939040/when-will-c-sharp-aes-algorithm-be-fips-compliant
        /// </remarks>
        private static byte[] Generate128BitsOfRandomEntropy()
        {
            #region implementation
            var randomBytes = new byte[16]; // 16 Bytes will give us 128 bits.
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                // Fill the array with cryptographically secure random bytes.
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
            #endregion
        }
    }
}