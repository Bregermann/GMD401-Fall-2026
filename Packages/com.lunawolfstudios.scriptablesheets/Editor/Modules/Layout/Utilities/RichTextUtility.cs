using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public static class RichTextUtility
	{
		// Wraps the text between two selection indices with the given tags, returning the range of the wrapped text.
		public static string WrapText(string text, int selectionA, int selectionB, string openTag, string closeTag, out int selectStart, out int selectEnd)
		{
			text = text ?? string.Empty;
			var start = Mathf.Clamp(Mathf.Min(selectionA, selectionB), 0, text.Length);
			var end = Mathf.Clamp(Mathf.Max(selectionA, selectionB), 0, text.Length);
			var selected = text.Substring(start, end - start);
			selectStart = start + openTag.Length;
			selectEnd = selectStart + selected.Length;
			return text.Substring(0, start) + openTag + selected + closeTag + text.Substring(end);
		}
	}
}
