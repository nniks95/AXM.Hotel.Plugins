using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Security.Cryptography;
using System.IO;
using System.IO.Pipes;

namespace AXM.GenerateQRImage.Plugins
{
    public class CreateQRCodeImage : IPlugin
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public void Execute(IServiceProvider serviceProvider)
        {

            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)serviceFactory.CreateOrganizationService(context.UserId);
                if (context == null || serviceFactory == null || service == null)
                {
                    throw new InvalidPluginExecutionException("Falied to initialize necessary services.");
                }
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {
                    Entity testTable = context.InputParameters["Target"] as Entity;
                    if (context.MessageName.ToLower() == "create")
                    {
                        string firstName = testTable.GetAttributeValue<string>("axm_firstname");
                        string lastName = testTable.GetAttributeValue<string>("axm_lastname");
                        Guid id = testTable.Id;

                        string qrValue = $"{id}+{firstName}+{lastName}";
                        string encodedText = Uri.EscapeDataString(qrValue);
                        string qrCodeApiUrl = $"https://quickchart.io/qr?text={encodedText}&size=150x150";

                        string base64Image = Task.Run(() => GetQRCodeImageAsBase64Async(qrCodeApiUrl)).GetAwaiter().GetResult();
                        byte[] imageBytes = Convert.FromBase64String(base64Image);

                        testTable["axm_qrcodeimage"] = imageBytes;

                        string password = "Blabla123!@#"; // This should come from a secure location
                        byte[] salt = GenerateSalt(16);

                        string encryptedFirstName = EncryptString(firstName, 16, password, salt);
                        string encryptedLastName = EncryptString(lastName, 16, password, salt);

                        testTable["axm_encryptedfirstname"] = encryptedFirstName;
                        testTable["axm_encryptedlastname"] = encryptedLastName;

                        string decryptedFirstName = DecryptString(encryptedFirstName, 16, password, salt);
                        string decryptedLastName = DecryptString(encryptedLastName, 16, password, salt);

                        testTable["axm_decryptedfirstname"] = decryptedFirstName;
                        testTable["axm_decryptedlastname"] = decryptedLastName;
                            
                        service.Update(testTable);

                    }
                }

                }
            catch (Exception ex) 
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }

        }
        private async Task<string> GetQRCodeImageAsBase64Async(string qrCodeUrl)
        {
            // Set a timeout to avoid long waiting times
            httpClient.Timeout = TimeSpan.FromSeconds(10); // Adjust timeout as needed

            var response = await httpClient.GetAsync(qrCodeUrl);
            response.EnsureSuccessStatusCode();

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
            return Convert.ToBase64String(imageBytes);
        }

        private static byte[] GenerateSalt(int size)
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] key = new byte[size];
            rng.GetBytes(key);
            return key;
        }
        private static byte[] DeriveKeyFromPassword(string password, byte[] salt, int keySizeInBytes)
        {
            using var rfc2898 = new Rfc2898DeriveBytes(password, salt, 10000); // 10000 iterations
            return rfc2898.GetBytes(keySizeInBytes);
        }
        private static string EncryptString(string input, int keySizeInBytes, string password, byte[] salt)
        {
            byte[] derivedKey = DeriveKeyFromPassword(password,salt, keySizeInBytes);

            using Aes aes = Aes.Create();
            aes.Key = derivedKey;
            aes.GenerateIV();
            using MemoryStream ms = new();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (CryptoStream cryptoStream = new(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (StreamWriter encryptWriter = new(cryptoStream))
            {
                encryptWriter.Write(input);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        private static string DecryptString(string encryptedInput, int keySizeInBytes, string password, byte[] salt)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedInput);

            byte[] derivedKey = DeriveKeyFromPassword(password,salt,keySizeInBytes);
            using Aes aes = Aes.Create();
            byte[] iv  = new byte[aes.IV.Length];
            Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);

            aes.Key = derivedKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using MemoryStream ms = new(encryptedBytes, iv.Length, encryptedBytes.Length - iv.Length);
            using CryptoStream cs = new (ms, decryptor, CryptoStreamMode.Read);
            using StreamReader reader = new (cs);

            return reader.ReadToEnd();
        }
    }
}
