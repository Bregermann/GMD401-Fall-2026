using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.PastePad
{
	public class PastePadEditorWindow : EditorWindow, IHasCustomMenu
	{
		private const string TextAreaControlName = "PastePadTextArea";
		private const int DefaultTagSize = 24;
		private const float GutterMinWidth = 28f;
		private const float GutterRightPadding = 4f;

		// EditorGUILayout.TextArea is backed by an internal TextEditor that exposes the live selection. activeEditor points at the
		// editor of the control currently being edited, which is the instance the TextArea reads from across Unity versions. Older
		// versions also expose it as s_RecycledEditor, kept here as a fallback for when activeEditor isn't set.
		private static readonly FieldInfo s_ActiveEditorField = typeof(EditorGUI).GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly FieldInfo s_RecycledEditorField = typeof(EditorGUI).GetField("s_RecycledEditor", BindingFlags.Static | BindingFlags.NonPublic);

		private static TextEditor GetTextEditor()
		{
			return (s_ActiveEditorField?.GetValue(null) as TextEditor) ?? (s_RecycledEditorField?.GetValue(null) as TextEditor);
		}

		// Serialized so Unity's Undo system can record and restore text changes from the toolbar.
		[SerializeField]
		private string m_Text;
		public string Text => m_Text;

		private bool m_WordWrap;
		private bool m_ShowLineNumbers;
		private Vector2 m_ScrollPosition;

		private bool m_Clear;

		private Color m_TagColor = Color.white;
		private int m_TagSize = DefaultTagSize;

		private string m_FindText = string.Empty;
		private string m_ReplaceText = string.Empty;
		private bool m_CaseSensitive;

		// Cached text area dimensions used to align the line number gutter and measure wrapped lines.
		private float m_TextAreaWidth;
		private float m_TextAreaHeight;

		// Selection cached while the text area is focused so toolbar clicks can wrap it after focus is lost.
		private int m_CursorIndex;
		private int m_SelectIndex;
		private bool m_HasPendingSelection;
		private int m_PendingSelectStart;
		private int m_PendingSelectEnd;

		public static void ShowWindow()
		{
			var window = CreateInstance<PastePadEditorWindow>();
			window.titleContent = PastePadContent.Window.Title;
			window.minSize = new Vector2(500, 300);

			// Initialize from the global defaults. The user can still change these per window.
			var settings = PastePadSettings.instance;
			window.m_WordWrap = settings.WordWrap;
			window.m_ShowLineNumbers = settings.ShowLineNumbers;
			window.m_TagColor = settings.DefaultColor;
			window.m_TagSize = settings.DefaultSize;
			window.m_CaseSensitive = settings.CaseSensitive;

			window.Show();
		}

		private void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		private void OnUndoRedoPerformed()
		{
			// The restored text invalidates any cached selection, so drop it and redraw.
			m_HasPendingSelection = false;
			Repaint();
		}

		private void OnGUI()
		{
			if (m_Clear)
			{
				// Deselect the control OnGUI and then clear the text.
				GUI.FocusControl("null");
				Undo.RecordObject(this, "Clear Paste Pad Text");
				m_Text = string.Empty;
				m_Clear = false;
			}
			DrawToolbar();

			EditorGUILayout.BeginHorizontal();
			// Reserve the gutter space now but draw its content after the scroll view so it uses the current scroll position.
			var gutterRect = Rect.zero;
			if (m_ShowLineNumbers)
			{
				var gutterWidth = GetGutterWidth();
				gutterRect = GUILayoutUtility.GetRect(gutterWidth, 0f, GUILayout.Width(gutterWidth), GUILayout.ExpandHeight(true));
			}
			m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
			PastePadLayout.TextAreaStyle.wordWrap = m_WordWrap;
			GUI.SetNextControlName(TextAreaControlName);
			m_Text = EditorGUILayout.TextArea(m_Text, PastePadLayout.TextAreaStyle, PastePadLayout.TextAreaOptions);
			if (Event.current.type == EventType.Repaint)
			{
				var textAreaRect = GUILayoutUtility.GetLastRect();
				m_TextAreaWidth = textAreaRect.width;
				m_TextAreaHeight = textAreaRect.height;
			}
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndHorizontal();

			if (m_ShowLineNumbers && Event.current.type == EventType.Repaint)
			{
				DrawLineNumbers(gutterRect);
			}
			UpdateTextEditorState();
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button(PastePadContent.Toolbar.New, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions))
			{
				ShowWindow();
			}
			if (GUILayout.Button(PastePadContent.Toolbar.Copy, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions))
			{
				Copy();
			}
			if (GUILayout.Button(PastePadContent.Toolbar.Save, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions))
			{
				Save();
			}
			m_WordWrap = GUILayout.Toggle(m_WordWrap, PastePadContent.Toolbar.WordWrap, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions);
			m_ShowLineNumbers = GUILayout.Toggle(m_ShowLineNumbers, PastePadContent.Toolbar.LineNumbers, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions);
			if (GUILayout.Button(PastePadContent.Toolbar.Bold, PastePadLayout.ToolbarBoldButton, PastePadLayout.TagButtonOptions))
			{
				WrapSelection("<b>", "</b>");
			}
			if (GUILayout.Button(PastePadContent.Toolbar.Italic, PastePadLayout.ToolbarItalicButton, PastePadLayout.TagButtonOptions))
			{
				WrapSelection("<i>", "</i>");
			}
			m_TagColor = EditorGUILayout.ColorField(GUIContent.none, m_TagColor, false, false, false, PastePadLayout.ColorFieldOptions);
			if (GUILayout.Button(PastePadContent.Toolbar.Color, EditorStyles.toolbarButton, PastePadLayout.LabeledTagButtonOptions))
			{
				WrapSelection($"<color=#{ColorUtility.ToHtmlStringRGB(m_TagColor)}>", "</color>");
			}
			m_TagSize = Mathf.Max(1, EditorGUILayout.IntField(m_TagSize, PastePadLayout.SizeFieldOptions));
			if (GUILayout.Button(PastePadContent.Toolbar.Size, EditorStyles.toolbarButton, PastePadLayout.LabeledTagButtonOptions))
			{
				WrapSelection($"<size={m_TagSize}>", "</size>");
			}
			m_FindText = EditorGUILayout.TextField(m_FindText, EditorStyles.toolbarTextField, PastePadLayout.GetFlexibleFieldWidth(m_FindText));
			if (GUILayout.Button(PastePadContent.Toolbar.Find, EditorStyles.toolbarButton))
			{
				FindOccurrences();
			}
			m_ReplaceText = EditorGUILayout.TextField(m_ReplaceText, EditorStyles.toolbarTextField, PastePadLayout.GetFlexibleFieldWidth(m_ReplaceText));
			if (GUILayout.Button(PastePadContent.Toolbar.Replace, EditorStyles.toolbarButton))
			{
				ReplaceOccurrences();
			}
			m_CaseSensitive = GUILayout.Toggle(m_CaseSensitive, PastePadContent.Toolbar.CaseSensitive, EditorStyles.toolbarButton, PastePadLayout.CaseToggleOptions);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(PastePadContent.Toolbar.Clear, EditorStyles.toolbarButton, PastePadLayout.ToolbarIconButtonOptions))
			{
				Clear();
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawLineNumbers(Rect gutterRect)
		{
			EditorGUI.DrawRect(gutterRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
			var numbers = BuildLineNumberText();
			var contentHeight = Mathf.Max(m_TextAreaHeight, gutterRect.height);
			GUI.BeginGroup(gutterRect);
			var numbersRect = new Rect(0f, -m_ScrollPosition.y, gutterRect.width - GutterRightPadding, contentHeight);
			GUI.Label(numbersRect, numbers, PastePadLayout.LineNumberStyle);
			GUI.EndGroup();
		}

		private string BuildLineNumberText()
		{
			var lines = (m_Text ?? string.Empty).Split('\n');
			var innerWidth = m_TextAreaWidth - PastePadLayout.TextAreaStyle.padding.horizontal;
			var builder = new StringBuilder();
			for (var i = 0; i < lines.Length; i++)
			{
				if (i > 0)
				{
					builder.Append('\n');
				}
				builder.Append(i + 1);
				if (m_WordWrap && innerWidth > 1f)
				{
					// Pad wrapped lines with blank rows so the next number lines up with its logical line.
					var rows = GetVisualRowCount(lines[i], innerWidth);
					for (var r = 1; r < rows; r++)
					{
						builder.Append('\n');
					}
				}
			}
			return builder.ToString();
		}

		private static int GetVisualRowCount(string line, float width)
		{
			if (string.IsNullOrEmpty(line))
			{
				return 1;
			}
			var style = PastePadLayout.LineNumberMeasureStyle;
			var height = style.CalcHeight(new GUIContent(line), width);
			return Mathf.Max(1, Mathf.RoundToInt(height / style.lineHeight));
		}

		private float GetGutterWidth()
		{
			var lineCount = 1;
			if (!string.IsNullOrEmpty(m_Text))
			{
				foreach (var character in m_Text)
				{
					if (character == '\n')
					{
						lineCount++;
					}
				}
			}
			var widest = PastePadLayout.LineNumberStyle.CalcSize(new GUIContent(lineCount.ToString())).x;
			return Mathf.Max(GutterMinWidth, widest + GutterRightPadding + 6f);
		}

		private void FindOccurrences()
		{
			var count = FindReplaceUtility.CountOccurrences(m_Text, m_FindText, m_CaseSensitive);
			ShowNotification(new GUIContent($"{count} occurrence(s)"));
		}

		private void ReplaceOccurrences()
		{
			var count = FindReplaceUtility.CountOccurrences(m_Text, m_FindText, m_CaseSensitive);
			if (count == 0)
			{
				ShowNotification(new GUIContent("No occurrences found"));
				return;
			}
			// Deselect so the text area picks up the replaced text instead of the focused editor's stale copy.
			GUI.FocusControl("null");
			Undo.RecordObject(this, "Replace Paste Pad Text");
			m_Text = FindReplaceUtility.ReplaceOccurrences(m_Text, m_FindText, m_ReplaceText, m_CaseSensitive);
			ShowNotification(new GUIContent($"Replaced {count} occurrence(s)"));
			Repaint();
		}

		private void WrapSelection(string openTag, string closeTag)
		{
			Undo.RecordObject(this, "Format Paste Pad Text");
			// Keep the original text selected inside the new tags so formatting can be chained.
			m_Text = RichTextUtility.WrapText(m_Text, m_CursorIndex, m_SelectIndex, openTag, closeTag, out m_PendingSelectStart, out m_PendingSelectEnd);
			m_HasPendingSelection = true;
			GUI.FocusControl(TextAreaControlName);
			Repaint();
		}

		private void UpdateTextEditorState()
		{
			if (GUI.GetNameOfFocusedControl() != TextAreaControlName)
			{
				return;
			}
			var editor = GetTextEditor();
			if (editor == null)
			{
				return;
			}
			if (m_HasPendingSelection)
			{
				editor.text = m_Text;
				editor.selectIndex = Mathf.Clamp(m_PendingSelectStart, 0, m_Text.Length);
				editor.cursorIndex = Mathf.Clamp(m_PendingSelectEnd, 0, m_Text.Length);
				m_HasPendingSelection = false;
				Repaint();
			}
			m_CursorIndex = editor.cursorIndex;
			m_SelectIndex = editor.selectIndex;
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(PastePadContent.Window.ContextMenu.New, false, ShowWindow);
			menu.AddItem(PastePadContent.Window.ContextMenu.Clear, false, Clear);
			menu.AddItem(PastePadContent.Window.ContextMenu.Copy, false, Copy);
			menu.AddItem(PastePadContent.Window.ContextMenu.Save, false, Save);
			menu.AddItem(PastePadContent.Window.ContextMenu.WordWrap, m_WordWrap, ToggleWordWrap);
			menu.AddSeparator(string.Empty);
			menu.AddItem(PastePadContent.Window.ContextMenu.OpenSettings, false, PastePadSettingsEditorWindow.ShowWindow);
		}

		private void Clear()
		{
			m_Clear = true;
		}

		private void Copy()
		{
			EditorGUIUtility.systemCopyBuffer = m_Text;
		}

		private void Save()
		{
			var selectedFilepath = EditorUtility.SaveFilePanel("Save to", Application.dataPath, "data", "txt");
			if (!string.IsNullOrEmpty(selectedFilepath))
			{
				File.WriteAllText(selectedFilepath, m_Text);
			}
		}

		private void ToggleWordWrap()
		{
			m_WordWrap = !m_WordWrap;
		}
	}
}
