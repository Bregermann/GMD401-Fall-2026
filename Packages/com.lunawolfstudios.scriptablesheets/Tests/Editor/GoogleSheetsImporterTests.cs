using LunaWolfStudiosEditor.ScriptableSheets.Importers;
using NUnit.Framework;

namespace LunaWolfStudiosEditor.ScriptableSheets.EditorTests
{
	[TestFixture]
	[Category(TestUtility.MainCategory)]
	public class GoogleSheetsImporterTests
	{
		[TestCase("Assets/Missions/11/Quest.asset", "")]
		[TestCase("Assets/Missions/11/Quest.asset", "   ")]
		[TestCase("Assets/Missions/11/Quest.asset", null)]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_EmptyFolder_ReturnsTrue(string assetPath, string folder)
		{
			Assert.IsTrue(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("Assets/Missions/11/Quest.asset", "Assets/Missions/11")]
		[TestCase("Assets/Missions/11/Quest.asset", "Assets/Missions/11/")]
		[TestCase("Assets/Missions/11/Quest.asset", "assets/missions/11")]
		[TestCase("Assets/Missions/11/Quest.asset", "Assets\\Missions\\11")]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_ExactFolderMatch_ReturnsTrue(string assetPath, string folder)
		{
			Assert.IsTrue(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("Assets/Missions/11/Side/Quest.asset", "Assets/Missions/11")]
		[TestCase("Assets/Missions/12/Quest.asset", "Assets/Missions/11")]
		[TestCase("Assets/Missions/111/Quest.asset", "Assets/Missions/11")]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_ExactFolderExcludesOthers_ReturnsFalse(string assetPath, string folder)
		{
			Assert.IsFalse(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("Assets/Missions/11/Quest.asset", "Assets/Missions/11/*")]
		[TestCase("Assets/Missions/11/Side/Quest.asset", "Assets/Missions/11/*")]
		[TestCase("Assets/Missions/11/Side/Boss/Quest.asset", "Assets/Missions/11/*")]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_WildcardMatchesSubfolders_ReturnsTrue(string assetPath, string folder)
		{
			Assert.IsTrue(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("Assets/Missions/111/Quest.asset", "Assets/Missions/11/*")]
		[TestCase("Assets/Missions/12/Quest.asset", "Assets/Missions/11/*")]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_WildcardExcludesSiblings_ReturnsFalse(string assetPath, string folder)
		{
			Assert.IsFalse(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("", "Assets/Missions/11")]
		[TestCase(null, "Assets/Missions/11")]
		[Category(TestUtility.MainCategory)]
		public void IsAssetPathInFolder_EmptyAssetPathWithFolder_ReturnsFalse(string assetPath, string folder)
		{
			Assert.IsFalse(GoogleSheetsImporter.IsAssetPathInFolder(assetPath, folder));
		}

		[TestCase("Assets/Missions/11", "Assets/Missions/11")]
		[TestCase("Assets/Missions/11/", "Assets/Missions/11")]
		[TestCase("Assets/Missions/11/*", "Assets/Missions/11")]
		[TestCase("Assets\\Missions\\11/*", "Assets/Missions/11")]
		[TestCase("", "")]
		[TestCase(null, "")]
		[Category(TestUtility.MainCategory)]
		public void GetFolderRoot_StripsWildcardAndTrailingSlash(string folder, string expected)
		{
			Assert.AreEqual(expected, GoogleSheetsImporter.GetFolderRoot(folder));
		}
	}
}
