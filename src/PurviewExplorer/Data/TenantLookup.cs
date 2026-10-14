// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace SardaCloud.PurviewExplorer.Data
{
    public enum TenantLookupStatus
    {
        /// <summary>The organization exists; <see cref="TenantLookupResult.TenantId"/> is its tenant ID.</summary>
        Found,

        /// <summary>Microsoft Entra ID says no organization has this domain.</summary>
        NotFound,

        /// <summary>The lookup couldn't be completed (network, proxy, unexpected answer); connect anyway.</summary>
        Unknown,
    }

    public sealed class TenantLookupResult
    {
        public TenantLookupStatus Status { get; set; }
        public string TenantId { get; set; }

        /// <summary>Why the status is <see cref="TenantLookupStatus.Unknown"/>.</summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Finds an organization's tenant ID from its domain with the public OpenID Connect metadata of Microsoft Entra ID,
    /// without signing in: <c>https://login.microsoftonline.com/{domain}/v2.0/.well-known/openid-configuration</c>.
    /// </summary>
    public static class TenantLookup
    {
        public static TenantLookupResult Resolve(CloudEnvironment environment, string domain, int timeoutMilliseconds = 10000)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(MetadataUrl(environment, domain));
                request.Method = "GET";
                request.Accept = "application/json";
                request.UserAgent = AppInfo.ProductAndVersion;
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;
                request.AllowAutoRedirect = false;
                if (request.Proxy != null) request.Proxy.Credentials = CredentialCache.DefaultCredentials;
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                        return Interpret((int)response.StatusCode, ReadBody(response));
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse error)
                {
                    using (error) return Interpret((int)error.StatusCode, ReadBody(error));
                }
            }
            catch (Exception ex) when (ex is WebException || ex is IOException || ex is UriFormatException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                return new TenantLookupResult { Status = TenantLookupStatus.Unknown, Message = ex.Message };
            }
        }

        internal static string MetadataUrl(CloudEnvironment environment, string domain) =>
            ConnectOptions.SignInHost(environment) + "/" + Uri.EscapeDataString((domain ?? "").Trim()) + "/v2.0/.well-known/openid-configuration";

        /// <summary>Reads the tenant ID from a metadata response, or recognizes "no such organization".</summary>
        internal static TenantLookupResult Interpret(int statusCode, string body)
        {
            JsonMap map = null;
            try { map = Json.Parse(body ?? "") as JsonMap; }
            catch (JsonException) { }

            if (statusCode == 200 && map?["token_endpoint"] is string endpoint
                && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Segments.Length > 1
                && Guid.TryParse(uri.Segments[1].TrimEnd('/'), out var tenant))
            {
                return new TenantLookupResult { Status = TenantLookupStatus.Found, TenantId = tenant.ToString() };
            }

            // AADSTS90002: tenant not found. The service answers 400 with error "invalid_tenant".
            bool notFound = string.Equals(map?["error"] as string, "invalid_tenant", StringComparison.OrdinalIgnoreCase)
                || (map?["error_codes"] is List<object> codes && codes.Exists(c => c is long l && l == 90002));
            if (notFound) return new TenantLookupResult { Status = TenantLookupStatus.NotFound };

            var description = map?["error_description"] as string;
            return new TenantLookupResult
            {
                Status = TenantLookupStatus.Unknown,
                Message = $"HTTP {statusCode}" + (string.IsNullOrWhiteSpace(description) ? "" : ": " + description.Split('\r', '\n')[0]),
            };
        }

        private static string ReadBody(HttpWebResponse response)
        {
            using (var stream = response.GetResponseStream())
            {
                if (stream == null) return "";
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    // The metadata is a few kilobytes; don't read more than 256 KB from anything unexpected.
                    var buffer = new char[1 << 18];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    return new string(buffer, 0, read);
                }
            }
        }
    }
}
