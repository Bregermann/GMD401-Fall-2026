using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Tables
{
	// Read-only cell for a Collection Table's Group Name and Index columns. The value is shown and exported but never written
	// back, so an import, copy, or paste can never rename the owning Object or change an element index.
	public struct CollectionLabelTableProperty : ITableProperty
	{
		private static readonly string s_PropertyName = $"k_{nameof(CollectionLabelTableProperty)}";

		private readonly Object m_RootObject;
		public Object RootObject => m_RootObject;

		private readonly string m_PropertyPath;
		public string PropertyPath => m_PropertyPath;

		private readonly string m_ControlName;
		public string ControlName => m_ControlName;

		private readonly string m_Label;
		private readonly string m_ColumnName;

		public CollectionLabelTableProperty(Object rootObject, string label, string columnName, string controlName)
		{
			m_RootObject = rootObject;
			m_PropertyPath = s_PropertyName;
			m_ControlName = controlName;
			m_Label = label;
			m_ColumnName = columnName;
		}

		public string GetProperty(FlatFileFormatSettings formatSettings)
		{
			return m_Label;
		}

		public void SetProperty(string value, FlatFileFormatSettings formatSettings)
		{
			// Group Name and Index are read-only. Warn when the pasted value does not match so mismatched data is noticed,
			// since Collection Tables cannot rename owners, reindex elements, or add new rows on import or paste.
			if (!string.IsNullOrEmpty(value) && value != m_Label)
			{
				Debug.LogWarning($"Collection Table {m_ColumnName} '{value}' does not match the expected '{m_Label}'. {m_ColumnName} columns are read-only and were skipped.");
			}
		}

		public void AddNewLine()
		{
			return;
		}

		public bool IsInputFieldProperty(bool isScriptableObject)
		{
			return false;
		}

		public bool IsUnityLocalizationProperty()
		{
			return false;
		}

		public bool NeedsSelectionBorder(bool lockNames = false)
		{
			return true;
		}
	}
}
