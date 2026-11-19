using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SebWindowsConfig.Utilities;

namespace SebWindowsConfig.Entities
{
	/// <summary>
	/// Which websites students can open, as the quick setup sees the URL filter: either any website, or only the exam
	/// site plus a list of allowed sites. Policies that don't fit that picture (block rules, regular expressions, inactive
	/// rules, wildcard hosts, the embedded content filter) were set up in the full editor and are left alone.
	/// </summary>
	internal class SiteAccess
	{
		private const string AllGoogleForms = ".docs.google.com/forms/*";

		internal bool Restricted { get; private set; }
		internal bool IsCustom { get; private set; }
		internal IList<string> AllowedSites { get; private set; } = new List<string>();

		/// <summary>
		/// Reads the URL filter from the given settings.
		/// </summary>
		internal static SiteAccess Read(IDictionary<string, object> settings)
		{
			var access = new SiteAccess
			{
				Restricted = Bool(settings, SEBSettings.KeyURLFilterEnable),
				IsCustom = Bool(settings, SEBSettings.KeyURLFilterEnableContentFilter)
			};

			if (settings.TryGetValue(SEBSettings.KeyURLFilterRules, out var value) && value is IEnumerable<object> rules)
			{
				foreach (var rule in rules.OfType<IDictionary<string, object>>())
				{
					var active = Bool(rule, SEBSettings.KeyURLFilterRuleActive);
					var regex = Bool(rule, SEBSettings.KeyURLFilterRuleRegex);
					var allow = rule.TryGetValue(SEBSettings.KeyURLFilterRuleAction, out var action) && action is int a && a == 1;
					var expression = rule.TryGetValue(SEBSettings.KeyURLFilterRuleExpression, out var e) ? (e as string)?.Trim() : null;

					// Anything quick setup can't show faithfully (e.g. "*", which allows every website) counts as custom.
					if (!active || regex || !allow || !IsValidSite(expression))
					{
						access.IsCustom = true;
					}
					else
					{
						access.AllowedSites.Add(expression);
					}
				}
			}

			return access;
		}

		/// <summary>
		/// The filter expressions that let students use the exam itself: its exact host and port (a leading dot means no
		/// other subdomains). For a Google Form only that form's pages are allowed, not all of Google.
		/// </summary>
		internal static IList<string> ExamSiteRules(string startUrl)
		{
			var rules = new List<string>();

			if (!TryParse(startUrl, out var uri))
			{
				return rules;
			}

			var host = uri.Host.ToLowerInvariant();
			var port = uri.IsDefaultPort ? "" : ":" + uri.Port;

			if (host == "forms.gle")
			{
				// A short link's form is only known after following it, so all forms have to stay reachable.
				rules.Add(".forms.gle" + uri.AbsolutePath);
				rules.Add(AllGoogleForms);
			}
			else if (host == "docs.google.com" && uri.AbsolutePath.StartsWith("/forms", StringComparison.OrdinalIgnoreCase))
			{
				var form = Regex.Match(uri.AbsolutePath, @"^/forms/(?:u/[^/]+/)?d/(e/)?([^/]+)", RegexOptions.IgnoreCase);

				if (form.Success)
				{
					var id = form.Groups[1].Value + form.Groups[2].Value;

					rules.Add($".docs.google.com/forms/d/{id}/*");
					rules.Add($".docs.google.com/forms/u/*/d/{id}/*");
				}
				else
				{
					rules.Add(AllGoogleForms);
				}
			}
			else
			{
				rules.Add("." + host + port);
			}

			return rules;
		}

		/// <summary>
		/// True for a short Google Forms link, which can't be limited to one form.
		/// </summary>
		internal static bool IsShortFormLink(string startUrl)
		{
			return TryParse(startUrl, out var uri) && uri.Host.Equals("forms.gle", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// A rule as people read it, without the leading dot that means "exactly this host".
		/// </summary>
		internal static string Display(string rule)
		{
			return rule.StartsWith(".") ? rule.Substring(1) : rule;
		}

		/// <summary>
		/// The sign-in site an exam on this link may need, e.g. Google sign-in for a Google Form, or <c>null</c>.
		/// </summary>
		internal static SignIn SignInFor(string startUrl)
		{
			if (!TryParse(startUrl, out var uri))
			{
				return null;
			}

			var host = uri.Host.ToLowerInvariant();

			if (host == "forms.gle" || host == "docs.google.com")
			{
				return new SignIn("Google", "accounts.google.com");
			}

			if (host == "forms.office.com" || host == "forms.microsoft.com" || host.EndsWith(".sharepoint.com"))
			{
				return new SignIn("Microsoft", "login.microsoftonline.com");
			}

			return null;
		}

		/// <summary>
		/// Checks one allowed site: a host name (no wildcards, so "*" can't allow every website), an optional port from
		/// 1 to 65535 and an optional path, which may use * wildcards.
		/// </summary>
		internal static bool IsValidSite(string site)
		{
			if (string.IsNullOrWhiteSpace(site) || site.Any(char.IsWhiteSpace))
			{
				return false;
			}

			var match = Regex.Match(site, @"^(?:[a-z][a-z0-9+.-]*://)?\.?(?<host>[^/:?#*]+)(?::(?<port>\d{1,5}))?(?<path>/[^?#]*)?$", RegexOptions.IgnoreCase);

			if (!match.Success || !match.Groups["host"].Value.Any(char.IsLetterOrDigit))
			{
				return false;
			}

			if (match.Groups["port"].Success && (!int.TryParse(match.Groups["port"].Value, out var port) || port < 1 || port > 65535))
			{
				return false;
			}

			try
			{
				// The editor converts every rule with this parser when saving; a rule it can't read would stop the save.
				return new SEBURLFilterExpression(site) != null;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool TryParse(string url, out Uri uri)
		{
			return Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out uri) && !string.IsNullOrEmpty(uri.Host);
		}

		private static bool Bool(IDictionary<string, object> settings, string key)
		{
			return settings.TryGetValue(key, out var value) && value is bool b && b;
		}

		internal class SignIn
		{
			internal SignIn(string provider, string site)
			{
				Provider = provider;
				Site = site;
			}

			internal string Provider { get; }
			internal string Site { get; }
		}
	}
}
