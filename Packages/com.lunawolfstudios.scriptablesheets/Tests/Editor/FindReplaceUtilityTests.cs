using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using NUnit.Framework;

namespace LunaWolfStudiosEditor.ScriptableSheets.EditorTests
{
	[TestFixture]
	[Category(TestUtility.MainCategory)]
	public class FindReplaceUtilityTests
	{
		[TestCase("aXaXa", "a", true, 3)]
		[TestCase("aaaa", "aa", true, 2)]
		[TestCase("Hello hello", "hello", true, 1)]
		[TestCase("Hello hello", "hello", false, 2)]
		[TestCase("abc", "z", true, 0)]
		[TestCase("", "a", true, 0)]
		[TestCase("abc", "", true, 0)]
		[Category(TestUtility.MainCategory)]
		public void CountOccurrences_ReturnsExpectedCount(string text, string find, bool caseSensitive, int expected)
		{
			Assert.AreEqual(expected, FindReplaceUtility.CountOccurrences(text, find, caseSensitive));
		}

		[TestCase("a.b.c", ".", "-", true, "a-b-c")]
		[TestCase("Hello hello", "hello", "world", true, "Hello world")]
		[TestCase("Hello hello", "hello", "world", false, "world world")]
		[TestCase("aaa", "a", "", true, "")]
		[TestCase("abc", "z", "y", true, "abc")]
		[Category(TestUtility.MainCategory)]
		public void ReplaceOccurrences_ReplacesExpected(string text, string find, string replace, bool caseSensitive, string expected)
		{
			Assert.AreEqual(expected, FindReplaceUtility.ReplaceOccurrences(text, find, replace, caseSensitive));
		}

		[Test]
		public void ReplaceOccurrences_PreservesCaseOfSurroundingText()
		{
			var result = FindReplaceUtility.ReplaceOccurrences("FOO foo Foo", "foo", "bar", false);
			Assert.AreEqual("bar bar bar", result);
		}
	}
}
