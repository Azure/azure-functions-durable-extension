// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DFTestBot
{
    class GitHubClient
    {
        static readonly HttpClient HttpClient;
        static readonly bool GitHubCommentsDisabled = false;

        static GitHubClient()
        {
            string gitHubCommentsEnabled = Environment.GetEnvironmentVariable("DISABLE_GITHUB_COMMENTS");
            if (!string.IsNullOrEmpty(gitHubCommentsEnabled))
            {
                bool.TryParse(gitHubCommentsEnabled, out GitHubCommentsDisabled);
            }

            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DFTestBot", "0.1.0"));
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        internal static async Task RefreshAccessToken()
        {
            //string jwt = GetJWTFromPrivateKeyFile(PathToPem);
            string jwt = await GetJWTFromPrivateKeyString();

            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            string accessTokensUrl = await GetAccessTokensUrl(jwt);
            string accessToken = await GetAccessToken(accessTokensUrl);
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        public static async Task<string> GetCommentBodyAsync(
            Uri commentIdApiUrl,
            ILogger log)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, commentIdApiUrl);
                
            using HttpResponseMessage response = await HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await RefreshAccessToken();
                await GetCommentBodyAsync(commentIdApiUrl, log);
            }
            else if (!response.IsSuccessStatusCode)
            {
                int statusCode = (int)response.StatusCode;
                string details = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to post comment: {statusCode}: {details}");
            }

            string json = await response.Content.ReadAsStringAsync();
            JObject jObject = JObject.Parse(json);
            string body = (string)jObject["body"];
            return body;
        }

        public static async Task PostCommentAsync(
            Uri commentApiUrl,
            string markdownComment,
            ILogger log)
        {
            string message = markdownComment;
            log.LogInformation($"Sending GitHub comment: {message}");

            if (!GitHubCommentsDisabled)
            {
                var newCommentPayload = new { body = message };
                using var request = new HttpRequestMessage(HttpMethod.Post, commentApiUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(newCommentPayload), Encoding.UTF8, "application/json"),
                };

                using HttpResponseMessage response = await HttpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await RefreshAccessToken();
                    await PostCommentAsync(commentApiUrl, markdownComment, log);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    int statusCode = (int)response.StatusCode;
                    string details = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to post comment: {statusCode}: {details}");
                }
            }
        }

        public static async Task PatchCommentAsync(
            Uri patchApiUrl,
            string currentCommentBody,
            string markdownComment,
            ILogger log)
        {
            string message = currentCommentBody + Environment.NewLine + Environment.NewLine;
            message += markdownComment;
            log.LogInformation($"Sending GitHub comment: {message}");

            if (!GitHubCommentsDisabled)
            {
                var newCommentPayload = new { body = message };
                using var request = new HttpRequestMessage(HttpMethod.Patch, patchApiUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(newCommentPayload), Encoding.UTF8, "application/json"),
                };

                using HttpResponseMessage response = await HttpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await RefreshAccessToken();
                    await PatchCommentAsync(patchApiUrl, currentCommentBody, markdownComment, log);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    int statusCode = (int)response.StatusCode;
                    string details = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to post comment: {statusCode}: {details}");
                }
            }
        }

            public static async Task<JObject> GetPullRequestInfoAsync(Uri pullRequestApiUrl)
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(pullRequestApiUrl);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await RefreshAccessToken();
                await GetPullRequestInfoAsync(pullRequestApiUrl);
            }
            string content = await GetResponseContentOrThrowAsync(response);
            return JObject.Parse(content);
        }

        static async Task<string> GetResponseContentOrThrowAsync(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                int statusCode = (int)response.StatusCode;
                HttpRequestMessage request = response.RequestMessage;
                throw new Exception($"HTTP request {request.Method} {request.RequestUri} failed: {statusCode}: {content}");
            }

            return content;
        }

        static async Task<string> GetJWTFromPrivateKeyString()
        {
            //string keyVaultUri = "https://vabachugithubkeyvault.vault.azure.net/";

            string keyVaultUri = Environment.GetEnvironmentVariable("KEY_VAULT_URI");
            string secretName = Environment.GetEnvironmentVariable("SECRET_NAME");

            var secretClient = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
            KeyVaultSecret keyVaultSecret = await secretClient.GetSecretAsync(secretName);
            string value = keyVaultSecret.Value;
            return GetAccessTokenFromPrivateKeyString(value);
        }

        public static string GetAccessTokenFromPrivateKeyString(string privateKeyPem)
        {
            byte[] privateKeyRaw = Convert.FromBase64String(privateKeyPem);

            // creating the RSA key 
            RSACryptoServiceProvider provider = new RSACryptoServiceProvider();

            provider.ImportRSAPrivateKey(new ReadOnlySpan<byte>(privateKeyRaw), out _);

            RsaSecurityKey rsaSecurityKey = new RsaSecurityKey(provider);

            // Generating the token 
            var now = DateTime.UtcNow;

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpochDate(now).ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var handler = new JwtSecurityTokenHandler();

            var token = new JwtSecurityToken
            (
                issuer: "83022", //App ID
                audience: "https://api.github.com",
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(10), // expires in 10 minutes
                new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256)
            );

            string installationToken = handler.WriteToken(token);
            return installationToken;
        }

        static async Task<string> GetAccessTokensUrl(string jwt)
        {
            string installationUrl = "https://api.github.com/app/installations";

            var request = new HttpRequestMessage(HttpMethod.Get, installationUrl);
            HttpResponseMessage response = await HttpClient.GetAsync(installationUrl);

            if ((int)response.StatusCode > 201)
            {
                return response.StatusCode.ToString();
            }

            dynamic json;
            using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.UTF8))
            {
                string content = await reader.ReadToEndAsync();
                try
                {
                    JArray jsonArray = JArray.Parse(content);
                    json = JObject.Parse(jsonArray[0].ToString());
                }
                catch (JsonReaderException e)
                {
                    throw new InvalidOperationException($"Invalid JSON: {e.Message}");
                }
            }

            if (json?.access_tokens_url == null)
            {
                throw new InvalidOperationException("Invalid access_tokens_url.");
            }

            string accessTokenUrl = json?.access_tokens_url;

            return accessTokenUrl;
        }

        static async Task<string> GetAccessToken(string accessTokensUrl)
        {
            string installationAccessTokenUrl = accessTokensUrl; // "https://api.github.com/app/installations/12133425/access_tokens";

            var request = new HttpRequestMessage(HttpMethod.Post, installationAccessTokenUrl);
            HttpResponseMessage response = await HttpClient.SendAsync(request);

            if ((int)response.StatusCode > 201)
            {
                return response.StatusCode.ToString();
            }

            dynamic json;
            using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.UTF8))
            {
                string content = await reader.ReadToEndAsync();
                try
                {
                    json = JObject.Parse(content);
                }
                catch (JsonReaderException e)
                {
                    throw new InvalidOperationException($"Invalid JSON: {e.Message}");
                }
            }

            if (json?.token == null)
            {
                throw new InvalidOperationException("Invalid token");
            }

            string token = json?.token;

            return token;
        }

        public static long ToUnixEpochDate(DateTime date) => new DateTimeOffset(date).ToUniversalTime().ToUnixTimeSeconds();
    }
}
