using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public class PropertyColumn : MultiColumnHeaderState.Column
	{
		public SerializedProperty property;
		// Cache the type as we will lose access to the properties target object on the next frame.
		public Type type;
	}
}
