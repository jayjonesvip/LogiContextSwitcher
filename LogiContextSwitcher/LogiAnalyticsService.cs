
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Newtonsoft.Json.Linq;

namespace LogiContextSwitcher
{
    public class KeyVaultService
    {
        public string GetSecret(string key)
        {
            var keyVaultClient = GetSecretClient();
            var result = keyVaultClient.GetSecret(key);

            return result.Value.Value;
        }

        private SecretClient GetSecretClient()
        {
            return new SecretClient(
                vaultUri : new Uri("https://spendmendvault-prod.vault.azure.net") ,
                credential : new ClientSecretCredential("841d81a5-896e-40d2-b3a1-8185b9135b7d",
                    "f6307ae9-380e-4e24-bc93-064857b2a025", 
                    "thnkbWSGKq1S0foH/V5+ju7Avhk1Ym8VkrkSQLVOtLM="));
        }
    }

    public class LogiAnalyticsService
    {
        private readonly KeyVaultService _keyVaultService;
        private readonly string _adminAuthToken; 
        private readonly string _clientAuthToken; 

        public LogiAnalyticsService()
        {
            _keyVaultService = new KeyVaultService();
            _adminAuthToken = "YWRtaW46THlSMWMhNDJkQHk4OQ==";
	        _clientAuthToken = "YzJhZDE0MTgtM2QxYS00Mzg4LWEyMTYtOTY2OTBlZWJjNTQwOklWSzNZOG5GdGJKZjVSa0h2VVNUZ2xIM0tHZVc3ZGxZeWozTA==";
        }

        public string GetConnString()
        {
            return _keyVaultService.GetSecret("SMPortalProd");
        }

        public bool VpnCheck()
        {

               return ((NetworkInterface.GetIsNetworkAvailable())
                    && NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(ni => ni.Description.Contains("spendmend-prod"))?.OperationalStatus == OperationalStatus.Up);
            
        }


        public string SyncUserSessionAsync(string username, string groupCode) 
        {

            if (string.IsNullOrEmpty(username)) throw new Exception("User not found...");

            var connectionParams = GetConnectionParams(groupCode);

            var attributes = new Dictionary<string, string>
            {
                {"Server", connectionParams.GetValueOrDefault("server")?.Replace("tcp:", "").Replace(",1433", "")},
                {"GroupCode", groupCode},
                {"DbUser", connectionParams.GetValueOrDefault("user id")},
                {"DbPassword", connectionParams.GetValueOrDefault("password")}
            };

            var logiUsers = GetLogiUsers();
            var user = logiUsers?.FirstOrDefault(x => x.name == username);
            
            if (user == null)
            {
                return "User does not exist in Logi. Please contact support.";
            }
            else 
            {
                try
                {
                    var jArray = JArray.FromObject(attributes.Select(x => new
                    {
                        key = x.Key,
                        value = x.Value,
                        encrypted = true
                    }));
                    
                    user.accounts[0].userAttributes = jArray;
                    HttpStatusCode result = PutLogiUser(user);

                    if (result == HttpStatusCode.OK)
                    {
                        return $"You context has been successfully switched to {groupCode.ToUpper()}!"; 

                    }
                    else
                    {
                        return $"Switching to {groupCode.ToUpper()}: {result}";
                    }
                }

                catch (Exception ex)
                {
                    if (!VpnCheck())
                    {
                        return "Please connect to the SpendMend VPN first..";
                    }

                    return $"There was an error switching your context. Please contact support. {ex.Message}";
                }
            }

            
        }


        public string GetTrustedAccessTokenJson(string logiUsername) 
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("authorization",$"Basic {_clientAuthToken}");
            var result =  httpClient
                .PostAsync("http://10.0.2.55:8080/composer/api/trusted-access/token",
                    new StringContent(JsonConvert.SerializeObject(new { username =  logiUsername }), Encoding.UTF8, "application/json")
                ).Result;

