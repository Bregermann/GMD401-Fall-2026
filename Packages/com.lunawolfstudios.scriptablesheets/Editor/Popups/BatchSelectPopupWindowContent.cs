using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Popups
{
	public class BatchSelectPopupWindowContent : AnchoredPopupWindowContent
	{
		private readonly string[] m_SheetNames;
		private readonly bool[] m_SheetOpen;
		private readonly Action<int[]> m_Open;
		private readonly Action<int[]> m_Delete;
		private readonly bool[] m_Selected;

		private Vector2 m_SizeOfBestFit = Vector2.zero;
		private Vector2 m_ScrollPos;

		public BatchSelectPopupWindowContent(Rect anchoredRect, string[] sheetNames, bool[] sheetOpen, Action<int[]> open, Action<int[]> delete) : base(anchoredRect)
		{
			m_SheetNames = sheetNames;
			m_SheetOpen = sheetOpen;
			m_Open = open;
			m_Delete = delete;
			m_Selected = new bool[sheetNames.Length];
		}

		public override Vector2 GetWindowSize()
		{
			// Can't call GUI.skin if opening via context menu.
			if (Event.current == null || m_SheetNames.Length <= 0)
			{
				return m_AnchoredRect.size;
			}
			// We need to calculate the best fit from inside OnGUI in case the user opens the popup from the context window.
			if (m_SizeOfBestFit == Vector2.zero)
			{
				var widestLabel = m_SheetNames.Max(n => GUI.skin.toggle.CalcSize(new GUIContent(n)).x);
				widestLabel = Mathf.Max(PopupContent.Window.BatchSelectMinWidth, widestLabel);
				m_SizeOfBestFit.x = Mathf.Min(widestLabel + PopupContent.Window.BatchSelectPadding, m_AnchoredRect.size.x);
				// Account for the counter, select all, and footer rows in addition to one row per Sheet.
				var contentHeight = (m_SheetNames.Length + 3) * EditorGUIUtility.singleLineHeight;
				m_SizeOfBestFit.y = Mathf.Min(contentHeight + PopupContent.Window.BatchSelectPadding, m_AnchoredRect.size.y);
			}
			return m_SizeOfBestFit;
		}

		public override void OnGUI()
		{
			var selectedCount = m_Selected.Count(s => s);

			EditorGUILayout.LabelField($"{PopupContent.Label.BatchSelect.text} ({selectedCount}/{m_SheetNames.Length})");

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(PopupContent.Button.SelectAll))
			{
				SetAllSelected(true);
			}
			if (GUILayout.Button(PopupContent.Button.DeselectAll))
			{
				SetAllSelected(false);
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
			for (var i = 0; i < m_SheetNames.Length; i++)
			{
				// Sheets already open in the Editor cannot be opened or deleted, so they are listed but not interactable.
				EditorGUI.BeginDisabledGroup(m_SheetOpen[i]);
				m_Selected[i] = EditorGUILayout.ToggleLeft(m_SheetNames[i], m_Selected[i]);
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndScrollView();

			var selectedIndices = GetSelectedIndices();

			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(selectedIndices.Length <= 0);
			if (GUILayout.Button(PopupContent.Button.Open, GUILayout.Width(PopupContent.Window.BatchSelectActionButtonWidth), GUILayout.Height(PopupContent.Window.BatchSelectActionButtonHeight)))
			{
				m_Open?.Invoke(selectedIndices);
				editorWindow.Close();
			}
			if (GUILayout.Button(PopupContent.Button.Delete, GUILayout.Width(PopupContent.Window.BatchSelectActionButtonWidth), GUILayout.Height(PopupContent.Window.BatchSelectActionButtonHeight)))
			{
				if (EditorUtility.DisplayDialog("Delete Sheets", $"Delete the selected ({selectedIndices.Length}) Sheet(s)?\n\nYou cannot undo the delete Sheets action.", "Delete", "Cancel"))
				{
					m_Delete?.Invoke(selectedIndices);
					editorWindow.Close();
				}
			}
			EditorGUI.EndDisabledGroup();
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		private void SetAllSelected(bool value)
		{
			for (var i = 0; i < m_Selected.Length; i++)
			{
				m_Selected[i] = value && !m_SheetOpen[i];
			}
		}

		private int[] GetSelectedIndices()
		{
			var selectedIndices = new List<int>();
			for (var i = 0; i < m_Selected.Length; i++)
			{
				if (m_Selected[i] && !m_SheetOpen[i])
				{
					selectedIndices.Add(i);
				}
			}
			return selectedIndices.ToArray();
		}
	}
}
