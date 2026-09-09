using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Importers
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(GoogleSheetsImporter))]
	public class GoogleSheetsImporterEditor : Editor
	{
		private static readonly Color s_ReadonlyColorLight = new Color(0.3f, 0.3f, 0.3f);
		private static readonly Color s_ReadonlyColorDark = new Color(0.6f, 0.6f, 0.6f);

		public override void OnInspectorGUI()
		{
			var importer = (GoogleSheetsImporter) target;
			DrawDefaultInspector();

			if (GUILayout.Button("Select Folder"))
			{
				SelectFolder(importer);
			}

			EditorGUILayout.Space();

			if (string.IsNullOrWhiteSpace(importer.FullTypeName) && importer.MonoScript == null)
			{
				EditorGUILayout.HelpBox($"Please set the {nameof(importer.FullTypeName)} field or assign a {nameof(importer.MonoScript)}.", MessageType.Warning);
			}

			if (!importer.IsValidSheetId())
			{
				EditorGUILayout.HelpBox(importer.GetInvalidSheetIdWarning(), MessageType.Warning);
			}

			if (!importer.IsValidSheetName())
			{
				EditorGUILayout.HelpBox(importer.GetInvalidSheetNameWarning(), MessageType.Warning);
			}

			var readonlyColor = EditorGUIUtility.isProSkin ? s_ReadonlyColorDark : s_ReadonlyColorLight;
			var readonlyStyle = new GUIStyle(EditorStyles.textField)
			{
				wordWrap = true,
				normal = { textColor = readonlyColor },
				hover = { textColor = readonlyColor },
				focused = { textColor = readonlyColor }
			};
			var urlHeight = readonlyStyle.CalcHeight(new GUIContent(importer.Url), EditorGUIUtility.currentViewWidth);
			EditorGUILayout.SelectableLabel($"{importer.Url}", readonlyStyle, GUILayout.Height(urlHeight));

			EditorGUILayout.Space();

			if (GUILayout.Button("Copy URL"))
			{
				EditorGUIUtility.systemCopyBuffer = importer.Url;
			}

			if (GUILayout.Button("Debug CSV to Console"))
			{
				DebugCsvToConsoleAsync(importer);
			}
		}

		private async void DebugCsvToConsoleAsync(GoogleSheetsImporter importer)
		{
			var csvData = await importer.GetCsvDataAsync();
			Debug.Log(csvData);
		}

		private void SelectFolder(GoogleSheetsImporter importer)
		{
			var startFolder = importer.HasFolder && AssetDatabase.IsValidFolder(importer.Folder.TrimEnd('*', '/')) ? importer.Folder.TrimEnd('*', '/') : "Assets";
			var absolutePath = EditorUtility.OpenFolderPanel("Select Folder", startFolder, string.Empty);
			if (string.IsNullOrEmpty(absolutePath))
			{
				return;
			}
			var relativePath = FileUtil.GetProjectRelativePath(absolutePath);
			if (string.IsNullOrEmpty(relativePath))
			{
				Debug.LogWarning($"Selected folder '{absolutePath}' must be inside the project. Please choose a folder under Assets or Packages.");
				return;
			}
			// Preserve the user's wildcard choice so reselecting a folder keeps the subfolder scope.
			if (importer.HasFolder && importer.Folder.Replace("\\", "/").TrimEnd('/').EndsWith("/*"))
			{
				relativePath += "/*";
			}
			foreach (var selectedTarget in targets)
			{
				var selectedImporter = (GoogleSheetsImporter) selectedTarget;
				Undo.RecordObject(selectedImporter, "Set Importer Folder");
				selectedImporter.Folder = relativePath;
				EditorUtility.SetDirty(selectedImporter);
			}
		}
	}
}