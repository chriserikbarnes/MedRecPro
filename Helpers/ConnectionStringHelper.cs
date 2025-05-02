using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Configuration;


public class AppSettings
{
    public string? DatabaseCutoverDate { get; set; }
    public string? DatabaseCredentialDev { get; set; }
    public string? DatabaseCredentialHf { get; set; }
    public string? DatabaseCredentialProd { get; set; }

}

namespace MedRecPro.Helpers
{


    /// <summary>
    /// Static class to help in the construction of the connection strings
    /// </summary>
    public static class ConnectionString
    {

        private static string? DatabaseKey = "PlaceholderKey";

        private static readonly IConfigurationRoot _config = new ConfigurationBuilder()
              .AddJsonFile(Path.GetFullPath("appsettings.json"), true, true)
              .Build();

        public sealed class Credential
        {
            /// <summary>
            /// The username for the account
            /// </summary>
            public string? UserName { get; set; } = null;
            /// <summary>
            /// Password for the account
            /// </summary>
            public string? Password { get; set; } = null;
        }

        /**************************************************************************/
        /// <summary>
        /// This fetches a connection string from web.config and selects
        /// the one based on the passed name. This helps in the migration of
        /// databases. The old server connection is post-fixed with the word
        /// old.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string? Get(string name)
        {
            #region implementation
            string? ret = null;
            string key = "ConnectionString.Get";

            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("Empty parameter was passed");
                }

                #region cached connection string
                key += name;
                key = key.GetHashString();

                ret = PerformanceHelper.GetCache<string>(key);

                if (!string.IsNullOrEmpty(ret))
                {
                    return ret;
                }
                #endregion

                #region build connection string

                //cutover date to change databases
                DateTime cutoverDate = new DateTime(2024, 8, 6); //default value year, month, day

                //cutover date defined in app config
                string? webConfigCutoverDate = _config.GetSection("appSettings")["DatabaseCutoverDate"];

                //cutover date
                ErrorHelper.AddErrorMsg("ConnectionString.Get (info): " + webConfigCutoverDate ?? "null");

                //Define the cutover date
                DateTime.TryParse(webConfigCutoverDate, out cutoverDate);

                //Check the current date
                string connectionStringName = DateTime.Now >= cutoverDate ? name : name + "-old";

                //return value
                ret = _config.GetConnectionString(connectionStringName) ?? string.Empty;

                //log info for connection
                ErrorHelper.AddErrorMsg("ConnectionString.Get (info): " + connectionStringName);
                ErrorHelper.AddErrorMsg("ConnectionString.Get (info): " + ret);

                //check for usage of username/password in formatted string
                if (!string.IsNullOrEmpty(ret)
                    && ret.Contains("User Id={0};Password={1};")
                    )
                {
                    ret = getDbCredentialedConnection(ret);
                }

                if (!string.IsNullOrEmpty(ret))
                {
                    PerformanceHelper.SetCache(key, ret);
                }

                #endregion

                // check for return
                if (string.IsNullOrEmpty(ret))
                {
                    throw new Exception("Empty connection string");
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("ConnectionString.Get (" + name ?? "null" + "): " + e);
            }

            return ret;
            #endregion
        }

        /**************************************************************************/
        private static string? getDbCredentialedConnection(string stringToFormat)
        {
            #region implementation
            string? ret = null;
            string? json, credential;
            Credential? credentialObj;

            try
            {

#if DEBUG || DEV
                if (stringToFormat.Contains("HangFire"))
                {
                    credential = _config.GetSection("appSettings")["DatabaseCredentialHf"];
                }
                else
                {
                    credential = _config.GetSection("appSettings")["DatabaseCredentialDev"];
                }
#else
                if (stringToFormat.Contains("HangFire"))
                {
                    credential = _config.GetSection("appSettings")["DatabaseCredentialHf"];
                }
                else
                {
                    credential = _config.GetSection("appSettings")["DatabaseCredentialProd"];
                }
#endif

                if (!string.IsNullOrEmpty(credential))
                {
                    //log encrypted credential
                    ErrorHelper.AddErrorMsg("ConnectionString.getDbCredentialedConnection (info): " + credential);

                    json = new StringCipher().Decrypt(credential, DatabaseKey);

                    if (!string.IsNullOrEmpty(json))
                    {
                        credentialObj = JsonConvert.DeserializeObject<Credential>(json);

                        if (credentialObj != null)
                        {
                            ret = string.Format(stringToFormat, credentialObj.UserName, credentialObj.Password);
                        }
                    } 
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("ConnectionString.getDbCredentialedConnection: " + e);
                throw new Exception(e.Message);
            }

            return ret;
            #endregion
        }

    }
}