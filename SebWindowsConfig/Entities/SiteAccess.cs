using System;
using System.Collections.Generic;
using System.Linq;

namespace SebWindowsConfig.Entities
{
	/// <summary>
	/// Which websites students can open, as the quick setup sees the URL filter: either any website, or only the exam
	/// site plus a list of allowed sites. Rules that don't fit that picture (block rules, regular expressions, inactive
	/// rules, the embedded content filter) were set up in the full editor and are left alone.
	/// </summary>
	internal class SiteAccess
	{
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
					var expression = rule.TryGetValue(SEBSettings.KeyURLFilterRuleExpression, out var e) ? e as string : null;

					if (!active || regex || !allow || string.IsNullOrWhiteSpace(expression))
					{
						access.IsCustom = true;
					}
					else
					{
						access.AllowedSites.Add(expression.Trim());
					}
				}
			}

			return access;
		}

		/// <summary>
		/// The filter expressions that let students use the exam itself. For a Google Form only the form pages are allowed,
		/// not all of docs.google.com, so students can't open their own documents.
		/// </summary>
		internal static IList<string> ExamSiteRules(string startUrl)
		{
			var rules = new List<string>();

			if (!Uri.TryCreate((startUrl ?? "").Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
			{
				return rules;
			}

			var host = uri.Host.ToLowerInvariant();

			if (host == "forms.gle")
			{
				rules.Add("forms.gle");
				rules.Add(GoogleForms);
			}
			else if (host == "docs.google.com" && uri.AbsolutePath.StartsWith("/forms", StringComparison.OrdinalIgnoreCase))
			{
				rules.Add(GoogleForms);
			}
			else
			{
				rules.Add(host.StartsWith("www.") ? host.Substring(4) : host);
			}

			return rules;
		}

		/// <summary>
		/// The sign-in site an exam on this link may need, e.g. Google sign-in for a Google Form, or <c>null</c>.
		/// </summary>
		internal static SignIn SignInFor(string startUrl)
		{
			if (!Uri.TryCreate((startUrl ?? "").Trim(), UriKind.Absolute, out var uri))
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
		/// A simple check of one line typed as an allowed site: a host name, optionally with a path or <c>*</c> wildcards.
		/// </summary>
		internal static bool IsValidSite(string site)
		{
			return !string.IsNullOrWhiteSpace(site) && !site.Any(char.IsWhiteSpace) && site.Any(char.IsLetterOrDigit);
		}

		private const string GoogleForms = "docs.google.com/forms/*";

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
