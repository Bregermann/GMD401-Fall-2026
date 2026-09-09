using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Layout
{
	public static class ColumnUtility
	{
		public const char ColumnReferencePrefix = '$';

		// Note: Tooltips need to be unique or the table will assume it's the same property.
		public const string ActionsTooltip = "Object Actions";
		public const string AssetPathTooltip = "Object Asset Path";
		public const string GuidColumnTooltip = "Globally Unique Identifier";
		public const string GroupNameTooltip = "Collection Group Name";
		public const string CollectionIndexTooltip = "Collection Index";
		public const string CollectionValueTooltip = "Collection Element Value";

		public static MultiColumnHeaderState.Column CreateActionsColumn(string columnName, int width)
		{
			return new MultiColumnHeaderState.Column()
			{
				allowToggleVisibility = false,
				autoResize = true,
				canSort = false,
				headerContent = new GUIContent(columnName, ActionsTooltip),
				headerTextAlignment = TextAlignment.Left,
				minWidth = width,
				maxWidth = width,
				sortingArrowAlignment = TextAlignment.Right,
			};
		}

		public static MultiColumnHeaderState.Column CreateAssetPathColumn(string columnName)
		{
			return new MultiColumnHeaderState.Column()
			{
				allowToggleVisibility = true,
				autoResize = true,
				canSort = true,
				headerContent = new GUIContent(columnName, AssetPathTooltip),
				headerTextAlignment = TextAlignment.Left,
				minWidth = SheetLayout.PropertyWidth,
				maxWidth = int.MaxValue,
				sortedAscending = true,
				sortingArrowAlignment = TextAlignment.Right,
			};
		}

		public static MultiColumnHeaderState.Column CreateGuidColumn(string columnName)
		{
			return new MultiColumnHeaderState.Column()
			{
				allowToggleVisibility = true,
				autoResize = true,
				canSort = true,
				headerContent = new GUIContent(columnName, GuidColumnTooltip),
				headerTextAlignment = TextAlignment.Left,
				minWidth = SheetLayout.GuidPropertyWidth,
				maxWidth = int.MaxValue,
				sortedAscending = true,
				sortingArrowAlignment = TextAlignment.Right,
			};
		}

		// Read-only column showing the name of the Object that owns a Collection Table row's element.
		public static MultiColumnHeaderState.Column CreateGroupNameColumn(string columnName)
		{
			return new MultiColumnHeaderState.Column()
			{
				allowToggleVisibility = true,
				autoResize = true,
				canSort = true,
				headerContent = new GUIContent(columnName, GroupNameTooltip),
				headerTextAlignment = TextAlignment.Left,
				minWidth = SheetLayout.PropertyWidth,
				maxWidth = int.MaxValue,
				sortedAscending = true,
				sortingArrowAlignment = TextAlignment.Right,
			};
		}

		// Read-only column showing an element's index within its owning collection.
		public static MultiColumnHeaderState.Column CreateCollectionIndexColumn(string columnName)
		{
			return new MultiColumnHeaderState.Column()
			{
				allowToggleVisibility = true,
				autoResize = true,
				// Collection Tables sort element rows within each owner group, so index sorting orders those rows.
				canSort = true,
				headerContent = new GUIContent(columnName, CollectionIndexTooltip),
				headerTextAlignment = TextAlignment.Left,
				minWidth = SheetLayout.PropertyWidthMedium,
				maxWidth = int.MaxValue,
				sortedAscending = true,
				sortingArrowAlignment = TextAlignment.Right,
			};
		}

		public static PropertyColumn CreatePropertyColumn(SerializedProperty serializedProperty, bool isScriptableObject, HeaderFormat headerFormat, string labelPrefix = "")
		{
			string formattedName;
			switch (headerFormat)
			{
				case HeaderFormat.Default:
					formattedName = serializedProperty.displayName;
					break;

				case HeaderFormat.Friendly:
					formattedName = serializedProperty.FriendlyPropertyPath();
					break;

				case HeaderFormat.Advanced:
					formattedName = serializedProperty.propertyPath;
					break;

				default:
					Debug.LogWarning($"Unsupported {nameof(HeaderFormat)} {headerFormat}. Using {nameof(HeaderFormat.Default)} format.");
					formattedName = serializedProperty.displayName;
					break;
			}
			var columnName = $"{labelPrefix}{formattedName}";
			var propertyType = serializedProperty.GetSheetsPropertyType(isScriptableObject);
			var minWidth = SheetLayout.PropertyWidth;
			var propertyPath = serializedProperty.propertyPath;
			switch (propertyType)
			{
				case SerializedPropertyType.Integer:
				case SerializedPropertyType.Boolean:
				case SerializedPropertyType.Float:
				case SerializedPropertyType.Color:
				case SerializedPropertyType.Character:
					minWidth = SheetLayout.PropertyWidthSmall;
					break;

				case SerializedPropertyType.ArraySize:
					minWidth = SheetLayout.PropertyWidthMedium;
					break;

				default:
					break;
			}
			return new PropertyColumn()
			{
				allowToggleVisibility = true,
				autoResize = true,
				canSort = true,
				headerContent = new GUIContent(columnName, propertyPath),
				headerTextAlignment = TextAlignment.Left,
				minWidth = minWidth,
				maxWidth = int.MaxValue,
				sortedAscending = true,
				sortingArrowAlignment = TextAlignment.Right,
				property = serializedProperty.Copy(),
				type = serializedProperty.serializedObject.targetObject.GetType(),
			};
		}

		public static string GetColumnIndexLabel(bool showIndex, int index, IndexFormat format)
		{
			return showIndex ? $"{GetIndexLabel(index, format)} " : string.Empty;
		}

		public static string GetIndexLabel(int index, IndexFormat format)
		{
			return format == IndexFormat.Alpha ? GetAlphaIndexLabel(index) : index.ToString();
		}

		// Strips a leading "{index} " label (numeric or alpha) added by GetColumnIndexLabel.
		public static string RemoveColumnIndexLabel(string headerText)
		{
			if (string.IsNullOrEmpty(headerText))
			{
				return headerText;
			}
			var spaceIndex = headerText.IndexOf(' ');
			return spaceIndex >= 0 ? headerText.Substring(spaceIndex + 1) : headerText;
		}

		// Converts a zero based index to spreadsheet style letters (0 => A, 25 => Z, 26 => AA).
		public static string GetAlphaIndexLabel(int index)
		{
			var label = string.Empty;
			for (index++; index > 0; index /= 26)
			{
				index--;
				label = (char) ('A' + index % 26) + label;
			}
			return label;
		}

		// Resolves a column reference token to a zero based index, accepting numeric (5 => 5) or
		// spreadsheet style letters (A => 0, Z => 25, AA => 26) regardless of the active index format.
		public static bool TryGetColumnReferenceIndex(string token, out int index)
		{
			index = -1;
			if (string.IsNullOrEmpty(token))
			{
				return false;
			}
			if (int.TryParse(token, out index))
			{
				return index >= 0;
			}
			var value = 0;
			foreach (var character in token)
			{
				var upper = char.ToUpperInvariant(character);
				if (upper < 'A' || upper > 'Z')
				{
					return false;
				}
				value = value * 26 + (upper - 'A' + 1);
			}
			index = value - 1;
			return true;
		}

		public static int[] GetClampedColumns(this MultiColumnHeaderState.Column[] columns, int maxColumns)
		{
			return Enumerable.Range(0, Mathf.Min(columns.Length, maxColumns)).ToArray();
		}
	}
}
