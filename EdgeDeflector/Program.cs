/*
 * Copyright © 2017–2021 Daniel Aleksandersen
 * SPDX-License-Identifier: MIT
 * License-Filename: LICENSE
 */

using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web;

namespace EdgeDeflector
{
    class Program
    {
        static bool IsUri(string uristring)
        {
            try
            {
                Uri uri = new Uri(uristring);
                return true;
            }
            catch (UriFormatException)
            {
            }
            catch (ArgumentNullException)
            {
            }
            return false;
        }

        static bool IsHttpUri(string uri)
        {
            return uri.StartsWith("HTTPS://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsMsEdgeUri(string uri)
        {
            return uri.StartsWith("MICROSOFT-EDGE:", StringComparison.OrdinalIgnoreCase) && !uri.Contains(" ");
        }

        static bool IsNonAuthoritativeWithUrlQueryParameter(string uri)
        {
            return uri.Contains("microsoft-edge:?") && uri.Contains("&url=");
        }

        static string GetURIFromCortanaLink(string uri)
        {
            NameValueCollection queryCollection = HttpUtility.ParseQueryString(uri);
            return queryCollection["url"];
        }

        static string RewriteMsEdgeUriSchema(string uri)
        {
            RegistryKey engine_key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\EdgeUriDeflector", false);

            string engine = (string) engine_key?.GetValue("SearchEngine");
            string msedge_protocol_pattern = "^microsoft-edge:/*";

            Regex rgx = new Regex(msedge_protocol_pattern);
            string new_uri = rgx.Replace(uri, string.Empty);

            if (IsHttpUri(new_uri))
            {
                return ReplaceSearchEngine(new_uri, engine);
            }

            // May be new-style Cortana URI - try and split out
            if (IsNonAuthoritativeWithUrlQueryParameter(uri))
            {
                string cortanaUri = GetURIFromCortanaLink(uri);
                if (IsHttpUri(cortanaUri))
                {
                    // Correctly form the new URI before returning
                    return ReplaceSearchEngine(cortanaUri, engine);
                }
            }

            // defer fallback to web browser
            return "http://" + ReplaceSearchEngine(new_uri, engine);
        }

        static string ReplaceSearchEngine(string new_uri, string engine)
        {
            int index = new_uri.IndexOf("&");
            if (index > 0) new_uri = new_uri.Substring(0, index);

            string bing_search = "bing.com/search?q=";

            return engine switch
            {
                "Google" => new_uri.Replace(bing_search, "google.com/search?q="),
                "DuckDuckGo" => new_uri.Replace(bing_search, "duckduckgo.com/?q="),
                "Reddit" => new_uri.Replace(bing_search, "reddit.com/search?q="),
                _ => new_uri,
            };
        }

        static void OpenUri(string uri)
        {
            if (!IsUri(uri) || !IsHttpUri(uri))
            {
                Environment.Exit(1);
            }

            ProcessStartInfo launcher = new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            };
            Process.Start(launcher);
        }

        static void Main(string[] args)
        {
            // Assume argument is URI
            if (args.Length == 1 && IsMsEdgeUri(args[0]))
            {
                string uri = RewriteMsEdgeUriSchema(args[0]);
                OpenUri(uri);
            }
        }
    }
}
