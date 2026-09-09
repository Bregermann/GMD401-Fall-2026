using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.PastePad
{
	public class PastePadSettingsProvider : SettingsProvider
	{
		[SettingsProvider]
		public static SettingsProvider CreatePastePadSettingsProvider()
		{
			return new PastePadSettingsProvider("Preferences/Paste Pad", SettingsScope.Project)
			{
				keywords = GetSearchKeywordsFromNestedStaticGUIContentFields<PastePadContent>()
			};
		}

		public PastePadSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope)
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		private void OnUndoRedoPerformed()
		{
			Repaint();
		}

		public override void OnGUI(string searchContext)
		{
			base.OnGUI(searchContext);
			PastePadSettings.instance.DrawGUI(false);
		}

		// Collects the keyword search terms from the nested static GUIContent fields so they stay in sync with the content.
		public static IEnumerable<string> GetSearchKeywordsFromNestedStaticGUIContentFields<T>()
		{
			var nestedTypes = typeof(T).GetNestedTypes(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			var guiContentFields = nestedTypes.SelectMany(n => n.GetFields(BindingFlags.Static | BindingFlags.Public)).Where(f => f.FieldType == typeof(GUIContent));
			var searchKeywords = guiContentFields.Select(f => ((GUIContent) f.GetValue(null)).text).Where(keyword => !string.IsNullOrEmpty(keyword));
			return searchKeywords;
		}
	}
}
