using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public class PastePadContent : Content
	{
		public static class Window
		{
			public static readonly GUIContent Title = GetIconContent(EditorIcon.Edit, "Paste Pad", string.Empty);
			public static readonly GUIContent SettingsTitle = GetIconContent(EditorIcon.Settings, "Paste Pad Settings", string.Empty);

			public class ContextMenu
			{
				public static readonly GUIContent New = new GUIContent("New");
				public static readonly GUIContent Clear = new GUIContent("Clear");
				public static readonly GUIContent Copy = new GUIContent("Copy");
				public static readonly GUIContent Save = new GUIContent("Save");
				public static readonly GUIContent WordWrap = new GUIContent("Word Wrap");
				public static readonly GUIContent OpenSettings = new GUIContent("Open Settings Window");
			}
		}

		public static class Settings
		{
			public static readonly GUIContent WordWrap = new GUIContent("Word Wrap", "Enable word wrap by default in new Paste Pad windows.");
			public static readonly GUIContent LineNumbers = new GUIContent("Line Numbers", "Show line numbers by default in new Paste Pad windows.");
			public static readonly GUIContent DefaultColor = new GUIContent("Default Color", "The default color used by the Paste Pad color button.");
			public static readonly GUIContent DefaultSize = new GUIContent("Default Size", "The default value used by the Paste Pad size button.");
			public static readonly GUIContent CaseSensitive = new GUIContent("Case Sensitive", "Enable case sensitive find and replace by default in new Paste Pad windows.");

			public static readonly GUIContent SaveChangesToDisk = new GUIContent("Save Changes to Disk", "Flushes setting changes to disk.");
			public static readonly GUIContent OpenFile = new GUIContent("Open File", "Opens the settings file directly with your preferred text editor.");
			public static readonly GUIContent OpenFolder = new GUIContent("Open Folder", "Opens the UserSettings folder for this project.");
			public static readonly GUIContent ResetDefaults = new GUIContent("Reset Defaults", "Resets all settings to default values.\n\nYou cannot undo this action.");
			public static readonly GUIContent OpenWindow = new GUIContent("Open Window", "Opens a separate settings window.");
		}

		public static class Toolbar
		{
			public static readonly GUIContent New = GetIconContent(EditorIcon.Create, "Open a new Paste Pad window.");
			public static readonly GUIContent Copy = GetIconContent(EditorIcon.Copy, "Copy the text to the clipboard.");
			public static readonly GUIContent Save = GetIconContent(EditorIcon.Save, "Save the text to a file.");
			public static readonly GUIContent WordWrap = GetIconContent(EditorIcon.WordWrap, "Toggle word wrap.");
			public static readonly GUIContent LineNumbers = GetIconContent(EditorIcon.LineNumbers, "Toggle line numbers.");
			public static readonly GUIContent Clear = GetIconContent(EditorIcon.Clear, "Clear the text.");
			public static readonly GUIContent Find = new GUIContent("Find", "Count the occurrences of the find text.");
			public static readonly GUIContent Replace = new GUIContent("Replace", "Replace all occurrences of the find text with the replace text.");
			public static readonly GUIContent CaseSensitive = new GUIContent("Aa", "Toggle case sensitivity for find and replace.");
			public static readonly GUIContent Bold = new GUIContent("B", "Wrap the selected text in bold tags.");
			public static readonly GUIContent Italic = new GUIContent("I", "Wrap the selected text in italic tags.");
			public static readonly GUIContent Color = new GUIContent("Color", "Wrap the selected text in a color tag using the chosen color.");
			public static readonly GUIContent Size = new GUIContent("Size", "Wrap the selected text in a size tag using the chosen size.");
		}
	}
}
