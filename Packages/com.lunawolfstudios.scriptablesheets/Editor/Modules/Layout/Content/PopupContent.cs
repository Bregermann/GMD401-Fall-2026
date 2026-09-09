using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public class PopupContent : Content
	{
		public static class Button
		{
			public static readonly GUIContent Cancel = GetIconContent(EditorIcon.Cancel, "Cancel (ESC)");
			public static readonly GUIContent Confirm = GetIconContent(EditorIcon.Confirm, "Confirm (Enter)");
			public static readonly GUIContent SelectAll = new GUIContent("Select All", "Select all Sheets");
			public static readonly GUIContent DeselectAll = new GUIContent("Deselect All", "Deselect all Sheets");
			public static readonly GUIContent Open = GetIconContent(EditorIcon.Open, " Open", "Open the selected Sheets");
			public static readonly GUIContent Delete = GetIconContent(EditorIcon.Delete, " Delete", "Delete the selected Sheets");
		}

		public static class Label
		{
			public static readonly GUIContent Rename = new GUIContent("New Name:");
			public static readonly GUIContent ColumnVisibility = new GUIContent("Columns");
			public static readonly GUIContent BatchSelect = new GUIContent("Sheets");
		}

		public static class Window
		{
			public static readonly int ColumnVisibilityRowsPerPage = 100;
			public static readonly float ColumnVisibilityMinWidth = 250;
			public static readonly float ColumnVisibilityPadding = 50;
			public static readonly Vector2 ColumnVisibilityMaxSize = new Vector2(600, 600);
			public static readonly Vector2 RenameSize = new Vector2(200, 100);
			public static readonly float BatchSelectMinWidth = 300;
			public static readonly float BatchSelectPadding = 50;
			public static readonly float BatchSelectActionButtonWidth = 80;
			public static readonly float BatchSelectActionButtonHeight = 20;
			public static readonly Vector2 BatchSelectMaxSize = new Vector2(600, 600);
		}
	}
}
