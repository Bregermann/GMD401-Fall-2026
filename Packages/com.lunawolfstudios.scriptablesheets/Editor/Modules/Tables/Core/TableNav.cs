using UnityEditor;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Tables
{
	public class TableNav<T> where T : ITableProperty
	{
		private const char ControlNameDelimiter = '-';

		public Vector2Int FocusedCoordinate { get; private set; }
		public Vector2Int PreviousFocusedCoordinate { get; private set; }
		public Vector2Int VisualCoordinate { get; private set; }
		public bool FocusChanged { get; private set; }
		public bool HasFocus { get; private set; }
		public bool WasKeyboardNav { get; private set; }
		public bool IsEditingTextField { get; private set; }
		public Object SelectedRootObject { get; private set; }

		// Unit direction to scroll when keyboard nav can't reach a populated cell, revealing cells hidden by virtualization.
		public Vector2Int PendingScroll { get; private set; }

		private bool m_PerformedAddNewLine;

		// Proxy control name for an AssetReference cell clicked this frame, focused on the next update since the Addressables
		// drawer's own control name is shared across rows and unreliable to read back.
		private string m_PendingFocusControlName;

		// Stable "{row}x{column}-" control name prefixes cached per cell so they are built once instead of re-formatted every
		// repaint. Rebuilt only when the table dimensions change. The control id appended in SetNextControlName keeps each unique.
		private string[,] m_ControlNamePrefixes;

		// Routes a click on an AssetReference or read-only cell to focus that exact cell's proxy on the next update.
		public void RequestFocus(string controlName)
		{
			m_PendingFocusControlName = controlName;
		}

		/// <summary>
		/// Attempts to find a coordinate within a specified property table using the name of the current focused control.
		/// </summary>
		/// <returns>True if the coordinate is parsed successfully and within the specified property table bounds.</returns>
		public bool UpdateFocusedCoordinate(Table<T> propertyTable, bool isScriptableObject, bool showReadOnly)
		{
			PreviousFocusedCoordinate = FocusedCoordinate;
			FocusChanged = false;
			HasFocus = false;
			WasKeyboardNav = false;
			PendingScroll = Vector2Int.zero;
			if (propertyTable != null)
			{
				// Focus the proxy of a clicked AssetReference cell and treat it as focused this frame so the highlight follows.
				if (!string.IsNullOrEmpty(m_PendingFocusControlName))
				{
					GUI.FocusControl(m_PendingFocusControlName);
					var requestedCoordinate = m_PendingFocusControlName.Split(ControlNameDelimiter)[0];
					m_PendingFocusControlName = null;
					if (propertyTable.IsValidCoordinate(requestedCoordinate, out Vector2Int clickedCoordinate))
					{
						// Leave FocusChanged false so the highlight moves to the clicked cell this frame instead of debouncing.
						FocusedCoordinate = clickedCoordinate;
						IsEditingTextField = false;
						HasFocus = true;
						return true;
					}
				}
				var focusedName = GUI.GetNameOfFocusedControl();
				if (!string.IsNullOrEmpty(focusedName))
				{
					var formattedCoordinate = focusedName.Split(ControlNameDelimiter)[0];
					if (propertyTable.IsValidCoordinate(formattedCoordinate, out Vector2Int coordinate))
					{
						var e = Event.current;
						if (e.type == EventType.KeyDown)
						{
							// Workaround to ensure we consume tab events when editing text fields.
							// https://forum.unity.com/threads/how-to-disable-the-tab-key-event.90440/#post-586383
							if (e.character == '\t')
							{
								e.Use();
							}
							var newCoordinate = coordinate;
							switch (e.keyCode)
							{
								case KeyCode.Escape:
									IsEditingTextField = false;
									break;

								case KeyCode.KeypadEnter:
								case KeyCode.Return:
									if (propertyTable.TryGet(coordinate, out T property))
									{
										if (property.IsInputFieldProperty(isScriptableObject) && (!property.IsUnityLocalizationProperty() || showReadOnly))
										{
											if (IsEditingTextField && (e.control || e.command))
											{
												if (!m_PerformedAddNewLine)
												{
													property.AddNewLine();
													m_PerformedAddNewLine = true;
												}
												e.Use();
												break;
											}
											IsEditingTextField = !IsEditingTextField;
											if (!IsEditingTextField)
											{
												newCoordinate.x += e.shift ? -1 : 1;
											}
										}
									}
									break;

								case KeyCode.Tab:
									newCoordinate.y += e.shift ? -1 : 1;
									e.Use();
									break;
							}
							if (!IsEditingTextField)
							{
								switch (e.keyCode)
								{
									case KeyCode.LeftArrow:
										newCoordinate.y--;
										break;

									case KeyCode.RightArrow:
										newCoordinate.y++;
										break;

									case KeyCode.UpArrow:
										newCoordinate.x--;
										break;

									case KeyCode.DownArrow:
										newCoordinate.x++;
										break;

									default:
										break;
								}
							}
							if (newCoordinate != coordinate)
							{
								// Step in the pressed direction so empty cells (null union columns, array gaps) are skipped.
								var step = newCoordinate - coordinate;
								var scanCoordinate = newCoordinate;
								var foundTarget = false;
								while (propertyTable.IsValidCoordinate(scanCoordinate))
								{
									if (propertyTable.TryGet(scanCoordinate, out T property))
									{
										GUI.FocusControl(property.ControlName);
										coordinate = scanCoordinate;
										WasKeyboardNav = true;
										foundTarget = true;
										break;
									}
									scanCoordinate += step;
								}
								// Nothing populated rendered this way; if the next cell is in bounds it's likely virtualized, so scroll.
								if (!foundTarget && propertyTable.IsValidCoordinate(newCoordinate))
								{
									PendingScroll = step;
									WasKeyboardNav = true;
								}
							}
						}
						else if (e.type == EventType.KeyUp)
						{
							switch (e.keyCode)
							{
								case KeyCode.KeypadEnter:
								case KeyCode.Return:
									m_PerformedAddNewLine = false;
									break;

								default:
									break;
							}
						}
						if (FocusedCoordinate != coordinate)
						{
							IsEditingTextField = false;
							FocusChanged = true;
							FocusedCoordinate = coordinate;
						}
						if (IsEditingTextField && !EditorGUIUtility.editingTextField)
						{
							// Force text field highlighting when we're editing text.
							if (propertyTable.TryGet(FocusedCoordinate, out T property))
							{
								EditorGUIUtility.editingTextField = true;
								EditorGUI.FocusTextInControl(property.ControlName);
							}
						}
						HasFocus = true;
						return true;
					}
					else
					{
						// Reset the focus control if it's an invalid coordinate.
						GUIUtility.keyboardControl = 0;
						GUI.FocusControl("null");
					}
				}
			}
			IsEditingTextField = false;
			FocusedCoordinate = Vector2Int.zero;
			return false;
		}

		public void ResetTextFieldEditing()
		{
			IsEditingTextField = false;
			EditorGUIUtility.editingTextField = false;
		}

		public bool UpdateFocusVisuals(Table<T> propertyTable, TableNavSettings settings, TableNavVisualState visualState, ref Vector2 scrollPosition, bool lockNames = false)
		{
			var visualCoordinateChanged = false;
			// Account for the edge case where focus can change when scrolling.
			if (propertyTable.IsValidCoordinate(FocusedCoordinate) && HasFocus && (WasKeyboardNav || !FocusChanged))
			{
				// Track when the visual coordinate changes for auto selection.
				if (VisualCoordinate != FocusedCoordinate)
				{
					VisualCoordinate = FocusedCoordinate;
					visualCoordinateChanged = true;
				}
			}
			else if (VisualCoordinate == Vector2Int.zero || !propertyTable.IsValidCoordinate(VisualCoordinate))
			{
				return false;
			}
			var needsSelectionBorder = false;
			if (propertyTable.TryGet(VisualCoordinate, out T property))
			{
				// Do not auto reselect if the same exact cell for the same Object was reselected.
				if (settings.AutoSelect && (visualCoordinateChanged || SelectedRootObject != property.RootObject))
				{
					SelectedRootObject = property.RootObject;
					Selection.activeObject = property.RootObject;
				}
				needsSelectionBorder = property.NeedsSelectionBorder(lockNames);
			}
			var selectionColor = GUI.skin.settings.selectionColor;
			var highlightColor = selectionColor;
			highlightColor.a = settings.HighlightAlpha;
			var focusedRowY = visualState.RowHeight * (VisualCoordinate.x + 1);
			if (settings.HighlightSelectedRow && propertyTable.Rows > 1)
			{
				var rowRect = new Rect(visualState.ColumnHeaderRowRect);
				rowRect.width += scrollPosition.x;
				rowRect.y += focusedRowY;
				EditorGUI.DrawRect(rowRect, highlightColor);
			}
			var focusedColumnRect = visualState.MultiColumnHeader.GetColumnRect(VisualCoordinate.y);
			if (settings.HighlightSelectedColumn && propertyTable.Columns > 2)
			{
				focusedColumnRect.y = visualState.ColumnHeaderRowRect.y;
				focusedColumnRect.height += visualState.RowHeight * propertyTable.Rows;
				EditorGUI.DrawRect(focusedColumnRect, highlightColor);
			}
			var focusedCellRect = visualState.MultiColumnHeader.GetCellRect(VisualCoordinate.y, new Rect(visualState.ColumnHeaderRowRect));
			focusedCellRect.y += focusedRowY;
			if (IsEditingTextField)
			{
				var borderThickness = 2;
				var borderColor = selectionColor;
				borderColor.b = 1.0f;
				borderColor.a = 1.0f;
				DrawBorder(focusedCellRect, borderThickness, borderColor);
			}
			else if (needsSelectionBorder)
			{
				var borderThickness = 1;
				var borderColor = selectionColor;
				borderColor.a = 1.0f;
				DrawBorder(focusedCellRect, borderThickness, borderColor);
			}
			if (PendingScroll != Vector2Int.zero)
			{
				// Nudge the scrollbar one cell in the pressed direction to reveal more of the virtualized table while held. Clamp to
				// the max scroll so it never overshoots past the content, which would jitter the header against the clamped content.
				if (PendingScroll.x != 0)
				{
					scrollPosition.y = Mathf.Clamp(scrollPosition.y + PendingScroll.x * visualState.RowHeight, 0, visualState.MaxScroll.y);
				}
				if (PendingScroll.y != 0)
				{
					var columnWidth = visualState.MultiColumnHeader.GetColumnRect(VisualCoordinate.y).width;
					scrollPosition.x = Mathf.Clamp(scrollPosition.x + PendingScroll.y * columnWidth, 0, visualState.MaxScroll.x);
				}
			}
			else if (settings.AutoScroll && WasKeyboardNav)
			{
				var focusedCellEndX = focusedCellRect.x + focusedCellRect.width;
				var focusedCellEndY = focusedCellRect.y + focusedCellRect.height;

				// Scrolling left.
				if (focusedCellRect.x < visualState.ScrollStart.x)
				{
					// Special case to show the Actions column when back on the first column.
					if (VisualCoordinate.y > 1)
					{
						scrollPosition.x = focusedColumnRect.x - visualState.ScrollViewArea.x;
					}
					else
					{
						scrollPosition.x = 0;
					}
				}
				// Scrolling right.
				else if (focusedCellEndX >= visualState.ScrollEnd.x)
				{
					scrollPosition.x = focusedCellEndX - visualState.ScrollViewArea.width - visualState.ScrollViewArea.x;
					// Special case to scroll to the very right on the last column.
					if (VisualCoordinate.x >= propertyTable.Columns - 1)
					{
						// Adding row height is not ideal, but Unity doesn't provide a way to get the absolute maximum scroll position X.
						scrollPosition.x += visualState.RowHeight;
					}
				}
				// Scrolling up.
				var adjustedScrollY = focusedCellRect.y - visualState.RowHeight;
				if (adjustedScrollY < visualState.ScrollStart.y)
				{
					if (VisualCoordinate.x > 0)
					{
						scrollPosition.y = adjustedScrollY - visualState.ScrollViewArea.y;
					}
					else
					{
						scrollPosition.y = 0;
					}
				}
				// Scrolling down.
				else if (focusedCellEndY >= visualState.ScrollEnd.y)
				{
					scrollPosition.y = focusedCellEndY - visualState.ScrollViewArea.height - visualState.ScrollViewArea.y;
					// Special case to scroll to the very bottom on the last row.
					if (VisualCoordinate.x >= propertyTable.Rows - 1)
					{
						scrollPosition.y += visualState.RowHeight;
					}
				}
			}
			return true;
		}

		public string SetNextControlName(Table<T> propertyTable, int row, int column)
		{
			// Ensure a unique control name by assigning a control id.
			// https://forum.unity.com/threads/gui-getnameoffocusedcontrol-is-not-working-correctly.165143/
			var controlName = GetControlNamePrefix(propertyTable.Rows, propertyTable.Columns, row, column) + GUIUtility.GetControlID(FocusType.Keyboard).ToString();
			GUI.SetNextControlName(controlName);
			return controlName;
		}

		// Returns the cached "{row}x{column}-" prefix for a cell, building it on first use. The grid is reallocated only when the
		// table dimensions change. Coordinates outside the cached range fall back to building the prefix directly.
		private string GetControlNamePrefix(int rows, int columns, int row, int column)
		{
			if (m_ControlNamePrefixes == null || m_ControlNamePrefixes.GetLength(0) != rows || m_ControlNamePrefixes.GetLength(1) != columns)
			{
				m_ControlNamePrefixes = new string[rows, columns];
			}
			if (row < 0 || row >= rows || column < 0 || column >= columns)
			{
				return row + "x" + column + ControlNameDelimiter;
			}
			var prefix = m_ControlNamePrefixes[row, column];
			if (prefix == null)
			{
				prefix = row + "x" + column + ControlNameDelimiter;
				m_ControlNamePrefixes[row, column] = prefix;
			}
			return prefix;
		}

		private static void DrawBorder(Rect rect, int thickness, Color color)
		{
			var halfThickness = thickness * 0.5f;
			// Top
			EditorGUI.DrawRect(new Rect(rect.x - halfThickness, rect.y - halfThickness, rect.width + thickness, thickness), color);
			// Bottom
			EditorGUI.DrawRect(new Rect(rect.x - halfThickness, rect.yMax - halfThickness, rect.width + thickness, thickness), color);
			// Left
			EditorGUI.DrawRect(new Rect(rect.x - halfThickness, rect.y - halfThickness, thickness, rect.height + thickness), color);
			// Right
			EditorGUI.DrawRect(new Rect(rect.xMax - halfThickness, rect.y - halfThickness, thickness, rect.height + thickness), color);
		}
	}
}
