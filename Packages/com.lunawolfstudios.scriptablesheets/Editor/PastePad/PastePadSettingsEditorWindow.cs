using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.PastePad
{
	public class PastePadSettingsEditorWindow : EditorWindow
	{
		private Vector2 m_ScrollPosition;

		public static void ShowWindow()
		{
			var window = GetWindow<PastePadSettingsEditorWindow>();
			window.titleContent = PastePadContent.Window.SettingsTitle;
			window.Show();
		}

		private void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		private void OnUndoRedoPerformed()
		{
			Repaint();
		}

		private void OnGUI()
		{
			m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
			PastePadSettings.instance.DrawGUI(true);
			EditorGUILayout.EndScrollView();
		}
	}
}
