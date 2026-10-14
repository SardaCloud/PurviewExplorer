// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>The kinds of Connect-IPPSSession failure that Purview Explorer can explain.</summary>
    public enum ConnectFailure
    {
        /// <summary>Not recognized: the module's message is shown as it is.</summary>
        Other,

        /// <summary>The user closed or cancelled the sign-in.</summary>
        Cancelled,

        /// <summary>
        /// "Object reference not set to an instance of an object.": the service failed while setting up the session's
        /// permissions. Happens for guest accounts in some organizations; a search-only session avoids it.
        /// </summary>
        ServiceSetupFailed,

        /// <summary>"UnAuthorized": the service rejected the token for that organization (empty 401).</summary>
        Unauthorized,

        /// <summary>"No cmdlet assigned to the user...": signed in, but no Purview role in the organization.</summary>
        NoRole,

        /// <summary>
        /// "A window handle must be configured": Web Account Manager couldn't show its sign-in window. The module then
        /// keeps using it in the same process, even with -DisableWAM, so only a restart helps.
        /// </summary>
        WebAccountManager,

        /// <summary>
        /// "Unexpected character encountered while parsing value: &lt;": the service couldn't route to the
        /// organization (it answered with an HTML page), typically because the name isn't its domain.
        /// </summary>
        UnknownOrganization,
    }

    /// <summary>Turns the module's connection errors into explanations a user can act on.</summary>
    public static class ConnectionErrors
    {
        public static ConnectFailure Classify(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return ConnectFailure.Other;
            bool Has(string text) => message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

            if (Has("No cmdlet assigned to the user")) return ConnectFailure.NoRole;
            if (Has("Object reference not set to an instance of an object")) return ConnectFailure.ServiceSetupFailed;
            if (Has("Unexpected character encountered while parsing value: <") || Has("doesn't have Region/Version information"))
                return ConnectFailure.UnknownOrganization;
            if (Has("UnAuthorized")) return ConnectFailure.Unauthorized;
            if (Has("window handle must be configured")) return ConnectFailure.WebAccountManager;
            if (Has("authentication_canceled") || Has("User canceled") || Has("User cancelled") || Has("canceled by the user") || Has("cancelled by the user"))
                return ConnectFailure.Cancelled;
            return ConnectFailure.Other;
        }

        /// <summary>
        /// The text to show for a failed connection: an explanation followed by what the module reported.
        /// <paramref name="searchOnlyError"/> is the error of a search-only attempt made as a fallback, if any;
        /// <paramref name="searchOnlySupported"/> tells whether the installed module can make one.
        /// </summary>
        public static string Describe(string moduleMessage, ConnectOptions options, string searchOnlyError = null, bool searchOnlySupported = true)
        {
            var raw = string.IsNullOrWhiteSpace(moduleMessage) ? "The connection was cancelled." : moduleMessage.Trim();
            bool delegated = options != null && options.IsDelegated;
            string organization = options?.TargetOrganization;
            string inOrganization = organization != null ? "in " + organization : "in your organization";

            string explanation;
            switch (Classify(raw))
            {
                case ConnectFailure.Cancelled:
                    return "The sign-in was cancelled.";

                case ConnectFailure.NoRole:
                    explanation =
                        $"You signed in, but your account has no Microsoft Purview role {inOrganization}. Ask an administrator there " +
                        "to add you to a role group that can read the configuration (for example Compliance Administrator or " +
                        "Global Reader). If your role is eligible in Privileged Identity Management, activate it, wait a few " +
                        "minutes and connect again.";
                    break;

                case ConnectFailure.Unauthorized:
                    explanation = delegated
                        ? $"Security & Compliance PowerShell didn't accept your account for {organization}. Check that your " +
                          $"account is a guest in {organization} with a Purview role there, or that {organization} has a partner " +
                          "relationship (GDAP) with your organization that includes such a role."
                        : "Security & Compliance PowerShell didn't accept your account. Check that you signed in with the " +
                          "account you meant to, and that it has a Purview role in the organization.";
                    break;

                case ConnectFailure.UnknownOrganization:
                    explanation = organization != null
                        ? $"Security & Compliance PowerShell doesn't recognize {organization}. Enter the organization's primary " +
                          ".onmicrosoft.com domain (for example adatum.onmicrosoft.com), not another domain or its tenant ID."
                        : "Security & Compliance PowerShell couldn't find the organization of this account.";
                    break;

                case ConnectFailure.WebAccountManager:
                    explanation =
                        "Web Account Manager, the Windows sign-in window, couldn't open, and the ExchangeOnlineManagement module " +
                        "keeps trying it until Purview Explorer is closed. Close Purview Explorer, open it again, and in " +
                        "File > Connect to Tenant select \"Sign in with a browser window instead of Web Account Manager\".";
                    break;

                case ConnectFailure.ServiceSetupFailed:
                    if (!delegated)
                    {
                        explanation = "Microsoft's Security & Compliance service failed while setting up the session. Try again later.";
                    }
                    else if (searchOnlyError != null)
                    {
                        explanation =
                            $"Microsoft's Security & Compliance service failed while setting up your permissions in {organization}. " +
                            "This happens with guest accounts in some organizations. Purview Explorer then tried a search-only " +
                            "session, which avoids the problem, but that failed too:\r\n    " + searchOnlyError.Trim() +
                            $"\r\n\r\nAsk for an account in {organization} itself, or try again later.";
                    }
                    else if (!searchOnlySupported)
                    {
                        explanation =
                            $"Microsoft's Security & Compliance service failed while setting up your permissions in {organization}. " +
                            "This happens with guest accounts in some organizations. Purview Explorer can work around it with a " +
                            "search-only session, which needs ExchangeOnlineManagement 3.9.0 or later:\r\n    " +
                            "Update-Module ExchangeOnlineManagement";
                    }
                    else
                    {
                        explanation =
                            $"Microsoft's Security & Compliance service failed while setting up your permissions in {organization}. " +
                            $"This happens with guest accounts in some organizations. Ask for an account in {organization} itself, " +
                            "or try again later.";
                    }
                    break;

                default:
                    return searchOnlyError == null ? raw : raw + "\r\n\r\nA search-only session also failed:\r\n    " + searchOnlyError.Trim();
            }
            return explanation + "\r\n\r\nWhat the module reported:\r\n    " + raw;
        }
    }
}
