using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using LunaWolfStudiosEditor.ScriptableSheets.Tables;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.EditorTests
{
	[TestFixture]
	[Category(TestUtility.MainCategory)]
	public class CollectionFieldPathsTests
	{
		private const int MaxIterations = 1000;
		private const int MaxDepth = 3;

		private CollectionFieldScriptableObject m_ScriptableObject;
		private SerializedObject m_SerializedObject;

		[SetUp]
		public void SetUp()
		{
			m_ScriptableObject = ScriptableObject.CreateInstance<CollectionFieldScriptableObject>();
			m_SerializedObject = new SerializedObject(m_ScriptableObject);
		}

		[TearDown]
		public void TearDown()
		{
			m_SerializedObject?.Dispose();
			if (m_ScriptableObject != null)
			{
				Object.DestroyImmediate(m_ScriptableObject);
			}
		}

		[Test]
		public void GetCollectionFieldPaths_ReturnsTopLevelAndNestedCollectionsWithinDepth()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, MaxDepth);
			CollectionAssert.AreEquivalent(new[] { "m_MyInts", "m_MyStrings", "m_MyGameObjects", "m_MyRecordArray", "m_MyRecords", "m_MyHolder.m_Nested", "m_MyDeep.m_Holder.m_Nested" }, paths);
		}

		[Test]
		public void GetCollectionFieldPaths_ExcludesScalarFieldsAndStrings()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, MaxDepth);
			CollectionAssert.DoesNotContain(paths, "m_MyInt");
			CollectionAssert.DoesNotContain(paths, "m_MyString");
		}

		[Test]
		public void GetCollectionFieldPaths_IncludesLeafValueAndObjectReferenceCollections()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, MaxDepth);
			Assert.Contains("m_MyInts", paths);
			Assert.Contains("m_MyStrings", paths);
			Assert.Contains("m_MyGameObjects", paths);
		}

		[Test]
		public void GetCollectionFieldPaths_DepthOne_ReturnsOnlyTopLevelCollections()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, 1);
			CollectionAssert.AreEquivalent(new[] { "m_MyInts", "m_MyStrings", "m_MyGameObjects", "m_MyRecordArray", "m_MyRecords" }, paths);
		}

		[Test]
		public void GetCollectionFieldPaths_DepthTwo_IncludesOneLevelDeepButExcludesTwoLevelsDeep()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, 2);
			Assert.Contains("m_MyHolder.m_Nested", paths);
			Assert.IsFalse(paths.Exists(p => p.Contains("m_MyDeep")));
		}

		[Test]
		public void GetCollectionFieldPaths_DepthThree_IncludesTwoLevelsDeep()
		{
			var paths = m_SerializedObject.GetCollectionFieldPaths(MaxIterations, 3);
			Assert.Contains("m_MyDeep.m_Holder.m_Nested", paths);
		}

		[Test]
		public void GetCollectionRows_FiltersByElementRelativeProperty()
		{
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord>
			{
				new CollectionRecord { m_Name = "Cheap", m_Cost = 10 },
				new CollectionRecord { m_Name = "Expensive", m_Cost = 100 },
			};
			var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, 1, false) };
			var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, "m_Name", "m_Cost" };
			var referencePrefix = "m_MyRecords.Array.data[0].";

			var result = SearchFilter.GetCollectionRows("p:m_Cost>50", rows, r => r.Owner, r => $"m_MyRecords.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
				new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(1, result[0].Index);
		}

		[Test]
		public void GetCollectionRows_ResolvesColumnReference()
		{
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord>
			{
				new CollectionRecord { m_Name = "Cheap", m_Cost = 10 },
				new CollectionRecord { m_Name = "Expensive", m_Cost = 100 },
			};
			var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, 1, false) };
			var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, "m_Name", "m_Cost" };
			var referencePrefix = "m_MyRecords.Array.data[0].";

			var result = SearchFilter.GetCollectionRows("p:$E>50", rows, r => r.Owner, r => $"m_MyRecords.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
				new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(1, result[0].Index);
		}

		[Test]
		public void GetCollectionRows_FiltersByLeafValueColumnReference()
		{
			m_ScriptableObject.m_MyStrings = new List<string> { "Frostbolt", "Fireball" };
			var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, 1, false) };
			// Leaf collections expose a single Value column keyed by the descriptive value tooltip rather than an element relative path.
			var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, ColumnUtility.CollectionValueTooltip };
			var referencePrefix = "m_MyStrings.Array.data[0].";

			var result = SearchFilter.GetCollectionRows("p:$D~=fire", rows, r => r.Owner, r => $"m_MyStrings.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
				new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(1, result[0].Index);
		}

		[Test]
		public void GetCollectionRows_ExcludesAddRowsFromPropertyFilter()
		{
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord> { new CollectionRecord { m_Name = "Expensive", m_Cost = 100 } };
			var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, -1, true) };
			var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, "m_Name", "m_Cost" };
			var referencePrefix = "m_MyRecords.Array.data[0].";

			var result = SearchFilter.GetCollectionRows("p:m_Cost>50", rows, r => r.Owner, r => r.IsAddRow ? string.Empty : $"m_MyRecords.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
				new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

			Assert.AreEqual(1, result.Count);
			Assert.IsFalse(result[0].IsAddRow);
		}

		[Test]
		public void GetCollectionRows_FiltersByGroupNameColumnReference()
		{
			m_ScriptableObject.name = "Boss Loot";
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord>
			{
				new CollectionRecord { m_Name = "Sword", m_Cost = 10 },
				new CollectionRecord { m_Name = "Shield", m_Cost = 20 },
			};
			var other = ScriptableObject.CreateInstance<CollectionFieldScriptableObject>();
			other.name = "Common Loot";
			other.m_MyRecords = new List<CollectionRecord> { new CollectionRecord { m_Name = "Coin", m_Cost = 1 } };
			try
			{
				var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, 1, false), (other, 0, false) };
				var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, "m_Name", "m_Cost" };
				var referencePrefix = "m_MyRecords.Array.data[0].";

				var result = SearchFilter.GetCollectionRows("p:$B~=Boss", rows, r => r.Owner, r => $"m_MyRecords.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
					new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

				Assert.AreEqual(2, result.Count);
				Assert.IsTrue(result.TrueForAll(r => r.Owner == m_ScriptableObject));
			}
			finally
			{
				Object.DestroyImmediate(other);
			}
		}

		[Test]
		public void GetCollectionRows_FiltersByIndexColumnReference()
		{
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord>
			{
				new CollectionRecord { m_Name = "First", m_Cost = 10 },
				new CollectionRecord { m_Name = "Second", m_Cost = 20 },
				new CollectionRecord { m_Name = "Third", m_Cost = 30 },
			};
			var rows = new List<(Object Owner, int Index, bool IsAddRow)> { (m_ScriptableObject, 0, false), (m_ScriptableObject, 1, false), (m_ScriptableObject, 2, false) };
			var columnPaths = new[] { ColumnUtility.ActionsTooltip, ColumnUtility.GroupNameTooltip, ColumnUtility.CollectionIndexTooltip, "m_Name", "m_Cost" };
			var referencePrefix = "m_MyRecords.Array.data[0].";

			var result = SearchFilter.GetCollectionRows("p:$C>=1", rows, r => r.Owner, r => $"m_MyRecords.Array.data[{r.Index}].", r => r.Index, r => r.IsAddRow,
				new SearchSettings(), false, false, columnPaths, m_ScriptableObject, referencePrefix);

			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(1, result[0].Index);
			Assert.AreEqual(2, result[1].Index);
		}

		[Test]
		public void SetProperty_SetsNestedNameField_WithoutRenamingOwner()
		{
			m_ScriptableObject.name = "OwnerName";
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord> { new CollectionRecord { m_Name = "Original", m_Cost = 1 } };
			var formatSettings = new FlatFileFormatSettings();
			var tableProperty = new SerializedTableProperty(m_ScriptableObject, "m_MyRecords.Array.data[0].m_Name", string.Empty);

			tableProperty.SetProperty("Renamed", formatSettings);

			using var verify = new SerializedObject(m_ScriptableObject);
			Assert.AreEqual("Renamed", verify.FindProperty("m_MyRecords.Array.data[0].m_Name").stringValue);
			Assert.AreEqual("OwnerName", m_ScriptableObject.name);
		}

		[Test]
		public void GetCollectionFieldPaths_DetectsPopulatedRecordList()
		{
			m_ScriptableObject.m_MyRecords = new List<CollectionRecord> { new CollectionRecord { m_Name = "a", m_Cost = 1 } };
			using var serializedObject = new SerializedObject(m_ScriptableObject);
			var paths = serializedObject.GetCollectionFieldPaths(MaxIterations, MaxDepth);
			Assert.Contains("m_MyRecords", paths);
		}

		[Serializable]
		public class CollectionFieldScriptableObject : ScriptableObject
		{
			public int m_MyInt;
			public string m_MyString;
			public int[] m_MyInts;
			public List<string> m_MyStrings;
			public GameObject[] m_MyGameObjects;
			public CollectionRecord[] m_MyRecordArray;
			public List<CollectionRecord> m_MyRecords;
			public CollectionRecordHolder m_MyHolder;
			public DeepHolder m_MyDeep;
		}

		[Serializable]
		public class CollectionRecord
		{
			public string m_Name;
			public int m_Cost;
		}

		[Serializable]
		public class CollectionRecordHolder
		{
			public List<CollectionRecord> m_Nested;
		}

		[Serializable]
		public class DeepHolder
		{
			public CollectionRecordHolder m_Holder;
		}
	}
}
