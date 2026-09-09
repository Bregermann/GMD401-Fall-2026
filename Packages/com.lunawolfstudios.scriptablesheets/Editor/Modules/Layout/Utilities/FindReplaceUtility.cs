using System;
using System.Text;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public static class FindReplaceUtility
	{
		// Counts the non-overlapping exact occurrences of a substring.
		public static int CountOccurrences(string text, string find, bool caseSensitive)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find))
			{
				return 0;
			}
			var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			var count = 0;
			var index = 0;
			while ((index = text.IndexOf(find, index, comparison)) != -1)
			{
				count++;
				index += find.Length;
			}
			return count;
		}

		// Replaces every exact occurrence of a substring, optionally ignoring case.
		public static string ReplaceOccurrences(string text, string find, string replace, bool caseSensitive)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(find))
			{
				return text;
			}
			replace = replace ?? string.Empty;
			if (caseSensitive)
			{
				return text.Replace(find, replace);
			}
			var builder = new StringBuilder();
			var index = 0;
			int found;
			while ((found = text.IndexOf(find, index, StringComparison.OrdinalIgnoreCase)) != -1)
			{
				builder.Append(text, index, found - index);
				builder.Append(replace);
				index = found + find.Length;
			}
			builder.Append(text, index, text.Length - index);
			return builder.ToString();
		}
	}
}
