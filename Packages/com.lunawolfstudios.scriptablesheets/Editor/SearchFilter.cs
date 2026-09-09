using LunaWolfStudiosEditor.ScriptableSheets.Comparables;
using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using LunaWolfStudiosEditor.ScriptableSheets.Serializables;
using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public static class SearchFilter
	{
		private const char FilterDelimiter = ':';
		private const string FilterPattern = @"([\w\.\[\]$]+)(==|!=|~=|=~|!~|~!|<=|>=|<|>|=)(.+)";

		// Property filters can be chained with these operators padded with whitespace. AND binds tighter than OR.
		private const string AndChainOperator = " & ";
		private const string OrChainOperator = " | ";

		public static List<Object> GetObjects(string input, List<Object> objs, SearchSettings settings, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths = null)
		{
			if (objs.Count > 0 && !string.IsNullOrEmpty(input))
			{
				if (input.Contains(FilterDelimiter))
				{
					var command = input.Split(FilterDelimiter)[0];
					var filter = input.Substring(command.Length + 1);
					if (!string.IsNullOrWhiteSpace(command) && !string.IsNullOrWhiteSpace(filter))
					{
						switch (command)
						{
							case "p":
							case "prop":
							case "property":
								if (TryGetPropertyConditions(objs[0], filter, useStringEnums, ignoreEnumCasing, columnPropertyPaths, out var conditionGroups))
								{
									return objs.Where(obj => MatchesPropertyConditions(obj, conditionGroups, settings)).ToList();
								}
								break;

							case "g":
							case "guid":
								return objs.Where(obj => MatchesGuidFilter(obj, filter, settings)).ToList();

							case "ap":
							case "path":
							case "assetpath":
								return objs.Where(obj => MatchesPathFilter(obj, filter, settings)).ToList();

							default:
								Debug.LogWarning($"'{command}' is not a valid search filter command.");
								break;
						}
					}
				}
				else
				{
					return objs.Where(obj => obj.name.MatchesSearch(input, settings)).ToList();
				}
			}
			return objs;
		}

		// Filters Collection Table rows by the search input. Property filters resolve element relative paths (and $ column
		// references) against each row's element using its full array element path, while name, guid, and asset path filters
		// resolve against the row's owning Object. Conditions are parsed once against a sample element and reused per row.
		public static List<T> GetCollectionRows<T>(string input, List<T> rows, Func<T, Object> getOwner, Func<T, string> getElementPrefix, Func<T, int> getElementIndex, Func<T, bool> isAddRow,
			SearchSettings settings, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths, Object referenceOwner, string referenceElementPrefix)
		{
			if (rows.Count > 0 && !string.IsNullOrEmpty(input))
			{
				if (input.Contains(FilterDelimiter))
				{
					var command = input.Split(FilterDelimiter)[0];
					var filter = input.Substring(command.Length + 1);
					if (!string.IsNullOrWhiteSpace(command) && !string.IsNullOrWhiteSpace(filter))
					{
						switch (command)
						{
							case "p":
							case "prop":
							case "property":
								if (referenceOwner != null && TryGetCollectionPropertyConditions(referenceOwner, referenceElementPrefix, filter, useStringEnums, ignoreEnumCasing, columnPropertyPaths, out var conditionGroups))
								{
									// Add rows are placeholders for empty owners and have no element to match against.
									return rows.Where(row => !isAddRow(row) && MatchesCollectionPropertyConditions(getOwner(row), getElementPrefix(row), getElementIndex(row), conditionGroups, settings)).ToList();
								}
								break;

							case "g":
							case "guid":
								return rows.Where(row => MatchesGuidFilter(getOwner(row), filter, settings)).ToList();

							case "ap":
							case "path":
							case "assetpath":
								return rows.Where(row => MatchesPathFilter(getOwner(row), filter, settings)).ToList();

							default:
								Debug.LogWarning($"'{command}' is not a valid search filter command.");
								break;
						}
					}
				}
				else
				{
					return rows.Where(row => getOwner(row) != null && getOwner(row).name.MatchesSearch(input, settings)).ToList();
				}
			}
			return rows;
		}

		// Parses element relative property conditions for a Collection Table against a sample element so each condition's path
		// can be prefixed with the per row array element path during matching.
		private static bool TryGetCollectionPropertyConditions(Object referenceOwner, string referenceElementPrefix, string filter, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths, out List<List<PropertyCondition>> conditionGroups)
		{
			conditionGroups = new List<List<PropertyCondition>>();
			var serializedObject = new SerializedObject(referenceOwner);
			foreach (var orGroup in filter.Split(new[] { OrChainOperator }, StringSplitOptions.None))
			{
				var conditions = new List<PropertyCondition>();
				foreach (var term in orGroup.Split(new[] { AndChainOperator }, StringSplitOptions.None))
				{
					if (!TryGetCollectionPropertyCondition(referenceOwner, serializedObject, referenceElementPrefix, term, useStringEnums, ignoreEnumCasing, columnPropertyPaths, out var condition))
					{
						return false;
					}
					conditions.Add(condition);
				}
				conditionGroups.Add(conditions);
			}
			return true;
		}

		private static bool TryGetCollectionPropertyCondition(Object referenceOwner, SerializedObject serializedObject, string referenceElementPrefix, string term, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths, out PropertyCondition condition)
		{
			condition = default;
			var match = Regex.Match(term, FilterPattern);
			if (!match.Success)
			{
				return false;
			}
			var propertyPath = match.Groups[1].Value;
			var filterOperation = match.Groups[2].Value;
			var filterValue = match.Groups[3].Value;
			if (string.IsNullOrEmpty(propertyPath) || string.IsNullOrEmpty(filterValue) || (filterOperation.Length < 2 && filterValue == "="))
			{
				return false;
			}
			// Resolve a column index reference to the element relative property path of that column.
			if (propertyPath[0] == ColumnUtility.ColumnReferencePrefix)
			{
				if (columnPropertyPaths == null
					|| !ColumnUtility.TryGetColumnReferenceIndex(propertyPath.Substring(1), out var columnIndex)
					|| columnIndex < 0 || columnIndex >= columnPropertyPaths.Count
					|| string.IsNullOrEmpty(columnPropertyPaths[columnIndex]))
				{
					return false;
				}
				propertyPath = columnPropertyPaths[columnIndex];
			}
			// The Group Name and Index columns are row metadata rather than serialized properties, so they match the owning
			// Object's name and the element's index instead of a property path.
			if (propertyPath == ColumnUtility.GroupNameTooltip)
			{
				condition = new PropertyCondition(CollectionConditionKind.GroupName, filterOperation, filterValue);
				return true;
			}
			if (propertyPath == ColumnUtility.CollectionIndexTooltip)
			{
				condition = new PropertyCondition(CollectionConditionKind.Index, filterOperation, filterValue);
				return true;
			}
			// The leaf Value column represents the element itself, so its element relative path is empty and its full path is the
			// array element with no trailing member access.
			if (propertyPath == ColumnUtility.CollectionValueTooltip)
			{
				propertyPath = string.Empty;
			}
			// Validate and resolve enum types against the sample element. The element relative path is stored so each row can
			// prefix it with its own array element path; there is no Object name special case since that is the Group Name column.
			var fullPath = GetCollectionElementPath(referenceElementPrefix, propertyPath);
			var property = serializedObject.FindProperty(fullPath);
			if (property == null)
			{
				return false;
			}
			if (useStringEnums && property.propertyType == SerializedPropertyType.Enum && property.TryGetEnumType(referenceOwner, out Type enumType))
			{
				try
				{
					var enumName = filterValue.Replace(UnityConstants.EnumPrefix, string.Empty);
					var enumObject = Enum.Parse(enumType, enumName, ignoreEnumCasing);
					filterValue = ((int) enumObject).ToString();
				}
				catch (Exception)
				{
					// Handle mismatched enum names gracefully.
				}
			}
			condition = new PropertyCondition(propertyPath, filterOperation, filterValue);
			return true;
		}

		private static bool MatchesCollectionPropertyConditions(Object owner, string elementPrefix, int elementIndex, List<List<PropertyCondition>> conditionGroups, SearchSettings settings)
		{
			return conditionGroups.Any(group => group.All(condition => MatchesCollectionCondition(owner, elementPrefix, elementIndex, condition, settings)));
		}

		private static bool MatchesCollectionCondition(Object owner, string elementPrefix, int elementIndex, PropertyCondition condition, SearchSettings settings)
		{
			switch (condition.CollectionKind)
			{
				case CollectionConditionKind.GroupName:
					return MatchesFilterOperation(owner != null ? owner.name : string.Empty, condition.FilterValue, condition.FilterOperation, settings);

				case CollectionConditionKind.Index:
					return int.TryParse(condition.FilterValue, out var indexValue) && MatchesFilterOperation(elementIndex, indexValue, condition.FilterOperation, settings);

				default:
					return MatchesPropertyFilter(owner, GetCollectionElementPath(elementPrefix, condition.PropertyPath), condition.FilterValue, condition.FilterOperation, settings);
			}
		}

		// Joins a row's array element prefix (ending in '.') with an element relative property path. An empty relative path refers
		// to the leaf element itself, so the trailing member access is dropped.
		private static string GetCollectionElementPath(string elementPrefix, string relativePath)
		{
			return string.IsNullOrEmpty(relativePath) ? elementPrefix.TrimEnd('.') : $"{elementPrefix}{relativePath}";
		}

		// Parses one or more property conditions chained with " & " (AND) and " | " (OR) into OR groups of AND conditions.
		private static bool TryGetPropertyConditions(Object referenceObject, string filter, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths, out List<List<PropertyCondition>> conditionGroups)
		{
			conditionGroups = new List<List<PropertyCondition>>();
			var serializedObject = new SerializedObject(referenceObject);
			foreach (var orGroup in filter.Split(new[] { OrChainOperator }, StringSplitOptions.None))
			{
				var conditions = new List<PropertyCondition>();
				foreach (var term in orGroup.Split(new[] { AndChainOperator }, StringSplitOptions.None))
				{
					if (!TryGetPropertyCondition(referenceObject, serializedObject, term, useStringEnums, ignoreEnumCasing, columnPropertyPaths, out var condition))
					{
						return false;
					}
					conditions.Add(condition);
				}
				conditionGroups.Add(conditions);
			}
			return true;
		}

		private static bool TryGetPropertyCondition(Object referenceObject, SerializedObject serializedObject, string term, bool useStringEnums, bool ignoreEnumCasing, IReadOnlyList<string> columnPropertyPaths, out PropertyCondition condition)
		{
			condition = default;
			var match = Regex.Match(term, FilterPattern);
			if (!match.Success)
			{
				return false;
			}
			var propertyPath = match.Groups[1].Value;
			var filterOperation = match.Groups[2].Value;
			var filterValue = match.Groups[3].Value;
			if (string.IsNullOrEmpty(propertyPath) || string.IsNullOrEmpty(filterValue) || (filterOperation.Length < 2 && filterValue == "="))
			{
				return false;
			}
			// Resolve a column index reference to the property path of that column.
			if (propertyPath[0] == ColumnUtility.ColumnReferencePrefix)
			{
				if (columnPropertyPaths == null
					|| !ColumnUtility.TryGetColumnReferenceIndex(propertyPath.Substring(1), out var columnIndex)
					|| columnIndex < 0 || columnIndex >= columnPropertyPaths.Count
					|| string.IsNullOrEmpty(columnPropertyPaths[columnIndex]))
				{
					return false;
				}
				propertyPath = columnPropertyPaths[columnIndex];
			}
			// The Name field is resolved specially during matching and is not exposed as a serialized property on all types such as Components.
			if (propertyPath == UnityConstants.Field.Name)
			{
				condition = new PropertyCondition(propertyPath, filterOperation, filterValue);
				return true;
			}
			var property = serializedObject.FindProperty(propertyPath);
			if (property == null)
			{
				return false;
			}
			// Convert string enum values to their index values before comparing.
			if (useStringEnums && property.propertyType == SerializedPropertyType.Enum && property.TryGetEnumType(referenceObject, out Type enumType))
			{
				try
				{
					var enumName = filterValue.Replace(UnityConstants.EnumPrefix, string.Empty);
					var enumObject = Enum.Parse(enumType, enumName, ignoreEnumCasing);
					filterValue = ((int) enumObject).ToString();
				}
				catch (Exception)
				{
					// Handle mismatched enum names gracefully.
				}
			}
			condition = new PropertyCondition(propertyPath, filterOperation, filterValue);
			return true;
		}

		private static bool MatchesPropertyConditions(Object obj, List<List<PropertyCondition>> conditionGroups, SearchSettings settings)
		{
			// Matches when any AND group is fully satisfied (an OR of ANDs).
			return conditionGroups.Any(group => group.All(condition => MatchesPropertyFilter(obj, condition.PropertyPath, condition.FilterValue, condition.FilterOperation, settings)));
		}

		// Distinguishes a Collection Table condition that targets the owning Object's name or the element's index from a regular
		// serialized property condition. Regular property filters always use ElementProperty.
		private enum CollectionConditionKind
		{
			ElementProperty,
			GroupName,
			Index,
		}

		private readonly struct PropertyCondition
		{
			public readonly CollectionConditionKind CollectionKind;
			public readonly string PropertyPath;
			public readonly string FilterOperation;
			public readonly string FilterValue;

			public PropertyCondition(string propertyPath, string filterOperation, string filterValue)
				: this(CollectionConditionKind.ElementProperty, propertyPath, filterOperation, filterValue)
			{
			}

			public PropertyCondition(CollectionConditionKind collectionKind, string filterOperation, string filterValue)
				: this(collectionKind, string.Empty, filterOperation, filterValue)
			{
			}

			public PropertyCondition(CollectionConditionKind collectionKind, string propertyPath, string filterOperation, string filterValue)
			{
				CollectionKind = collectionKind;
				PropertyPath = propertyPath;
				FilterOperation = filterOperation;
				FilterValue = filterValue;
			}
		}

		private static bool MatchesPropertyFilter(Object obj, string propertyPath, string filterValue, string filterOperation, SearchSettings settings)
		{
			var propertyValue = ComparableUtility.GetPropertyComparable(obj, propertyPath);
			if (propertyValue == null)
			{
				if (filterOperation == "=" || filterOperation == "==")
				{
					return filterValue == "?";
				}
				else
				{
					return false;
				}
			}
			switch (propertyValue)
			{
				case int propertyIntValue:
					if (int.TryParse(filterValue, out int intValue))
					{
						return MatchesFilterOperation(propertyIntValue, intValue, filterOperation, settings);
					}
					else if (filterValue.Length == 1)
					{
						return MatchesFilterOperation(propertyIntValue, filterValue[0], filterOperation, settings);
					}
					else
					{
						return false;
					}

				case long propertyLongValue:
					if (long.TryParse(filterValue, out long longValue))
					{
						return MatchesFilterOperation(propertyLongValue, longValue, filterOperation, settings);
					}
					else
					{
						return false;
					}

#if UNITY_2022_1_OR_NEWER
				case uint propertyUIntValue:
					if (uint.TryParse(filterValue, out uint uintValue))
					{
						return MatchesFilterOperation(propertyUIntValue, uintValue, filterOperation, settings);
					}
					else
					{
						return false;
					}

				case ulong propertyULongValue:
					if (ulong.TryParse(filterValue, out ulong ulongValue))
					{
						return MatchesFilterOperation(propertyULongValue, ulongValue, filterOperation, settings);
					}
					else
					{
						return false;
					}
#endif
				case bool propertyBoolValue:
					if (bool.TryParse(filterValue, out bool boolValue))
					{
						return MatchesFilterOperation(propertyBoolValue, boolValue, filterOperation, settings);
					}
					else if (int.TryParse(filterValue, out int boolIntValue))
					{
						return MatchesFilterOperation(propertyBoolValue, boolIntValue >= 1, filterOperation, settings);
					}
					else
					{
						return false;
					}

				case float propertyFloatValue:
					if (float.TryParse(filterValue, out float floatValue))
					{
						return MatchesFilterOperation(propertyFloatValue, floatValue, filterOperation, settings);
					}
					else
					{
						return false;
					}

				case double propertyDoubleValue:
					if (double.TryParse(filterValue, out double doubleValue))
					{
						return MatchesFilterOperation(propertyDoubleValue, doubleValue, filterOperation, settings);
					}
					else
					{
						return false;
					}

				case string propertyStringValue:
					return MatchesFilterOperation(propertyStringValue, filterValue, filterOperation, settings);

				case ColorComparable propertyColorValue:
					if (!filterValue.StartsWith("#"))
					{
						filterValue = '#' + filterValue;
					}
					if (ColorUtility.TryParseHtmlString(filterValue, out Color colorValue))
					{
						return MatchesFilterOperation(propertyColorValue, (ColorComparable) colorValue, filterOperation, settings);
					}
					else
					{
						return false;
					}

				case AnimationCurveComparable propertyAnimationCurveValue:
					// Handle case where user might copy from the inspector.
					var animationCurveJson = filterValue.Replace("UnityEditor.AnimationCurveWrapperJSON:{\"c", "{\"m_AnimationC");
					try
					{
						var animationCurveValue = JsonUtility.FromJson<SerializableAnimationCurve>(animationCurveJson).AnimationCurve;
						return MatchesFilterOperation(propertyAnimationCurveValue, (AnimationCurveComparable) animationCurveValue, filterOperation, settings);
					}
					catch (Exception)
					{
						// Handle JSON exceptions gracefully.
					}
					return false;

				case GradientComparable propertyGradientValue:
					// Handle case where user might copy from the inspector.
					var gradientJson = filterValue.Replace("UnityEditor.GradientWrapperJSON:{\"g", "{\"m_G");
					try
					{
						var gradientValue = JsonUtility.FromJson<SerializableGradient>(gradientJson).Gradient;
						return MatchesFilterOperation(propertyGradientValue, (GradientComparable) gradientValue, filterOperation, settings);
					}
					catch (Exception)
					{
						// Handle JSON exceptions gracefully.
					}
					return false;

				default:
					Debug.LogWarning($"Unsupported property type '{propertyValue.GetType()}' for property '{propertyPath}' on Object '{obj.name}'.");
					return false;
			}
		}

		private static bool MatchesGuidFilter(Object obj, string guid, SearchSettings searchSettings)
		{
			var objGuid = ComparableUtility.GetGUIDComparable(obj);
			return objGuid.ToString().MatchesSearch(guid, searchSettings);
		}

		private static bool MatchesPathFilter(Object obj, string path, SearchSettings searchSettings)
		{
			var objAssetPath = ComparableUtility.GetAssetPathComparable(obj);
			return objAssetPath.ToString().MatchesSearch(path, searchSettings);
		}

		private static bool MatchesFilterOperation<T>(T propertyValue, T filterValue, string filterOperation, SearchSettings settings) where T : IComparable
		{
			switch (filterOperation)
			{
				case "=":
				case "==":
					return propertyValue.CompareTo(filterValue) == 0;

				case "!=":
					return propertyValue.CompareTo(filterValue) != 0;

				case ">":
					return propertyValue.CompareTo(filterValue) > 0;

				case "<":
					return propertyValue.CompareTo(filterValue) < 0;

				case ">=":
					return propertyValue.CompareTo(filterValue) >= 0;

				case "<=":
					return propertyValue.CompareTo(filterValue) <= 0;

				case "~=":
				case "=~":
					return propertyValue.ToString().MatchesSearch(filterValue.ToString(), settings);

				case "!~":
				case "~!":
					return !propertyValue.ToString().MatchesSearch(filterValue.ToString(), settings);

				default:
					return false;
			}
		}
	}
}
