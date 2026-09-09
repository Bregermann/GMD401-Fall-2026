using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using NUnit.Framework;

namespace LunaWolfStudiosEditor.ScriptableSheets.EditorTests
{
	[TestFixture]
	[Category(TestUtility.MainCategory)]
	public class RichTextUtilityTests
	{
		[Test]
		public void WrapText_WrapsFullSelection()
		{
			var result = RichTextUtility.WrapText("Hello", 0, 5, "<b>", "</b>", out var selectStart, out var selectEnd);
			Assert.AreEqual("<b>Hello</b>", result);
			Assert.AreEqual(3, selectStart);
			Assert.AreEqual(8, selectEnd);
		}

		[Test]
		public void WrapText_WrapsPartialSelection()
		{
			var result = RichTextUtility.WrapText("abcdef", 2, 4, "<i>", "</i>", out var selectStart, out var selectEnd);
			Assert.AreEqual("ab<i>cd</i>ef", result);
			Assert.AreEqual(5, selectStart);
			Assert.AreEqual(7, selectEnd);
		}

		[Test]
		public void WrapText_HandlesReversedSelection()
		{
			var result = RichTextUtility.WrapText("Hello", 5, 0, "<b>", "</b>", out var selectStart, out var selectEnd);
			Assert.AreEqual("<b>Hello</b>", result);
			Assert.AreEqual(3, selectStart);
			Assert.AreEqual(8, selectEnd);
		}

		[Test]
		public void WrapText_InsertsEmptyTagsWhenNoSelection()
		{
			var result = RichTextUtility.WrapText("ab", 1, 1, "<b>", "</b>", out var selectStart, out var selectEnd);
			Assert.AreEqual("a<b></b>b", result);
			Assert.AreEqual(4, selectStart);
			Assert.AreEqual(4, selectEnd);
		}

		[Test]
		public void WrapText_ClampsOutOfRangeIndices()
		{
			var result = RichTextUtility.WrapText("Hi", -3, 99, "<b>", "</b>", out var selectStart, out var selectEnd);
			Assert.AreEqual("<b>Hi</b>", result);
			Assert.AreEqual(3, selectStart);
			Assert.AreEqual(5, selectEnd);
		}

		[Test]
		public void WrapText_HandlesNullText()
		{
			var result = RichTextUtility.WrapText(null, 0, 0, "<b>", "</b>", out var selectStart, out var selectEnd);
			Assert.AreEqual("<b></b>", result);
			Assert.AreEqual(3, selectStart);
			Assert.AreEqual(3, selectEnd);
		}

		[Test]
		public void WrapText_WrapsWithColorTag()
		{
			var result = RichTextUtility.WrapText("x", 0, 1, "<color=#FF0000>", "</color>", out var selectStart, out var selectEnd);
			Assert.AreEqual("<color=#FF0000>x</color>", result);
			Assert.AreEqual(15, selectStart);
			Assert.AreEqual(16, selectEnd);
		}
	}
}
