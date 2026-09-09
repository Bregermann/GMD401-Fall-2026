using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.PastePad
{
#if UNITY_2020_1_OR_NEWER

	// We cannot persist a ScriptableSingleton in Unity versions prior to 2020 because FilePathAttribute is internal.
	[FilePath("UserSettings/PastePadSettings.asset", FilePathAttribute.Location.ProjectFolder)]
#endif
	public class PastePadSettings : ScriptableSingleton<PastePadSettings>
	{
		private const int DefaultSizeValue = 24;

		[SerializeField]
		private bool m_WordWrap;
		public bool WordWrap { get => m_WordWrap; set => m_WordWrap = value; }

		[SerializeField]
		private bool m_ShowLineNumbers;
		public bool ShowLineNumbers { get => m_ShowLineNumbers; set => m_ShowLineNumbers = value; }

		[SerializeField]
		private Color m_DefaultColor = Color.white;
		public Color DefaultColor { get => m_DefaultColor; set => m_DefaultColor = value; }

		[SerializeField]
		private int m_DefaultSize = DefaultSizeValue;
		public int DefaultSize { get => m_DefaultSize; set => m_DefaultSize = value; }

		[SerializeField]
		private bool m_CaseSensitive;
		public bool CaseSensitive { get => m_CaseSensitive; set => m_CaseSensitive = value; }

#if UNITY_2020_1_OR_NEWER
		private string m_FilePath;
		private string m_FolderPath;

		private void Awake()
		{
			if (!File.Exists(GetFilePath()))
			{
				ResetDefaultsAndSave();
			}
			m_FilePath = Path.Combine(Application.dataPath, "..", GetFilePath());
			m_FolderPath = Path.GetDirectoryName(m_FilePath);
		}
#endif

		public void DrawGUI(bool isSeparateWindow)
		{
			Undo.RecordObject(this, nameof(PastePadSettings));

			m_WordWrap = EditorGUILayout.Toggle(PastePadContent.Settings.WordWrap, m_WordWrap);
			m_ShowLineNumbers = EditorGUILayout.Toggle(PastePadContent.Settings.LineNumbers, m_ShowLineNumbers);
			m_DefaultColor = EditorGUILayout.ColorField(PastePadContent.Settings.DefaultColor, m_DefaultColor);
			m_DefaultSize = Mathf.Max(1, EditorGUILayout.IntField(PastePadContent.Settings.DefaultSize, m_DefaultSize));
			m_CaseSensitive = EditorGUILayout.Toggle(PastePadContent.Settings.CaseSensitive, m_CaseSensitive);

			SheetLayout.DrawHorizontalLine();

#if UNITY_2020_1_OR_NEWER
			if (GUILayout.Button(PastePadContent.Settings.SaveChangesToDisk))
			{
				Save(true);
			}
			if (GUILayout.Button(PastePadContent.Settings.OpenFile))
			{
				Application.OpenURL(m_FilePath);
			}
			if (GUILayout.Button(PastePadContent.Settings.OpenFolder))
			{
				Application.OpenURL(m_FolderPath);
			}
#endif
			if (GUILayout.Button(PastePadContent.Settings.ResetDefaults))
			{
				if (EditorUtility.DisplayDialog("Paste Pad", "Reset settings to default values?", "Confirm", "Cancel"))
				{
					ResetDefaultsAndSave();
				}
			}
			if (!isSeparateWindow && GUILayout.Button(PastePadContent.Settings.OpenWindow))
			{
				PastePadSettingsEditorWindow.ShowWindow();
			}
		}

		private void ResetDefaultsAndSave()
		{
			m_WordWrap = false;
			m_ShowLineNumbers = false;
			m_DefaultColor = Color.white;
			m_DefaultSize = DefaultSizeValue;
			m_CaseSensitive = false;

#if UNITY_2020_1_OR_NEWER
			Save(true);
#endif
		}
	}
}
