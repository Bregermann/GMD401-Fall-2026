using LunaWolfStudiosEditor.ScriptableSheets.Scanning;
using System.Linq;
using UnityEditor;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public static class AssetContextMenu
	{
		private const string MenuPath = "Assets/Open in Scriptable Sheets";
		private const int MenuPriority = 21;

		[MenuItem(MenuPath, false, MenuPriority)]
		private static void OpenInScriptableSheets()
		{
			var asset = Selection.activeObject;
			if (asset == null || !SheetAssetExtensions.TryGetSheetAsset(asset, out _))
			{
				return;
			}
			var windows = ScriptableSheetsEditorWindow.Instances;
			if (windows.Count == 0)
			{
				ScriptableSheetsEditorWindow.OpenInNewWindow(asset);
				return;
			}
			windows.OrderByDescending(w => w.FocusOrder).First().OpenAsset(asset);
		}

		// Unity cannot hide a selection-dependent menu item. It can only be disabled.
		[MenuItem(MenuPath, true, MenuPriority)]
		private static bool ValidateOpenInScriptableSheets()
		{
			var asset = Selection.activeObject;
			return asset != null && AssetDatabase.Contains(asset) && SheetAssetExtensions.TryGetSheetAsset(asset, out _);
		}
	}
}