            return result.Content.ReadAsStringAsync().Result;
        }

        private string GetLogiGroupId(bool isCustomer = false) 
        { 
            using var httpClient = new HttpClient();

	        httpClient.DefaultRequestHeaders.Add("authorization", $"Basic {_adminAuthToken}");
	        var groups = httpClient
		        .GetAsync("http://10.0.2.55:8080/composer/api/groups").Result;

	        var groupsJson = groups.Content.ReadAsStringAsync().Result;
	
	        return (JsonConvert.DeserializeObject<IEnumerable<dynamic>>(groupsJson))
                ?.FirstOrDefault(x => 
                    (isCustomer && x.label == "CUST" )
                    || (!isCustomer && x.label == "INTERNAL")
                    )?.id;

        }

        private List<dynamic> GetLogiUsers() 
        { 
            using var httpClient = new HttpClient();

	        httpClient.DefaultRequestHeaders.Add("authorization", $"Basic {_adminAuthToken}");
	        var result = httpClient
		        .GetAsync($"http://10.0.2.55:8080/composer/api/users?limit=-1&offset=0").Result;

	        return JsonConvert.DeserializeObject<List<dynamic>>(result.Content.ReadAsStringAsync().Result);
        }

        private HttpStatusCode PutLogiUser(dynamic logiUser) 
        { 
            using var httpClient = new HttpClient();

            var ok = JsonConvert.SerializeObject(logiUser);

            httpClient.DefaultRequestHeaders.Add("authorization", $"Basic {_adminAuthToken}");
	        var result = httpClient.PutAsync($"http://10.0.2.55:8080/composer/api/users/{logiUser.id}",
                new StringContent(JsonConvert.SerializeObject(logiUser), Encoding.UTF8, "application/vnd.composer.v2+json"));

            return result.Result.StatusCode;
        }

        /// <summary>
        /// Gets and splits our "normal" connection string into its respective parts.
        /// </summary>
        /// <param name="groupCode"></param>
        /// <returns></returns>
        private Dictionary<string,string> GetConnectionParams(string groupCode) 
        {
            var connectionString = _keyVaultService.GetSecret($"customer-silo-{groupCode}-ODS");

            return connectionString.Split(';')
                .Where(kvp => kvp.Contains('='))
                .Select(kvp => kvp.Split(new char[] { '=' }, 2))
                .ToDictionary(kvp => kvp[0].Trim(),
                                kvp => kvp[1].Trim(),
                                StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Creates a new User in Logi and Returns the UserId.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="groupId"></param>
        /// <param name="attributeKeys"></param>
        /// <returns></returns>
        private string CreateUser(string username, string groupId, Dictionary<string,string> attributeKeys) {
             var user = new {
		            accountId = "609d313286c8fa20571644f2",
		            accounts = new object[] {
			            new {
				            accountId = "609d313286c8fa20571644f2",
				            groups = new string[] { groupId },
				            roles = new string[] {},
				            userAttributes = new object[] {
					            new { key = "", value = "", encrypted = true }
				            },
				            disabled = false, 
			            }
		            },
		            name = username,
		            activeGroups = new string[] { groupId },
		            activeRoles = new string[] { },
		            groups = new string[] { groupId },
		            system = false,
		            forcePasswordChange = false,
		            //userOrigin = "CONTEXT_SWITCHER",
		            availableAttributeKeys = attributeKeys.Select(x => new { 
                        key = x.Key, value = x.Value, encrypted = true
                    }),
		            password = ""
	            };

            using var httpClient = new HttpClient();

	        httpClient.DefaultRequestHeaders.Add("authorization", $"Basic {_adminAuthToken}");
            var result = httpClient
		        .PostAsync("http://10.0.2.55:8080/composer/api/users",
			        new StringContent(JsonConvert.SerializeObject(user), Encoding.UTF8, "application/vnd.composer.v2+json")
		        ).Result;
            var test = result.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<dynamic>(test).id;
        }
    }
}
