using LunaWolfStudiosEditor.ScriptableSheets.Comparables;
using LunaWolfStudiosEditor.ScriptableSheets.Importers;
using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using LunaWolfStudiosEditor.ScriptableSheets.PastePad;
using LunaWolfStudiosEditor.ScriptableSheets.Popups;
using LunaWolfStudiosEditor.ScriptableSheets.Scanning;
using LunaWolfStudiosEditor.ScriptableSheets.Settings;
using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using LunaWolfStudiosEditor.ScriptableSheets.Tables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public class ScriptableSheetsEditorWindow : EditorWindow, IHasCustomMenu
	{
		public static readonly List<ScriptableSheetsEditorWindow> Instances = new List<ScriptableSheetsEditorWindow>();

		private static readonly Dictionary<Type, MonoScript> s_MonoScriptCache = new Dictionary<Type, MonoScript>();

		private const string JsonExtension = "json";

		private static WindowSessionState s_NextWindowSessionStateToLoad;
		private static bool s_IsNextWindowSessionStateClone;
		private static bool s_IsNewWindowSessionState;

		// Asset queued by the "Open in Scriptable Sheets" context menu to be selected by the next window that opens.
		private static Object s_PendingOpenAsset;
		private static int s_FocusCounter;

		private readonly ObjectScanner m_Scanner = new ObjectScanner();
		private readonly Paginator m_Paginator = new Paginator();
		private readonly TableNav<ITableProperty> m_TableNav = new TableNav<ITableProperty>();
		private readonly TableSmartPaste<ITableProperty> m_TableSmartPaste = new TableSmartPaste<ITableProperty>();

		private Object m_MainAsset;
		private int m_SelectedMainAssetIndex;
		private GoogleSheetsImporter m_GoogleSheetsImporter;
		private bool m_IsGoogleSheetDownloading;
		private string m_PendingImportFolder = string.Empty;

		private List<Object> m_SortedObjects = new List<Object>();
		// Cached search results so the filter is not re-evaluated over the whole set every repaint. Recomputed only when the
		// search configuration changes or m_SearchResultsDirty is set (Object set, column layout, cell edits, or paste/import).
		private List<Object> m_CachedMatchingObjects = new List<Object>();
		// Cached, filtered Collection Table rows. Used instead of m_CachedMatchingObjects while a collection is expanded so the
		// search filter evaluates element relative property paths against each element rather than the owning Objects.
		private List<CollectionRow> m_CachedCollectionRows = new List<CollectionRow>();
		// Frozen sort order of the full Collection Table row set. Like m_SortedObjects, it is re-sorted only when the sort, type,
		// or collection changes, not while editing fields, so editing a value never reorders the row the user is on.
		private List<CollectionRow> m_SortedCollectionRows = new List<CollectionRow>();
		private bool m_CollectionRowsResortNeeded = true;
		private bool m_SearchResultsDirty = true;
		private string m_LastSearchInput;
		private bool m_LastSearchCaseSensitive;
		private bool m_LastSearchStartsWith;
		private bool m_LastUseStringEnums;
		private bool m_LastIgnoreEnumCasing;
		private Type m_SelectedType;
		private Type m_PreviousSelectedType;
		private string m_NewAssetPath;
		private int m_PreviousSelectedPage = 1;
		private int m_PendingPageRestore;
		// Cached on the Layout event so the selected cell label emits the same control count during Repaint.
		private GUIContent m_SelectedCellContent;

		private Table<ITableProperty> m_PropertyTable;
		private TableAction m_TableAction;
		private int[] m_CachedVisibleColumns;
		// Captures the selected cell when auto generate defers a smart paste so the deferred paste starts from the same cell.
		private Vector2Int? m_DeferredActionCoordinate;
		// True while auto generate has queued an object creation so the table build is skipped until the Objects exist.
		private bool m_AutoGeneratePending;
		private string m_SelectedFilepath;
		private string m_ImportedFileContents;
		private bool m_IsImportJson;

		private SearchField m_SearchField;

		private MultiColumnHeaderState.Column[] m_Columns;
		private MultiColumnHeaderState m_MultiColumnHeaderState;
		private SheetsMultiColumnHeader m_MultiColumnHeader;
		private Dictionary<string, int> m_MultiColumnTooltipPaths;
		private string[] m_ColumnPropertyPaths;
		// Collection View state. m_SelectedCollectionPath is the array/list property path expanded into rows, or empty for the
		// default column behavior. The cached field arrays back the toolbar dropdown and are rebuilt with the column layout.
		// Index 0 is always the empty "None" path. Populated in RefreshColumnLayout from the selected type's top level fields.
		private string m_SelectedCollectionPath = string.Empty;
		private string[] m_CollectionFieldPaths;
		private string[] m_CollectionFieldDisplayNames;
		// The Name column is always built right after the Actions column.
		private const int NameColumnIndex = 1;
		// The Object the columns were built from, used to enumerate references for the Map Asset Preview menu.
		private Object m_AssetPreviewRepresentative;
		// SerializedObjects wrapped while resolving multi hop preview chains, cleared each frame.
		private readonly Dictionary<Object, SerializedObject> m_PreviewSerializedObjectCache = new Dictionary<Object, SerializedObject>();
		// Row SerializedObjects reused across repaints so each drawn Object is wrapped once instead of every frame.
		// Unlike the preview cache this persists between frames and is only cleared when the scanned Object set changes.
		private readonly Dictionary<Object, SerializedObject> m_RowSerializedObjectCache = new Dictionary<Object, SerializedObject>();
		// Asset paths cached per Object so the per-row AssetDatabase.GetAssetPath lookup is not repeated every repaint.
		// Shares the row cache lifecycle (cleared on scan/type/main asset change) and is invalidated on rename and delete.
		private readonly Dictionary<Object, string> m_AssetPathCache = new Dictionary<Object, string>();
		private bool m_SortingChanged;
		private int m_VisibleColumnCopyIndex;

		private Rect m_ScrollViewArea;
		private Rect m_TableScrollViewRect;
		private Vector2 m_ScrollPosition;
		private Vector2 m_SheetAssetScrollPosition;
		private Vector2 m_ObjectTypeScrollPosition;

		private ScriptableSheetsSettings m_Settings;
		private bool m_WaitingForColumnRefresh;
		private bool m_Reinitialized;

		// Cached window session data.
		private SheetAsset m_SelectableSheetAssets;
		private SheetAsset m_SelectedSheetAsset;
		private int m_SelectedTypeIndex;
		private Dictionary<SheetAsset, HashSet<int>> m_PinnedIndexSets;
		private Dictionary<SheetAsset, int> m_SelectedTypeIndexes;
		private int m_NewAmount;
		private string m_SearchInput;
		private Dictionary<string, TableLayout> m_TableLayouts;

		// Serialized so Unity's Undo system can snapshot it. Holds the JSON of this window's column layouts and search filter
		// captured around each column context menu action. The live working copies are m_TableLayouts and m_SearchInput.
		[SerializeField]
		private string m_ColumnLayoutUndoState;
		// The last undo state we applied, used to ignore unrelated undo/redo operations elsewhere in the editor.
		private string m_LastAppliedColumnLayoutUndoState;

		private bool m_Initialized;

		private Object m_PendingOpenAsset;
		private int m_PendingOpenAttempts;
		private const int PendingOpenMaxAttempts = 8;
		private bool m_PendingOpenScheduled;
		private int m_FocusOrder;

		public int FocusOrder => m_FocusOrder;

		[MenuItem("Window/Scriptable Sheets")]
		public static void ShowWindow()
		{
			var window = CreateInstance<ScriptableSheetsEditorWindow>();
			window.minSize = new Vector2(600, 400);
			window.Show();
		}

		private void OnEnable()
		{
			Instances.Add(this);
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			m_SearchField = new SearchField();
			m_Settings = ScriptableSheetsSettings.instance;

			WindowSessionState windowSessionState;
			if (s_IsNewWindowSessionState)
			{
				// Load a new window session state when selected from the context menu.
				// In this case null will use default settings for a new window.
				windowSessionState = m_Settings.LoadWindowSessionState(null);
				s_IsNewWindowSessionState = false;
			}
			else if (s_NextWindowSessionStateToLoad == null)
			{
				// Load this window based on its instance id and position.
				// If no matches are found then load any recently closed window.
				// If none are found then load a new window.
				windowSessionState = m_Settings.LoadWindowSessionStateFromWindow(this);
			}
			else
			{
				// Load a specific Window that was opened from the context menu.
				windowSessionState = m_Settings.LoadWindowSessionState(s_NextWindowSessionStateToLoad);
				if (!s_IsNextWindowSessionStateClone)
				{
					m_Settings.DeleteWindowSessionState(s_NextWindowSessionStateToLoad);
				}
				s_NextWindowSessionStateToLoad = null;
				s_IsNextWindowSessionStateClone = false;
			}

			m_SelectableSheetAssets = windowSessionState.SelectableSheetAssets;
			m_SelectedSheetAsset = windowSessionState.SelectedSheetAsset;
			m_SelectedTypeIndex = windowSessionState.SelectedTypeIndex;
			m_PinnedIndexSets = windowSessionState.PinnedIndexSets;
			m_SelectedTypeIndexes = windowSessionState.SelectedTypeIndexes;
			m_NewAmount = windowSessionState.NewAmount;
			m_SearchInput = windowSessionState.SearchInput;
			m_TableLayouts = windowSessionState.TableLayouts;
			m_LastAppliedColumnLayoutUndoState = m_ColumnLayoutUndoState;

			// Pick up an asset queued from the "Open in Scriptable Sheets" context menu so this freshly opened window selects
			// its Sheet Asset before the first scan and navigates to the asset once the table is built.
			if (s_PendingOpenAsset != null)
			{
				m_PendingOpenAsset = s_PendingOpenAsset;
				s_PendingOpenAsset = null;
				m_PendingOpenAttempts = 0;
				m_PendingOpenScheduled = false;
				if (SheetAssetExtensions.TryGetSheetAsset(m_PendingOpenAsset, out var pendingSheetAsset))
				{
					m_SelectableSheetAssets |= pendingSheetAsset;
					m_SelectedSheetAsset = pendingSheetAsset;
				}
			}

			// Workaround because we cannot directly scan for objects within OnEnable.
			// This is due to a bug with calling AssetDatabase.Refresh within OnEnable when opening a window via custom Layout.
			m_Reinitialized = true;

			// Defer titleContent assignment to ensure it isn't overwritten by Unity's layout restoration system.
			// Without this, multiple windows with the same name may share the same GUIContent instance on startup, causing renaming issues.
			// But also initialize regardless incase the window is immediately destroyed from a layout change.
			InitializeTitleContent(windowSessionState.Title);
			m_Initialized = false;
			EditorApplication.delayCall += () => InitializeTitleContent(windowSessionState.Title);
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			ClearRowSerializedObjectCache();
		}

		private void OnFocus()
		{
			// Property values may have changed in another Inspector while the window was unfocused, so refresh the cached
			// search results when the user returns to the sheet.
			m_SearchResultsDirty = true;
			m_FocusOrder = ++s_FocusCounter;
		}

		private void OnDestroy()
		{
			// Workaround for when a single docked window is maximized then minimized, Unity briefly clones then destroys the window.
			// This ensures we're not saving each clone.
			if (m_Initialized || Instances.Count != 2 || Instances[0].titleContent.text != Instances[1].titleContent.text)
			{
				m_Settings.WindowDestroyed();
			}
			Instances.Remove(this);
		}

		private void OnInspectorUpdate()
		{
			if (m_Settings.Workload.AutoUpdate)
			{
				Repaint();
			}
		}

		private void InitializeTitleContent(string title)
		{
			titleContent = SheetsContent.Window.GetDefaultTitleContent();
			if (!string.IsNullOrWhiteSpace(title))
			{
				titleContent.text = title;
			}
			m_Initialized = true;
		}

		public void ForceRefreshColumnLayout()
		{
			m_TableNav.ResetTextFieldEditing();
			m_WaitingForColumnRefresh = true;
		}

		public WindowSessionState GetWindowSessionState()
		{
			var windowSession = new WindowSessionState()
			{
				InstanceId = this.GetInstanceIdCompat(),
				Title = titleContent.text,
				Position = position.ToString(),
				SelectableSheetAssets = m_SelectableSheetAssets,
				SelectedSheetAsset = m_SelectedSheetAsset,
				SelectedTypeIndex = m_SelectedTypeIndex,
				PinnedIndexSets = m_PinnedIndexSets,
				SelectedTypeIndexes = m_SelectedTypeIndexes,
				NewAmount = m_NewAmount,
				SearchInput = m_SearchInput,
				TableLayouts = m_TableLayouts,
			};
			return windowSession;
		}

		public void ResetSelectedType()
		{
			m_PreviousSelectedType = null;
			// Restore the type that was last selected for this Sheet Asset, falling back to the first pinned type.
			if (!m_SelectedTypeIndexes.TryGetValue(m_SelectedSheetAsset, out m_SelectedTypeIndex))
			{
				m_SelectedTypeIndex = m_PinnedIndexSets[m_SelectedSheetAsset].FirstOrDefault();
			}
		}

		public void ScanObjects()
		{
			// The scan rebuilds the Object set so previously cached row SerializedObjects no longer apply.
			ClearRowSerializedObjectCache();
			m_Scanner.ScanObjects(m_Settings.ObjectManagement.Scan, m_SelectedSheetAsset);
		}

		// Opens a brand new window that will navigate to the given asset once it initializes.
		internal static void OpenInNewWindow(Object asset)
		{
			s_PendingOpenAsset = asset;
			s_IsNewWindowSessionState = true;
			ShowWindow();
		}

		// Navigates this existing window to the given asset.
		public void OpenAsset(Object asset)
		{
			if (asset == null || !SheetAssetExtensions.TryGetSheetAsset(asset, out var sheetAsset))
			{
				return;
			}
			m_SelectableSheetAssets |= sheetAsset;
			m_SelectedSheetAsset = sheetAsset;
			m_PendingOpenAsset = asset;
			m_PendingOpenAttempts = 0;
			m_PendingOpenScheduled = false;
			// Force a column refresh and a fresh scan for the selected Sheet Asset so the asset's type can be resolved.
			m_PreviousSelectedType = null;
			ScanObjects();
			Focus();
			Repaint();
		}

		private void PreparePendingOpenSelection()
		{
			if (!SheetAssetExtensions.TryGetSheetAsset(m_PendingOpenAsset, out var sheetAsset))
			{
				m_PendingOpenAsset = null;
				return;
			}
			m_SelectableSheetAssets |= sheetAsset;
			if (m_SelectedSheetAsset != sheetAsset)
			{
				m_SelectedSheetAsset = sheetAsset;
				ScanObjects();
			}
			else if (m_Scanner.ObjectTypes == null || m_Scanner.ObjectTypes.Length == 0)
			{
				ScanObjects();
			}

			var targetType = m_PendingOpenAsset.GetType();
			var typeIndex = Array.IndexOf(m_Scanner.ObjectTypes, targetType);
			if (typeIndex < 0)
			{
				// The asset's type is not in the scan results, so it is excluded or outside the configured scan path.
				WarnPendingOpenNotFound();
				return;
			}
			m_SelectedTypeIndex = typeIndex;
			m_SelectedType = targetType;
			m_SelectedTypeIndexes[sheetAsset] = typeIndex;

			if (m_Settings.UserInterface.AutoPin)
			{
				m_PinnedIndexSets[sheetAsset].Add(typeIndex);
			}

			// Show the asset as a normal row rather than expanded collection element rows.
			if (m_SelectedCollectionPath.Length > 0)
			{
				m_SelectedCollectionPath = string.Empty;
				GetTableLayout().SelectedCollectionPath = string.Empty;
				m_CollectionRowsResortNeeded = true;
			}

			PreparePendingOpenMainAsset(sheetAsset, targetType);
		}

		private void PreparePendingOpenMainAsset(SheetAsset sheetAsset, Type targetType)
		{
			if (sheetAsset != SheetAsset.ScriptableObject || !m_Settings.UserInterface.SubAssetFilters)
			{
				return;
			}
			if (!AssetDatabase.IsSubAsset(m_PendingOpenAsset))
			{
				return;
			}
			if (!m_Scanner.SubAssetsByTypeAndMainAsset.TryGetValue(targetType, out var subAssetByMainAsset))
			{
				return;
			}
			var assetPath = AssetDatabase.GetAssetPath(m_PendingOpenAsset);
			var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
			// Mirror the dropdown's null filtering so the computed index aligns with what OnGUI draws for the main asset popup.
			var mainAssets = subAssetByMainAsset.Keys.Where(key => key != null).ToList();
			var mainAssetIndex = mainAssets.IndexOf(mainAsset);
			if (mainAssetIndex >= 0)
			{
				m_SelectedMainAssetIndex = mainAssetIndex;
				GetTableLayout().MainAssetIndex = mainAssetIndex;
			}
		}

		private void ResolvePendingOpenDeferred()
		{
			m_PendingOpenScheduled = false;
			if (m_PendingOpenAsset == null || m_SelectedType == null)
			{
				return;
			}

			var index = m_CachedMatchingObjects.IndexOf(m_PendingOpenAsset);
			if (index < 0)
			{
				// The asset exists for this type but an active search filters it out, so clear the filter and try again.
				if (!string.IsNullOrEmpty(m_SearchInput) && m_SortedObjects.Contains(m_PendingOpenAsset))
				{
					m_SearchInput = string.Empty;
					m_SearchResultsDirty = true;
					Repaint();
					return;
				}
				// Allow a few attempts for a sub asset or main asset change to settle before giving up.
				if (++m_PendingOpenAttempts > PendingOpenMaxAttempts)
				{
					WarnPendingOpenNotFound();
				}
				else
				{
					Repaint();
				}
				return;
			}

			var rowsPerPage = m_Settings.Workload.RowsPerPage;
			if (rowsPerPage > 0)
			{
				var targetPage = index / rowsPerPage + 1;
				// Apply through the normal page restore path so the page is set consistently during the next frame.
				m_PendingPageRestore = targetPage;
				GetTableLayout().CurrentPage = targetPage;
				// Scroll the asset's row to the top of the view so virtualization draws it and its cell can be focused.
				var rowOnPage = index - (targetPage - 1) * rowsPerPage;
				var rowHeight = EditorGUIUtility.singleLineHeight * m_Settings.UserInterface.RowLineHeight;
				m_ScrollPosition = new Vector2(0, rowOnPage * rowHeight);
			}
			Repaint();
		}

		private void FocusPendingOpenRow(int rowIndex, Object rootObject)
		{
			var controlName = GetPendingOpenControlName(rowIndex);
			if (controlName == null)
			{
				return;
			}
			m_TableNav.RequestFocus(controlName);
			Selection.activeObject = rootObject;
			EditorGUIUtility.PingObject(rootObject);
			m_PendingOpenAsset = null;
			m_PendingOpenAttempts = 0;
			Repaint();
		}

		private string GetPendingOpenControlName(int rowIndex)
		{
			if (m_PropertyTable == null || rowIndex < 0 || rowIndex >= m_PropertyTable.Rows)
			{
				return null;
			}
			if (m_MultiColumnHeader.IsColumnVisible(NameColumnIndex))
			{
				var visibleNameColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(NameColumnIndex);
				if (m_PropertyTable.IsValidCoordinate(rowIndex, visibleNameColumnIndex)
					&& m_PropertyTable.TryGet(rowIndex, visibleNameColumnIndex, out ITableProperty nameProperty))
				{
					return nameProperty.ControlName;
				}
			}
			for (var column = 0; column < m_PropertyTable.Columns; column++)
			{
				if (m_PropertyTable.TryGet(rowIndex, column, out ITableProperty property))
				{
					return property.ControlName;
				}
			}
			return null;
		}

		private void WarnPendingOpenNotFound()
		{
			var assetPath = AssetDatabase.GetAssetPath(m_PendingOpenAsset);
			Debug.LogWarning($"Scriptable Sheets could not find '{assetPath}' under the current scan path(s):\n{m_Settings.ObjectManagement.Scan.GetJoinedScanPaths()}\nUpdate the scan path under Project Settings -> Preferences -> Scriptable Sheets -> Object Management -> Scanning to include this asset.");
			m_PendingOpenAsset = null;
			m_PendingOpenAttempts = 0;
		}

		private bool IsScriptableObject()
		{
			return m_SelectedSheetAsset == SheetAsset.ScriptableObject && m_SelectedType != null && m_SelectedType.IsSubclassOf(typeof(ScriptableObject));
		}

		// True when Collection View is enabled and a valid collection field is selected for the current type, meaning the table
		// expands that array or list into rows instead of showing its elements as columns.
		private bool IsCollectionTable()
		{
			return m_Settings.UserInterface.CollectionTables && !string.IsNullOrEmpty(m_SelectedCollectionPath);
		}

		// Collects the selectable Collection View fields from the given SerializedObject and restores the remembered selection.
		// Index 0 is always the empty "None" path. Array or list fields qualify up to the Collection Table Depth, descending through
		// serializable class fields. Strings are excluded since their elements are characters rather than editable records. A
		// remembered selection that no longer exists falls back to None.
		private void RefreshCollectionFields(SerializedObject serializedObject)
		{
			var maxCollectionDepth = Mathf.Clamp(m_Settings.Workload.CollectionTableDepth, 1, 10);
			var collectionPaths = serializedObject.GetCollectionFieldPaths(m_Settings.Workload.MaxIterations, maxCollectionDepth);
			m_CollectionFieldPaths = new string[collectionPaths.Count + 1];
			m_CollectionFieldDisplayNames = new string[collectionPaths.Count + 1];
			m_CollectionFieldPaths[0] = string.Empty;
			m_CollectionFieldDisplayNames[0] = "None";
			for (var i = 0; i < collectionPaths.Count; i++)
			{
				m_CollectionFieldPaths[i + 1] = collectionPaths[i];
				m_CollectionFieldDisplayNames[i + 1] = GetCollectionDisplayName(serializedObject, collectionPaths[i]);
			}
			m_SelectedCollectionPath = GetTableLayout().SelectedCollectionPath ?? string.Empty;
			if (m_SelectedCollectionPath.Length > 0 && Array.IndexOf(m_CollectionFieldPaths, m_SelectedCollectionPath) < 0)
			{
				m_SelectedCollectionPath = string.Empty;
			}
		}

		// Builds the dropdown label for a collection path, joining nested segment display names with a slash.
		private string GetCollectionDisplayName(SerializedObject serializedObject, string collectionPath)
		{
			var segments = collectionPath.Split('.');
			var displayName = string.Empty;
			var prefix = string.Empty;
			for (var i = 0; i < segments.Length; i++)
			{
				prefix = i == 0 ? segments[i] : $"{prefix}.{segments[i]}";
				var property = serializedObject.FindProperty(prefix);
				var segmentName = property != null ? property.displayName : segments[i];
				displayName = i == 0 ? segmentName : $"{displayName}/{segmentName}";
			}
			return displayName;
		}

		private void OnUndoRedoPerformed()
		{
			// Unity restored our serialized undo state to a different snapshot, so reapply the column layout it represents.
			if (m_ColumnLayoutUndoState != m_LastAppliedColumnLayoutUndoState)
			{
				ApplyColumnLayoutUndoState();
			}
			// An undo or redo can change property values that an active filter matches on, so refresh the cached results.
			m_SearchResultsDirty = true;
			Repaint();
		}

		private void OnGUI()
		{
			if (m_Reinitialized || m_Settings.Workload.AutoScan)
			{
				m_Reinitialized = false;
				ScanObjects();
			}

			if (m_PendingOpenAsset != null)
			{
				PreparePendingOpenSelection();
			}

			// Handle page navigation shortcuts before table nav so they aren't consumed as cell navigation.
			HandlePageNavigationShortcuts();

			// Handle table nav and smart paste inputs up front to prevent the events being used.
			if (m_TableNav.UpdateFocusedCoordinate(m_PropertyTable, IsScriptableObject(), m_Settings.UserInterface.ShowReadOnly))
			{
				// Repaint if there was keyboard navigation to force column highlighting for non text fields.
				if (m_TableNav.WasKeyboardNav)
				{
					Repaint();
				}
				if (!m_TableNav.IsEditingTextField)
				{
					if (m_Settings.DataTransfer.SmartPasteEnabled && m_TableSmartPaste.UpdatePasteContent())
					{
						SetTableAction(TableAction.SmartPaste);
					}
					else
					{
						m_TableSmartPaste.TryCopySingleCell(m_PropertyTable, m_TableNav.FocusedCoordinate, GetFlatFileFormatSettings());
					}
				}
			}

			if (m_Settings.Workload.Debug)
			{
				Debug.Log($"Focused cell coordinate '{m_TableNav.FocusedCoordinate}'.");
			}

			EditorGUILayout.BeginHorizontal();
			GUI.SetNextControlName(string.Empty);
			var previousSelectedSheetAsset = m_SelectedSheetAsset;
			m_SelectableSheetAssets = (SheetAsset) EditorGUILayout.EnumFlagsField(string.Empty, m_SelectableSheetAssets, SheetLayout.Property);
			if (m_SelectableSheetAssets == SheetAsset.Default)
			{
				m_SelectableSheetAssets = m_Settings.UserInterface.DefaultSheetAssets;
				m_SelectedSheetAsset = m_SelectableSheetAssets.FirstFlagOrDefault();
			}
			if (GUILayout.Button(SheetsContent.Button.Rescan, SheetLayout.InlineButton))
			{
				ScanObjects();
				EditorGUILayout.EndHorizontal();
				return;
			}
			m_SheetAssetScrollPosition = EditorGUILayout.BeginScrollView(m_SheetAssetScrollPosition, SheetLayout.DoubleLineHeight);
			EditorGUILayout.BeginHorizontal();
			foreach (SheetAsset sheetAsset in Enum.GetValues(typeof(SheetAsset)))
			{
				if (sheetAsset != SheetAsset.Default)
				{
					var isSelected = m_SelectedSheetAsset == sheetAsset;
					if (m_SelectableSheetAssets.HasFlag(sheetAsset))
					{
						var assetNameContent = SheetsContent.Label.GetAssetNameContent(sheetAsset.ToString());
						var width = GUI.skin.button.CalcSize(assetNameContent).x;
						if (GUILayout.Button(assetNameContent, SheetLayout.GetButtonStyle(isSelected), GUILayout.Width(width)))
						{
							m_SelectedSheetAsset = sheetAsset;
						}
					}
					else if (isSelected)
					{
						// If the selected sheet asset is disabled then default to the next selected sheet asset.
						m_SelectedSheetAsset = m_SelectableSheetAssets.FirstFlagOrDefault();
					}
					if (previousSelectedSheetAsset != m_SelectedSheetAsset)
					{
						// Remember the selected type for the previous Sheet Asset so it can be restored when switching back.
						m_SelectedTypeIndexes[previousSelectedSheetAsset] = m_SelectedTypeIndex;
						var hasRememberedType = m_SelectedTypeIndexes.ContainsKey(m_SelectedSheetAsset);
						ResetSelectedType();
						previousSelectedSheetAsset = m_SelectedSheetAsset;
						ScanObjects();

						// If there are no pins or remembered type then use the default type when the Sheet Asset changes. Ignore for ScriptableObjects.
						if (!hasRememberedType && m_SelectedSheetAsset != SheetAsset.ScriptableObject && m_PinnedIndexSets[m_SelectedSheetAsset].Count <= 0 && m_Scanner.ObjectTypes.Length > 0)
						{
							var defaultSheetAssetType = m_SelectedSheetAsset.GetDefaultType();
							for (var i = 0; i < m_Scanner.ObjectTypes.Length; i++)
							{
								if (m_Scanner.ObjectTypes[i].FullName == defaultSheetAssetType)
								{
									m_SelectedTypeIndex = i;
									break;
								}
							}
						}

						// Exit early after Scanning Objects.
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.EndScrollView();
						EditorGUILayout.EndHorizontal();
						return;
					}
				}
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndScrollView();

			var isScriptableObject = IsScriptableObject();
			if (m_Scanner.ObjectsByType.Keys.Count <= 0)
			{
				if (m_SelectedType == null && m_SelectedSheetAsset == SheetAsset.ScriptableObject && m_Scanner.ObjectTypes.Length > 0)
				{
					m_SelectedTypeIndex = 0;
					m_SelectedType = m_Scanner.ObjectTypes[m_SelectedTypeIndex];
					isScriptableObject = IsScriptableObject();
				}
				if (!isScriptableObject || m_Settings.ObjectManagement.Scan.Option != ScanOption.Assembly || m_Scanner.ObjectTypes.Length <= 0)
				{
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.HelpBox($"Did not find any objects of type {m_SelectedSheetAsset} under path(s):\n{m_Settings.ObjectManagement.Scan.GetJoinedScanPaths()}\nUpdate the scan path, create a new asset, or check exclusions.", MessageType.Warning);
					m_NewAssetPath = m_Settings.ObjectManagement.Scan.GetFirstScanPath();
					m_Paginator.GoToFirstPage();
					m_SortedObjects.Clear();
					return;
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			var previousSelectedTypeIndex = m_SelectedTypeIndex;
			GUI.SetNextControlName(string.Empty);
			m_SelectedTypeIndex = EditorGUILayout.Popup(string.Empty, m_SelectedTypeIndex, m_Scanner.ObjectTypeNames);
			var activePinnedIndexSet = m_PinnedIndexSets[m_SelectedSheetAsset];
			if (m_Settings.UserInterface.AutoPin && previousSelectedTypeIndex != m_SelectedTypeIndex)
			{
				activePinnedIndexSet.Add(m_SelectedTypeIndex);
			}
			if (activePinnedIndexSet.Contains(m_SelectedTypeIndex))
			{
				if (GUILayout.Button(SheetsContent.Button.Unpin, SheetLayout.InlineButton))
				{
					activePinnedIndexSet.Remove(m_SelectedTypeIndex);
				}
			}
			else
			{
				if (GUILayout.Button(SheetsContent.Button.Pin, SheetLayout.InlineButton))
				{
					activePinnedIndexSet.Add(m_SelectedTypeIndex);
				}
			}
			if (activePinnedIndexSet.Count > 1 && GUILayout.Button(SheetsContent.Button.UnpinAll, SheetLayout.InlineButton))
			{
				activePinnedIndexSet.Clear();
			}
			EditorGUILayout.EndHorizontal();

			if (activePinnedIndexSet.Count > 0)
			{
				m_ObjectTypeScrollPosition = EditorGUILayout.BeginScrollView(m_ObjectTypeScrollPosition, SheetLayout.DoubleLineHeight);
				EditorGUILayout.BeginHorizontal();
				foreach (var index in activePinnedIndexSet)
				{
					if (index < m_Scanner.ObjectTypes.Length)
					{
						var isSelected = m_SelectedTypeIndex == index;
						var objectTypeContent = SheetsContent.Label.GetObjectTypeContent(m_Scanner.FriendlyObjectTypeNames[index], m_Scanner.ObjectTypeNames[index]);
						var width = GUI.skin.button.CalcSize(objectTypeContent).x;
						if (GUILayout.Button(objectTypeContent, SheetLayout.GetButtonStyle(isSelected), GUILayout.Width(width)))
						{
							m_SelectedTypeIndex = index;
						}
					}
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndScrollView();
			}

			if (m_SelectedTypeIndex >= m_Scanner.ObjectTypes.Length)
			{
				m_SelectedTypeIndex = 0;
			}

			m_SelectedType = m_Scanner.ObjectTypes[m_SelectedTypeIndex];
			isScriptableObject = IsScriptableObject();
			if (!m_Scanner.ObjectsByType.TryGetValue(m_SelectedType, out List<Object> filteredObjects))
			{
				filteredObjects = new List<Object>();
				m_Scanner.ObjectsByType[m_SelectedType] = filteredObjects;
			}
			else
			{
				filteredObjects.RemoveAll(o => o == null);
			}
			MonoScript monoScript = null;
			var hasMainAssetIndexChanged = false;
			if (isScriptableObject)
			{
				if (m_Settings.UserInterface.SubAssetFilters && m_Scanner.SubAssetsByTypeAndMainAsset.TryGetValue(m_SelectedType, out var subAssetByMainAsset))
				{
					subAssetByMainAsset = subAssetByMainAsset.Where(kvp => kvp.Key != null).ToDictionary(kvp => kvp.Key, pair => pair.Value);
					if (subAssetByMainAsset.Count > 0)
					{
						var separator = subAssetByMainAsset.Count > SheetLayout.SubMenuThreshold ? '/' : '.';
						var subAssetMainAssetsAsString = subAssetByMainAsset.Keys.Select(mainAsset => mainAsset.GetType().Name + separator + mainAsset.name).ToArray();
						var previousMainAssetIndex = m_SelectedMainAssetIndex;
						GUI.SetNextControlName(string.Empty);
						m_SelectedMainAssetIndex = EditorGUILayout.Popup(string.Empty, m_SelectedMainAssetIndex, subAssetMainAssetsAsString);
						if (m_SelectedMainAssetIndex < 0 || m_SelectedMainAssetIndex >= subAssetByMainAsset.Keys.Count)
						{
							m_SelectedMainAssetIndex = 0;
						}
						hasMainAssetIndexChanged = previousMainAssetIndex != m_SelectedMainAssetIndex;
						if (hasMainAssetIndexChanged)
						{
							var tableLayout = GetTableLayout();
							tableLayout.MainAssetIndex = m_SelectedMainAssetIndex;
						}
						m_MainAsset = subAssetByMainAsset.Keys.ElementAt(m_SelectedMainAssetIndex);
						subAssetByMainAsset[m_MainAsset].RemoveAll(obj => obj == null);
						filteredObjects = subAssetByMainAsset[m_MainAsset];
					}
					else
					{
						m_Scanner.SubAssetsByTypeAndMainAsset.Remove(m_SelectedType);
						m_MainAsset = null;
						m_SelectedMainAssetIndex = 0;
					}
				}
				else
				{
					m_MainAsset = null;
					m_SelectedMainAssetIndex = 0;
				}
				if (!s_MonoScriptCache.TryGetValue(m_SelectedType, out monoScript))
				{
					// Do not allow instantiation of UnityEditorInternal types.
					if (!m_SelectedType.FullName.Contains(UnityConstants.Type.UnityEditorInternal))
					{
						var tempScriptableObject = CreateInstance(m_SelectedType);
						if (tempScriptableObject != null)
						{
							monoScript = MonoScript.FromScriptableObject(tempScriptableObject);
							DestroyImmediate(tempScriptableObject);
						}
					}
					s_MonoScriptCache.Add(m_SelectedType, monoScript);
				}
				if (monoScript != null)
				{
					EditorGUI.BeginDisabledGroup(true);
					EditorGUILayout.ObjectField(string.Empty, monoScript, typeof(MonoScript), false);
					EditorGUI.EndDisabledGroup();
				}
			}

			// Refresh up front on a type change or a pending column refresh so the dropdown and create button below agree across
			// the Layout and Repaint events. The full column layout refresh runs later in this method, but a reactive change such
			// as Collection Table Depth can invalidate the selected collection path. Resetting it only after those controls are
			// drawn would change which controls render between events and corrupt the GUILayout state.
			var hasFirstSelectedObject = filteredObjects.Count > 0 && filteredObjects[0] != null && filteredObjects[0].GetType() == m_SelectedType;
			if (hasFirstSelectedObject && (m_PreviousSelectedType != m_SelectedType || (m_WaitingForColumnRefresh && !m_TableNav.IsEditingTextField)))
			{
				RefreshCollectionFields(new SerializedObject(filteredObjects[0]));
			}

			if (m_Settings.UserInterface.CollectionTables && filteredObjects.Count > 0 && m_CollectionFieldPaths != null && m_CollectionFieldPaths.Length > 1)
			{
				var currentCollectionIndex = Mathf.Max(0, Array.IndexOf(m_CollectionFieldPaths, m_SelectedCollectionPath));
				GUI.SetNextControlName(string.Empty);
				var newCollectionIndex = EditorGUILayout.Popup(SheetsContent.Label.CollectionTable, currentCollectionIndex, m_CollectionFieldDisplayNames);
				if (newCollectionIndex != currentCollectionIndex && newCollectionIndex >= 0 && newCollectionIndex < m_CollectionFieldPaths.Length)
				{
					m_TableNav.ResetTextFieldEditing();
					m_SelectedCollectionPath = m_CollectionFieldPaths[newCollectionIndex];
					GetTableLayout().SelectedCollectionPath = m_SelectedCollectionPath;
					m_CollectionRowsResortNeeded = true;
					ForceRefreshColumnLayout();
				}
			}

			EditorGUILayout.BeginHorizontal();
			// Keep the create button for a type with no instances even if a stale collection path leaves IsCollectionTable true,
			// otherwise the first instance could never be created.
			if (isScriptableObject && monoScript != null && (!IsCollectionTable() || filteredObjects.Count <= 0))
			{
				if (string.IsNullOrEmpty(m_NewAssetPath))
				{
					m_NewAssetPath = m_Settings.ObjectManagement.Scan.GetFirstScanPath();
				}
				if (GUILayout.Button(SheetsContent.Button.GetCreateContent(m_NewAmount), SheetLayout.InlineButton))
				{
					if (ConfirmOver9000(m_NewAmount))
					{
						var lastCreatedObject = CreateNewObjects(m_NewAmount);
						EditorGUILayout.EndHorizontal();
						GUIUtility.keyboardControl = 0;
						m_Paginator.SetObjectsPerPage(m_Settings.Workload.RowsPerPage);
						m_Paginator.SetTotalObjects(m_Paginator.TotalObjects + m_NewAmount);
						if (!m_Paginator.IsOnLastPage())
						{
							m_Paginator.GoToLastPage();
						}
						if (lastCreatedObject != null)
						{
							EditorGUIUtility.PingObject(lastCreatedObject);
							if (m_Settings.UserInterface.TableNav.AutoSelect)
							{
								// Workaround to wait for the Object on the next page to get auto selected before selecting the newly created Object.
								EditorApplication.delayCall += () => EditorApplication.delayCall += () => Selection.activeObject = lastCreatedObject;
							}
							else
							{
								Selection.activeObject = lastCreatedObject;
							}
						}
						return;
					}
				}
				// Force reset the control name.
				GUI.SetNextControlName(string.Empty);
				m_NewAmount = EditorGUILayout.IntField(m_NewAmount, SheetLayout.PropertySmall);
				m_NewAmount = Mathf.Clamp(m_NewAmount, 1, 9999);
				try
				{
					var selectedNewAssetPath = SheetLayout.DrawAssetPathSettingGUI(GUIContent.none, SheetsContent.Button.EditNewAssetPath, m_NewAssetPath, SheetLayout.Empty);
					if (AssetDatabase.IsValidFolder(selectedNewAssetPath))
					{
						m_NewAssetPath = selectedNewAssetPath;
					}
					else
					{
						Debug.LogWarning($"'{selectedNewAssetPath}' is not a valid path for new assets.\nPlease select a folder under Assets or a mutable Package.");
					}
					// If the folder was deleted we need to reset the new asset path.
					if (!AssetDatabase.IsValidFolder(m_NewAssetPath))
					{
						m_NewAssetPath = m_Settings.ObjectManagement.Scan.GetFirstScanPath();
					}
				}
				catch (ArgumentException)
				{
					// Workaround for Unity GUI Error bug.
					GUIUtility.ExitGUI();
				}
			}

			if (filteredObjects.Count <= 0)
			{
				EditorGUILayout.EndHorizontal();
				try
				{
					if (m_MainAsset == null)
					{
						EditorGUILayout.HelpBox($"Did not find any objects of type {m_SelectedType} under path(s):\n{m_Settings.ObjectManagement.Scan.GetJoinedScanPaths()}\nUpdate the scan path, create a new asset, or check exclusions.", MessageType.Warning);
					}
					else
					{
						var mainAssetPath = AssetDatabase.GetAssetPath(m_MainAsset);
						EditorGUILayout.HelpBox($"Did not find any objects of type {m_SelectedType} under main asset:\n{mainAssetPath}\nSelect a new main asset or create a new subasset.", MessageType.Warning);
					}
				}
				catch (ArgumentException)
				{
					// Workaround for Unity GUI Error bug.
					GUIUtility.ExitGUI();
				}
				m_Paginator.GoToFirstPage();
				m_SortedObjects.Clear();
				return;
			}
			if (filteredObjects[0] == null)
			{
				EditorGUILayout.EndHorizontal();
				// An SO was deleted externally. Repaint the window.
				Repaint();
				return;
			}
			var hasSelectedTypeChanged = m_PreviousSelectedType != m_SelectedType;
			m_PreviousSelectedType = m_SelectedType;

			if (hasSelectedTypeChanged || hasMainAssetIndexChanged)
			{
				m_TableNav.ResetTextFieldEditing();
				// Bound memory by dropping row SerializedObjects cached for the previous type or main asset.
				ClearRowSerializedObjectCache();
			}
			// Cancel a queued or pending paste/import when the type changes since its data was captured for the previous type.
			if (hasSelectedTypeChanged && (m_TableAction == TableAction.Import || m_TableAction == TableAction.SmartPaste))
			{
				CancelPendingAction();
			}

			if (string.IsNullOrEmpty(m_NewAssetPath) || hasSelectedTypeChanged || hasMainAssetIndexChanged)
			{
				var assetPath = AssetDatabase.GetAssetPath(filteredObjects[0]);
				var extension = Path.GetExtension(assetPath);
				var defaultNewAssetPath = assetPath.Replace($"{filteredObjects[0].name}{extension}", string.Empty);
				// Use default asset path for assets within immutable packages.
				if (PackageUtility.IsAssetImmutable(defaultNewAssetPath))
				{
					defaultNewAssetPath = UnityConstants.DefaultAssetPath;
				}
				else
				{
					// Check for subasset paths.
					if (defaultNewAssetPath.EndsWith(extension))
					{
						defaultNewAssetPath = defaultNewAssetPath.Substring(0, defaultNewAssetPath.LastIndexOf('/') + 1);
					}
					// In older versions of Unity IsValidFolder will return false if it ends in a forward slash.
					// https://issuetracker.unity3d.com/issues/assetdatabase-dot-isvalidfolder-returns-false-when-the-end-of-the-path-string-contains-a-directory-separator-slash
					defaultNewAssetPath = defaultNewAssetPath.TrimEnd('/');
					if (string.IsNullOrEmpty(defaultNewAssetPath) || !AssetDatabase.IsValidFolder(defaultNewAssetPath))
					{
						var firstScanPath = m_Settings.ObjectManagement.Scan.GetFirstScanPath();
						if (AssetDatabase.IsValidFolder(firstScanPath))
						{
							defaultNewAssetPath = firstScanPath;
						}
						else
						{
							defaultNewAssetPath = UnityConstants.DefaultAssetPath;
						}
					}
				}
				m_NewAssetPath = defaultNewAssetPath;
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			if (m_MultiColumnHeader == null || hasSelectedTypeChanged || (m_WaitingForColumnRefresh && !m_TableNav.IsEditingTextField))
			{
				m_WaitingForColumnRefresh = false;
				// The column layout drives $ index references in property filters, so refreshing it can change the matches.
				m_SearchResultsDirty = true;
				// Refresh flag to ensure it's set on first frame when reloading domain. Otherwise base types will not restore column layout correctly.
				isScriptableObject = IsScriptableObject();
				if (isScriptableObject && m_Settings.UserInterface.OverrideArraySize)
				{
					var tempScriptableObject = CreateInstance(m_SelectedType);
					if (tempScriptableObject != null)
					{
						RefreshColumnLayout(tempScriptableObject, hasSelectedTypeChanged, filteredObjects);
						DestroyImmediate(tempScriptableObject);
					}
					else
					{
						RefreshColumnLayout(filteredObjects[0], hasSelectedTypeChanged, filteredObjects);
					}
				}
				else if (isScriptableObject && filteredObjects[0].GetType() != m_SelectedType)
				{
					// Upcast Object and copy over array sizes.
					var tempScriptableObject = CreateInstance(m_SelectedType);
					if (tempScriptableObject != null)
					{
						var filteredObjectJson = EditorJsonUtility.ToJson(filteredObjects[0]);
						EditorJsonUtility.FromJsonOverwrite(filteredObjectJson, tempScriptableObject);
						RefreshColumnLayout(tempScriptableObject, hasSelectedTypeChanged, filteredObjects);
						DestroyImmediate(tempScriptableObject);
					}
					else
					{
						RefreshColumnLayout(filteredObjects[0], hasSelectedTypeChanged, filteredObjects);
					}
				}
				else
				{
					RefreshColumnLayout(filteredObjects[0], hasSelectedTypeChanged, filteredObjects);
				}
			}

			var resortNeeded = m_SortingChanged || hasSelectedTypeChanged || hasMainAssetIndexChanged;
			// Collection Tables re-sort their element rows on the same triggers so editing fields never reorders them.
			m_CollectionRowsResortNeeded |= resortNeeded;
			if (resortNeeded)
			{
				m_SortedObjects = m_MultiColumnHeader.GetSorted(filteredObjects);
				m_SortingChanged = false;
				// The order or set changed, so the cached search results no longer apply.
				m_SearchResultsDirty = true;
			}
			else
			{
				// Drop any Objects destroyed since the last frame so stale references never reach the table.
				if (m_SortedObjects.RemoveAll(o => o == null) > 0)
				{
					m_SearchResultsDirty = true;
				}
				// Only reconcile against the filtered set when its size changed (Objects created or removed). Otherwise the
				// existing sort order is still valid, so we skip rebuilding it and its hash set allocations every repaint.
				if (m_SortedObjects.Count != filteredObjects.Count)
				{
					// Add new objects to the bottom of the current sort.
					m_SortedObjects = m_SortedObjects.Intersect(filteredObjects).ToList();
					m_SortedObjects.AddRange(filteredObjects.Except(m_SortedObjects));
					m_SearchResultsDirty = true;
				}
			}

			EditorGUILayout.BeginHorizontal();
			var visibleColumnsLength = m_MultiColumnHeaderState.visibleColumns.Length;
			var excessVisibleColumns = visibleColumnsLength - m_Settings.Workload.VisibleColumnLimit;
			if (excessVisibleColumns > 0)
			{
				SetVisibleColumns(m_MultiColumnHeaderState.visibleColumns.Take(visibleColumnsLength - excessVisibleColumns).ToArray());
			}
			GUI.enabled = m_MultiColumnHeaderState.visibleColumns.Length < Mathf.Min(m_Columns.Length, m_Settings.Workload.VisibleColumnLimit);
			if (GUILayout.Button(SheetsContent.Button.ShowColumns, SheetLayout.InlineButton))
			{
				SetVisibleColumns(m_Columns.GetClampedColumns(m_Settings.Workload.VisibleColumnLimit));
			}
			GUI.enabled = true;
			var totalColumns = m_MultiColumnHeaderState.columns.Length;
			var totalVisibleColumns = m_MultiColumnHeaderState.visibleColumns.Length;
			var columnLabelContent = SheetsContent.Label.GetColumnContent(totalVisibleColumns, m_Settings.Workload.VisibleColumnLimit, totalColumns);
			var columnLabelWidth = GUI.skin.label.CalcSize(columnLabelContent).x;
			// GUI.color does not work in light theme so change the normal text color and then reset it based on light or dark theme.
			// https://docs.unity3d.com/ScriptReference/GUI-color.html
			var centerLabelStyleTextColor = SheetLayout.CenterLabelStyle.normal.textColor;
			if (totalColumns > totalVisibleColumns && totalVisibleColumns >= m_Settings.Workload.VisibleColumnLimit)
			{
				SheetLayout.CenterLabelStyle.normal.textColor = Color.yellow;
			}
			if (GUILayout.Button(columnLabelContent, SheetLayout.CenterLabelStyle, GUILayout.Width(columnLabelWidth)))
			{
				// Ensure we reset the color if the button was pressed.
				SheetLayout.CenterLabelStyle.normal.textColor = centerLabelStyleTextColor;
				ShowColumnVisibilityPopup();
			}
			SheetLayout.CenterLabelStyle.normal.textColor = centerLabelStyleTextColor;
			GUI.enabled = m_MultiColumnHeaderState.visibleColumns.Length > 1;
			if (GUILayout.Button(SheetsContent.Button.HideColumns, SheetLayout.InlineButton))
			{
				SetVisibleColumns(new int[] { 0 });
			}
			GUI.enabled = true;
			SheetLayout.DrawVerticalLine();
			if (GUILayout.Button(SheetsContent.Button.Stretch, SheetLayout.InlineButton))
			{
				m_MultiColumnHeader.ResizeToFit();
				// ResizeToFit is a Unity function that doesn't update widths immediately so we wait for a delay before caching the new widths.
				EditorApplication.delayCall -= CacheColumnLayout;
				EditorApplication.delayCall += CacheColumnLayout;
			}
			if (GUILayout.Button(SheetsContent.Button.Compact, SheetLayout.InlineButton))
			{
				m_MultiColumnHeader.ResizeToMinWidth();
				CacheColumnLayout();
			}
			if (GUILayout.Button(SheetsContent.Button.Expand, SheetLayout.InlineButton))
			{
				m_MultiColumnHeader.ResizeToHeaderWidth(SheetLayout.InlineLabelSpacing);
				CacheColumnLayout();
			}
			SheetLayout.DrawVerticalLine();
			if (GUILayout.Button(SheetsContent.Button.CopyToClipboard, SheetLayout.InlineButton))
			{
				SetTableAction(TableAction.Copy);
			}
			EditorGUI.BeginDisabledGroup(!m_TableNav.HasFocus);
			if (GUILayout.Button(SheetsContent.Button.CopyRowToClipboard, SheetLayout.InlineButton))
			{
				SetTableAction(TableAction.CopyRow);
			}
			if (GUILayout.Button(SheetsContent.Button.CopyColumnToClipboard, SheetLayout.InlineButton))
			{
				SetTableAction(TableAction.CopyColumn);
			}
			if (GUILayout.Button(SheetsContent.Button.SmartPaste, SheetLayout.InlineButton))
			{
				SetTableAction(TableAction.SmartPaste);
			}
			EditorGUI.EndDisabledGroup();
			SheetLayout.DrawVerticalLine();
			List<GoogleSheetsImporter> filteredImporters;
			var isCollectionTable = IsCollectionTable();
			if (m_Settings.UserInterface.SubAssetFilters && m_MainAsset != null)
			{
				filteredImporters = m_Settings.GoogleSheetsImporters?.Where(i => i != null && i.IsTypeMatch(m_SelectedType, monoScript) && i.MainAsset == m_MainAsset && i.IsCollectionMatch(isCollectionTable, m_SelectedCollectionPath)).ToList();
			}
			else
			{
				filteredImporters = m_Settings.GoogleSheetsImporters?.Where(i => i != null && i.IsTypeMatch(m_SelectedType, monoScript) && i.MainAsset == null && i.IsCollectionMatch(isCollectionTable, m_SelectedCollectionPath)).ToList();
			}
			List<GoogleSheetsImporter> importerChoices;
			if (filteredImporters == null || filteredImporters.Count <= 0)
			{
				importerChoices = new List<GoogleSheetsImporter>();
			}
			else
			{
				// Prioritize importers with matching window names, otherwise offer the importers without a window name.
				var windowNameMatches = filteredImporters.Where(i => !string.IsNullOrWhiteSpace(i.WindowName) && i.WindowName == titleContent.text).ToList();
				var matches = windowNameMatches.Count > 0 ? windowNameMatches : filteredImporters.Where(i => string.IsNullOrWhiteSpace(i.WindowName));
				importerChoices = matches.OrderBy(i => i.name).ToList();
			}
			EditorGUI.BeginDisabledGroup(importerChoices.Count <= 0 || m_IsGoogleSheetDownloading);
			if (importerChoices.Count > 1)
			{
				// Multiple importers target the same type, so let the user choose which one to import from.
				m_GoogleSheetsImporter = null;
				if (GUILayout.Button(SheetsContent.Button.GetGoogleSheetsImporterDropdownContent(importerChoices.Count), SheetLayout.InlineButton))
				{
					var importerMenu = new GenericMenu();
					foreach (var importerChoice in importerChoices)
					{
						var selectedImporter = importerChoice;
						importerMenu.AddItem(new GUIContent(selectedImporter.name), false, () =>
						{
							m_GoogleSheetsImporter = selectedImporter;
							DownloadGoogleSheetsDataAsync();
						});
					}
					importerMenu.ShowAsContext();
				}
			}
			else
			{
				m_GoogleSheetsImporter = importerChoices.FirstOrDefault();
				var googleSheetsImporterName = m_GoogleSheetsImporter == null ? string.Empty : m_GoogleSheetsImporter.name;
				if (GUILayout.Button(SheetsContent.Button.GetGoogleSheetsImporterContent(googleSheetsImporterName), SheetLayout.InlineButton))
				{
					DownloadGoogleSheetsDataAsync();
				}
			}
			EditorGUI.EndDisabledGroup();
			if (GUILayout.Button(SheetsContent.Button.ImportFile, SheetLayout.InlineButton))
			{
				var filePath = EditorUtility.OpenFilePanel("Import", Application.dataPath, "*");
				if (!string.IsNullOrEmpty(filePath))
				{
					m_ImportedFileContents = File.ReadAllText(filePath);
					if (!string.IsNullOrEmpty(m_ImportedFileContents))
					{
						var extension = FlatFileUtility.GetExtensionFromPath(filePath);
						if (!string.IsNullOrEmpty(extension))
						{
							// Auto detect new delimiter based on file extension.
							if (FlatFileUtility.FlatFileDelimiters.TryGetValue(extension, out string delimiter))
							{
								m_Settings.DataTransfer.SetColumnDelimiter(delimiter);
							}
							else if (extension == JsonExtension)
							{
								m_IsImportJson = true;
							}
						}
						// A local file import is not folder scoped.
						m_PendingImportFolder = string.Empty;
						SetTableAction(TableAction.Import);
					}
					else
					{
						Debug.LogWarning($"File at path '{filePath}' is empty.");
					}
				}
			}
			if (GUILayout.Button(SheetsContent.Button.SaveToDisk, SheetLayout.InlineButton))
			{
				var dataPath = Application.dataPath;
				var fileExtension = "dsv";
				if (FlatFileUtility.FlatFileExtensions.TryGetValue(m_Settings.DataTransfer.GetColumnDelimiter(), out string delimiterExtension))
				{
					fileExtension = delimiterExtension;
				}
				m_SelectedFilepath = EditorUtility.SaveFilePanel("Save to", dataPath, $"{m_SelectedType.Name}", fileExtension);
				if (!string.IsNullOrEmpty(m_SelectedFilepath))
				{
					SetTableAction(TableAction.Save);
				}
			}
			SheetLayout.DrawVerticalLine();
			if (GUILayout.Button(SheetsContent.Button.NewPastePad, SheetLayout.InlineButton))
			{
				PastePadEditorWindow.ShowWindow();
			}

			GUILayout.FlexibleSpace();
			// Resolve the selected cell label only on Layout so the control count stays consistent during Repaint, otherwise stale table state can flip it mid-frame after switching Sheet Assets.
			if (Event.current.type == EventType.Layout)
			{
				m_SelectedCellContent = TryGetSelectedCellContent(out var selectedCellContent) ? selectedCellContent : null;
			}
			if (m_SelectedCellContent != null)
			{
				var selectedCellWidth = GUI.skin.label.CalcSize(m_SelectedCellContent).x;
				EditorGUILayout.LabelField(m_SelectedCellContent, SheetLayout.CenterLabelStyle, GUILayout.Width(selectedCellWidth));
			}
			GUI.SetNextControlName(string.Empty);
			m_SearchInput = m_SearchField.OnGUI(m_SearchInput, SheetLayout.GetSearchBarFlexibleWidth(m_SearchInput));
			var useStringEnums = m_Settings.DataTransfer.UseStringEnums;
			var ignoreEnumCasing = m_Settings.DataTransfer.IgnoreCase;
			var searchSettings = m_Settings.ObjectManagement.Search;
			// Re-run the filter only when the search configuration changed or something invalidated the results. Scanning the
			// whole set every repaint is what causes the lag on large data sets, so otherwise reuse the cached matches.
			if (m_SearchResultsDirty
				|| m_SearchInput != m_LastSearchInput
				|| searchSettings.CaseSensitive != m_LastSearchCaseSensitive
				|| searchSettings.StartsWith != m_LastSearchStartsWith
				|| useStringEnums != m_LastUseStringEnums
				|| ignoreEnumCasing != m_LastIgnoreEnumCasing)
			{
				if (IsCollectionTable())
				{
					var builtRows = BuildCollectionRows(m_SortedObjects);
					if (m_CollectionRowsResortNeeded || m_SortedCollectionRows.Count == 0)
					{
						m_SortedCollectionRows = SortCollectionRows(builtRows);
						m_CollectionRowsResortNeeded = false;
					}
					else
					{
						m_SortedCollectionRows = ReconcileCollectionRows(m_SortedCollectionRows, builtRows);
					}
					m_CachedCollectionRows = FilterCollectionRows(m_SortedCollectionRows, m_SearchInput, searchSettings, useStringEnums, ignoreEnumCasing);
				}
				else
				{
					m_CachedMatchingObjects = SearchFilter.GetObjects(m_SearchInput, m_SortedObjects, searchSettings, useStringEnums, ignoreEnumCasing, m_ColumnPropertyPaths);
				}
				m_LastSearchInput = m_SearchInput;
				m_LastSearchCaseSensitive = searchSettings.CaseSensitive;
				m_LastSearchStartsWith = searchSettings.StartsWith;
				m_LastUseStringEnums = useStringEnums;
				m_LastIgnoreEnumCasing = ignoreEnumCasing;
				m_SearchResultsDirty = false;
			}
			var matchingObjects = m_CachedMatchingObjects;
			if (m_TableAction == TableAction.Import && !string.IsNullOrWhiteSpace(m_PendingImportFolder) && !IsCollectionTable())
			{
				matchingObjects = matchingObjects.Where(o => GoogleSheetsImporter.IsAssetPathInFolder(AssetDatabase.GetAssetPath(o), m_PendingImportFolder)).ToList();
			}
			var collectionRows = IsCollectionTable() ? m_CachedCollectionRows : null;

			m_Paginator.SetObjectsPerPage(m_Settings.Workload.RowsPerPage);
			m_Paginator.SetTotalObjects(IsCollectionTable() ? collectionRows.Count : matchingObjects.Count);
			if (m_PendingPageRestore > 0)
			{
				m_Paginator.SetCurrentPage(m_PendingPageRestore);
				m_PendingPageRestore = 0;
			}
			if (m_PendingOpenAsset != null && !IsCollectionTable() && !m_PendingOpenScheduled)
			{
				m_PendingOpenScheduled = true;
				EditorApplication.delayCall += ResolvePendingOpenDeferred;
			}
			var totalPages = m_Paginator.GetTotalPages();
			if (totalPages > 1)
			{
				SheetLayout.DrawVerticalLine();
				var showFirstAndLastPageButtons = totalPages > SheetLayout.FirstAndLastPageThreshold;
				if (showFirstAndLastPageButtons)
				{
					EditorGUI.BeginDisabledGroup(m_Paginator.IsOnFirstPage());
					if (GUILayout.Button(SheetsContent.Button.FirstPage, SheetLayout.InlineButton))
					{
						m_Paginator.GoToFirstPage();
					}
					EditorGUI.EndDisabledGroup();
				}
				if (GUILayout.Button(SheetsContent.Button.PreviousPage, SheetLayout.InlineButton))
				{
					m_Paginator.PreviousPage();
				}
				var pageLabelContent = SheetsContent.Label.GetPageContent(m_Paginator.CurrentPage, totalPages, m_Paginator.TotalObjects);
				var pageLabelWidth = GUI.skin.label.CalcSize(pageLabelContent).x;
				EditorGUILayout.LabelField(pageLabelContent, SheetLayout.CenterLabelStyle, GUILayout.Width(pageLabelWidth));
				if (GUILayout.Button(SheetsContent.Button.NextPage, SheetLayout.InlineButton))
				{
					m_Paginator.NextPage();
				}
				if (showFirstAndLastPageButtons)
				{
					EditorGUI.BeginDisabledGroup(m_Paginator.IsOnLastPage());
					if (GUILayout.Button(SheetsContent.Button.LastPage, SheetLayout.InlineButton))
					{
						m_Paginator.GoToLastPage();
					}
					EditorGUI.EndDisabledGroup();
				}
			}
			EditorGUILayout.EndHorizontal();

			SheetLayout.DrawHorizontalLine();

			// Reset text field selection when page changes and persist the page per type and sheet.
			if (m_PreviousSelectedPage != m_Paginator.CurrentPage)
			{
				m_PreviousSelectedPage = m_Paginator.CurrentPage;
				m_TableNav.ResetTextFieldEditing();
				GetTableLayout().CurrentPage = m_Paginator.CurrentPage;
			}
			// Queue auto generation if the pending paste or import needs more Objects. The table keeps drawing while generation is
			// queued so the sheet stays visible, and the action itself is deferred below until the new Objects exist.
			TryAutoGenerateForPendingAction(matchingObjects, isScriptableObject, monoScript);
			// Ignore pagination if we're trying to perform an action on all rows. Collection View paginates its flattened rows
			// while the default view paginates the owning Objects, so only the active one is computed to avoid paging the owners
			// with the larger collection row page count.
			var pageAllRows = !m_Settings.DataTransfer.PageRowsOnly && m_TableAction != TableAction.None;
			List<Object> paginatedObjects = null;
			List<CollectionRow> paginatedCollectionRows = null;
			int totalRows;
			if (IsCollectionTable())
			{
				paginatedCollectionRows = pageAllRows ? collectionRows : m_Paginator.GetPageObjects(collectionRows);
				totalRows = paginatedCollectionRows.Count;
			}
			else
			{
				paginatedObjects = pageAllRows ? matchingObjects : m_Paginator.GetPageObjects(matchingObjects);
				totalRows = paginatedObjects.Count;
			}

			GUILayout.FlexibleSpace();
			var windowRect = GUILayoutUtility.GetLastRect();
			windowRect.width = position.width;
			windowRect.height = position.height;
			var rowHeight = EditorGUIUtility.singleLineHeight * m_Settings.UserInterface.RowLineHeight;
			var columnHeaderRowRect = new Rect(windowRect)
			{
				height = rowHeight,
			};
			m_MultiColumnHeader.OnGUI(columnHeaderRowRect, m_ScrollPosition.x);
			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			// GetRect returns an empty rect during certain event types like layout.
			// In versions after 2022.3.43f1 and 6000.0.15f1. Unity starts returning a rect with float.MaxValue so we need to check that as well.
			// So validate the width and height before updating the scroll view.
			// https://forum.unity.com/threads/guilayoututility-getrect-with-inconsistent-results.8278/
			{
				var scrollViewArea = GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue);
				if (scrollViewArea.width > 1 && scrollViewArea.width < float.MaxValue && scrollViewArea.height > 1 && scrollViewArea.height < float.MaxValue)
				{
					m_ScrollViewArea = scrollViewArea;
					m_TableScrollViewRect = new Rect(windowRect)
					{
						height = (totalRows + SheetLayout.TableViewRowPadding) * rowHeight,
						xMax = m_MultiColumnHeaderState.widthOfAllVisibleColumns,
						yMin = windowRect.y + rowHeight
					};
					if (!m_TableNav.WasKeyboardNav && m_TableNav.HasFocus && m_PropertyTable.IsValidCoordinate(m_TableNav.PreviousFocusedCoordinate))
					{
						if (m_PropertyTable.TryGet(m_TableNav.PreviousFocusedCoordinate, out ITableProperty property))
						{
							// Force reselect property if it shifted during a layout update. Usually caused by virtualization setting.
							if (GUI.GetNameOfFocusedControl() != property.ControlName)
							{
								GUI.FocusControl(property.ControlName);
							}
						}
						else
						{
							// Reset the focused control because it is out of view. Cannot use empty string because it'll try to select a valid empty string control.
							GUI.FocusControl("null");
						}
					}
				}
			}
			m_ScrollPosition = GUI.BeginScrollView(m_ScrollViewArea, m_ScrollPosition, m_TableScrollViewRect, false, false);
			var scrollStart = new Vector2(m_ScrollViewArea.x + m_ScrollPosition.x, m_ScrollViewArea.y + m_ScrollPosition.y);
			var scrollEnd = new Vector2(scrollStart.x + m_ScrollViewArea.width, scrollStart.y + m_ScrollViewArea.height);

			Profiler.BeginSample("DrawTable");
			if (m_PropertyTable == null)
			{
				m_PropertyTable = new Table<ITableProperty>(totalRows, m_MultiColumnHeaderState.visibleColumns.Length);
			}
			else
			{
				// Reuse the existing table across repaints instead of allocating a new one every frame.
				m_PropertyTable.Reset(totalRows, m_MultiColumnHeaderState.visibleColumns.Length);
			}
			var startingRowIndex = 0;
			// Add a buffer to the starting column index for scrolling.
			var startingColumnIndex = -1;
			if (m_Settings.Workload.Virtualization && m_TableAction == TableAction.None)
			{
				var adjustedScrollStartY = scrollStart.y - rowHeight - m_ScrollViewArea.y;
				startingRowIndex = Mathf.Max(0, Mathf.CeilToInt(adjustedScrollStartY / rowHeight));
				var totalColumnWidth = m_MultiColumnHeader.DockedColumns.Count > 0 ? scrollStart.x - m_MultiColumnHeader.GetWidthOfAllVisibleDockedColumns() : 0f;
				foreach (var visibleColumn in m_MultiColumnHeaderState.visibleColumns)
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(visibleColumn);
					totalColumnWidth += m_MultiColumnHeader.GetColumnRect(visibleColumnIndex).width;
					if (totalColumnWidth > scrollStart.x)
					{
						break;
					}
					startingColumnIndex++;
				}
			}
			startingColumnIndex = Mathf.Max(0, startingColumnIndex);
			var lastVisibleRow = false;
			m_PreviewSerializedObjectCache.Clear();
			for (var rowIndex = startingRowIndex; rowIndex < totalRows; rowIndex++)
			{
				Profiler.BeginSample("DrawRow");
				var lastVisibleColumn = false;

				var rowRect = new Rect(columnHeaderRowRect);
				rowRect.y += rowHeight * (rowIndex + 1);

				var visualRowRect = new Rect(rowRect)
				{
					x = m_ScrollPosition.x
				};
				EditorGUI.DrawRect(visualRowRect, rowIndex % 2 == 0 ? SheetLayout.DarkerColor : SheetLayout.LighterColor);

				if (IsCollectionTable())
				{
					var collectionRow = paginatedCollectionRows[rowIndex];
					var collectionOwner = collectionRow.Owner;
					var collectionSerializedObject = GetRowSerializedObject(collectionOwner);
					collectionSerializedObject.Update();
					if (DrawCollectionRow(rowIndex, rowRect, collectionSerializedObject, collectionOwner, collectionRow.ElementIndex, collectionRow.IsAddRow, startingColumnIndex, scrollEnd, ref lastVisibleColumn, ref lastVisibleRow))
					{
						GUI.EndScrollView(true);
						GUIUtility.keyboardControl = 0;
						return;
					}
					if (collectionSerializedObject.ApplyModifiedProperties())
					{
						m_SearchResultsDirty = true;
					}
					Profiler.EndSample();
					if (lastVisibleRow && m_TableAction == TableAction.None)
					{
						break;
					}
					continue;
				}

				var rootObject = paginatedObjects[rowIndex];
				var isFirstFilteredObject = rootObject == filteredObjects[0];
				var assetPath = GetCachedAssetPath(rootObject);
				var isPendingOpenRow = m_PendingOpenAsset != null && rootObject == m_PendingOpenAsset;

				var serializedObject = GetRowSerializedObject(rootObject);
				serializedObject.Update();

				var columnIndex = 0;
				var isActionsDocked = m_MultiColumnHeader.DockedColumns.Contains(columnIndex);
				if ((columnIndex >= startingColumnIndex || isActionsDocked) && m_MultiColumnHeader.IsColumnVisible(columnIndex))
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
					var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
					columnRect.y = rowRect.y;
					var actionRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
					if (m_Settings.UserInterface.RowLineHeight > 1)
					{
						actionRect.height = EditorGUIUtility.singleLineHeight * SheetLayout.MaxActionRectLineHeight;
					}

					if (m_Settings.UserInterface.ShowRowIndex)
					{
						var rowIndexLabel = SheetsContent.Label.GetRowIndex(rowIndex, m_Settings.UserInterface.RowIndexFormat);
						EditorGUI.LabelField(actionRect, rowIndexLabel);
						actionRect.x += SheetLayout.PropertyWidthSmall / 2;
					}
					var firstActionRect = new Rect(actionRect.x, actionRect.y, SheetLayout.InlineButtonWidth, actionRect.height);
					var secondActionRect = new Rect(firstActionRect.xMax + SheetLayout.InlineButtonSpacing, actionRect.y, SheetLayout.InlineButtonWidth, actionRect.height);
					if (GUI.Button(firstActionRect, SheetsContent.Button.Select))
					{
						EditorGUIUtility.PingObject(rootObject);
						Selection.activeObject = rootObject;
					}
					if (GUI.Button(secondActionRect, SheetsContent.Button.Delete))
					{
						var assetName = rootObject.name;
						var isSubAsset = AssetDatabase.IsSubAsset(rootObject);
						if (!m_Settings.UserInterface.ConfirmDelete || EditorUtility.DisplayDialog($"Delete {assetName} {(isSubAsset ? "subasset" : "asset")}?", $"{assetPath}\n\nYou cannot undo the delete assets action.", "Delete", "Cancel"))
						{
							if (!PackageUtility.IsAssetImmutable(rootObject))
							{
								if (m_Settings.Workload.Debug)
								{
									Debug.Log($"Deleting asset with name {assetName} at path {assetPath}");
								}
								m_Scanner.ObjectsByType[m_SelectedType].Remove(rootObject);
								RemoveRowSerializedObject(rootObject);
								if (isSubAsset)
								{
									AssetDatabase.RemoveObjectFromAsset(rootObject);
									if (isScriptableObject && m_Settings.UserInterface.SubAssetFilters)
									{
										m_Scanner.SubAssetsByTypeAndMainAsset[m_SelectedType][m_MainAsset].Remove(rootObject);
									}
									// RemoveObjectFromAsset only detaches the sub asset, so destroy the instance to fully remove it.
									DestroyImmediate(rootObject, true);
									AssetDatabase.SaveAssets();
								}
								else
								{
									// Resolve the live path rather than the cached one, which an import or paste rename can leave stale and point
									// DeleteAsset at a file that no longer exists. Only fall back to a direct destroy when the asset cannot be
									// resolved, where the file is still present so nothing gets reimported.
									var livePath = AssetDatabase.GetAssetPath(rootObject);
									if (string.IsNullOrEmpty(livePath) || !AssetDatabase.DeleteAsset(livePath))
									{
										DestroyImmediate(rootObject, true);
									}
									// The delete runs mid GUI and returns early, so refresh to clear the deleted asset from the Project window.
									AssetDatabase.Refresh();
								}
								GUI.EndScrollView(true);
								GUIUtility.keyboardControl = 0;
								return;
							}
							else
							{
								Debug.LogWarning($"Unable to delete asset {assetName} at path {assetPath}\nThe asset is in an immutable folder.");
							}
						}
						actionRect.x = secondActionRect.xMax + SheetLayout.InlineButtonSpacing;
					}
				}

				columnIndex++;
				var isNameDocked = m_MultiColumnHeader.DockedColumns.Contains(columnIndex);
				if ((columnIndex >= startingColumnIndex || isNameDocked) && m_MultiColumnHeader.IsColumnVisible(columnIndex))
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
					var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
					columnRect.y = rowRect.y;
					if (CanDrawCellRect(columnRect))
					{
						var assetNameRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
						// Only draw asset previews if the height is greater than 1.
						if (assetNameRect.height > EditorGUIUtility.singleLineHeight)
						{
							var previewObject = GetNamePreviewObject(serializedObject, rootObject);
							DrawUtility.TableAssetPreview(previewObject, assetNameRect, m_Settings.UserInterface.AssetPreview);
							assetNameRect.height = EditorGUIUtility.singleLineHeight;
						}
						// AudioClip assets get an inline play button next to their name.
						if (rootObject is AudioClip audioClip)
						{
							var playRect = new Rect(assetNameRect.xMax - SheetLayout.InlineButtonWidth, assetNameRect.y, SheetLayout.InlineButtonWidth, assetNameRect.height);
							assetNameRect.width -= SheetLayout.InlineButtonWidth + SheetLayout.InlineButtonSpacing;
							var isAudioClipPlaying = AudioClipUtility.IsPlaying(audioClip);
							if (GUI.Button(playRect, SheetsContent.Button.GetPlayContent(audioClip.name, isAudioClipPlaying)))
							{
								m_TableNav.ResetTextFieldEditing();
								AudioClipUtility.Toggle(audioClip);
							}
						}
						var nameProperty = GetNameProperty(serializedObject);
						var nextControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
						EditorGUI.BeginDisabledGroup(m_Settings.UserInterface.LockNames);
						var newName = EditorGUI.TextField(assetNameRect, rootObject.name);
						EditorGUI.EndDisabledGroup();
						var nameTableProperty = new SerializedTableProperty(nameProperty.serializedObject.targetObject, nameProperty.propertyPath, nextControlName);
						m_PropertyTable.Set(rowIndex, visibleColumnIndex, nameTableProperty);
						HandleReadOnlyCellClick(assetNameRect, nextControlName);
						if (newName != rootObject.name)
						{
							// The name, and for main assets the asset path, is about to change so drop the stale cached path.
							m_AssetPathCache.Remove(rootObject);
							// A rename can change which Objects match a name search, so the cached results are stale.
							m_SearchResultsDirty = true;
							// Only rename if it's the main asset.
							if (assetPath.Contains($"/{rootObject.name}."))
							{
								AssetDatabase.RenameAsset(assetPath, newName);
							}
							else
							{
								rootObject.name = newName;
							}
							// Exit early for performance.
							// We prefer this over using a delayed field because the delayed field is less reliable especially when changing asset type mid edit.
							return;
						}
					}
				}

				if (m_Settings.UserInterface.ShowAssetPath)
				{
					columnIndex++;
					if (columnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(columnIndex))
					{
						var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
						var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
						columnRect.y = rowRect.y;
						if (CanDrawCellRect(columnRect))
						{
							var assetPathRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
							assetPathRect.height = EditorGUIUtility.singleLineHeight;

							EditorGUI.BeginDisabledGroup(true);
							var nextControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
							var assetPathTableProperty = new AssetPathTableProperty(rootObject, assetPath, nextControlName);
							EditorGUI.TextField(assetPathRect, assetPathTableProperty.GetProperty());
							m_PropertyTable.Set(rowIndex, visibleColumnIndex, assetPathTableProperty);
							EditorGUI.EndDisabledGroup();
							HandleReadOnlyCellClick(assetPathRect, nextControlName);
						}
					}
				}

				if (m_Settings.UserInterface.ShowGuid)
				{
					columnIndex++;
					if (columnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(columnIndex))
					{
						var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
						var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
						columnRect.y = rowRect.y;
						if (CanDrawCellRect(columnRect))
						{
							var guidRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
							guidRect.height = EditorGUIUtility.singleLineHeight;

							EditorGUI.BeginDisabledGroup(true);
							var nextControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
							var guidTableProperty = new GuidTableProperty(rootObject, assetPath, nextControlName);
							EditorGUI.TextField(guidRect, guidTableProperty.GetProperty());
							m_PropertyTable.Set(rowIndex, visibleColumnIndex, guidTableProperty);
							EditorGUI.EndDisabledGroup();
							HandleReadOnlyCellClick(guidRect, nextControlName);
						}
					}
				}

				var iterator = serializedObject.GetIterator();
				var includeChildren = true;
				var renderingOverrides = m_Settings.Experimental.GetRenderingOverrides();
				var iterations = 0;
				while (iterator.NextVisible(includeChildren) && iterations++ < m_Settings.Workload.MaxIterations)
				{
					// Some Unity assets like Prefabs include the m_Name property in their iterator. Skip over it because we draw it separately for all Objects.
					if (iterator.propertyPath == UnityConstants.Field.Name)
					{
						continue;
					}

					var useAssetReferenceDrawer = !m_Settings.UserInterface.ShowReadOnly && iterator.IsAssetReference();
					var isExpandableManagedReference = m_Settings.UserInterface.ManagedReferences && iterator.IsManagedReference();
					// Descend into this row's own subtype so its fields line up with the union columns.
					includeChildren = (m_Settings.UserInterface.ShowChildren && !useAssetReferenceDrawer) || isExpandableManagedReference;
					if (!m_MultiColumnTooltipPaths.TryGetValue(iterator.propertyPath, out int nextColumnIndex))
					{
						continue;
					}
					var showReadOnly = m_Settings.UserInterface.ShowReadOnly;
					if (isExpandableManagedReference)
					{
						// During an import, seed empty cells for every union child column so values belonging to a row's newly
						// selected subtype are not dropped just because the field did not exist before the paste. Real cells drawn
						// later in this row overwrite the placeholders for fields the current subtype already declares.
						if (m_TableAction == TableAction.SmartPaste || m_TableAction == TableAction.Import)
						{
							RegisterManagedReferenceChildPlaceholders(rowIndex, iterator.propertyPath, rootObject);
						}
						if (nextColumnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(nextColumnIndex))
						{
							var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(nextColumnIndex);
							var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
							columnRect.y = rowRect.y;
							var propertyRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
							if (CanDrawCellRect(columnRect))
							{
								var propertyControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
								if (DrawManagedReferenceDropdown(propertyRect, iterator, rootObject))
								{
									// The subtype changed which alters the available columns, so refresh and exit this frame to avoid using a stale iterator.
									ForceRefreshColumnLayout();
									return;
								}
								m_PropertyTable.Set(rowIndex, visibleColumnIndex, new ManagedReferenceTableProperty(rootObject, iterator.propertyPath, propertyControlName));
							}
						}
						continue;
					}
					if (nextColumnIndex >= startingColumnIndex && (iterator.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, showReadOnly, out bool isReadOnlyUnityField) || useAssetReferenceDrawer))
					{
						if (m_MultiColumnHeader.IsColumnVisible(nextColumnIndex))
						{
							var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(nextColumnIndex);
							var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
							columnRect.y = rowRect.y;
							var propertyRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
							if (CanDrawCellRect(columnRect))
							{
								var propertyControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
								var isCustomField = (isScriptableObject || serializedObject.targetObject is Component) && !isReadOnlyUnityField;
								HandleObjectReferenceCellDoubleClick(iterator, propertyRect);
								var assetReferenceClicked = IsAssetReferenceCellClicked(useAssetReferenceDrawer, propertyRect);
								Profiler.BeginSample("DrawProperty");
								EditorGUI.BeginDisabledGroup(!iterator.editable || isReadOnlyUnityField);
								if (renderingOverrides.Contains(iterator.propertyType))
								{
									propertyRect.height = EditorGUI.GetPropertyHeight(iterator, false);
									EditorGUI.PropertyField(propertyRect, iterator, GUIContent.none, false);
								}
								else
								{
									Object previewOverride = null;
									if (m_MultiColumnHeader.AssetPreviewChains.TryGetValue(nextColumnIndex, out var previewChain))
									{
										previewOverride = ResolveAssetPreviewObject(serializedObject, previewChain);
									}
									iterator.DrawProperty(propertyRect, rootObject, isCustomField, out bool arraySizeChanged, showReadOnly, m_Settings.UserInterface.AssetPreview, m_NewAssetPath, OnObjectCreated, m_TableNav.ResetTextFieldEditing, previewOverride, propertyControlName);
									if (m_Settings.UserInterface.ShowArrays && arraySizeChanged
										&& (isFirstFilteredObject || m_Settings.UserInterface.IsBestFitArraySize))
									{
										// The first filtered Object drives the column layout. One of its array sizes changed so the column layout gets refreshed.
										// OR we're using best fit and need to recheck the column layout.
										m_WaitingForColumnRefresh = true;
									}
								}
								EditorGUI.EndDisabledGroup();
								Profiler.EndSample();
								var tableProperty = new SerializedTableProperty(rootObject, iterator.propertyPath, propertyControlName);
								m_PropertyTable.Set(rowIndex, visibleColumnIndex, tableProperty);
								if (assetReferenceClicked)
								{
									m_TableNav.RequestFocus(propertyControlName);
									Repaint();
								}
								HandleReadOnlyCellClick(propertyRect, propertyControlName);
							}
							if (m_Settings.Workload.Virtualization)
							{
								if (propertyRect.x >= scrollEnd.x)
								{
									lastVisibleColumn = true;
								}
								if (propertyRect.y >= scrollEnd.y)
								{
									lastVisibleRow = true;
								}
								if (lastVisibleColumn && m_TableAction == TableAction.None)
								{
									break;
								}
							}
						}
					}
				}
				if (serializedObject.ApplyModifiedProperties())
				{
					// A property value changed this frame, so an active property filter may now match a different set.
					m_SearchResultsDirty = true;
				}
				// Focus and select the pending "Open in Scriptable Sheets" asset.
				if (isPendingOpenRow)
				{
					FocusPendingOpenRow(rowIndex, rootObject);
				}
				Profiler.EndSample();
				if (lastVisibleRow && m_TableAction == TableAction.None)
				{
					break;
				}
			}
			Profiler.EndSample();

			var tableNavVisualState = new TableNavVisualState()
			{
				MultiColumnHeader = m_MultiColumnHeader,
				ScrollViewArea = m_ScrollViewArea,
				ColumnHeaderRowRect = columnHeaderRowRect,
				RowHeight = rowHeight,
				ScrollStart = scrollStart,
				ScrollEnd = scrollEnd,
				// Matches the range GUI.BeginScrollView clamps to, so nudging the scroll never overshoots and jitters the header.
				MaxScroll = new Vector2(Mathf.Max(0, m_TableScrollViewRect.width - m_ScrollViewArea.width), Mathf.Max(0, m_TableScrollViewRect.height - m_ScrollViewArea.height)),
			};
			var highlightHeader = m_TableNav.UpdateFocusVisuals(m_PropertyTable, m_Settings.UserInterface.TableNav, tableNavVisualState, ref m_ScrollPosition, m_Settings.UserInterface.LockNames);

			GUI.EndScrollView(true);

			if (m_Settings.UserInterface.TableNav.HighlightSelectedColumn && highlightHeader)
			{
				var highlightColor = GUI.skin.button.focused.textColor;
				highlightColor.a = m_Settings.UserInterface.TableNav.HighlightAlpha;
				var focusedColumnHeaderRect = m_MultiColumnHeader.GetColumnRect(m_TableNav.VisualCoordinate.y);
				focusedColumnHeaderRect.x -= m_ScrollPosition.x;
				focusedColumnHeaderRect.y = columnHeaderRowRect.y;
				focusedColumnHeaderRect.height = EditorGUIUtility.singleLineHeight;
				EditorGUI.DrawRect(focusedColumnHeaderRect, highlightColor);
			}

			// Handle file actions after property table is drawn. Skip while auto generation is queued so the action waits for the new Objects.
			if (m_TableAction != TableAction.None && !m_AutoGeneratePending)
			{
				var tableAction = m_TableAction;
				m_TableAction = TableAction.None;
				var flatFileFormatSettings = GetFlatFileFormatSettings();
				switch (tableAction)
				{
					case TableAction.Copy:
						EditorGUIUtility.systemCopyBuffer = m_PropertyTable.ToFlatFileFormat(flatFileFormatSettings);
						break;

					case TableAction.CopyRow:
						flatFileFormatSettings.FirstRowIndex = m_TableNav.FocusedCoordinate.x;
						flatFileFormatSettings.FirstRowOnly = true;
						EditorGUIUtility.systemCopyBuffer = m_PropertyTable.ToFlatFileFormat(flatFileFormatSettings);
						break;

					case TableAction.CopyColumn:
						if (m_VisibleColumnCopyIndex > 0)
						{
							flatFileFormatSettings.FirstColumnIndex = m_VisibleColumnCopyIndex;
							m_VisibleColumnCopyIndex = 0;
						}
						else
						{
							flatFileFormatSettings.FirstColumnIndex = m_TableNav.FocusedCoordinate.y;
						}
						flatFileFormatSettings.FirstColumnOnly = true;
						EditorGUIUtility.systemCopyBuffer = m_PropertyTable.ToFlatFileFormat(flatFileFormatSettings);
						break;

					case TableAction.CopyJson:
						EditorGUIUtility.systemCopyBuffer = m_PropertyTable.ToJsonFormat(m_Settings.DataTransfer.JsonSerializationFormat, flatFileFormatSettings);
						break;

					case TableAction.SmartPaste:
						// Use the cell captured before auto generate cleared focus, otherwise the current selection.
						var focusedCoordinate = m_DeferredActionCoordinate ?? m_TableNav.FocusedCoordinate;
						m_DeferredActionCoordinate = null;
						//  If page rows or visible columns are disabled, then apply offsets so we're still starting the paste from the selected cell.
						if (!m_Settings.DataTransfer.PageRowsOnly)
						{
							focusedCoordinate.x += (m_Paginator.CurrentPage - 1) * m_Paginator.ObjectsPerPage;
						}
						if (!m_Settings.DataTransfer.VisibleColumnsOnly)
						{
							focusedCoordinate.y = m_CachedVisibleColumns[focusedCoordinate.y];
						}
						flatFileFormatSettings.SetStartingIndex(focusedCoordinate);
						m_TableSmartPaste.Paste(m_PropertyTable, flatFileFormatSettings);
						// This notifies the selected field to immediately update its contents after a paste action.
						m_TableNav.ResetTextFieldEditing();
						break;

					case TableAction.Import:
						if (m_IsImportJson)
						{
							m_IsImportJson = false;
							m_PropertyTable.FromJsonFormat(m_ImportedFileContents, m_Settings.DataTransfer.JsonSerializationFormat, flatFileFormatSettings);
						}
						else
						{
							m_PropertyTable.FromFlatFileFormat(m_ImportedFileContents, flatFileFormatSettings);
						}
						m_ImportedFileContents = string.Empty;
						m_PendingImportFolder = string.Empty;
						GUIUtility.keyboardControl = 0;
						break;

					case TableAction.Save:
						var extension = FlatFileUtility.GetExtensionFromPath(m_SelectedFilepath);
						var flatFileContents = string.Empty;
						if (extension == JsonExtension)
						{
							flatFileContents = m_PropertyTable.ToJsonFormat(m_Settings.DataTransfer.JsonSerializationFormat, flatFileFormatSettings);
						}
						else
						{
							// Auto detect delimiter to use based on file extension if possible.
							if (FlatFileUtility.FlatFileDelimiters.TryGetValue(extension, out string delimiter))
							{
								flatFileFormatSettings.ColumnDelimiter = delimiter;
							}
							flatFileContents = m_PropertyTable.ToFlatFileFormat(flatFileFormatSettings);
						}
						File.WriteAllText(m_SelectedFilepath, flatFileContents);
						if (m_SelectedFilepath.Contains(Application.dataPath))
						{
							AssetDatabase.Refresh();
						}
						m_SelectedFilepath = string.Empty;
						break;

					default:
						Debug.LogWarning($"Unsupported {nameof(tableAction)} {tableAction}.");
						break;
				}
				// Paste and import write new values, which can change which Objects match an active property filter.
				if (tableAction == TableAction.SmartPaste || tableAction == TableAction.Import)
				{
					m_SearchResultsDirty = true;
					// A name value renames the asset outside this window, so drop the cached paths or a later delete targets the old path.
					m_AssetPathCache.Clear();
				}
			}
			// Keep the temporarily expanded columns until the deferred action runs so the smart paste remap still has them.
			if (!m_AutoGeneratePending && m_CachedVisibleColumns != null && m_CachedVisibleColumns.Length > 0)
			{
				SetVisibleColumns(m_CachedVisibleColumns);
				m_CachedVisibleColumns = null;
			}

			if (m_Settings.Workload.AutoSave)
			{
				AssetDatabase.SaveAssets();
			}
		}

		private bool ConfirmOver9000(int amount)
		{
			if (amount > 9000)
			{
				return EditorUtility.DisplayDialog("It's Over 9000!!!", "What!? 9000!? There's no way that can be right!", "Send it", "Cancel");
			}
			return true;
		}

		// Creates the specified number of new ScriptableObjects of the selected type using the current naming and path settings.
		// Registers them with the scanner and returns the last created Object. Does not adjust layout, pagination, or selection.
		private ScriptableObject CreateNewObjects(int amount)
		{
			if (!AssetDatabase.IsValidFolder(m_NewAssetPath))
			{
				// If the path got deleted somehow, attempt to recreate it. Done before StartAssetEditing so the Refresh never runs inside the batch.
				Directory.CreateDirectory(m_NewAssetPath);
				AssetDatabase.Refresh();
				if (!AssetDatabase.IsValidFolder(m_NewAssetPath))
				{
					m_NewAssetPath = UnityConstants.DefaultAssetPath;
				}
			}
			AssetDatabase.StartAssetEditing();
			ScriptableObject lastCreatedObject = null;
			for (var i = 0; i < amount; i++)
			{
				var selectedTypeName = m_SelectedType.Name;
				var newObjectName = m_Settings.ObjectManagement.NewObjectName;
				if (string.IsNullOrEmpty(newObjectName))
				{
					newObjectName = selectedTypeName;
				}
				var prefix = m_Settings.ObjectManagement.NewObjectPrefix;
				var suffix = m_Settings.ObjectManagement.NewObjectSuffix;
				if (m_Settings.ObjectManagement.UseExpansion)
				{
					var newObjectIndex = i + m_Settings.ObjectManagement.StartingIndex;
					var indexPadding = m_Settings.ObjectManagement.IndexPadding;
					newObjectName = newObjectName.ExpandAll(newObjectIndex, selectedTypeName, indexPadding);
					prefix = prefix.ExpandAll(newObjectIndex, selectedTypeName, indexPadding);
					suffix = suffix.ExpandAll(newObjectIndex, selectedTypeName, indexPadding);
				}
				newObjectName = $"{prefix}{newObjectName}{suffix}{UnityConstants.Extensions.Asset}";
				var uniqueAssetPath = AssetDatabase.GenerateUniqueAssetPath(m_NewAssetPath + "/" + newObjectName);
				if (m_Settings.Workload.Debug)
				{
					Debug.Log($"Creating new asset of type {m_SelectedType} with name {newObjectName} at path {uniqueAssetPath}");
				}
				var newScriptableObject = CreateInstance(m_SelectedType);
				if (m_Settings.ObjectManagement.DefaultMainAsset != null)
				{
					m_MainAsset = m_Settings.ObjectManagement.DefaultMainAsset;
				}
				if (m_MainAsset != null)
				{
					AssetDatabase.AddObjectToAsset(newScriptableObject, m_MainAsset);
					newScriptableObject.name = newObjectName.Substring(0, newObjectName.LastIndexOf('.'));

					if (!m_Scanner.SubAssetsByTypeAndMainAsset.TryGetValue(m_SelectedType, out var subAssetByMainAsset))
					{
						subAssetByMainAsset = new Dictionary<Object, List<Object>>();
						m_Scanner.SubAssetsByTypeAndMainAsset[m_SelectedType] = subAssetByMainAsset;
					}
					if (!m_Scanner.SubAssetsByTypeAndMainAsset[m_SelectedType].TryGetValue(m_MainAsset, out var subAssets))
					{
						subAssets = new List<Object>();
						m_Scanner.SubAssetsByTypeAndMainAsset[m_SelectedType][m_MainAsset] = subAssets;
						m_SelectedMainAssetIndex = m_Scanner.SubAssetsByTypeAndMainAsset[m_SelectedType].Count - 1;
					}
					subAssets.Add(newScriptableObject);
				}
				else
				{
					AssetDatabase.CreateAsset(newScriptableObject, uniqueAssetPath);
				}
				m_Scanner.ObjectsByType[m_SelectedType].Add(newScriptableObject);
				lastCreatedObject = newScriptableObject;
			}
			AssetDatabase.StopAssetEditing();
			AssetDatabase.SaveAssets();
			// Force a synchronous import so the new assets finish importing before a follow up import or paste renames them, which
			// otherwise races the asset pipeline workers and logs a build asset version error on the deleted source file.
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			return lastCreatedObject;
		}

		// Queues object generation so a pending paste or import can fill every row. Returns true while a generation is pending or queued,
		// in which case the caller should return so the table is not built this frame. Returns false to let the normal parse proceed.
		// The actual creation is deferred to a delayCall so the GUI control count stays consistent between the Layout and Repaint passes.
		private bool TryAutoGenerateForPendingAction(List<Object> matchingObjects, bool isScriptableObject, MonoScript monoScript)
		{
			if (!m_Settings.ObjectManagement.AutoGenerate || !isScriptableObject || monoScript == null || IsCollectionTable())
			{
				return false;
			}
			if (m_TableAction != TableAction.SmartPaste && m_TableAction != TableAction.Import)
			{
				return false;
			}
			// Keep skipping the table build while generation is queued so the decision is identical across the Layout and Repaint passes.
			if (m_AutoGeneratePending)
			{
				return true;
			}
			// A search filter narrows the comparison set and the generated Objects may not match it, so skip to avoid runaway creation.
			if (!string.IsNullOrEmpty(m_SearchInput))
			{
				return false;
			}
			var formatSettings = GetFlatFileFormatSettings();
			int rowCount;
			int startRow;
			if (m_TableAction == TableAction.Import)
			{
				rowCount = m_IsImportJson ? TableUtility.GetJsonRowCount(m_ImportedFileContents) : TableUtility.GetFlatFileRowCount(m_ImportedFileContents, formatSettings);
				startRow = formatSettings.FirstRowIndex;
			}
			else
			{
				rowCount = TableUtility.GetFlatFileRowCount(m_TableSmartPaste.PasteContent, formatSettings);
				startRow = m_TableNav.FocusedCoordinate.x;
				// Match the page offset the paste will apply when it is not restricted to the current page.
				if (!m_Settings.DataTransfer.PageRowsOnly)
				{
					startRow += (m_Paginator.CurrentPage - 1) * m_Paginator.ObjectsPerPage;
				}
			}
			int deficit;
			if (m_Settings.DataTransfer.PageRowsOnly)
			{
				// The parse is limited to the current page, so only generate enough to fill the remaining room on the page up to the row limit.
				var pageObjectCount = m_Paginator.GetPageObjects(matchingObjects).Count;
				var targetPageCount = Mathf.Min(startRow + rowCount, m_Paginator.ObjectsPerPage);
				deficit = targetPageCount - pageObjectCount;
			}
			else
			{
				deficit = startRow + rowCount - matchingObjects.Count;
			}
			if (deficit <= 0)
			{
				return false;
			}
			// Capture the selected cell now since generating Objects later clears focus and would reset it to (0, 0).
			if (m_TableAction == TableAction.SmartPaste)
			{
				m_DeferredActionCoordinate = m_TableNav.FocusedCoordinate;
			}
			// Defer the confirmation and creation outside of OnGUI so we never change the GUI control count mid frame.
			m_AutoGeneratePending = true;
			EditorApplication.delayCall += () => GenerateObjectsForPendingAction(deficit, rowCount);
			return true;
		}

		private void GenerateObjectsForPendingAction(int amount, int rowCount)
		{
			m_AutoGeneratePending = false;
			// The pending action may have been cancelled if the user changed type before this deferred call ran.
			if (m_TableAction != TableAction.Import && m_TableAction != TableAction.SmartPaste)
			{
				return;
			}
			var importFolderRoot = GoogleSheetsImporter.GetFolderRoot(m_PendingImportFolder);
			var useImportFolder = m_TableAction == TableAction.Import
				&& m_MainAsset == null
				&& m_Settings.ObjectManagement.DefaultMainAsset == null
				&& !string.IsNullOrEmpty(importFolderRoot)
				&& AssetDatabase.IsValidFolder(importFolderRoot);
			var proceed = true;
			if (amount > 9000)
			{
				proceed = ConfirmOver9000(amount);
			}
			else if (m_Settings.ObjectManagement.ConfirmGeneration)
			{
				var destination = m_MainAsset != null ? $"{m_MainAsset.name} (Sub Asset)" : useImportFolder ? importFolderRoot : m_NewAssetPath;
				var message = $"Auto generate {amount} new {m_SelectedType.Name} object(s) at '{destination}' to match the {rowCount} row(s) of data?";
				proceed = EditorUtility.DisplayDialog("Auto Generate Objects", message, "Generate", "Cancel");
			}
			if (proceed)
			{
				if (useImportFolder)
				{
					var previousNewAssetPath = m_NewAssetPath;
					m_NewAssetPath = importFolderRoot;
					CreateNewObjects(amount);
					m_NewAssetPath = previousNewAssetPath;
				}
				else
				{
					CreateNewObjects(amount);
				}
			}
			else
			{
				CancelPendingAction();
			}
			Repaint();
		}

		private void CancelPendingAction()
		{
			m_TableAction = TableAction.None;
			m_ImportedFileContents = string.Empty;
			m_PendingImportFolder = string.Empty;
			m_IsImportJson = false;
			m_DeferredActionCoordinate = null;
			m_AutoGeneratePending = false;
		}

		private void RefreshColumnLayout(Object obj, bool hasSelectedTypeChanged, List<Object> filteredObjects)
		{
			var columns = new List<MultiColumnHeaderState.Column>();

			var extraPadding = m_Settings.UserInterface.ShowRowIndex ? SheetLayout.PropertyWidthSmall / 2 : 0;
			var actionColumnWidth = SheetLayout.InlineButtonWidth * 2 + SheetLayout.InlineButtonSpacing * 2 + extraPadding;
			var actionColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
			var actionColumn = ColumnUtility.CreateActionsColumn($"{actionColumnLabel}Actions", actionColumnWidth);
			columns.Add(actionColumn);

			var isScriptableObject = IsScriptableObject();

			var serializedObject = new SerializedObject(obj);
			RefreshCollectionFields(serializedObject);
			if (IsCollectionTable())
			{
				BuildCollectionColumns(serializedObject, isScriptableObject, columns, filteredObjects);
			}
			else
			{
				var nameProperty = GetNameProperty(serializedObject);
				var nameColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
				var nameColumn = ColumnUtility.CreatePropertyColumn(nameProperty, isScriptableObject, m_Settings.UserInterface.HeaderFormat, nameColumnLabel);
				columns.Add(nameColumn);
	
				if (m_Settings.UserInterface.ShowAssetPath)
				{
					var assetPathColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
					var assetPathColumn = ColumnUtility.CreateAssetPathColumn($"{assetPathColumnLabel}Asset Path");
					columns.Add(assetPathColumn);
				}
	
				if (m_Settings.UserInterface.ShowGuid)
				{
					var guidColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
					var guidColumn = ColumnUtility.CreateGuidColumn($"{guidColumnLabel}GUID");
					columns.Add(guidColumn);
				}
	
				var iterator = serializedObject.GetIterator();
				var includeChildren = true;
				var iterations = 0;
				Dictionary<Object, SerializedObject> serializedObjects = new Dictionary<Object, SerializedObject>();
				while (iterator.NextVisible(includeChildren) && iterations++ < m_Settings.Workload.MaxIterations)
				{
					var useAssetReferenceDrawer = !m_Settings.UserInterface.ShowReadOnly && iterator.IsAssetReference();
					var isExpandableManagedReference = m_Settings.UserInterface.ManagedReferences && iterator.IsManagedReference();
					includeChildren = m_Settings.UserInterface.ShowChildren && !useAssetReferenceDrawer && !isExpandableManagedReference;
					if (isExpandableManagedReference)
					{
						// Build a type dropdown column plus the union of every concrete subtype's fields instead of only the first Object's subtype.
						AddManagedReferenceColumns(iterator, isScriptableObject, columns, filteredObjects);
						continue;
					}
					if (useAssetReferenceDrawer || iterator.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, m_Settings.UserInterface.ShowReadOnly, out bool isReadOnly))
					{
						if (m_Settings.UserInterface.OverrideArraySize && iterator.propertyType == SerializedPropertyType.ArraySize)
						{
							var maxSize = 0;
							if (m_Settings.UserInterface.BestFitArraySize)
							{
								for (var i = 0; i < filteredObjects.Count && i < m_Settings.Workload.MaxIterations; i++)
								{
									if (!serializedObjects.TryGetValue(filteredObjects[i], out var serializedObj))
									{
										serializedObj = new SerializedObject(filteredObjects[i]);
										serializedObjects[filteredObjects[i]] = serializedObj;
									}
									var prop = serializedObj.FindProperty(iterator.propertyPath);
									var size = prop != null ? prop.intValue : 0;
									if (size > maxSize)
									{
										maxSize = size;
									}
								}
							}
							iterator.intValue = Mathf.Max(maxSize, m_Settings.UserInterface.ArraySize);
						}
						var propertyColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
						var propertyColumn = ColumnUtility.CreatePropertyColumn(iterator, isScriptableObject, m_Settings.UserInterface.HeaderFormat, propertyColumnLabel);
						columns.Add(propertyColumn);
					}
				}
			}

			// Remove duplicate columns like the name field for Prefabs.
			m_Columns = columns.GroupBy(c => c.headerContent.tooltip).Select(g => g.First()).ToArray();

			int[] cachedVisibleColumns;
			if (m_MultiColumnHeader == null || hasSelectedTypeChanged || m_Columns.Length != m_MultiColumnHeaderState.columns.Length)
			{
				m_MultiColumnHeaderState = new MultiColumnHeaderState(m_Columns)
				{
					visibleColumns = m_Columns.GetClampedColumns(m_Settings.Workload.VisibleColumnLimit)
				};
			}
			else
			{
				cachedVisibleColumns = m_MultiColumnHeaderState.visibleColumns;
				m_MultiColumnHeaderState = new MultiColumnHeaderState(m_Columns)
				{
					visibleColumns = cachedVisibleColumns.Take(m_Settings.Workload.VisibleColumnLimit).ToArray()
				};
			}
			m_MultiColumnHeader = new SheetsMultiColumnHeader(m_MultiColumnHeaderState, CopyColumnHeaderColumn, SetPropertySearchFilter);
			TryRestoreTableLayout();
			// Re-apply the appropriate action column width based on if we're showing row index.
			m_MultiColumnHeader.state.columns[0].width = actionColumnWidth;
#if UNITY_2021_2_OR_NEWER
			// Raised when Column widths change.
			m_MultiColumnHeader.columnSettingsChanged += OnColumnSettingsChanged;
#endif
			m_MultiColumnHeader.sortingChanged += OnSortingChanged;
			m_MultiColumnHeader.visibleColumnsChanged += OnVisibleColumnsChanged;
			m_MultiColumnHeader.dockedColumnsChanged += OnDockedColumnsChanged;
			m_MultiColumnHeader.headerColorsChanged += OnHeaderColorsChanged;
			m_MultiColumnHeader.managedTypeFiltersChanged += OnManagedTypeFiltersChanged;
			m_MultiColumnHeader.assetPreviewSourceChanged += OnAssetPreviewSourceChanged;
			m_MultiColumnHeader.recordColumnUndo = RecordColumnLayoutUndo;

			// Keep a live Object of the current type so the Map Asset Preview menu can enumerate its references on demand. The
			// obj used to build the columns may be a temporary instance that is destroyed right after, so prefer a real Object.
			m_AssetPreviewRepresentative = filteredObjects != null && filteredObjects.Count > 0 ? filteredObjects[0] : obj;
			if (IsCollectionTable())
			{
				// Map Asset Preview maps owner level object reference fields, which don't apply to a Collection Table's flattened
				// element rows, so the option is hidden and no mappings are applied here. Normal table mappings stay persisted.
				m_MultiColumnHeader.AssetPreviewEntriesProvider = null;
				m_MultiColumnHeader.AssetPreviewChains = new Dictionary<int, string[]>();
			}
			else
			{
				m_MultiColumnHeader.AssetPreviewEntriesProvider = BuildAssetPreviewEntries;
				RestoreAssetPreviewSources(GetTableLayout());
			}

			// Cache to map column tooltip paths directly to an index.
			m_MultiColumnTooltipPaths = m_MultiColumnHeaderState.columns
				.Select((column, index) => new KeyValuePair<string, int>(column.headerContent.tooltip, index))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			// Cache property paths by column index so filters can reference columns.
			m_ColumnPropertyPaths = m_MultiColumnHeaderState.columns.Select(c => c.headerContent.tooltip).ToArray();
		}

		private void BuildCollectionColumns(SerializedObject serializedObject, bool isScriptableObject, List<MultiColumnHeaderState.Column> columns, List<Object> filteredObjects)
		{
			var groupNameColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
			columns.Add(ColumnUtility.CreateGroupNameColumn($"{groupNameColumnLabel}Group Name"));

			var indexColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
			columns.Add(ColumnUtility.CreateCollectionIndexColumn($"{indexColumnLabel}Index"));

			var collectionProperty = serializedObject.FindProperty(m_SelectedCollectionPath);
			if (collectionProperty == null || !collectionProperty.isArray)
			{
				return;
			}
			if (collectionProperty.arraySize < 1)
			{
				collectionProperty.arraySize = 1;
			}
			var sampleElement = collectionProperty.GetArrayElementAtIndex(0);

			// Serialize reference elements get a type dropdown Value column plus the union of every concrete subtype's fields
			// found across every owner's elements, mirroring the default table's managed reference handling.
			if (m_Settings.UserInterface.ManagedReferences && sampleElement.IsManagedReference())
			{
				AddCollectionManagedReferenceColumns(sampleElement, isScriptableObject, columns, filteredObjects);
				return;
			}

			// Leaf element collections (ints, strings, object references) have no child fields, so they get a single Value column.
			if (!sampleElement.hasVisibleChildren)
			{
				var valueColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
				var valueColumn = ColumnUtility.CreatePropertyColumn(sampleElement, isScriptableObject, m_Settings.UserInterface.HeaderFormat, valueColumnLabel);
				valueColumn.headerContent = new GUIContent($"{valueColumnLabel}Value", ColumnUtility.CollectionValueTooltip);
				columns.Add(valueColumn);
				return;
			}

			var elementPrefix = $"{sampleElement.propertyPath}.";
			var child = sampleElement.Copy();
			var end = sampleElement.GetEndProperty();
			var enterChildren = true;
			var iterations = 0;
			var seenRelativePaths = new HashSet<string>();
			while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end) && iterations++ < m_Settings.Workload.MaxIterations)
			{
				enterChildren = m_Settings.UserInterface.ShowChildren;
				if (!child.propertyPath.StartsWith(elementPrefix))
				{
					break;
				}
				// Resize nested element arrays so their element columns appear, scanning every owner's elements for the largest
				// size when Best Fit is enabled. Mirrors the top level array handling but across the expanded collection.
				if (m_Settings.UserInterface.OverrideArraySize && child.propertyType == SerializedPropertyType.ArraySize)
				{
					var relativeSizePath = child.propertyPath.Substring(elementPrefix.Length);
					var bestFitSize = m_Settings.UserInterface.BestFitArraySize ? GetCollectionNestedArraySize(filteredObjects, relativeSizePath) : 0;
					child.intValue = Mathf.Max(bestFitSize, m_Settings.UserInterface.ArraySize);
				}
				var relativePath = child.propertyPath.Substring(elementPrefix.Length);
				if (!seenRelativePaths.Add(relativePath))
				{
					continue;
				}
				var useAssetReferenceDrawer = !m_Settings.UserInterface.ShowReadOnly && child.IsAssetReference();
				if (useAssetReferenceDrawer || child.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, m_Settings.UserInterface.ShowReadOnly, out _))
				{
					var columnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
					var column = ColumnUtility.CreatePropertyColumn(child, isScriptableObject, m_Settings.UserInterface.HeaderFormat, columnLabel);
					// Key the column by the element relative path so every element row matches regardless of its index.
					column.headerContent.tooltip = relativePath;
					columns.Add(column);
				}
			}
		}

		// Adds the serialize reference type dropdown Value column followed by the union of every concrete subtype's element
		// fields found across the owners' collections. Each subtype field column is keyed by its element relative path so a
		// row only renders the fields belonging to its own concrete subtype, mirroring AddManagedReferenceColumns.
		private void AddCollectionManagedReferenceColumns(SerializedProperty sampleElement, bool isScriptableObject, List<MultiColumnHeaderState.Column> columns, List<Object> filteredObjects)
		{
			var valueColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
			var valueColumn = ColumnUtility.CreatePropertyColumn(sampleElement, isScriptableObject, m_Settings.UserInterface.HeaderFormat, valueColumnLabel);
			valueColumn.headerContent = new GUIContent($"{valueColumnLabel}Value", ColumnUtility.CollectionValueTooltip);
			columns.Add(valueColumn);

			// The Value column carries the subtype filter, so disabled subtypes are keyed by its tooltip.
			var disabledTypes = GetDisabledManagedTypes(ColumnUtility.CollectionValueTooltip);
			var seenRelativePaths = new HashSet<string>();
			var examined = 0;
			for (var i = 0; i < filteredObjects.Count && examined < m_Settings.Workload.MaxIterations; i++)
			{
				var owner = filteredObjects[i];
				if (owner == null)
				{
					continue;
				}
				var ownerSerializedObject = GetRowSerializedObject(owner);
				ownerSerializedObject.Update();
				var collectionProperty = ownerSerializedObject.FindProperty(m_SelectedCollectionPath);
				if (collectionProperty == null || !collectionProperty.isArray)
				{
					continue;
				}
				for (var elementIndex = 0; elementIndex < collectionProperty.arraySize && examined < m_Settings.Workload.MaxIterations; elementIndex++, examined++)
				{
					var element = collectionProperty.GetArrayElementAtIndex(elementIndex);
					if (element == null || !element.IsManagedReference())
					{
						continue;
					}
					// Skip subtypes toggled off for this column so their fields don't contribute columns.
					if (disabledTypes != null && disabledTypes.Contains(element.GetManagedReferenceTypeName()))
					{
						continue;
					}
					var elementPrefix = $"{element.propertyPath}.";
					var child = element.Copy();
					var end = element.GetEndProperty();
					var enterChildren = true;
					while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end) && child.propertyPath.StartsWith(elementPrefix))
					{
						enterChildren = m_Settings.UserInterface.ShowChildren;
						var relativePath = child.propertyPath.Substring(elementPrefix.Length);
						if (!seenRelativePaths.Add(relativePath))
						{
							continue;
						}
						if (child.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, m_Settings.UserInterface.ShowReadOnly, out _))
						{
							var childColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
							var childColumn = ColumnUtility.CreatePropertyColumn(child, isScriptableObject, m_Settings.UserInterface.HeaderFormat, childColumnLabel);
							// Key the column by the element relative path so every element row matches regardless of its index.
							childColumn.headerContent.tooltip = relativePath;
							columns.Add(childColumn);
						}
					}
				}
			}
		}

		// Returns the largest size found for a nested element array across every owner's elements, capped by Max Iterations.
		// The relative size path is the element relative path of the nested array's size property (e.g. "m_Rows.Array.size").
		private int GetCollectionNestedArraySize(List<Object> owners, string relativeSizePath)
		{
			var maxSize = 0;
			var examined = 0;
			for (var i = 0; i < owners.Count && examined < m_Settings.Workload.MaxIterations; i++)
			{
				var owner = owners[i];
				if (owner == null)
				{
					continue;
				}
				var serializedObject = GetRowSerializedObject(owner);
				serializedObject.Update();
				var collectionProperty = serializedObject.FindProperty(m_SelectedCollectionPath);
				if (collectionProperty == null || !collectionProperty.isArray)
				{
					continue;
				}
				for (var elementIndex = 0; elementIndex < collectionProperty.arraySize && examined < m_Settings.Workload.MaxIterations; elementIndex++, examined++)
				{
					var sizeProperty = serializedObject.FindProperty($"{m_SelectedCollectionPath}.Array.data[{elementIndex}].{relativeSizePath}");
					if (sizeProperty != null && sizeProperty.intValue > maxSize)
					{
						maxSize = sizeProperty.intValue;
					}
				}
			}
			return maxSize;
		}

		// A single Collection View row: an element of an owner's collection, or a placeholder add row for an empty owner.
		private readonly struct CollectionRow
		{
			public readonly Object Owner;
			public readonly int ElementIndex;
			public readonly bool IsAddRow;

			public CollectionRow(Object owner, int elementIndex, bool isAddRow)
			{
				Owner = owner;
				ElementIndex = elementIndex;
				IsAddRow = isAddRow;
			}
		}

		// Flattens the matching owners into Collection View rows, grouped by owner. Each element becomes a row; the add and
		// remove buttons live on the Owner column of those rows. An owner with an empty collection contributes a single
		// placeholder add row so it still has a button to add its first element.
		private List<CollectionRow> BuildCollectionRows(List<Object> owners)
		{
			var rows = new List<CollectionRow>();
			if (string.IsNullOrEmpty(m_SelectedCollectionPath))
			{
				return rows;
			}
			for (var i = 0; i < owners.Count; i++)
			{
				var owner = owners[i];
				if (owner == null)
				{
					continue;
				}
				var serializedObject = GetRowSerializedObject(owner);
				serializedObject.Update();
				var collectionProperty = serializedObject.FindProperty(m_SelectedCollectionPath);
				var size = collectionProperty != null && collectionProperty.isArray ? collectionProperty.arraySize : 0;
				if (size <= 0)
				{
					rows.Add(new CollectionRow(owner, -1, true));
					continue;
				}
				for (var elementIndex = 0; elementIndex < size && elementIndex < m_Settings.Workload.MaxIterations; elementIndex++)
				{
					rows.Add(new CollectionRow(owner, elementIndex, false));
				}
			}
			return rows;
		}

		// Filters the already sorted Collection Table rows by the search input. Property filters evaluate each row's element using
		// its full array element path, while name, guid, and asset path filters evaluate the owning Object.
		private List<CollectionRow> FilterCollectionRows(List<CollectionRow> rows, string input, SearchSettings settings, bool useStringEnums, bool ignoreEnumCasing)
		{
			if (string.IsNullOrEmpty(input))
			{
				return rows;
			}
			// Use the first real element as the reference to parse the filter's element relative property paths and enum types.
			Object referenceOwner = null;
			var referenceElementPrefix = string.Empty;
			foreach (var row in rows)
			{
				if (!row.IsAddRow)
				{
					referenceOwner = row.Owner;
					referenceElementPrefix = $"{m_SelectedCollectionPath}.Array.data[{row.ElementIndex}].";
					break;
				}
			}
			return SearchFilter.GetCollectionRows(input, rows, row => row.Owner,
				row => row.IsAddRow ? string.Empty : $"{m_SelectedCollectionPath}.Array.data[{row.ElementIndex}].",
				row => row.ElementIndex, row => row.IsAddRow, settings, useStringEnums, ignoreEnumCasing, m_ColumnPropertyPaths, referenceOwner, referenceElementPrefix);
		}

		// Reapplies the frozen sort order to the freshly built rows so editing values never reorders the table. Rows still present
		// keep their sorted position, removed rows drop out, and rows added since the last sort are appended to the bottom.
		private List<CollectionRow> ReconcileCollectionRows(List<CollectionRow> sorted, List<CollectionRow> current)
		{
			var currentKeys = new HashSet<(int, int)>();
			foreach (var row in current)
			{
				if (row.Owner != null)
				{
					currentKeys.Add((row.Owner.GetInstanceIdCompat(), row.ElementIndex));
				}
			}
			var result = new List<CollectionRow>(current.Count);
			var seen = new HashSet<(int, int)>();
			foreach (var row in sorted)
			{
				if (row.Owner != null)
				{
					var key = (row.Owner.GetInstanceIdCompat(), row.ElementIndex);
					if (currentKeys.Contains(key) && seen.Add(key))
					{
						result.Add(row);
					}
				}
			}
			foreach (var row in current)
			{
				if (row.Owner != null && seen.Add((row.Owner.GetInstanceIdCompat(), row.ElementIndex)))
				{
					result.Add(row);
				}
			}
			return result;
		}

		// Sorts Collection Table rows grouped by their owning Object, then by the selected column within each group. The Group
		// Name column sorts the groups; the Index and field columns keep groups in name order and sort the element rows inside
		// each group, so a field like Health Restored finally sorts 0, 10, 100 instead of staying in element index order.
		private List<CollectionRow> SortCollectionRows(List<CollectionRow> rows)
		{
			if (rows.Count <= 1)
			{
				return rows;
			}
			var sortedColumnIndex = m_MultiColumnHeaderState.sortedColumnIndex;
			if (sortedColumnIndex < 0 || sortedColumnIndex >= m_MultiColumnHeaderState.columns.Length)
			{
				return rows;
			}
			var column = m_MultiColumnHeader.GetColumn(sortedColumnIndex);
			if (!column.canSort)
			{
				return rows;
			}
			var tooltip = column.headerContent.tooltip;
			var isAscending = column.sortedAscending;
			var isGroupName = tooltip == ColumnUtility.GroupNameTooltip;
			var isIndex = tooltip == ColumnUtility.CollectionIndexTooltip;
			var alphanumComparer = new AlphanumComparer();
			var fieldComparer = new CollectionFieldComparer();
			// Groups stay in ascending name order unless the Group Name column itself is driving the sort direction.
			var groupAscending = !isGroupName || isAscending;

			// Resolve each row's owner name and field value once so the comparisons below never re-read a SerializedObject.
			var indexed = new List<(CollectionRow Row, string OwnerName, int OwnerId, IComparable FieldKey)>(rows.Count);
			foreach (var row in rows)
			{
				var ownerName = row.Owner != null ? row.Owner.name : string.Empty;
				var ownerId = row.Owner != null ? row.Owner.GetInstanceIdCompat() : 0;
				var fieldKey = isGroupName || isIndex ? null : GetCollectionElementComparable(row, tooltip);
				indexed.Add((row, ownerName, ownerId, fieldKey));
			}
			indexed.Sort((a, b) =>
			{
				var groupCompare = alphanumComparer.Compare(a.OwnerName, b.OwnerName);
				if (!groupAscending)
				{
					groupCompare = -groupCompare;
				}
				if (groupCompare != 0)
				{
					return groupCompare;
				}
				// Keep every row of the same owner contiguous even when two owners share a name.
				if (a.OwnerId != b.OwnerId)
				{
					return a.OwnerId.CompareTo(b.OwnerId);
				}
				if (isIndex)
				{
					return isAscending ? a.Row.ElementIndex.CompareTo(b.Row.ElementIndex) : b.Row.ElementIndex.CompareTo(a.Row.ElementIndex);
				}
				if (!isGroupName)
				{
					var fieldCompare = fieldComparer.Compare(a.FieldKey, b.FieldKey);
					if (!isAscending)
					{
						fieldCompare = -fieldCompare;
					}
					if (fieldCompare != 0)
					{
						return fieldCompare;
					}
				}
				// Element index keeps rows stable within a group and breaks ties for equal field values.
				return a.Row.ElementIndex.CompareTo(b.Row.ElementIndex);
			});
			var sorted = new List<CollectionRow>(rows.Count);
			foreach (var entry in indexed)
			{
				sorted.Add(entry.Row);
			}
			return sorted;
		}

		// Resolves the comparable value of a Collection Table row's element for the given field column. The Value column compares
		// the element itself, while field columns are keyed by their element relative path.
		private IComparable GetCollectionElementComparable(CollectionRow row, string columnTooltip)
		{
			if (row.IsAddRow || row.Owner == null)
			{
				return null;
			}
			var elementPath = $"{m_SelectedCollectionPath}.Array.data[{row.ElementIndex}]";
			var propertyPath = columnTooltip == ColumnUtility.CollectionValueTooltip ? elementPath : $"{elementPath}.{columnTooltip}";
			return ComparableUtility.GetPropertyComparable(row.Owner, propertyPath);
		}

		// Orders element field values, falling back to alphanumeric ordering for strings and sorting missing values first.
		private class CollectionFieldComparer : IComparer<IComparable>
		{
			private readonly AlphanumComparer m_AlphanumComparer = new AlphanumComparer();

			public int Compare(IComparable x, IComparable y)
			{
				if (x == null)
				{
					return y == null ? 0 : -1;
				}
				if (y == null)
				{
					return 1;
				}
				if (x is string xString && y is string yString)
				{
					return m_AlphanumComparer.Compare(xString, yString);
				}
				return x.CompareTo(y);
			}
		}

		// Inserts a new element at the given index (clamped) on the selected collection. Used by the Owner column add button to
		// insert after the current element, or to add the first element of an empty owner.
		private void InsertCollectionElement(SerializedObject serializedObject, int elementIndex)
		{
			var collectionProperty = serializedObject.FindProperty(m_SelectedCollectionPath);
			if (collectionProperty != null && collectionProperty.isArray)
			{
				m_TableNav.ResetTextFieldEditing();
				var insertIndex = Mathf.Clamp(elementIndex, 0, collectionProperty.arraySize);
				collectionProperty.InsertArrayElementAtIndex(insertIndex);
				// Duplicating the element copies its serialize reference id so the copy would share one instance with the source.
				// Give the inserted element its own managed reference instance so editing one no longer changes the other.
				collectionProperty.GetArrayElementAtIndex(insertIndex).ReassignManagedReferenceValue();
				serializedObject.ApplyModifiedProperties();
				// The element count changed, so the cached Collection Table rows must be rebuilt.
				m_SearchResultsDirty = true;
				// Re-sort so the new element lands within its group instead of appending to the bottom.
				m_CollectionRowsResortNeeded = true;
			}
		}

		private void RemoveCollectionElement(SerializedObject serializedObject, int elementIndex)
		{
			var collectionProperty = serializedObject.FindProperty(m_SelectedCollectionPath);
			if (collectionProperty != null && collectionProperty.isArray && elementIndex >= 0 && elementIndex < collectionProperty.arraySize)
			{
				m_TableNav.ResetTextFieldEditing();
				var size = collectionProperty.arraySize;
				collectionProperty.DeleteArrayElementAtIndex(elementIndex);
				// Object reference elements need a second delete because the first only nulls the reference.
				if (collectionProperty.arraySize == size)
				{
					collectionProperty.DeleteArrayElementAtIndex(elementIndex);
				}
				serializedObject.ApplyModifiedProperties();
				// The element count changed, so the cached Collection Table rows must be rebuilt.
				m_SearchResultsDirty = true;
			}
		}

		// Draws a single Collection View row: the owner (read-only), the element index (read-only), and one cell per element
		// field. Add rows render only the owner and a button that appends an element to that owner's collection. Returns true
		// when the element count changed (add or remove) so the caller can rebuild the table for the next frame.
		private bool DrawCollectionRow(int rowIndex, Rect rowRect, SerializedObject serializedObject, Object owner, int elementIndex, bool isAddRow,
			int startingColumnIndex, Vector2 scrollEnd, ref bool lastVisibleColumn, ref bool lastVisibleRow)
		{
			var isScriptableObject = IsScriptableObject();
			var assetPreview = m_Settings.UserInterface.AssetPreview;

			// Actions column.
			var columnIndex = 0;
			var isActionsDocked = m_MultiColumnHeader.DockedColumns.Contains(columnIndex);
			if ((columnIndex >= startingColumnIndex || isActionsDocked) && m_MultiColumnHeader.IsColumnVisible(columnIndex))
			{
				var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
				var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
				columnRect.y = rowRect.y;
				var actionRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
				if (m_Settings.UserInterface.RowLineHeight > 1)
				{
					actionRect.height = EditorGUIUtility.singleLineHeight * SheetLayout.MaxActionRectLineHeight;
				}
				if (m_Settings.UserInterface.ShowRowIndex)
				{
					var rowIndexLabel = SheetsContent.Label.GetRowIndex(rowIndex, m_Settings.UserInterface.RowIndexFormat);
					EditorGUI.LabelField(actionRect, rowIndexLabel);
					actionRect.x += SheetLayout.PropertyWidthSmall / 2;
				}
				var selectRect = new Rect(actionRect.x, actionRect.y, SheetLayout.InlineButtonWidth, actionRect.height);
				if (GUI.Button(selectRect, SheetsContent.Button.Select))
				{
					EditorGUIUtility.PingObject(owner);
					Selection.activeObject = owner;
				}
			}

			// Owner column (read-only).
			columnIndex = 1;
			var isOwnerDocked = m_MultiColumnHeader.DockedColumns.Contains(columnIndex);
			if ((columnIndex >= startingColumnIndex || isOwnerDocked) && m_MultiColumnHeader.IsColumnVisible(columnIndex))
			{
				var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
				var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
				columnRect.y = rowRect.y;
				if (CanDrawCellRect(columnRect))
				{
					var cellRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
					// Match the array size +/- button width and stack the minus under the plus when the row spans multiple lines.
					const float buttonWidth = 20f;
					var lineHeight = EditorGUIUtility.singleLineHeight;
					var isTall = cellRect.height > lineHeight;
					if (isTall)
					{
						DrawUtility.TableAssetPreview(owner, cellRect, assetPreview);
					}
					var nameRect = new Rect(cellRect.x, cellRect.y, cellRect.width, lineHeight);
					Rect addRect;
					var removeRect = Rect.zero;
					if (isAddRow)
					{
						addRect = new Rect(cellRect.xMax - buttonWidth, cellRect.y, buttonWidth, lineHeight);
						nameRect.width = Mathf.Max(0, cellRect.width - buttonWidth);
					}
					else if (isTall)
					{
						addRect = new Rect(cellRect.xMax - buttonWidth, cellRect.y, buttonWidth, lineHeight);
						removeRect = new Rect(addRect.x, cellRect.y + lineHeight, buttonWidth, lineHeight);
						nameRect.width = Mathf.Max(0, cellRect.width - buttonWidth);
					}
					else
					{
						addRect = new Rect(cellRect.xMax - buttonWidth * 2, cellRect.y, buttonWidth, lineHeight);
						removeRect = new Rect(addRect.xMax, cellRect.y, buttonWidth, lineHeight);
						nameRect.width = Mathf.Max(0, cellRect.width - buttonWidth * 2);
					}
					var controlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
					EditorGUI.BeginDisabledGroup(true);
					EditorGUI.TextField(nameRect, owner != null ? owner.name : SerializedTableProperty.NullObjectValue);
					EditorGUI.EndDisabledGroup();
					m_PropertyTable.Set(rowIndex, visibleColumnIndex, new CollectionLabelTableProperty(owner, owner != null ? owner.name : string.Empty, "Group Name", controlName));
						HandleReadOnlyCellClick(nameRect, controlName);
					if (GUI.Button(addRect, SheetsContent.Button.AddElement))
					{
						// Duplicate the clicked element so its value lands in the new next index; add the first element for empty owners.
						InsertCollectionElement(serializedObject, isAddRow ? 0 : elementIndex);
						return true;
					}
					if (!isAddRow && GUI.Button(removeRect, SheetsContent.Button.RemoveElement))
					{
						RemoveCollectionElement(serializedObject, elementIndex);
						return true;
					}
				}
			}

			// Index column (read-only).
			columnIndex = 2;
			if (columnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(columnIndex))
			{
				var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
				var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
				columnRect.y = rowRect.y;
				if (CanDrawCellRect(columnRect))
				{
					var indexRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
					indexRect.height = EditorGUIUtility.singleLineHeight;
					var controlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
					var indexValue = isAddRow ? string.Empty : elementIndex.ToString();
					EditorGUI.BeginDisabledGroup(true);
					EditorGUI.TextField(indexRect, indexValue);
					EditorGUI.EndDisabledGroup();
					m_PropertyTable.Set(rowIndex, visibleColumnIndex, new CollectionLabelTableProperty(owner, indexValue, "Index", controlName));
						HandleReadOnlyCellClick(indexRect, controlName);
				}
			}

			if (isAddRow)
			{
				return false;
			}

			// Element field columns, matched by their element relative property path.
			var elementPath = $"{m_SelectedCollectionPath}.Array.data[{elementIndex}]";
			var element = serializedObject.FindProperty(elementPath);
			if (element == null)
			{
				return false;
			}
			// Serialize reference elements render a subtype dropdown in the Value column, then fall through to draw the
			// selected subtype's fields in the union columns below.
			var isManagedReferenceElement = m_Settings.UserInterface.ManagedReferences && element.IsManagedReference();
			if (isManagedReferenceElement)
			{
				if (m_MultiColumnTooltipPaths.TryGetValue(ColumnUtility.CollectionValueTooltip, out int typeColumnIndex)
					&& typeColumnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(typeColumnIndex))
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(typeColumnIndex);
					var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
					columnRect.y = rowRect.y;
					var propertyRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
					if (CanDrawCellRect(columnRect))
					{
						var propertyControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
						if (DrawManagedReferenceDropdown(propertyRect, element, owner))
						{
							// The subtype changed which alters the available columns, so refresh and exit this frame to avoid using a stale iterator.
							ForceRefreshColumnLayout();
							return true;
						}
						m_PropertyTable.Set(rowIndex, visibleColumnIndex, new ManagedReferenceTableProperty(owner, elementPath, propertyControlName));
					}
				}
			}
			// Leaf element collections render the element value itself in the single Value column.
			else if (!element.hasVisibleChildren)
			{
				if (m_MultiColumnTooltipPaths.TryGetValue(ColumnUtility.CollectionValueTooltip, out int valueColumnIndex)
					&& valueColumnIndex >= startingColumnIndex && m_MultiColumnHeader.IsColumnVisible(valueColumnIndex))
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(valueColumnIndex);
					var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
					columnRect.y = rowRect.y;
					var propertyRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
					if (CanDrawCellRect(columnRect))
					{
						var propertyControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
						var isCustomField = isScriptableObject || serializedObject.targetObject is Component;
						HandleObjectReferenceCellDoubleClick(element, propertyRect);
						Profiler.BeginSample("DrawProperty");
						EditorGUI.BeginDisabledGroup(!element.editable);
						element.DrawProperty(propertyRect, owner, isCustomField, out _, m_Settings.UserInterface.ShowReadOnly, assetPreview, m_NewAssetPath, OnObjectCreated, m_TableNav.ResetTextFieldEditing, null, propertyControlName);
						EditorGUI.EndDisabledGroup();
						Profiler.EndSample();
						m_PropertyTable.Set(rowIndex, visibleColumnIndex, new SerializedTableProperty(owner, elementPath, propertyControlName));
						HandleReadOnlyCellClick(propertyRect, propertyControlName);
					}
				}
				return false;
			}
			var elementPrefix = $"{elementPath}.";
			var child = element.Copy();
			var end = element.GetEndProperty();
			var enterChildren = true;
			var iterations = 0;
			var renderingOverrides = m_Settings.Experimental.GetRenderingOverrides();
			var showReadOnly = m_Settings.UserInterface.ShowReadOnly;
			while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end) && iterations++ < m_Settings.Workload.MaxIterations)
			{
				enterChildren = m_Settings.UserInterface.ShowChildren;
				if (!child.propertyPath.StartsWith(elementPrefix))
				{
					break;
				}
				var relativePath = child.propertyPath.Substring(elementPrefix.Length);
				if (!m_MultiColumnTooltipPaths.TryGetValue(relativePath, out int nextColumnIndex))
				{
					continue;
				}
				var useAssetReferenceDrawer = !showReadOnly && child.IsAssetReference();
				if (nextColumnIndex >= startingColumnIndex && (child.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, showReadOnly, out bool isReadOnlyUnityField) || useAssetReferenceDrawer))
				{
					if (m_MultiColumnHeader.IsColumnVisible(nextColumnIndex))
					{
						var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(nextColumnIndex);
						var columnRect = m_MultiColumnHeader.GetColumnRect(visibleColumnIndex);
						columnRect.y = rowRect.y;
						var propertyRect = m_MultiColumnHeader.GetCellRect(visibleColumnIndex, columnRect);
						if (CanDrawCellRect(columnRect))
						{
							var propertyControlName = m_TableNav.SetNextControlName(m_PropertyTable, rowIndex, visibleColumnIndex);
							var isCustomField = (isScriptableObject || serializedObject.targetObject is Component) && !isReadOnlyUnityField;
							var assetReferenceClicked = IsAssetReferenceCellClicked(useAssetReferenceDrawer, propertyRect);
							Profiler.BeginSample("DrawProperty");
							HandleObjectReferenceCellDoubleClick(child, propertyRect);
							EditorGUI.BeginDisabledGroup(!child.editable || isReadOnlyUnityField);
							if (renderingOverrides.Contains(child.propertyType))
							{
								propertyRect.height = EditorGUI.GetPropertyHeight(child, false);
								EditorGUI.PropertyField(propertyRect, child, GUIContent.none, false);
							}
							else
							{
								child.DrawProperty(propertyRect, owner, isCustomField, out bool arraySizeChanged, showReadOnly, assetPreview, m_NewAssetPath, OnObjectCreated, m_TableNav.ResetTextFieldEditing, null, propertyControlName);
								if (m_Settings.UserInterface.ShowArrays && arraySizeChanged)
								{
									// A nested element array size changed which alters the element columns, so refresh the layout.
									m_WaitingForColumnRefresh = true;
								}
							}
							EditorGUI.EndDisabledGroup();
							Profiler.EndSample();
							m_PropertyTable.Set(rowIndex, visibleColumnIndex, new SerializedTableProperty(owner, child.propertyPath, propertyControlName));
							if (assetReferenceClicked)
							{
								m_TableNav.RequestFocus(propertyControlName);
								Repaint();
							}
							HandleReadOnlyCellClick(propertyRect, propertyControlName);
						}
						if (m_Settings.Workload.Virtualization)
						{
							if (propertyRect.x >= scrollEnd.x)
							{
								lastVisibleColumn = true;
							}
							if (propertyRect.y >= scrollEnd.y)
							{
								lastVisibleRow = true;
							}
							if (lastVisibleColumn && m_TableAction == TableAction.None)
							{
								break;
							}
						}
					}
				}
			}
			return false;
		}

		// Adds a type dropdown column for a serialize reference field followed by the union of every concrete subtype's
		// fields found across the filtered Objects. Each child column is keyed by its field path so a row only renders the 
		// fields belonging to its own concrete subtype.
		private void AddManagedReferenceColumns(SerializedProperty managedReferenceProperty, bool isScriptableObject, List<MultiColumnHeaderState.Column> columns, List<Object> filteredObjects)
		{
			var referenceColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
			columns.Add(ColumnUtility.CreatePropertyColumn(managedReferenceProperty, isScriptableObject, m_Settings.UserInterface.HeaderFormat, referenceColumnLabel));

			var referencePath = managedReferenceProperty.propertyPath;
			var childPathPrefix = $"{referencePath}.";
			var disabledTypes = GetDisabledManagedTypes(referencePath);
			var seenChildPaths = new HashSet<string>();
			for (var i = 0; i < filteredObjects.Count && i < m_Settings.Workload.MaxIterations; i++)
			{
				var filteredObject = filteredObjects[i];
				if (filteredObject == null)
				{
					continue;
				}
				var referenceProperty = new SerializedObject(filteredObject).FindProperty(referencePath);
				if (referenceProperty == null || !referenceProperty.IsManagedReference())
				{
					continue;
				}
				// Skip subtypes toggled off for this column so their fields don't contribute columns.
				if (disabledTypes != null && disabledTypes.Contains(referenceProperty.GetManagedReferenceTypeName()))
				{
					continue;
				}
				var child = referenceProperty.Copy();
				var end = referenceProperty.GetEndProperty();
				var enterChildren = true;
				while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end) && child.propertyPath.StartsWith(childPathPrefix))
				{
					enterChildren = m_Settings.UserInterface.ShowChildren;
					if (!seenChildPaths.Add(child.propertyPath))
					{
						continue;
					}
					if (child.IsPropertyVisible(m_Settings.UserInterface.ShowArrays, m_Settings.UserInterface.ShowReadOnly, out _))
					{
						var childColumnLabel = ColumnUtility.GetColumnIndexLabel(m_Settings.UserInterface.ShowColumnIndex, columns.Count, m_Settings.UserInterface.ColumnIndexFormat);
						columns.Add(ColumnUtility.CreatePropertyColumn(child, isScriptableObject, m_Settings.UserInterface.HeaderFormat, childColumnLabel));
					}
				}
			}
		}

		// Seeds an empty cell for every visible union child column of a managed reference so an import can populate fields that
		// belong to a row's newly assigned subtype. Real cells drawn later in the row overwrite these for fields that exist.
		private void RegisterManagedReferenceChildPlaceholders(int rowIndex, string referencePath, Object rootObject)
		{
			var childPrefix = $"{referencePath}.";
			for (var columnIndex = 0; columnIndex < m_ColumnPropertyPaths.Length; columnIndex++)
			{
				var columnPath = m_ColumnPropertyPaths[columnIndex];
				if (columnPath != null && columnPath.StartsWith(childPrefix) && m_MultiColumnHeader.IsColumnVisible(columnIndex))
				{
					var visibleColumnIndex = m_MultiColumnHeader.GetVisibleColumnIndex(columnIndex);
					m_PropertyTable.Set(rowIndex, visibleColumnIndex, new SerializedTableProperty(rootObject, columnPath, string.Empty));
				}
			}
		}

		// Draws the concrete subtype dropdown for a serialize reference cell. Returns true when the subtype was changed.
		// The dropdown is read-only on Unity versions before 2021.2 where the managed reference value cannot be set.
		private bool DrawManagedReferenceDropdown(Rect propertyRect, SerializedProperty property, Object rootObject)
		{
			propertyRect.height = EditorGUIUtility.singleLineHeight;
			var baseType = property.GetManagedReferenceFieldType(rootObject);
			var concreteTypes = SerializedPropertyUtility.GetConcreteManagedReferenceTypes(baseType);
			var options = new string[concreteTypes.Length + 1];
			options[0] = ManagedReferenceTableProperty.NullTypeValue;
			for (var i = 0; i < concreteTypes.Length; i++)
			{
				options[i + 1] = concreteTypes[i].Name;
			}
			var currentName = property.GetManagedReferenceTypeName();
			var currentIndex = 0;
			if (!string.IsNullOrEmpty(currentName))
			{
				var matchIndex = Array.FindIndex(concreteTypes, t => t.Name == currentName);
				currentIndex = matchIndex >= 0 ? matchIndex + 1 : 0;
			}
			EditorGUI.BeginDisabledGroup(!SerializedPropertyUtility.CanSetManagedReferenceValue);
			var newIndex = EditorGUI.Popup(propertyRect, currentIndex, options);
			EditorGUI.EndDisabledGroup();
			if (newIndex != currentIndex && SerializedPropertyUtility.CanSetManagedReferenceValue)
			{
				m_TableNav.ResetTextFieldEditing();
				property.SetManagedReferenceValue(newIndex == 0 ? null : concreteTypes[newIndex - 1]);
				property.serializedObject.ApplyModifiedProperties();
				return true;
			}
			return false;
		}

		private SerializedProperty GetNameProperty(SerializedObject obj)
		{
			var nameProperty = obj.FindProperty(UnityConstants.Field.Name);
			// For Components attached to a Prefab we need to get the Prefab GameObject before finding the property.
			if (nameProperty == null && obj.targetObject is Component)
			{
				var targetComponent = (Component) obj.targetObject;
				var parentSerializedObject = GetRowSerializedObject(targetComponent.gameObject);
				nameProperty = parentSerializedObject.FindProperty(UnityConstants.Field.Name);
			}
			return nameProperty;
		}

		private void CopyColumnHeaderColumn(object columnData)
		{
			m_VisibleColumnCopyIndex = m_MultiColumnHeader.GetVisibleColumnIndex((int) columnData);
			SetTableAction(TableAction.CopyColumn);
		}

		private void SetPropertySearchFilter(object columnData)
		{
			var columnPropertyPath = m_MultiColumnHeader.state.columns[(int) columnData].headerContent.tooltip;

			string newSearchInput;
			if (columnPropertyPath == ColumnUtility.AssetPathTooltip)
			{
				newSearchInput = "ap:";
			}
			else if (columnPropertyPath == ColumnUtility.GuidColumnTooltip)
			{
				newSearchInput = "g:";
			}
			else if (columnPropertyPath == ColumnUtility.GroupNameTooltip || columnPropertyPath == ColumnUtility.CollectionIndexTooltip || columnPropertyPath == ColumnUtility.CollectionValueTooltip)
			{
				// Collection Table meta columns have no property path, so filter them by their column reference instead.
				newSearchInput = $"p:{ColumnUtility.ColumnReferencePrefix}{ColumnUtility.GetAlphaIndexLabel((int) columnData)}";
			}
			else
			{
				newSearchInput = $"p:{columnPropertyPath}";
			}

			if (m_SearchInput != newSearchInput)
			{
				RecordColumnLayoutUndo("Filter by Property");
				m_TableNav.ResetTextFieldEditing();
				m_SearchInput = newSearchInput;
				FlushColumnLayoutUndo();
			}
		}

		private bool CanDrawCellRect(Rect cellRect)
		{
			// When columns are docked the width of columns can go to zero.
			// If the user performs an action like copy/paste we should draw the cell for that frame.
			return cellRect.width > 0 || m_TableAction != TableAction.None;
		}

		// True when a left mouse press lands on an AssetReference cell. Check before drawing it, since the drawer consumes the event.
		private static bool IsAssetReferenceCellClicked(bool useAssetReferenceDrawer, Rect propertyRect)
		{
			var e = Event.current;
			return useAssetReferenceDrawer && e.type == EventType.MouseDown && e.button == 0 && propertyRect.Contains(e.mousePosition);
		}

		// Selects read-only cells on click. Disabled controls don't consume the press, so an unconsumed click here means a
		// read-only cell was clicked. Editable cells and dropdowns consume it first, making this a no-op for them. Call after the cell draws.
		private void HandleReadOnlyCellClick(Rect cellRect, string controlName)
		{
			var e = Event.current;
			if (e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(e.mousePosition))
			{
				m_TableNav.RequestFocus(controlName);
				Repaint();
				e.Use();
			}
		}

		// Opens the referenced Object in this window when its cell is double-clicked, mirroring the Open in Scriptable Sheets
		// context menu. Handles both direct Object references and Addressable Asset References. Call before drawing the cell so
		// the press is consumed before the field's own double-click handling runs. The open is deferred since OpenAsset rescans
		// and must not mutate scan state mid draw.
		private void HandleObjectReferenceCellDoubleClick(SerializedProperty property, Rect cellRect)
		{
			var e = Event.current;
			if (e.type != EventType.MouseDown || e.button != 0 || e.clickCount != 2 || !cellRect.Contains(e.mousePosition))
			{
				return;
			}
			Object referencedObject;
			if (property.propertyType == SerializedPropertyType.ObjectReference)
			{
				referencedObject = property.objectReferenceValue;
			}
			else if (property.IsAssetReference())
			{
				referencedObject = property.GetAssetReferenceObject();
			}
			else
			{
				return;
			}
			if (referencedObject == null || !SheetAssetExtensions.TryGetSheetAsset(referencedObject, out _))
			{
				return;
			}
			e.Use();
			EditorApplication.delayCall += () => OpenAsset(referencedObject);
			Repaint();
		}

		private void SetTableAction(TableAction TableAction)
		{
			if (!m_Settings.DataTransfer.VisibleColumnsOnly)
			{
				// Temporarily restore all columns and cache current visible settings.
				m_CachedVisibleColumns = m_MultiColumnHeaderState.visibleColumns;
				SetVisibleColumns(Enumerable.Range(0, m_Columns.Length).ToArray());
			}
			m_TableAction = TableAction;
		}

		private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
		{
			m_SortingChanged = true;
			GUIUtility.keyboardControl = 0;
			var tableLayout = GetTableLayout();
			tableLayout.SortedColumnIndex = m_MultiColumnHeaderState.sortedColumnIndex;
			tableLayout.IsSortedAscending = m_MultiColumnHeader.IsSortedAscending(m_MultiColumnHeaderState.sortedColumnIndex);
		}

		private void OnColumnSettingsChanged(int column)
		{
			CacheColumnLayout();
		}

		private void OnVisibleColumnsChanged(MultiColumnHeader multiColumnHeader)
		{
			CacheColumnLayout();
			FlushColumnLayoutUndo();
		}

		private void OnDockedColumnsChanged(MultiColumnHeader multiColumnHeader)
		{
			CacheColumnLayout();
			FlushColumnLayoutUndo();
		}

		private void OnHeaderColorsChanged(MultiColumnHeader multiColumnHeader)
		{
			CacheColumnLayout();
			FlushColumnLayoutUndo();
			// The color picker is a separate window, so repaint to reflect changes in realtime.
			Repaint();
		}

		private void OnManagedTypeFiltersChanged(MultiColumnHeader multiColumnHeader)
		{
			CacheManagedTypeFilters();
			FlushColumnLayoutUndo();
			// Disabling a subtype removes its columns so the layout must be rebuilt.
			ForceRefreshColumnLayout();
			Repaint();
		}

		// Persists the per column preview source chains so they survive the column layout changing. The mapping only affects
		// which preview is drawn, so no column rebuild is required.
		private void OnAssetPreviewSourceChanged(MultiColumnHeader multiColumnHeader)
		{
			CacheAssetPreviewSources();
			FlushColumnLayoutUndo();
			Repaint();
		}

		// Captures the current column layouts and search filter for native Undo. Called right before a context menu action
		// mutates them while the live state still reflects the pre change values.
		private void RecordColumnLayoutUndo(string actionName)
		{
			m_ColumnLayoutUndoState = SerializeColumnLayoutState();
			m_LastAppliedColumnLayoutUndoState = m_ColumnLayoutUndoState;
			Undo.IncrementCurrentGroup();
			Undo.RegisterCompleteObjectUndo(this, actionName);
			Undo.SetCurrentGroupName(actionName);
		}

		// Writes the post change layout state into the serialized field so a redo restores it.
		private void FlushColumnLayoutUndo()
		{
			m_ColumnLayoutUndoState = SerializeColumnLayoutState();
			m_LastAppliedColumnLayoutUndoState = m_ColumnLayoutUndoState;
		}

		private string SerializeColumnLayoutState()
		{
			var state = new ColumnLayoutUndoState
			{
				TableLayouts = m_TableLayouts,
				SearchInput = m_SearchInput,
			};
			return JsonConvert.SerializeObject(state);
		}

		// Restores the column layouts and search filter from the serialized undo state, then rebuilds the columns so the
		// docked, hidden, header color, managed type, and asset preview changes are reapplied from the restored layout.
		private void ApplyColumnLayoutUndoState()
		{
			m_LastAppliedColumnLayoutUndoState = m_ColumnLayoutUndoState;
			if (string.IsNullOrEmpty(m_ColumnLayoutUndoState))
			{
				return;
			}
			try
			{
				var state = JsonConvert.DeserializeObject<ColumnLayoutUndoState>(m_ColumnLayoutUndoState);
				if (state == null)
				{
					return;
				}
				m_TableLayouts = state.TableLayouts ?? new Dictionary<string, TableLayout>();
				m_SearchInput = state.SearchInput;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to restore the column layout from undo. {ex.Message}");
				return;
			}
			ForceRefreshColumnLayout();
		}

		// The undoable slice of the window session state. Search filter is included so Filter by property participates in undo.
		private class ColumnLayoutUndoState
		{
			public Dictionary<string, TableLayout> TableLayouts { get; set; }
			public string SearchInput { get; set; }
		}

		private void CacheAssetPreviewSources()
		{
			var tableLayout = GetTableLayout();
			var sources = new Dictionary<string, string[]>();
			foreach (var columnChain in m_MultiColumnHeader.AssetPreviewChains)
			{
				if (columnChain.Value == null || columnChain.Value.Length <= 0 || columnChain.Key < 0 || columnChain.Key >= m_ColumnPropertyPaths.Length)
				{
					continue;
				}
				var columnPath = m_ColumnPropertyPaths[columnChain.Key];
				if (!string.IsNullOrEmpty(columnPath))
				{
					sources[columnPath] = columnChain.Value;
				}
			}
			tableLayout.AssetPreviewSources = sources;
		}

		private void RestoreAssetPreviewSources(TableLayout tableLayout)
		{
			var chains = new Dictionary<int, string[]>();
			if (tableLayout.AssetPreviewSources != null && tableLayout.AssetPreviewSources.Count > 0)
			{
				// Match by column property path since the column count and order can change.
				for (var i = 0; i < m_MultiColumnHeaderState.columns.Length; i++)
				{
					var columnPath = m_MultiColumnHeaderState.columns[i].headerContent.tooltip;
					if (!string.IsNullOrEmpty(columnPath) && tableLayout.AssetPreviewSources.TryGetValue(columnPath, out var chain) && chain != null && chain.Length > 0)
					{
						chains[i] = chain;
					}
				}
			}
			m_MultiColumnHeader.AssetPreviewChains = chains;
		}

		// Builds the flattened Map Asset Preview menu entries from the representative Object, recursing through child
		// ScriptableObject references up to the configured depth. Shared by every column since chains resolve from the row Object.
		private List<SheetsMultiColumnHeader.AssetPreviewEntry> BuildAssetPreviewEntries()
		{
			var entries = new List<SheetsMultiColumnHeader.AssetPreviewEntry>
			{
				// Default clears any mapping so the column uses its own preview.
				new SheetsMultiColumnHeader.AssetPreviewEntry("Default", new string[0])
			};
			var maxDepth = Mathf.Clamp(m_Settings.Workload.AssetPreviewDepth, 1, 10);
			// Temp instances let us enumerate a type's own fields without loading any assigned asset.
			var tempInstances = new List<Object>();
			try
			{
				// Enumerate a fresh instance of the selected type so the sources match the columns even when those were built from
				// a temporary instance that has since been destroyed. Fall back to a live Object for non ScriptableObject types.
				Object root = null;
				if (IsScriptableObject() && m_SelectedType != null)
				{
					ScriptableObject tempRoot = null;
					try
					{
						tempRoot = CreateInstance(m_SelectedType);
					}
					catch
					{
						tempRoot = null;
					}
					if (tempRoot != null)
					{
						tempInstances.Add(tempRoot);
						root = tempRoot;
					}
				}
				if (root == null)
				{
					root = m_AssetPreviewRepresentative;
				}
				if (root == null)
				{
					return entries;
				}
				AppendAssetPreviewEntries(entries, root.GetType(), new SerializedObject(root), new List<string>(), string.Empty, maxDepth, new HashSet<Type>(), tempInstances);
			}
			finally
			{
				foreach (var tempInstance in tempInstances)
				{
					if (tempInstance != null)
					{
						DestroyImmediate(tempInstance);
					}
				}
			}
			return entries;
		}

		private void AppendAssetPreviewEntries(List<SheetsMultiColumnHeader.AssetPreviewEntry> entries, Type ownerType, SerializedObject ownerSerializedObject,
			List<string> chainPrefix, string menuPrefix, int remainingDepth, HashSet<Type> visitedTypes, List<Object> tempInstances)
		{
			var iterator = ownerSerializedObject.GetIterator();
			var enterChildren = true;
			var iterations = 0;
			while (iterator.NextVisible(enterChildren) && iterations++ < m_Settings.Workload.MaxIterations)
			{
				// Only consider top level fields so the mapped path stays stable across the column layout.
				enterChildren = false;
				if (iterator.propertyPath == UnityConstants.Field.Name)
				{
					continue;
				}
				// Skip read-only fields like the Script reference when their column is hidden so it isn't offered as a source.
				if (!m_Settings.UserInterface.ShowReadOnly && iterator.IsReadOnly())
				{
					continue;
				}
				var isObjectReference = iterator.propertyType == SerializedPropertyType.ObjectReference;
				if (!isObjectReference && !iterator.IsAssetReference())
				{
					continue;
				}
				var chain = new List<string>(chainPrefix) { iterator.propertyPath };
				var menuPath = $"{menuPrefix}{iterator.displayName}";
				// Native Unity serialized references such as a Material's m_Shader or the m_Script reference aren't reflectable C#
				// fields, so a null field type is expected here and simply means the reference can't expand into a child ScriptableObject.
				var fieldType = isObjectReference ? ReflectionUtility.GetNestedFieldType(ownerType, iterator.propertyPath, false) : null;
				var canExpand = fieldType != null && typeof(ScriptableObject).IsAssignableFrom(fieldType) && remainingDepth > 1 && !visitedTypes.Contains(fieldType);
				ScriptableObject childInstance = null;
				if (canExpand)
				{
					try
					{
						childInstance = ScriptableObject.CreateInstance(fieldType);
					}
					catch
					{
						childInstance = null;
					}
				}
				if (childInstance != null)
				{
					tempInstances.Add(childInstance);
					// Default at this level shows the referenced ScriptableObject's own preview.
					entries.Add(new SheetsMultiColumnHeader.AssetPreviewEntry($"{menuPath}/Default", chain.ToArray()));
					visitedTypes.Add(fieldType);
					AppendAssetPreviewEntries(entries, fieldType, new SerializedObject(childInstance), chain, $"{menuPath}/", remainingDepth - 1, visitedTypes, tempInstances);
					visitedTypes.Remove(fieldType);
				}
				else
				{
					entries.Add(new SheetsMultiColumnHeader.AssetPreviewEntry(menuPath, chain.ToArray()));
				}
			}
		}

		// Resolves the Object a chain of property hops points to, starting from the row Object. Returns null when any hop is unset.
		private Object ResolveAssetPreviewObject(SerializedObject rowSerializedObject, string[] chain)
		{
			if (chain == null || chain.Length <= 0)
			{
				return null;
			}
			var serializedObject = rowSerializedObject;
			Object resolved = null;
			for (var i = 0; i < chain.Length; i++)
			{
				var property = serializedObject.FindProperty(chain[i]);
				if (property == null)
				{
					return null;
				}
				Object next = null;
				if (property.propertyType == SerializedPropertyType.ObjectReference)
				{
					next = property.objectReferenceValue;
				}
				else if (property.IsAssetReference())
				{
					next = property.GetAssetReferenceObject();
				}
				if (next == null)
				{
					return null;
				}
				resolved = next;
				if (i < chain.Length - 1)
				{
					serializedObject = GetPreviewSerializedObject(next);
				}
			}
			return resolved;
		}

		// Caches SerializedObjects created while resolving multi hop chains so each referenced asset is wrapped once per frame.
		private SerializedObject GetPreviewSerializedObject(Object obj)
		{
			if (!m_PreviewSerializedObjectCache.TryGetValue(obj, out var serializedObject))
			{
				serializedObject = new SerializedObject(obj);
				m_PreviewSerializedObjectCache[obj] = serializedObject;
			}
			return serializedObject;
		}

		// Returns a SerializedObject for the given Object, reusing a cached instance across repaints rather than allocating one
		// every frame. A new instance is created only when missing or when the previous target was destroyed.
		private SerializedObject GetRowSerializedObject(Object obj)
		{
			if (!m_RowSerializedObjectCache.TryGetValue(obj, out var serializedObject) || serializedObject.targetObject == null)
			{
				serializedObject?.Dispose();
				serializedObject = new SerializedObject(obj);
				m_RowSerializedObjectCache[obj] = serializedObject;
			}
			return serializedObject;
		}

		// Returns the cached asset path for the given Object, resolving it via AssetDatabase on the first lookup.
		private string GetCachedAssetPath(Object obj)
		{
			if (!m_AssetPathCache.TryGetValue(obj, out var assetPath))
			{
				assetPath = AssetDatabase.GetAssetPath(obj);
				m_AssetPathCache[obj] = assetPath;
			}
			return assetPath;
		}

		private void RemoveRowSerializedObject(Object obj)
		{
			if (m_RowSerializedObjectCache.TryGetValue(obj, out var serializedObject))
			{
				serializedObject.Dispose();
				m_RowSerializedObjectCache.Remove(obj);
			}
			m_AssetPathCache.Remove(obj);
		}

		private void ClearRowSerializedObjectCache()
		{
			foreach (var serializedObject in m_RowSerializedObjectCache.Values)
			{
				serializedObject.Dispose();
			}
			m_RowSerializedObjectCache.Clear();
			m_AssetPathCache.Clear();
		}

		// Resolves the Object whose asset preview should appear in the Name column for the given row, falling back to the row Object.
		private Object GetNamePreviewObject(SerializedObject serializedObject, Object rootObject)
		{
			if (!m_MultiColumnHeader.AssetPreviewChains.TryGetValue(NameColumnIndex, out var chain))
			{
				return rootObject;
			}
			var resolved = ResolveAssetPreviewObject(serializedObject, chain);
			return resolved != null ? resolved : rootObject;
		}

		// Persists the toggled off managed reference subtypes keyed by the managed reference column's property path
		// so the filter survives the column layout changing.
		private void CacheManagedTypeFilters()
		{
			var tableLayout = GetTableLayout();
			var managedTypeFilters = new Dictionary<string, string[]>();
			foreach (var disabledTypes in m_MultiColumnHeader.DisabledManagedTypes)
			{
				if (disabledTypes.Value == null || disabledTypes.Value.Count <= 0 || disabledTypes.Key >= m_ColumnPropertyPaths.Length)
				{
					continue;
				}
				var columnPath = m_ColumnPropertyPaths[disabledTypes.Key];
				if (!string.IsNullOrEmpty(columnPath))
				{
					managedTypeFilters[columnPath] = disabledTypes.Value.ToArray();
				}
			}
			tableLayout.ManagedTypeFilters = managedTypeFilters;
		}

		private void RestoreManagedTypeFilters(TableLayout tableLayout)
		{
			var disabledManagedTypes = new Dictionary<int, HashSet<string>>();
			if (tableLayout.ManagedTypeFilters != null && tableLayout.ManagedTypeFilters.Count > 0)
			{
				// Match by property path because the column count changes as subtypes are toggled.
				for (var i = 0; i < m_MultiColumnHeaderState.columns.Length; i++)
				{
					var columnPath = m_MultiColumnHeaderState.columns[i].headerContent.tooltip;
					if (tableLayout.ManagedTypeFilters.TryGetValue(columnPath, out var disabledTypes) && disabledTypes != null && disabledTypes.Length > 0)
					{
						disabledManagedTypes[i] = new HashSet<string>(disabledTypes);
					}
				}
			}
			m_MultiColumnHeader.DisabledManagedTypes = disabledManagedTypes;
		}

		// Returns the managed reference subtypes toggled off for the given managed reference column path, or null when none.
		private HashSet<string> GetDisabledManagedTypes(string referencePath)
		{
			var tableLayout = GetTableLayout();
			if (tableLayout.ManagedTypeFilters != null && tableLayout.ManagedTypeFilters.TryGetValue(referencePath, out var disabledTypes) && disabledTypes != null && disabledTypes.Length > 0)
			{
				return new HashSet<string>(disabledTypes);
			}
			return null;
		}

		private void SetVisibleColumns(int[] visibleColumns)
		{
			// Avoid overwriting visible columns while a table action is in progress.
			if (m_TableAction != TableAction.None)
			{
				return;
			}
			m_MultiColumnHeaderState.visibleColumns = visibleColumns;
			CacheColumnLayout();
		}

		private void CacheColumnLayout()
		{
			var tableLayout = GetTableLayout();
			tableLayout.ColumnCount = m_MultiColumnHeaderState.columns.Length;
			tableLayout.ColumnWidths = m_MultiColumnHeaderState.columns.Select(c => c.width).ToArray();
			tableLayout.VisibleColumns = m_MultiColumnHeaderState.visibleColumns;
			tableLayout.DockedColumns = m_MultiColumnHeader.DockedColumns.ToArray();
			tableLayout.ColumnHeaderColors = Enumerable.Range(0, m_MultiColumnHeaderState.columns.Length)
				.Select(i => m_MultiColumnHeader.HeaderColors.TryGetValue(i, out var color) ? ColorUtility.ToHtmlStringRGB(color) : null)
				.ToArray();
		}

		private void TryRestoreTableLayout()
		{
			var tableLayoutName = m_SelectedType.FullName;
			if (!m_TableLayouts.TryGetValue(tableLayoutName, out TableLayout tableLayout))
			{
				m_PendingPageRestore = 1;
				m_MultiColumnHeader.ResizeToHeaderWidth(SheetLayout.InlineLabelSpacing);
				m_MultiColumnHeader.sortedColumnIndex = 1;
				return;
			}

			// Restore managed type filters first since they survive the column count changing as subtypes are toggled.
			RestoreManagedTypeFilters(tableLayout);

			// Defer applying the restored page until the paginator has the current totals to clamp against.
			m_PendingPageRestore = tableLayout.CurrentPage;
			m_MultiColumnHeaderState.sortedColumnIndex = tableLayout.SortedColumnIndex;
			if (tableLayout.SortedColumnIndex < m_MultiColumnHeaderState.columns.Length)
			{
				m_MultiColumnHeaderState.sortedColumnIndex = tableLayout.SortedColumnIndex;
			}
			else
			{
				m_MultiColumnHeaderState.sortedColumnIndex = 1;
			}
			m_SelectedMainAssetIndex = tableLayout.MainAssetIndex;
			if (m_SelectedMainAssetIndex <= 0)
			{
				m_MultiColumnHeader.SetSortDirection(m_MultiColumnHeaderState.sortedColumnIndex, tableLayout.IsSortedAscending);
			}
			else
			{
				// Delay the call when there's a main asset index selected so the UI has time to update.
				EditorApplication.delayCall += () => m_MultiColumnHeader.SetSortDirection(m_MultiColumnHeaderState.sortedColumnIndex, tableLayout.IsSortedAscending);
			}

			if (tableLayout.DockedColumns != null)
			{
				m_MultiColumnHeader.DockedColumns = new HashSet<int>(tableLayout.DockedColumns);
			}

			if (tableLayout.VisibleColumns == null || tableLayout.ColumnCount != m_MultiColumnHeaderState.columns.Length)
			{
				m_MultiColumnHeader.ResizeToHeaderWidth(SheetLayout.InlineLabelSpacing);
				return;
			}

			m_MultiColumnHeaderState.visibleColumns = tableLayout.VisibleColumns;

			if (tableLayout.ColumnWidths == null || tableLayout.ColumnCount != tableLayout.ColumnWidths.Length)
			{
				m_MultiColumnHeader.ResizeToHeaderWidth(SheetLayout.InlineLabelSpacing);
				return;
			}

			for (var i = 0; i < tableLayout.ColumnCount; i++)
			{
				m_MultiColumnHeaderState.columns[i].width = tableLayout.ColumnWidths[i];
			}

			RestoreHeaderColors(tableLayout);
		}

		private void RestoreHeaderColors(TableLayout tableLayout)
		{
			if (tableLayout.ColumnHeaderColors == null || tableLayout.ColumnHeaderColors.Length != m_MultiColumnHeaderState.columns.Length)
			{
				return;
			}

			var headerColors = new Dictionary<int, Color>();
			for (var i = 0; i < tableLayout.ColumnHeaderColors.Length; i++)
			{
				var hex = tableLayout.ColumnHeaderColors[i];
				if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString($"#{hex}", out var color))
				{
					headerColors[i] = color;
				}
			}
			m_MultiColumnHeader.HeaderColors = headerColors;
		}

		// Builds the alphanumeric label for the currently selected cell, or returns false when none is selected.
		private bool TryGetSelectedCellContent(out GUIContent content)
		{
			content = null;
			var coordinate = m_TableNav.VisualCoordinate;
			if (coordinate == Vector2Int.zero || m_PropertyTable == null || !m_PropertyTable.IsValidCoordinate(coordinate))
			{
				return false;
			}
			var visibleColumns = m_MultiColumnHeaderState.visibleColumns;
			if (coordinate.y < 0 || coordinate.y >= visibleColumns.Length)
			{
				return false;
			}
			var columnLabel = ColumnUtility.GetAlphaIndexLabel(visibleColumns[coordinate.y]);
			content = SheetsContent.Label.GetSelectedCellContent(columnLabel, coordinate.x + 1);
			return true;
		}

		// Ctrl/Cmd + Left/Right jumps to the previous/next page only while not editing a text field.
		private void HandlePageNavigationShortcuts()
		{
			var e = Event.current;
			if (e.type != EventType.KeyDown || !(e.control || e.command) || (EditorGUIUtility.editingTextField && !m_TableNav.HasFocus) || m_TableNav.IsEditingTextField
				|| m_Paginator.ObjectsPerPage <= 0 || m_Paginator.GetTotalPages() <= 1)
			{
				return;
			}
			if (e.keyCode == KeyCode.LeftArrow)
			{
				m_Paginator.PreviousPage();
				e.Use();
				Repaint();
			}
			else if (e.keyCode == KeyCode.RightArrow)
			{
				m_Paginator.NextPage();
				e.Use();
				Repaint();
			}
		}

		private TableLayout GetTableLayout()
		{
			var tableLayoutName = m_SelectedType.FullName;
			if (!m_TableLayouts.TryGetValue(tableLayoutName, out var tableLayout))
			{
				tableLayout = new TableLayout();
				m_TableLayouts.Add(tableLayoutName, tableLayout);
			}
			return tableLayout;
		}

		private async void DownloadGoogleSheetsDataAsync()
		{
			if (!m_GoogleSheetsImporter.IsValidSheetId())
			{
				Debug.LogWarning($"Invalid {nameof(GoogleSheetsImporter)} {m_GoogleSheetsImporter.name}. {m_GoogleSheetsImporter.GetInvalidSheetIdWarning()}");
				return;
			}
			if (!m_GoogleSheetsImporter.IsValidSheetName())
			{
				Debug.LogWarning($"Invalid {nameof(GoogleSheetsImporter)} {m_GoogleSheetsImporter.name}. {m_GoogleSheetsImporter.GetInvalidSheetNameWarning()}");
				return;
			}

			var selectedType = m_SelectedType;
			var mainAsset = m_MainAsset;
			var sheetName = m_GoogleSheetsImporter.SheetName;
			var downloadUrl = m_GoogleSheetsImporter.Url;
			// Cache the importer name and folder since changing type during the download resets the importer to null.
			var importerName = m_GoogleSheetsImporter.name;
			var importerFolder = m_GoogleSheetsImporter.Folder;
			m_IsGoogleSheetDownloading = true;
			m_ImportedFileContents = await m_GoogleSheetsImporter.GetCsvDataAsync();
			m_IsGoogleSheetDownloading = false;
			if (!string.IsNullOrEmpty(m_ImportedFileContents))
			{
				Debug.Log($"Successfully downloaded '{sheetName}' CSV data from '{downloadUrl}'. Using {nameof(GoogleSheetsImporter)} {importerName}.");
				if (selectedType == m_SelectedType)
				{
					if (mainAsset == m_MainAsset)
					{
						m_IsImportJson = false;
						m_Settings.DataTransfer.SetRowDelimiter("\n");
						m_Settings.DataTransfer.SetColumnDelimiter(",");
						m_Settings.DataTransfer.WrapOption = WrapOption.DoubleQuotes;
						m_Settings.DataTransfer.EscapeOption = EscapeOption.Repeat;
						m_PendingImportFolder = importerFolder;
						SetTableAction(TableAction.Import);
					}
					else
					{
						m_ImportedFileContents = string.Empty;
						var newMainAssetName = m_MainAsset == null ? "null" : m_MainAsset.name;
						Debug.LogWarning($"Selected main asset has changed. Expected {mainAsset.name} but was {newMainAssetName}. Google Sheets import will be ignored.");
					}
				}
				else
				{
					m_ImportedFileContents = string.Empty;
					Debug.LogWarning($"Selected type has changed. Expected {selectedType} but was {m_SelectedType}. Google Sheets import will be ignored.");
				}
			}
			else
			{
				Debug.LogWarning($"Imported content cannot be null or empty.");
			}
		}

		private FlatFileFormatSettings GetFlatFileFormatSettings()
		{
			var formatSettings = new FlatFileFormatSettings()
			{
				RowDelimiter = m_Settings.DataTransfer.GetRowDelimiter(),
				ColumnDelimiter = m_Settings.DataTransfer.GetColumnDelimiter(),
				// Skip Actions column.
				FirstColumnIndex = 1,
				RemoveEmptyRows = m_Settings.DataTransfer.RemoveEmptyRows,
				UseStringEnums = m_Settings.DataTransfer.UseStringEnums,
				IgnoreCase = m_Settings.DataTransfer.IgnoreCase,
				WrapOption = m_Settings.DataTransfer.WrapOption,
				EscapeOption = m_Settings.DataTransfer.EscapeOption,
				CustomEscapeSequence = m_Settings.DataTransfer.GetCustomEscapeSequence(),
				HeaderColumnIndex = m_Settings.UserInterface.ShowColumnIndex,
			};
			if (m_Settings.DataTransfer.Headers)
			{
				formatSettings.AlwaysStrip = m_Settings.DataTransfer.AlwaysStrip;
				formatSettings.ColumnHeaders = m_MultiColumnHeaderState.visibleColumns.Select
				(
					// Remove column index if it's in the header name.
					i => m_Settings.UserInterface.ShowColumnIndex ? ColumnUtility.RemoveColumnIndexLabel(m_Columns[i].headerContent.text) : m_Columns[i].headerContent.text
				).ToArray();
			}
			return formatSettings;
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
		{
			var alphanumComparer = new AlphanumComparer();
			var windowSessionStates = m_Settings.SaveAndGetWindowSessionStates().OrderBy(s => s.Title, alphanumComparer).ThenBy(s => s.InstanceId).ToArray();
			if (Instances.Count < windowSessionStates.Length)
			{
				menu.AddItem(SheetsContent.Window.ContextMenu.OpenRecentSheet, false, ShowWindow);
			}
			else
			{
				menu.AddDisabledItem(SheetsContent.Window.ContextMenu.OpenRecentSheet, false);
			}
			menu.AddItem(SheetsContent.Window.ContextMenu.NewSheet, false, NewSheet);
			menu.AddItem(SheetsContent.Window.ContextMenu.RenameSheet, false, ShowRenameSheetPopup);
			menu.AddSeparator(string.Empty);
			var titleCounts = windowSessionStates.GroupBy(s => s.Title).ToDictionary(g => g.Key, g => g.Count());
			foreach (var windowSessionState in windowSessionStates)
			{
				var isActiveInstance = Instances.Any(i => i.GetInstanceIdCompat() == windowSessionState.InstanceId);
				var friendlyName = titleCounts[windowSessionState.Title] > 1 ? $"{windowSessionState.Title}/{windowSessionState.InstanceId}" : windowSessionState.Title;
				// Cannot open or delete windows that are open in the Editor.
				if (isActiveInstance)
				{
					menu.AddDisabledItem(SheetsContent.Window.ContextMenu.GetOpenSheetContent(friendlyName), false);
					menu.AddDisabledItem(SheetsContent.Window.ContextMenu.GetDeleteSheetContent(friendlyName), false);
				}
				else
				{
					menu.AddItem(SheetsContent.Window.ContextMenu.GetOpenSheetContent(friendlyName), false, () => OpenSheet(windowSessionState));
					menu.AddItem(SheetsContent.Window.ContextMenu.GetDeleteSheetContent(friendlyName), false, () => DeleteSheet(windowSessionState));
				}
				menu.AddItem(SheetsContent.Window.ContextMenu.GetCloneSheetContent(friendlyName), false, () => CloneSheet(windowSessionState));
			}
			menu.AddSeparator(string.Empty);
			menu.AddItem(SheetsContent.Window.ContextMenu.BatchSelect, false, ShowBatchSelectPopup);
			menu.AddSeparator(string.Empty);
			menu.AddItem(SheetsContent.Window.ContextMenu.NewPastePad, false, PastePadEditorWindow.ShowWindow);
			menu.AddItem(SheetsContent.Window.ContextMenu.OpenSettings, false, ScriptableSheetsSettingsEditorWindow.ShowWindow);
			menu.AddSeparator(string.Empty);
			menu.AddItem(SheetsContent.Window.ContextMenu.EditColumnVisibility, false, ShowColumnVisibilityPopup);
			menu.AddSeparator(string.Empty);
			menu.AddItem(SheetsContent.Window.ContextMenu.Copy, false, () => SetTableAction(TableAction.Copy));
			menu.AddItem(SheetsContent.Window.ContextMenu.CopyJson, false, () => SetTableAction(TableAction.CopyJson));
		}

		private void NewSheet()
		{
			s_IsNewWindowSessionState = true;
			ShowWindow();
		}

		private void ShowRenameSheetPopup()
		{
			var anchoredRect = new Rect(position.position, PopupContent.Window.RenameSize);
			var renamePopupWindow = new InputPopupWindowContent(anchoredRect, PopupContent.Label.Rename, titleContent.text, OnRenameConfirmed);
			PopupWindow.Show(anchoredRect, renamePopupWindow);
		}

		private void OnRenameConfirmed(string input)
		{
			InitializeTitleContent(input);
			Repaint();
		}

		private void OpenSheet(WindowSessionState windowSessionState)
		{
			s_NextWindowSessionStateToLoad = windowSessionState;
			s_IsNextWindowSessionStateClone = false;
			ShowWindow();
		}

		private void CloneSheet(WindowSessionState windowSessionState)
		{
			s_NextWindowSessionStateToLoad = windowSessionState;
			s_IsNextWindowSessionStateClone = true;
			ShowWindow();
		}

		private void DeleteSheet(WindowSessionState windowSessionState)
		{
			m_Settings.DeleteWindowSessionState(windowSessionState);
		}

		private void ShowBatchSelectPopup()
		{
			var alphanumComparer = new AlphanumComparer();
			var windowSessionStates = m_Settings.SaveAndGetWindowSessionStates().OrderBy(s => s.Title, alphanumComparer).ThenBy(s => s.InstanceId).ToArray();
			var titleCounts = windowSessionStates.GroupBy(s => s.Title).ToDictionary(g => g.Key, g => g.Count());
			var sheetNames = new string[windowSessionStates.Length];
			var sheetOpen = new bool[windowSessionStates.Length];
			for (var i = 0; i < windowSessionStates.Length; i++)
			{
				var windowSessionState = windowSessionStates[i];
				sheetNames[i] = titleCounts[windowSessionState.Title] > 1 ? $"{windowSessionState.Title}/{windowSessionState.InstanceId}" : windowSessionState.Title;
				sheetOpen[i] = Instances.Any(instance => instance.GetInstanceIdCompat() == windowSessionState.InstanceId);
			}
			var anchoredRect = new Rect(position.position, PopupContent.Window.BatchSelectMaxSize);
			var batchSelectPopupWindow = new BatchSelectPopupWindowContent(anchoredRect, sheetNames, sheetOpen, selectedIndices => BatchOpenSheets(windowSessionStates, selectedIndices), selectedIndices => BatchDeleteSheets(windowSessionStates, selectedIndices));
			PopupWindow.Show(anchoredRect, batchSelectPopupWindow);
		}

		private void BatchOpenSheets(WindowSessionState[] windowSessionStates, int[] selectedIndices)
		{
			foreach (var index in selectedIndices)
			{
				OpenSheet(windowSessionStates[index]);
			}
		}

		private void BatchDeleteSheets(WindowSessionState[] windowSessionStates, int[] selectedIndices)
		{
			foreach (var index in selectedIndices)
			{
				DeleteSheet(windowSessionStates[index]);
			}
		}

		private void ShowColumnVisibilityPopup()
		{
			var anchoredRect = new Rect(position.position, PopupContent.Window.ColumnVisibilityMaxSize);
			var columnLayoutPopupWindow = new ColumnVisibilityPopupWindowContent(anchoredRect, m_MultiColumnHeaderState, m_Settings.Workload.VisibleColumnLimit, SetVisibleColumns);
			PopupWindow.Show(anchoredRect, columnLayoutPopupWindow);
		}

		// Special case when Objects are created from another Type other than the selected Type.
		private void OnObjectCreated(Object newObject, Type objectType)
		{
			if (!IsScriptableObject())
			{
				return;
			}
			if (!m_Scanner.ObjectsByType.ContainsKey(objectType))
			{
				ScanObjects();
			}
			else
			{
				m_Scanner.ObjectsByType[objectType].Add(newObject);
			}
		}
	}
}
