using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public static class PastePadLayout
	{
		private const float FieldMinWidth = 60f;
		private const float FieldMaxWidth = 180f;
		private const float FieldCaretPadding = 14f;

		public static readonly GUILayoutOption[] TextAreaOptions = { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) };
		public static readonly GUIStyle TextAreaStyle = new GUIStyle(EditorStyles.textArea);

		public static readonly GUIStyle LineNumberStyle = new GUIStyle(EditorStyles.textArea)
		{
			alignment = TextAnchor.UpperRight,
			wordWrap = false,
			normal = { background = null, textColor = new Color(0.5f, 0.5f, 0.5f) },
		};

		// Mirrors the text area wrapping with no padding so CalcHeight returns a clean multiple of the line height.
		public static readonly GUIStyle LineNumberMeasureStyle = new GUIStyle(EditorStyles.textArea)
		{
			wordWrap = true,
			padding = new RectOffset(0, 0, 0, 0),
		};

		public static readonly GUIStyle ToolbarBoldButton = new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold };
		public static readonly GUIStyle ToolbarItalicButton = new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Italic };

		public static readonly GUILayoutOption[] ToolbarIconButtonOptions = { GUILayout.Width(28) };
		public static readonly GUILayoutOption[] TagButtonOptions = { GUILayout.Width(24) };
		public static readonly GUILayoutOption[] LabeledTagButtonOptions = { GUILayout.Width(44) };
		public static readonly GUILayoutOption[] ColorFieldOptions = { GUILayout.Width(44) };
		public static readonly GUILayoutOption[] SizeFieldOptions = { GUILayout.Width(44) };
		public static readonly GUILayoutOption[] CaseToggleOptions = { GUILayout.Width(28) };

		public static GUILayoutOption[] GetFlexibleFieldWidth(string input)
		{
			var textWidth = EditorStyles.toolbarTextField.CalcSize(new GUIContent(input)).x;
			var width = Mathf.Clamp(textWidth + FieldCaretPadding, FieldMinWidth, FieldMaxWidth);
			return new GUILayoutOption[] { GUILayout.Width(width) };
		}
	}
}
