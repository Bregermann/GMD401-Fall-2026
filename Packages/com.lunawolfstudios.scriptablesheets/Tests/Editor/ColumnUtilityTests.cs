using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using NUnit.Framework;
using UnityEditor.IMGUI.Controls;

namespace LunaWolfStudiosEditor.ScriptableSheets.EditorTests
{
	[TestFixture]
	[Category(TestUtility.MainCategory)]
	public class ColumnUtilityTests
	{
		[Test]
		public void GetClampedColumns_Returns_AllColumns_When_ColumnsCount_LessThan_MaxColumns()
		{
			var columns = new MultiColumnHeaderState.Column[]
			{
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column()
			};
			var maxColumns = 5;
			var result = columns.GetClampedColumns(maxColumns);
			Assert.AreEqual(columns.Length, result.Length);
			for (var i = 0; i < columns.Length; i++)
			{
				Assert.AreEqual(i, result[i]);
			}
		}

		[Test]
		public void GetClampedColumns_Returns_MaxColumns_When_ColumnsCount_GreaterThan_MaxColumns()
		{
			var columns = new MultiColumnHeaderState.Column[]
			{
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column(),
				new MultiColumnHeaderState.Column()
			};
			var maxColumns = 3;
			var result = columns.GetClampedColumns(maxColumns);
			Assert.AreEqual(maxColumns, result.Length);
			for (var i = 0; i < maxColumns; i++)
			{
				Assert.AreEqual(i, result[i]);
			}
		}

		[TestCase(false, 3, IndexFormat.Alpha, "")]
		[TestCase(false, 3, IndexFormat.Numeric, "")]
		[TestCase(true, 0, IndexFormat.Numeric, "0 ")]
		[TestCase(true, 12, IndexFormat.Numeric, "12 ")]
		[TestCase(true, 0, IndexFormat.Alpha, "A ")]
		[TestCase(true, 25, IndexFormat.Alpha, "Z ")]
		[TestCase(true, 26, IndexFormat.Alpha, "AA ")]
		[TestCase(true, 27, IndexFormat.Alpha, "AB ")]
		[Category(TestUtility.MainCategory)]
		public void GetColumnIndexLabel_ReturnsExpectedLabel(bool showIndex, int index, IndexFormat format, string expected)
		{
			Assert.AreEqual(expected, ColumnUtility.GetColumnIndexLabel(showIndex, index, format));
		}

		[TestCase(0, "A")]
		[TestCase(25, "Z")]
		[TestCase(26, "AA")]
		[TestCase(27, "AB")]
		[TestCase(701, "ZZ")]
		[TestCase(702, "AAA")]
		[Category(TestUtility.MainCategory)]
		public void GetAlphaIndexLabel_ConvertsIndexToLetters(int index, string expected)
		{
			Assert.AreEqual(expected, ColumnUtility.GetAlphaIndexLabel(index));
		}

		[TestCase("0 Name", "Name")]
		[TestCase("12 Health Points", "Health Points")]
		[TestCase("AA Reward", "Reward")]
		[TestCase("Name", "Name")]
		[TestCase("", "")]
		[TestCase(null, null)]
		[Category(TestUtility.MainCategory)]
		public void RemoveColumnIndexLabel_StripsLeadingIndex(string headerText, string expected)
		{
			Assert.AreEqual(expected, ColumnUtility.RemoveColumnIndexLabel(headerText));
		}
	}
}
