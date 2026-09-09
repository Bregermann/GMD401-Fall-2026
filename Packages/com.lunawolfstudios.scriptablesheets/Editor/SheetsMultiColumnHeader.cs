using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEditor.GenericMenu;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public class SheetsMultiColumnHeader : MultiColumnHeader
	{
		private static readonly FieldInfo s_ColumnRectsField = typeof(MultiColumnHeader).GetField("m_ColumnRects", BindingFlags.Instance | BindingFlags.NonPublic);

		// Internal ColorPicker.Show overload that reports changes in realtime via a callback.
		// Unity 6 added a trailing setAlphaIfTransparentOnNextPick parameter to the signature.
#if UNITY_6000_0_OR_NEWER
		private static readonly Type[] s_ColorPickerShowParameters = { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool), typeof(bool) };
#else
		private static readonly Type[] s_ColorPickerShowParameters = { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool) };
#endif

		private static readonly MethodInfo s_ColorPickerShow = AppDomain.CurrentDomain.GetAssemblies()
			.Select(a => a.GetType("UnityEditor.ColorPicker")).FirstOrDefault(t => t != null)
			?.GetMethod("Show", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, s_ColorPickerShowParameters, null);

		private readonly MenuFunction2 m_CopyColumnFunc;
		private readonly MenuFunction2 m_SearchFilterFunc;

		private int m_ContextColumn;

		private HashSet<int> m_DockedColumns = new HashSet<int>();
		public HashSet<int> DockedColumns { get => m_DockedColumns; set => m_DockedColumns = value; }

		// Custom header text colors by column index. Columns without an entry use the default header color.
		private Dictionary<int, Color> m_HeaderColors = new Dictionary<int, Color>();
		public Dictionary<int, Color> HeaderColors { get => m_HeaderColors; set => m_HeaderColors = value; }

		// Managed reference subtypes toggled off per managed reference column index. Used for the context menu checkmarks.
		private Dictionary<int, HashSet<string>> m_DisabledManagedTypes = new Dictionary<int, HashSet<string>>();
		public Dictionary<int, HashSet<string>> DisabledManagedTypes { get => m_DisabledManagedTypes; set => m_DisabledManagedTypes = value; }

		// Mapped asset preview source chain per column index. Absent or empty means the column uses its default preview.
		private Dictionary<int, string[]> m_AssetPreviewChains = new Dictionary<int, string[]>();
		public Dictionary<int, string[]> AssetPreviewChains { get => m_AssetPreviewChains; set => m_AssetPreviewChains = value; }

		// Builds the Map Asset Preview menu entries lazily when the context menu opens. Shared by every column.
		public Func<List<AssetPreviewEntry>> AssetPreviewEntriesProvider { get; set; }

		public event HeaderCallback dockedColumnsChanged;
		public event HeaderCallback headerColorsChanged;
		public event HeaderCallback managedTypeFiltersChanged;
		public event HeaderCallback assetPreviewSourceChanged;

		// Invoked with an action name right before a context menu action mutates column state so the owner can checkpoint undo.
		public Action<string> recordColumnUndo;

		private float m_ScrollX;
		private float m_WidthOfAllVisibleDockedColumns;

		public SheetsMultiColumnHeader(MultiColumnHeaderState state, MenuFunction2 copyColumnFunc, MenuFunction2 searchFilterFunc) : base(state)
		{
			m_CopyColumnFunc = copyColumnFunc;
			m_SearchFilterFunc = searchFilterFunc;
		}

		public override void OnGUI(Rect rect, float xScroll)
		{
			// We want to draw the ColumnHeader as a single line, but each row will be the row height.
			var totalHeaderRect = new Rect(0f, 0f, rect.width, rect.height);
			rect.height = EditorGUIUtility.singleLineHeight;

			base.OnGUI(rect, xScroll);

			if (DockedColumns.Count > 0)
			{
				UpdateColumnHeaderRectsWithDocking(totalHeaderRect, xScroll);
			}
			else if (totalHeaderRect.height > rect.height)
			{
				UpdateColumnHeaderRectHeight(totalHeaderRect);
			}
		}

		private void UpdateColumnHeaderRectHeight(Rect totalHeaderRect)
		{
			var columnRects = (Rect[]) s_ColumnRectsField.GetValue(this);

			for (var i = 0; i < columnRects.Length; i++)
			{
				columnRects[i].height = totalHeaderRect.height;
			}

			s_ColumnRectsField.SetValue(this, columnRects);
		}

		private void UpdateColumnHeaderRectsWithDocking(Rect totalHeaderRect, float xScroll)
		{
			var columnRects = (Rect[]) s_ColumnRectsField.GetValue(this);

			var rect = totalHeaderRect;
			var dockedRect = new Rect();
			for (var i = 0; i < state.visibleColumns.Length; i++)
			{
				var num = state.visibleColumns[i];
				var column = state.columns[num];
				if (i > 0)
				{
					rect.x += rect.width;
				}

				rect.width = column.width;
				columnRects[i] = rect;

				// Name and Actions column move with scroll.
				if (num <= 1 && DockedColumns.Contains(num))
				{
					// Logic for the first docked column differs from the next.
					if (dockedRect == Rect.zero)
					{
						columnRects[i].x = Mathf.Max(columnRects[i].x, columnRects[0].x + xScroll);
						dockedRect = columnRects[i];
					}
					else
					{
						columnRects[i].x += xScroll;
						dockedRect.xMax = columnRects[i].xMax;
					}
				}
				else if (dockedRect.xMax > columnRects[i].x)
				{
					var columRectXMax = columnRects[i].xMax;
					columnRects[i].x = dockedRect.xMax;
					columnRects[i].width = Mathf.Max(0, columRectXMax - columnRects[i].x);
				}
			}

			s_ColumnRectsField.SetValue(this, columnRects);

			m_ScrollX = xScroll;
			// Store this once so we don't have to repeatedly call it for the ColumnHeaderGUI.
			m_WidthOfAllVisibleDockedColumns = GetWidthOfAllVisibleDockedColumns();
		}

		protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
		{
			headerRect.height = EditorGUIUtility.singleLineHeight;

			var isDocked = false;
			if (DockedColumns.Count > 0)
			{
				if (DockedColumns.Contains(columnIndex))
				{
					isDocked = true;
					// If Actions is docked then we just add the scroll X to all docked columns.
					if (DockedColumns.Contains(0))
					{
						headerRect.x += m_ScrollX;
					}
					else
					{
						headerRect.x = Mathf.Max(headerRect.x, GetColumnRect(0).x + m_ScrollX);
					}
					EditorGUI.DrawRect(headerRect, SheetLayout.DockedHeaderBackgroundColor);
				}
				// Don't hide the Actions column label.
				else if (columnIndex > 0 && m_WidthOfAllVisibleDockedColumns > 0)
				{
					var xOffset = m_WidthOfAllVisibleDockedColumns + m_ScrollX;
					if (headerRect.x < xOffset)
					{
						var delta = xOffset - headerRect.x;
						headerRect.x += delta;
						headerRect.xMax -= delta;
						if (headerRect.width < SheetLayout.MinColumnHeaderWidth)
						{
							return;
						}
					}
				}
			}

			SortingButton(column, headerRect, columnIndex);

			var style = GetStyle(column.headerTextAlignment);
			if (m_HeaderColors.TryGetValue(columnIndex, out var headerColor))
			{
				style = new GUIStyle(style)
				{
					normal = { textColor = headerColor }
				};
			}
			else if (isDocked)
			{
				style = new GUIStyle(style)
				{
					normal = { textColor = SheetLayout.DockedHeaderLabelColor }
				};
			}

			var num = SheetLayout.kWindowToolbarHeight;
			var position = new Rect(headerRect.x, headerRect.yMax - num, headerRect.width, num);
			GUI.Label(position, column.headerContent, style);

			var e = Event.current;
			if (e.type == EventType.MouseDown && e.button == 1 && headerRect.Contains(e.mousePosition))
			{
				m_ContextColumn = columnIndex;
			}
		}

		protected override void AddColumnHeaderContextMenuItems(GenericMenu menu)
		{
			var contextColumn = state.columns[m_ContextColumn];
			var contextColumnName = contextColumn.headerContent.text;
			menu.AddDisabledItem(new GUIContent($"[{contextColumnName}]"));

			// Can only Dock Actions and Name column.
			if (m_ContextColumn <= 1)
			{
				menu.AddItem(EditorGUIUtility.TrTextContent("Dock column"), DockedColumns.Contains(m_ContextColumn), ToggleColumnDocking, m_ContextColumn);
			}

			if (m_ContextColumn > 0)
			{
				menu.AddItem(EditorGUIUtility.TrTextContent("Hide column"), false, ToggleColumnVisibility, m_ContextColumn);
				menu.AddItem(EditorGUIUtility.TrTextContent("Copy column"), false, m_CopyColumnFunc, m_ContextColumn);
				// Collection Table meta columns have no serialized property path to copy, but they can still be filtered by reference.
				if (IsCollectionMetaColumn(contextColumn))
				{
					menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Copy property path"));
				}
				else
				{
					menu.AddItem(EditorGUIUtility.TrTextContent("Copy property path"), false, CopyPropertyPath, m_ContextColumn);
				}
				menu.AddItem(EditorGUIUtility.TrTextContent("Filter by property"), false, m_SearchFilterFunc, m_ContextColumn);
			}

			menu.AddItem(EditorGUIUtility.TrTextContent("Header color"), false, OpenHeaderColorPicker, m_ContextColumn);
			if (m_HeaderColors.ContainsKey(m_ContextColumn))
			{
				menu.AddItem(EditorGUIUtility.TrTextContent("Reset header color"), false, ResetHeaderColor, m_ContextColumn);
			}

			AddAssetPreviewMenuItems(menu);
			AddManagedTypeMenuItems(menu, contextColumn);

			menu.AddSeparator(string.Empty);

			menu.AddItem(EditorGUIUtility.TrTextContent("Resize to Fit"), false, ResizeToFit);
			menu.AddSeparator(string.Empty);

			var columnNameCounters = new Dictionary<string, int>();
			for (int i = 0; i < state.columns.Length && i < SheetLayout.MenuItemLimit; i++)
			{
				var column = state.columns[i];
				var columnName = column.headerContent.text;
				var menuItemName = columnName;

				if (columnNameCounters.ContainsKey(columnName))
				{
					columnNameCounters[columnName]++;
					menuItemName = $"{columnName} ({columnNameCounters[columnName]})";
				}
				else
				{
					columnNameCounters[columnName] = 0;
				}

				if (column.allowToggleVisibility)
				{
					menu.AddItem(new GUIContent(menuItemName), state.visibleColumns.Contains(i), ToggleColumnVisibility, i);
				}
				else
				{
					menu.AddDisabledItem(new GUIContent(menuItemName));
				}
			}

			m_ContextColumn = 0;
		}

		public float GetWidthOfAllVisibleDockedColumns()
		{
			return state.visibleColumns.Where(v => m_DockedColumns.Contains(v)).Sum(d => state.columns[d].width);
		}

		protected virtual void OnDockedColumnsChanged()
		{
			dockedColumnsChanged?.Invoke(this);
		}

		private GUIStyle GetStyle(TextAlignment alignment)
		{
			switch (alignment)
			{
				case TextAlignment.Left:
					return DefaultStyles.columnHeader;
				case TextAlignment.Center:
					return DefaultStyles.columnHeaderCenterAligned;
				case TextAlignment.Right:
					return DefaultStyles.columnHeaderRightAligned;
				default:
					return DefaultStyles.columnHeader;
			}
		}

		private void ToggleColumnVisibility(object columnData)
		{
			recordColumnUndo?.Invoke("Toggle Column Visibility");
			ToggleVisibility((int) columnData);
		}

		private void ToggleColumnDocking(object columnData)
		{
			recordColumnUndo?.Invoke("Dock Column");
			var columnIndex = (int) columnData;
			if (DockedColumns.Contains(columnIndex))
			{
				DockedColumns.Remove(columnIndex);
			}
			else
			{
				DockedColumns.Add(columnIndex);
			}
			OnDockedColumnsChanged();
		}

		private void CopyPropertyPath(object columnData)
		{
			EditorGUIUtility.systemCopyBuffer = state.columns[(int) columnData].headerContent.tooltip;
		}

		// The Collection Table Group Name, Index, and Value columns are keyed by descriptive tooltips rather than property paths.
		private static bool IsCollectionMetaColumn(MultiColumnHeaderState.Column column)
		{
			var tooltip = column.headerContent.tooltip;
			return tooltip == ColumnUtility.GroupNameTooltip || tooltip == ColumnUtility.CollectionIndexTooltip || tooltip == ColumnUtility.CollectionValueTooltip;
		}

		protected virtual void OnHeaderColorsChanged()
		{
			headerColorsChanged?.Invoke(this);
		}

		private void OpenHeaderColorPicker(object columnData)
		{
			var columnIndex = (int) columnData;
			var initialColor = m_HeaderColors.TryGetValue(columnIndex, out var existingColor)
				? existingColor
				: GetStyle(state.columns[columnIndex].headerTextAlignment).normal.textColor;

			if (s_ColorPickerShow == null)
			{
				Debug.LogWarning("Unable to open the color picker to set the header color.");
				return;
			}

			// Checkpoint once as the picker opens so the realtime edits collapse into a single undo step.
			recordColumnUndo?.Invoke("Header Color");

			void OnColorChanged(Color color)
			{
				m_HeaderColors[columnIndex] = color;
				OnHeaderColorsChanged();
			}

			// showAlpha and hdr are false because we only tint the header label. Unity 6 adds a trailing
			// setAlphaIfTransparentOnNextPick flag which is also false.
#if UNITY_6000_0_OR_NEWER
			var args = new object[] { (Action<Color>) OnColorChanged, initialColor, false, false, false };
#else
			var args = new object[] { (Action<Color>) OnColorChanged, initialColor, false, false };
#endif

			// GenericMenu callbacks fire outside OnGUI where the editor skin isn't active, so the
			// ColorPicker fails to resolve its styles. Defer so it opens in a valid GUI context.
			EditorApplication.delayCall += () => s_ColorPickerShow.Invoke(null, args);
		}

		private void ResetHeaderColor(object columnData)
		{
			var columnIndex = (int) columnData;
			if (!m_HeaderColors.ContainsKey(columnIndex))
			{
				return;
			}
			recordColumnUndo?.Invoke("Reset Header Color");
			m_HeaderColors.Remove(columnIndex);
			OnHeaderColorsChanged();
		}

		// Adds a Map Asset Preview submenu for picking an object reference property, recursively through child ScriptableObjects,
		// whose asset preview replaces the column's default preview. Only one source can be selected per column and Default clears it.
		private void AddAssetPreviewMenuItems(GenericMenu menu)
		{
			// The Actions column has no cell content to preview.
			if (m_ContextColumn <= 0 || AssetPreviewEntriesProvider == null)
			{
				return;
			}
			var entries = AssetPreviewEntriesProvider();
			// The first entry is always Default, so anything beyond it means there are real sources to offer.
			if (entries == null || entries.Count <= 1)
			{
				return;
			}
			var selectedChain = m_AssetPreviewChains.TryGetValue(m_ContextColumn, out var chain) ? chain : null;
			menu.AddSeparator(string.Empty);
			foreach (var entry in entries)
			{
				var isSelected = ChainsEqual(entry.Chain, selectedChain);
				menu.AddItem(new GUIContent($"Map Asset Preview/{entry.MenuPath}"), isSelected, SetAssetPreviewSource, new AssetPreviewMenuItem(m_ContextColumn, entry.Chain));
			}
		}

		private void SetAssetPreviewSource(object menuItemData)
		{
			var menuItem = (AssetPreviewMenuItem) menuItemData;
			var selectedChain = m_AssetPreviewChains.TryGetValue(menuItem.ColumnIndex, out var chain) ? chain : null;
			if (ChainsEqual(menuItem.Chain, selectedChain))
			{
				return;
			}
			recordColumnUndo?.Invoke("Map Asset Preview");
			if (menuItem.Chain == null || menuItem.Chain.Length <= 0)
			{
				m_AssetPreviewChains.Remove(menuItem.ColumnIndex);
			}
			else
			{
				m_AssetPreviewChains[menuItem.ColumnIndex] = menuItem.Chain;
			}
			assetPreviewSourceChanged?.Invoke(this);
		}

		private static bool ChainsEqual(string[] a, string[] b)
		{
			var aEmpty = a == null || a.Length <= 0;
			var bEmpty = b == null || b.Length <= 0;
			if (aEmpty || bEmpty)
			{
				return aEmpty && bEmpty;
			}
			return a.SequenceEqual(b);
		}

		// A selectable Map Asset Preview menu entry. MenuPath is the slash delimited label and Chain is the property hop path.
		public class AssetPreviewEntry
		{
			public string MenuPath { get; }
			public string[] Chain { get; }

			public AssetPreviewEntry(string menuPath, string[] chain)
			{
				MenuPath = menuPath;
				Chain = chain;
			}
		}

		private class AssetPreviewMenuItem
		{
			public int ColumnIndex { get; }
			public string[] Chain { get; }

			public AssetPreviewMenuItem(int columnIndex, string[] chain)
			{
				ColumnIndex = columnIndex;
				Chain = chain;
			}
		}

		// Adds a Managed Type Columns submenu with a toggle for each concrete subtype of a managed reference column.
		// Every subtype is enabled by default. Toggling one off hides the columns contributed by that subtype's fields
		// without hiding any rows.
		private void AddManagedTypeMenuItems(GenericMenu menu, MultiColumnHeaderState.Column contextColumn)
		{
			if (!(contextColumn is PropertyColumn propertyColumn) || propertyColumn.property == null || !propertyColumn.property.IsManagedReference())
			{
				return;
			}
			var baseType = propertyColumn.property.GetManagedReferenceFieldType(propertyColumn.type);
			var concreteTypes = SerializedPropertyUtility.GetConcreteManagedReferenceTypes(baseType);
			if (concreteTypes.Length <= 0)
			{
				return;
			}
			var disabledTypes = m_DisabledManagedTypes.TryGetValue(m_ContextColumn, out var disabled) ? disabled : null;
			menu.AddSeparator(string.Empty);
			foreach (var concreteType in concreteTypes)
			{
				var typeName = concreteType.Name;
				var isEnabled = disabledTypes == null || !disabledTypes.Contains(typeName);
				menu.AddItem(new GUIContent($"Managed Type Columns/{typeName}"), isEnabled, ToggleManagedType, new ManagedTypeMenuItem(m_ContextColumn, typeName));
			}
		}

		private void ToggleManagedType(object menuItemData)
		{
			var menuItem = (ManagedTypeMenuItem) menuItemData;
			recordColumnUndo?.Invoke("Managed Type Columns");
			if (!m_DisabledManagedTypes.TryGetValue(menuItem.ColumnIndex, out var disabledTypes))
			{
				disabledTypes = new HashSet<string>();
				m_DisabledManagedTypes[menuItem.ColumnIndex] = disabledTypes;
			}
			if (!disabledTypes.Remove(menuItem.TypeName))
			{
				disabledTypes.Add(menuItem.TypeName);
			}
			if (disabledTypes.Count <= 0)
			{
				m_DisabledManagedTypes.Remove(menuItem.ColumnIndex);
			}
			managedTypeFiltersChanged?.Invoke(this);
		}

		private class ManagedTypeMenuItem
		{
			public int ColumnIndex { get; }
			public string TypeName { get; }

			public ManagedTypeMenuItem(int columnIndex, string typeName)
			{
				ColumnIndex = columnIndex;
				TypeName = typeName;
			}
		}
	}
}