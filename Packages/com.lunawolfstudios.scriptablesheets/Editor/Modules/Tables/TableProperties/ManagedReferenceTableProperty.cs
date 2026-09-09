using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.Tables
{
	// Represents the concrete type cell of a serialize reference field. The value is the short subtype name and
	// setting it assigns a new instance of the matching subtype (requires Unity 2021.2+).
	public struct ManagedReferenceTableProperty : ITableProperty
	{
		public const string NullTypeValue = "null";

		private readonly Object m_RootObject;
		public Object RootObject => m_RootObject;

		private readonly string m_PropertyPath;
		public string PropertyPath => m_PropertyPath;

		private readonly string m_ControlName;
		public string ControlName => m_ControlName;

		public ManagedReferenceTableProperty(Object rootObject, string propertyPath, string controlName)
		{
			m_RootObject = rootObject;
			m_PropertyPath = propertyPath;
			m_ControlName = controlName;
		}

		public void AddNewLine()
		{
			Debug.LogWarning($"Cannot add new lines to {nameof(ITableProperty)} {nameof(ManagedReferenceTableProperty)}!");
			return;
		}

		public string GetProperty(FlatFileFormatSettings formatSettings)
		{
			var property = new SerializedObject(m_RootObject).FindProperty(m_PropertyPath);
			if (property == null)
			{
				return string.Empty;
			}
			var typeName = property.GetManagedReferenceTypeName();
			return string.IsNullOrEmpty(typeName) ? NullTypeValue : typeName;
		}

		public void SetProperty(string value, FlatFileFormatSettings formatSettings)
		{
			if (value == null || !SerializedPropertyUtility.CanSetManagedReferenceValue)
			{
				return;
			}
			var serializedObject = new SerializedObject(m_RootObject);
			var property = serializedObject.FindProperty(m_PropertyPath);
			if (property == null || !property.IsManagedReference())
			{
				return;
			}
			if (value.Length == 0 || value == NullTypeValue)
			{
				property.SetManagedReferenceValue(null);
				serializedObject.ApplyModifiedProperties();
				return;
			}
			var baseType = property.GetManagedReferenceFieldType(m_RootObject);
			var matchedType = SerializedPropertyUtility.GetConcreteManagedReferenceTypes(baseType).FirstOrDefault(t => t.Name == value);
			if (matchedType != null)
			{
				property.SetManagedReferenceValue(matchedType);
				serializedObject.ApplyModifiedProperties();
			}
			else
			{
				Debug.LogWarning($"Cannot set managed reference at path {m_PropertyPath} on {nameof(Object)} {m_RootObject.name}. No concrete subtype named '{value}' was found.");
			}
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
