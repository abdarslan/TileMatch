using System.Collections.Generic;
using System.Linq;
using TileMatch.Model;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TileMatch.Editor
{
    public class LevelDesignTool : EditorWindow
    {
        // ── Constants & Dynamic Scaling ──────────────────────────────────────
        private float CellSize         => 36f * _zoom;
        private float HalfCell         => CellSize * 0.5f;
        private const float GridPadding      = 8f;
        private float PaletteIconSize  => 48f * _zoom;
        private const float SidebarWidth     = 240f;
        private const float ToolbarHeight    = 26f;
        private const int   MaxTypeID        = 15;
        private const string SpritesPath     = "Assets/_Project/Sprites";

        private float _zoom = 1.0f;

        // ── State: target asset ─────────────────────────────────────────────
        private LevelData _level;
        private List<LevelData> _allLevels = new List<LevelData>();
        private string[] _levelNames = new string[0];
        private int _selectedLevelIndex = -1;
        private bool _autoNextLevel = true;

        // ── State: grid configuration ───────────────────────────────────────
        private int _gridCols  = 8;
        private int _gridRows  = 8;
        private int _layerCount = 4;

        // ── State: design-time tile grid ────────────────────────────────────
        // _cells[layer][col, row] = typeID (0 = empty)
        private int[][,] _cells;
        private bool[] _layerOffsetX;
        private bool[] _layerOffsetY;

        // ── State: layer navigation & tools ─────────────────────────────────
        private int  _activeLayer   = 0;
        private int  _selectedType  = 1;
        private bool _eraseMode     = false;
        private bool _showBlocking  = false;
        
        private bool _symX = false;
        private bool _symY = false;

        // Post-bake blocking map: key = tileID, value = is directly clickable
        private HashSet<int> _bakedClickable = new HashSet<int>();

        // ── State: copy/paste ────────────────────────────────────────────────
        private int[,] _clipboard;

        // ── State: validation ────────────────────────────────────────────────
        private LevelValidator.ValidationResult _lastValidation;

        // ── State: orders ────────────────────────────────────────────────────
        private Vector2 _orderScrollPos;
        private bool    _orderFoldout = true;
        private ReorderableList _orderList;

        // ── State: palette sprites ───────────────────────────────────────────
        private Sprite[] _palette;
        private bool     _paletteLoaded = false;

        // ── State: scroll ─────────────────────────────────────────────────
        private Vector2 _gridScrollPos;
        private Vector2 _mainScrollPos;
        private Vector2 _paletteScrollPos;

        // ── GUIStyles (lazy) ─────────────────────────────────────────────────
        private GUIStyle _cellStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool     _stylesInit = false;

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("TileMatch/Level Design Tool")]
        public static void Open()
        {
            var window = GetWindow<LevelDesignTool>("Level Design Tool");
            window.minSize = new Vector2(900, 620);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            LoadPalette();
            RefreshLevelList();
            InitCells();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _cellStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 9
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleLeft
            };

            _labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = false,
                fontSize = 12
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private void LoadPalette()
        {
            _palette = new Sprite[MaxTypeID + 1];
            for (int i = 1; i <= MaxTypeID; i++)
            {
                string path = $"{SpritesPath}/Icon_{i:D2}.png";
                _palette[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            _paletteLoaded = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void InitCells()
        {
            _cells = new int[_layerCount][,];
            _layerOffsetX = new bool[_layerCount];
            _layerOffsetY = new bool[_layerCount];

            for (int l = 0; l < _layerCount; l++)
                _cells[l] = new int[_gridCols, _gridRows];

            _bakedClickable.Clear();
            _lastValidation = null;
            _showBlocking   = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                DrawMainArea();
                DrawRightSidebar();
            }

            ProcessKeyboardShortcuts();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SIDEBAR
        // ══════════════════════════════════════════════════════════════════════
        private void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
            {
                EditorGUILayout.Space(4);
                DrawTargetAssetSection();
                EditorGUILayout.Space(6);
                DrawGridConfigSection();
                EditorGUILayout.Space(6);
                DrawLayerSection();
                EditorGUILayout.Space(6);
                DrawToolSection();
                GUILayout.FlexibleSpace();
                DrawBakeSection();
                EditorGUILayout.Space(4);
            }
        }

        private void DrawTargetAssetSection()
        {
            GUILayout.Label("Target Level", _headerStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↻", GUILayout.Width(25)))
                {
                    RefreshLevelList();
                }
                
                EditorGUI.BeginChangeCheck();
                _selectedLevelIndex = EditorGUILayout.Popup(_selectedLevelIndex, _levelNames);
                if (EditorGUI.EndChangeCheck() && _selectedLevelIndex >= 0 && _selectedLevelIndex < _allLevels.Count)
                {
                    _level = _allLevels[_selectedLevelIndex];
                    ImportFromLevel();
                    InitializeReorderableList();
                }

                EditorGUI.BeginChangeCheck();
                _level = (LevelData)EditorGUILayout.ObjectField(GUIContent.none, _level, typeof(LevelData), false, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    SyncSelectedLevelIndex();
                    if (_level != null)
                    {
                        ImportFromLevel();
                        InitializeReorderableList();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Auto Level", GUILayout.Height(24)))
                {
                    CreateNextLevel();
                    ImportFromLevel();
                    InitializeReorderableList();
                }
                if (GUILayout.Button("Custom...", GUILayout.Width(70), GUILayout.Height(24)))
                {
                    CreateNewLevel();
                    ImportFromLevel();
                    InitializeReorderableList();
                }
            }

            _autoNextLevel = EditorGUILayout.ToggleLeft("Auto Create Next Level on Bake", _autoNextLevel);

            if (_level == null)
            {
                EditorGUILayout.HelpBox("Select or create a LevelData asset to begin.", MessageType.Info);
            }
        }

        private void InitializeReorderableList()
        {
            if (_level == null) return;
            _orderList = new ReorderableList(_level.EditorPendingOrders, typeof(OrderData), true, false, true, true);
            
            _orderList.elementHeightCallback = (int index) => {
                if (index >= _level.EditorPendingOrders.Count) return 40f;
                var order = _level.EditorPendingOrders[index];
                return 56f + (order.requiredTypeIDs.Count * 32f);
            };

            _orderList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                if (index >= _level.EditorPendingOrders.Count) return;
                var order = _level.EditorPendingOrders[index];
                
                rect.y += 8; 
                rect.height -= 16;

                Color originalBg = GUI.backgroundColor;
                GUI.backgroundColor = order.requiredTypeIDs.Count < 3 ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
                GUI.Box(new Rect(rect.x, rect.y, rect.width, rect.height), GUIContent.none, EditorStyles.helpBox);
                GUI.backgroundColor = originalBg;

                rect.y += 4; rect.x += 4; rect.width -= 8;
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, 22), $"Order {index + 1}", EditorStyles.boldLabel);
                rect.y += 28;

                for (int j = 0; j < order.requiredTypeIDs.Count; j++)
                {
                    int typeID = order.requiredTypeIDs[j];
                    int newType = EditorGUI.IntSlider(new Rect(rect.x, rect.y, rect.width - 65, 20), typeID, 1, MaxTypeID);
                    if (newType != typeID)
                    {
                        Undo.RecordObject(_level, "Edit Order Slot");
                        order.requiredTypeIDs[j] = newType;
                        EditorUtility.SetDirty(_level);
                    }

                    if (_palette != null && newType >= 1 && newType <= MaxTypeID && _palette[newType] != null)
                        GUI.DrawTexture(new Rect(rect.x + rect.width - 60, rect.y - 4, 28, 28), _palette[newType].texture);

                    if (GUI.Button(new Rect(rect.x + rect.width - 25, rect.y, 25, 20), "-"))
                    {
                        Undo.RecordObject(_level, "Remove Order Slot");
                        order.requiredTypeIDs.RemoveAt(j);
                        EditorUtility.SetDirty(_level);
                        break;
                    }
                    rect.y += 30;
                }

                if (GUI.Button(new Rect(rect.x, rect.y, rect.width, 20), "+ Add Slot"))
                {
                    Undo.RecordObject(_level, "Add Order Slot");
                    order.requiredTypeIDs.Add(1);
                    EditorUtility.SetDirty(_level);
                }
            };

            _orderList.onAddCallback = (ReorderableList l) => {
                // Disabled: orders are auto-synced
            };

            _orderList.onRemoveCallback = (ReorderableList l) => {
                // Disabled: orders are auto-synced
            };
        }

        private void DrawGridConfigSection()
        {
            GUILayout.Label("Grid Config", _headerStyle);

            EditorGUI.BeginChangeCheck();
            _gridCols   = Mathf.Clamp(EditorGUILayout.IntField("Columns",  _gridCols),  2, 20);
            _gridRows   = Mathf.Clamp(EditorGUILayout.IntField("Rows",     _gridRows),  2, 20);
            _layerCount = Mathf.Clamp(EditorGUILayout.IntField("Z Layers", _layerCount), 1, 12);
            
            EditorGUILayout.Space(4);
            _zoom = EditorGUILayout.Slider("Zoom UI", _zoom, 0.5f, 4.0f);

            if (EditorGUI.EndChangeCheck())
                ResizeCells();
        }

        private void DrawLayerSection()
        {
            GUILayout.Label("Layer (Z)", _headerStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▼", GUILayout.Width(28)) && _activeLayer > 0)
                    _activeLayer--;
                GUILayout.Label($"Z = {_activeLayer}", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("▲", GUILayout.Width(28)) && _activeLayer < _layerCount - 1)
                    _activeLayer++;
            }
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Layer"))
                    CopyLayer();
                GUI.enabled = _clipboard != null;
                if (GUILayout.Button("Paste Layer"))
                    PasteLayer();
                GUI.enabled = true;
            }

            if (GUILayout.Button("Clear Layer"))
            {
                Undo.RecordObject(this, "Clear Layer");
                _cells[_activeLayer] = new int[_gridCols, _gridRows];
                SyncOrdersWithGrid();
            }
        }

        private void DrawToolSection()
        {
            GUILayout.Label("Tool", _headerStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = _eraseMode ? prev : Color.cyan;
                if (GUILayout.Button("Paint (P)"))
                    _eraseMode = false;
                GUI.backgroundColor = _eraseMode ? Color.red : prev;
                if (GUILayout.Button("Erase (E)"))
                    _eraseMode = true;
                GUI.backgroundColor = prev;
            }
            
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Left Click: Paint\nRight Click: Erase", MessageType.Info);
        }

        private void DrawPaletteSection()
        {
            GUILayout.Label("Tile Types Palette", _headerStyle);

            if (!_paletteLoaded) return;

            // Allow the palette to take up more vertical space and wrap
            _paletteScrollPos = EditorGUILayout.BeginScrollView(_paletteScrollPos, GUILayout.Height(150));
            
            // Estimate available width (Window width minus sidebars and scrollbar)
            float availableWidth = position.width - SidebarWidth - 320f - 25f;
            if (availableWidth < PaletteIconSize) availableWidth = PaletteIconSize;
            
            int iconsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (PaletteIconSize + 4f)));
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            int currentInRow = 0;
            
            for (int i = 1; i <= MaxTypeID; i++)
            {
                if (currentInRow >= iconsPerRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    currentInRow = 0;
                }

                bool selected = _selectedType == i;

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = selected ? Color.yellow : prev;

                GUIContent content = _palette[i] != null
                    ? new GUIContent(_palette[i].texture, $"Type {i}")
                    : new GUIContent(i.ToString());

                if (GUILayout.Button(content, GUILayout.Width(PaletteIconSize), GUILayout.Height(PaletteIconSize)))
                {
                    _selectedType = i;
                    _eraseMode    = false;
                }

                GUI.backgroundColor = prev;
                currentInRow++;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawBakeSection()
        {
            EditorGUI.BeginDisabledGroup(_level == null);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
            if (GUILayout.Button("▶  Bake Level", GUILayout.Height(34)))
                BakeLevel();
            GUI.backgroundColor = prev;

            if (_showBlocking)
            {
                bool toggle = EditorGUILayout.ToggleLeft("Show Blocking Overlay", _showBlocking);
                if (toggle != _showBlocking)
                {
                    _showBlocking = toggle;
                    Repaint();
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        // ══════════════════════════════════════════════════════════════════════
        // MAIN AREA (grid + orders + validation)
        // ══════════════════════════════════════════════════════════════════════
        private void DrawMainArea()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawLayerTabs();

                _mainScrollPos = EditorGUILayout.BeginScrollView(_mainScrollPos);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawGrid();
                        DrawGridOptions();
                    }
                    DrawVerticalLayerNames();
                }
                
                DrawPaletteSection();
                EditorGUILayout.Space(12);
                DrawValidationPanel();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawGridOptions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Layer Offset:", EditorStyles.boldLabel, GUILayout.Width(90));
                        
                        if (_layerOffsetX != null && _activeLayer < _layerOffsetX.Length)
                        {
                            EditorGUI.BeginChangeCheck();
                            _layerOffsetX[_activeLayer] = EditorGUILayout.ToggleLeft("X (0.5)", _layerOffsetX[_activeLayer], GUILayout.Width(70));
                            _layerOffsetY[_activeLayer] = EditorGUILayout.ToggleLeft("Y (0.5)", _layerOffsetY[_activeLayer], GUILayout.Width(70));
                            if (EditorGUI.EndChangeCheck())
                            {
                                Repaint();
                            }
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Symmetry:", EditorStyles.boldLabel, GUILayout.Width(90));
                        _symX = EditorGUILayout.ToggleLeft("X-Axis", _symX, GUILayout.Width(70));
                        _symY = EditorGUILayout.ToggleLeft("Y-Axis", _symY, GUILayout.Width(70));
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawVerticalLayerNames()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(120)))
            {
                GUILayout.FlexibleSpace();
                
                GUIStyle largeText = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };

                GUILayout.Label("TOP", largeText);
                GUILayout.Space(10);
                
                for (int i = _layerCount - 1; i >= 0; i--)
                {
                    Color c = (i == _activeLayer) ? Color.cyan : Color.gray;
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel) 
                    { 
                        normal = { textColor = c }, 
                        alignment = TextAnchor.MiddleCenter, 
                        fontSize = 14 
                    };
                    GUILayout.Label($"Layer {i}", style);
                    GUILayout.Space(10);
                }
                
                GUILayout.Label("BOTTOM", largeText);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawLayerTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int l = 0; l < _layerCount; l++)
                {
                    bool isActive = l == _activeLayer;
                    Color prev = GUI.backgroundColor;
                    if (isActive) GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);

                    if (GUILayout.Toggle(isActive, $"  Z{l}  ", EditorStyles.toolbarButton))
                        _activeLayer = l;

                    GUI.backgroundColor = prev;
                }
                GUILayout.FlexibleSpace();

                string badge = "";
                if (_activeLayer == 0) badge = " [BOTTOM LAYER] ";
                else if (_activeLayer == _layerCount - 1) badge = " [TOP LAYER] ";

                GUILayout.Label($"{badge} Grid: {_gridCols}×{_gridRows}  |  Active: Z{_activeLayer}", EditorStyles.miniLabel);
                GUILayout.Space(8);
            }
        }

        private void DrawGrid()
        {
            if (_cells == null || _cells.Length != _layerCount) return;

            float totalW = _gridCols * CellSize + GridPadding * 2;
            float totalH = _gridRows * CellSize + GridPadding * 2;

            _gridScrollPos = EditorGUILayout.BeginScrollView(_gridScrollPos,
                GUILayout.Width(totalW + 20),
                GUILayout.Height(Mathf.Min(totalH + 20, position.height * 0.55f)));

            Rect canvasRect = GUILayoutUtility.GetRect(totalW, totalH);

            DrawGridBackground(canvasRect);
            DrawInactiveLayers(canvasRect);
            DrawActiveLayer(canvasRect);
            DrawGridLines(canvasRect);
            HandleGridInput(canvasRect);

            EditorGUILayout.EndScrollView();
        }

        private void DrawGridBackground(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.15f, 0.15f, 0.15f));
        }

        private void DrawInactiveLayers(Rect canvas)
        {
            for (int l = 0; l < _layerCount; l++)
            {
                if (l == _activeLayer) continue;

                float alpha = 0.28f - Mathf.Abs(l - _activeLayer) * 0.06f;
                alpha = Mathf.Max(alpha, 0.06f);

                for (int c = 0; c < _gridCols; c++)
                {
                    for (int r = 0; r < _gridRows; r++)
                    {
                        int typeID = _cells[l][c, r];
                        if (typeID == 0) continue;

                        Rect cellRect = GetCellRect(canvas, c, r, l);
                        Color tileColor = GetTypeColor(typeID);
                        tileColor.a = alpha;
                        EditorGUI.DrawRect(cellRect, tileColor);
                    }
                }
            }
        }

        private void DrawActiveLayer(Rect canvas)
        {
            for (int c = 0; c < _gridCols; c++)
            {
                for (int r = 0; r < _gridRows; r++)
                {
                    int typeID = _cells[_activeLayer][c, r];
                    if (typeID == 0) continue;

                    Rect cellRect = GetCellRect(canvas, c, r, _activeLayer);

                    bool isClickable = true;
                    if (_showBlocking)
                    {
                        int tileID = EncodeID(c, r, _activeLayer);
                        isClickable = _bakedClickable.Contains(tileID);
                    }

                    Color bg = GetTypeColor(typeID);
                    if (_showBlocking && !isClickable)
                        bg = Color.Lerp(bg, new Color(1f, 0.15f, 0.1f), 0.55f);

                    EditorGUI.DrawRect(cellRect, bg);

                    if (_palette != null && typeID <= MaxTypeID && _palette[typeID] != null && _palette[typeID].texture != null)
                    {
                        Rect iconRect = new Rect(cellRect.x + 3, cellRect.y + 3, cellRect.width - 6, cellRect.height - 6);
                        GUI.DrawTexture(iconRect, _palette[typeID].texture, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.Label(cellRect, typeID.ToString(), _cellStyle);
                    }

                    if (_showBlocking && !isClickable)
                    {
                        EditorGUI.DrawRect(cellRect, new Color(1f, 0.1f, 0.1f, 0.35f));
                    }
                }
            }
        }

        private void DrawGridLines(Rect canvas)
        {
            Handles.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);

            for (int c = 0; c <= _gridCols; c++)
            {
                float x = canvas.x + GridPadding + c * CellSize;
                Handles.DrawLine(
                    new Vector3(x, canvas.y + GridPadding),
                    new Vector3(x, canvas.y + GridPadding + _gridRows * CellSize));
            }

            for (int r = 0; r <= _gridRows; r++)
            {
                float y = canvas.y + GridPadding + r * CellSize;
                Handles.DrawLine(
                    new Vector3(canvas.x + GridPadding,                        y),
                    new Vector3(canvas.x + GridPadding + _gridCols * CellSize, y));
            }

            // Draw half-grid lines faintly to help visualize offsets
            Handles.color = new Color(0.35f, 0.35f, 0.35f, 0.15f);
            for (int c = 0; c < _gridCols; c++)
            {
                float x = canvas.x + GridPadding + c * CellSize + HalfCell;
                Handles.DrawLine(
                    new Vector3(x, canvas.y + GridPadding),
                    new Vector3(x, canvas.y + GridPadding + _gridRows * CellSize));
            }
            for (int r = 0; r < _gridRows; r++)
            {
                float y = canvas.y + GridPadding + r * CellSize + HalfCell;
                Handles.DrawLine(
                    new Vector3(canvas.x + GridPadding, y),
                    new Vector3(canvas.x + GridPadding + _gridCols * CellSize, y));
            }

            // Active-layer border highlight
            Handles.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            float bx = canvas.x + GridPadding;
            float by = canvas.y + GridPadding;
            float bw = _gridCols * CellSize;
            float bh = _gridRows * CellSize;
            Handles.DrawLine(new Vector3(bx,      by),      new Vector3(bx + bw, by));
            Handles.DrawLine(new Vector3(bx + bw, by),      new Vector3(bx + bw, by + bh));
            Handles.DrawLine(new Vector3(bx + bw, by + bh), new Vector3(bx,      by + bh));
            Handles.DrawLine(new Vector3(bx,      by + bh), new Vector3(bx,      by));

            // Emphasize center lines (Quarters)
            Handles.color = new Color(0.9f, 0.4f, 0.4f, 0.6f); // Faint Red
            float cx = bx + (bw / 2f);
            float cy = by + (bh / 2f);
            Handles.DrawLine(new Vector3(cx, by), new Vector3(cx, by + bh));
            Handles.DrawLine(new Vector3(bx, cy), new Vector3(bx + bw, cy));
        }

        private void HandleGridInput(Rect canvas)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (!canvas.Contains(e.mousePosition)) return;
            if (e.button != 0 && e.button != 1) return;

            float offsetX = (_layerOffsetX != null && _layerOffsetX[_activeLayer]) ? HalfCell : 0f;
            float offsetY = (_layerOffsetY != null && _layerOffsetY[_activeLayer]) ? HalfCell : 0f;

            int col = Mathf.FloorToInt((e.mousePosition.x - canvas.x - GridPadding - offsetX) / CellSize);
            int row = Mathf.FloorToInt((e.mousePosition.y - canvas.y - GridPadding - offsetY) / CellSize);

            if (col < 0 || col >= _gridCols || row < 0 || row >= _gridRows) return;

            bool isErasing = _eraseMode || e.button == 1;
            int newValue = isErasing ? 0 : _selectedType;

            Undo.RecordObject(this, isErasing ? "Erase Tile" : "Paint Tile");
            
            _cells[_activeLayer][col, row] = newValue;
            
            if (_symX)
            {
                int symCol = _gridCols - 1 - col;
                if (symCol >= 0 && symCol < _gridCols) _cells[_activeLayer][symCol, row] = newValue;
            }
            if (_symY)
            {
                int symRow = _gridRows - 1 - row;
                if (symRow >= 0 && symRow < _gridRows) _cells[_activeLayer][col, symRow] = newValue;
            }
            if (_symX && _symY)
            {
                int symCol = _gridCols - 1 - col;
                int symRow = _gridRows - 1 - row;
                if (symCol >= 0 && symCol < _gridCols && symRow >= 0 && symRow < _gridRows)
                    _cells[_activeLayer][symCol, symRow] = newValue;
            }

            SyncOrdersWithGrid();

            _showBlocking   = false;
            _lastValidation = null;

            e.Use();
            Repaint();
        }

        private void SyncOrdersWithGrid()
        {
            if (_level == null || _level.EditorPendingOrders == null) return;

            Dictionary<int, int> gridCounts = new Dictionary<int, int>();
            for (int l = 0; l < _layerCount; l++)
            {
                for (int c = 0; c < _gridCols; c++)
                {
                    for (int r = 0; r < _gridRows; r++)
                    {
                        int typeID = _cells[l][c, r];
                        if (typeID != 0)
                        {
                            if (!gridCounts.ContainsKey(typeID)) gridCounts[typeID] = 0;
                            gridCounts[typeID]++;
                        }
                    }
                }
            }

            Dictionary<int, int> orderCounts = new Dictionary<int, int>();
            foreach (var order in _level.EditorPendingOrders)
            {
                foreach (var typeID in order.requiredTypeIDs)
                {
                    if (!orderCounts.ContainsKey(typeID)) orderCounts[typeID] = 0;
                    orderCounts[typeID]++;
                }
            }

            Undo.RecordObject(_level, "Sync Orders");
            bool changed = false;

            foreach (var kvp in gridCounts)
            {
                int typeID = kvp.Key;
                int target = kvp.Value;
                int current = orderCounts.ContainsKey(typeID) ? orderCounts[typeID] : 0;

                while (current < target)
                {
                    OrderData targetOrder = null;
                    foreach (var order in _level.EditorPendingOrders)
                    {
                        if (order.requiredTypeIDs.Count > 0 && order.requiredTypeIDs.Count < 3 && order.requiredTypeIDs[0] == typeID)
                        {
                            targetOrder = order;
                            break;
                        }
                    }
                    if (targetOrder == null)
                    {
                        targetOrder = new OrderData { requiredTypeIDs = new List<int>() };
                        _level.EditorPendingOrders.Add(targetOrder);
                    }
                    targetOrder.requiredTypeIDs.Add(typeID);
                    current++;
                    changed = true;
                }
            }

            foreach (var kvp in orderCounts)
            {
                int typeID = kvp.Key;
                int current = kvp.Value;
                int target = gridCounts.ContainsKey(typeID) ? gridCounts[typeID] : 0;

                while (current > target)
                {
                    OrderData targetOrder = null;
                    for (int i = _level.EditorPendingOrders.Count - 1; i >= 0; i--)
                    {
                        if (_level.EditorPendingOrders[i].requiredTypeIDs.Contains(typeID))
                        {
                            targetOrder = _level.EditorPendingOrders[i];
                            break;
                        }
                    }

                    if (targetOrder != null)
                    {
                        targetOrder.requiredTypeIDs.Remove(typeID);
                        current--;
                        changed = true;
                    }
                }
            }

            for (int i = _level.EditorPendingOrders.Count - 1; i >= 0; i--)
            {
                if (_level.EditorPendingOrders[i].requiredTypeIDs.Count == 0)
                {
                    _level.EditorPendingOrders.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(_level);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════
        // RIGHT SIDEBAR (Palette + Orders)
        // ══════════════════════════════════════════════════════════════════════
        private void DrawRightSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
            {
                EditorGUILayout.Space(4);
                DrawOrderEditor();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ORDER EDITOR
        // ══════════════════════════════════════════════════════════════════════
        private void DrawOrderEditor()
        {
            if (_level == null) return;

            _orderFoldout = EditorGUILayout.Foldout(_orderFoldout, "Pending Orders", true, EditorStyles.foldoutHeader);
            if (!_orderFoldout) return;

            if (_orderList == null && _level.EditorPendingOrders != null)
                InitializeReorderableList();

            if (_orderList != null)
            {
                EditorGUI.indentLevel++;
                _orderScrollPos = EditorGUILayout.BeginScrollView(_orderScrollPos);
                _orderList.DoLayoutList();
                EditorGUILayout.EndScrollView();
                EditorGUI.indentLevel--;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // VALIDATION PANEL
        // ══════════════════════════════════════════════════════════════════════
        private void DrawValidationPanel()
        {
            if (_lastValidation == null) return;

            EditorGUILayout.Space(8);
            GUILayout.Label("Level Validation", _headerStyle);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                MessageType msgType = _lastValidation.IsSolvable ? MessageType.Info : MessageType.Warning;
                string header = _lastValidation.IsSolvable
                    ? "Level appears solvable."
                    : "Level may be unsolvable. See issues below.";

                EditorGUILayout.HelpBox(header, msgType);

                foreach (string issue in _lastValidation.Issues)
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);

                foreach (string hint in _lastValidation.Hints)
                    EditorGUILayout.HelpBox($"Hint: {hint}", MessageType.Info);

                GUILayout.Label(
                    $"Total tiles: {_lastValidation.TileCount}  |  " +
                    $"Immediately clickable: {_lastValidation.ClickableCount}  |  " +
                    $"Blocked: {_lastValidation.BlockedCount}",
                    _labelStyle);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // BAKE
        // ══════════════════════════════════════════════════════════════════════
        private void BakeLevel()
        {
            Undo.RecordObject(_level, "Bake Level");

            List<TileData> allTiles = BuildTileList();
            BakeBlockingRelationships(allTiles);

            _level.EditorActiveTiles = allTiles;
            EditorUtility.SetDirty(_level);
            AssetDatabase.SaveAssets();

            _bakedClickable.Clear();
            foreach (var t in allTiles)
                if (t.blockingTileIDs.Count == 0)
                    _bakedClickable.Add(t.tileID);

            _showBlocking   = true;
            _lastValidation = LevelValidator.Validate(_level);

#if UNITY_EDITOR
            Debug.Log($"[LevelDesignTool] Baked {allTiles.Count} tiles. " +
#endif
                      $"Clickable: {_bakedClickable.Count}. " +
                      $"Solvable: {_lastValidation.IsSolvable}");

            if (_lastValidation.IsSolvable && _autoNextLevel)
            {
                CreateNextLevel();
                ImportFromLevel();
                InitializeReorderableList();
            }

            Repaint();
        }

        private float GetBaseTileSize()
        {
            return 1.0f;
        }

        private List<TileData> BuildTileList()
        {
            var list  = new List<TileData>();
            int nextID = 1;

            int minC = int.MaxValue;
            int maxC = int.MinValue;
            int minR = int.MaxValue;
            int maxR = int.MinValue;

            for (int l = 0; l < _layerCount; l++)
            {
                for (int c = 0; c < _gridCols; c++)
                {
                    for (int r = 0; r < _gridRows; r++)
                    {
                        if (_cells[l][c, r] != 0)
                        {
                            if (c < minC) minC = c;
                            if (c > maxC) maxC = c;
                            if (r < minR) minR = r;
                            if (r > maxR) maxR = r;
                        }
                    }
                }
            }

            float baseSize = GetBaseTileSize();
            float offsetX = 0f;
            float offsetY = 0f;

            if (minC <= maxC)
            {
                offsetX = (minC + maxC) * baseSize * 0.5f;
                offsetY = (minR + maxR) * baseSize * 0.5f;
            }

            for (int l = 0; l < _layerCount; l++)
            {
                for (int c = 0; c < _gridCols; c++)
                {
                    for (int r = 0; r < _gridRows; r++)
                    {
                        int typeID = _cells[l][c, r];
                        if (typeID == 0) continue;

                        float cellX = c * baseSize - offsetX;
                        float cellY = offsetY - r * baseSize;

                        if (_layerOffsetX != null && l < _layerOffsetX.Length && _layerOffsetX[l]) cellX += baseSize * 0.5f;
                        if (_layerOffsetY != null && l < _layerOffsetY.Length && _layerOffsetY[l]) cellY -= baseSize * 0.5f;

                        list.Add(new TileData
                        {
                            tileID          = nextID++,
                            typeID          = typeID,
                            visualPosition  = new Vector3(cellX, cellY, l),
                            blockingTileIDs = new List<int>()
                        });
                    }
                }
            }

            return list;
        }

        private void BakeBlockingRelationships(List<TileData> tiles)
        {
            var byLayer = new Dictionary<int, List<TileData>>();
            foreach (var t in tiles)
            {
                int layer = Mathf.RoundToInt(t.visualPosition.z);
                if (!byLayer.ContainsKey(layer))
                    byLayer[layer] = new List<TileData>();
                byLayer[layer].Add(t);
            }

            // Footprint: each tile occupies [x-halfW, x+halfW] x [y-halfH, y+halfH] in world space
            float halfSize = GetBaseTileSize() * 0.5f;
            float halfX = halfSize;
            float halfY = halfSize;
            const float epsilon = 0.001f;

            foreach (var lower in tiles)
            {
                lower.blockingTileIDs.Clear();

                int upperLayer = Mathf.RoundToInt(lower.visualPosition.z) + 1;
                if (!byLayer.ContainsKey(upperLayer)) continue;

                float lx = lower.visualPosition.x;
                float ly = lower.visualPosition.y;

                foreach (var upper in byLayer[upperLayer])
                {
                    float ux = upper.visualPosition.x;
                    float uy = upper.visualPosition.y;

                    float aMaxX = lx + halfX;
                    float aMinX = lx - halfX;
                    float bMaxX = ux + halfX;
                    float bMinX = ux - halfX;

                    float aMaxY = ly + halfY;
                    float aMinY = ly - halfY;
                    float bMaxY = uy + halfY;
                    float bMinY = uy - halfY;

                    bool overlapsX = aMaxX - bMinX > epsilon && bMaxX - aMinX > epsilon;
                    bool overlapsY = aMaxY - bMinY > epsilon && bMaxY - aMinY > epsilon;

                    if (overlapsX && overlapsY)
                        lower.blockingTileIDs.Add(upper.tileID);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private Rect GetCellRect(Rect canvas, int col, int row, int layer)
        {
            float offsetX = 0f;
            float offsetY = 0f;
            if (_layerOffsetX != null && layer < _layerOffsetX.Length && _layerOffsetX[layer]) offsetX = CellSize * 0.5f;
            if (_layerOffsetY != null && layer < _layerOffsetY.Length && _layerOffsetY[layer]) offsetY = CellSize * 0.5f;

            return new Rect(
                canvas.x + GridPadding + col * CellSize + offsetX,
                canvas.y + GridPadding + row * CellSize + offsetY,
                CellSize, CellSize);
        }

        /// <summary>Reconstruct the baked tileID for a cell after baking.</summary>
        private int EncodeID(int col, int row, int layer)
        {
            int id = 1;
            for (int l = 0; l < layer; l++)
                for (int c = 0; c < _gridCols; c++)
                    for (int r = 0; r < _gridRows; r++)
                        if (_cells[l][c, r] != 0) id++;

            for (int c = 0; c < col; c++)
                for (int r = 0; r < _gridRows; r++)
                    if (_cells[layer][c, r] != 0) id++;

            for (int r = 0; r < row; r++)
                if (_cells[layer][col, r] != 0) id++;

            return id;
        }

        private static Color GetTypeColor(int typeID)
        {
            float hue = (typeID * 0.618033988f) % 1f;
            return Color.HSVToRGB(hue, 0.72f, 0.85f);
        }

        private void ResizeCells()
        {
            var old       = _cells;
            var oldOffX   = _layerOffsetX;
            var oldOffY   = _layerOffsetY;
            int oldLayers = old?.Length ?? 0;

            _cells = new int[_layerCount][,];
            _layerOffsetX = new bool[_layerCount];
            _layerOffsetY = new bool[_layerCount];

            for (int l = 0; l < _layerCount; l++)
            {
                _cells[l] = new int[_gridCols, _gridRows];
                if (l < oldLayers && old[l] != null)
                {
                    if (oldOffX != null && l < oldOffX.Length) _layerOffsetX[l] = oldOffX[l];
                    if (oldOffY != null && l < oldOffY.Length) _layerOffsetY[l] = oldOffY[l];

                    int oldCols = old[l].GetLength(0);
                    int oldRows = old[l].GetLength(1);
                    for (int c = 0; c < Mathf.Min(_gridCols, oldCols); c++)
                        for (int r = 0; r < Mathf.Min(_gridRows, oldRows); r++)
                            _cells[l][c, r] = old[l][c, r];
                }
            }

            _activeLayer    = Mathf.Clamp(_activeLayer, 0, _layerCount - 1);
            _showBlocking   = false;
            _lastValidation = null;
        }

        private void CopyLayer()
        {
            _clipboard = (int[,])_cells[_activeLayer].Clone();
        }

        private void PasteLayer()
        {
            if (_clipboard == null) return;

            int srcCols = _clipboard.GetLength(0);
            int srcRows = _clipboard.GetLength(1);

            Undo.RecordObject(this, "Paste Layer");
            for (int c = 0; c < Mathf.Min(_gridCols, srcCols); c++)
                for (int r = 0; r < Mathf.Min(_gridRows, srcRows); r++)
                    _cells[_activeLayer][c, r] = _clipboard[c, r];

            Repaint();
        }

        private void ProcessKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                if (e.delta.y < 0 && _activeLayer > 0)
                {
                    _activeLayer--;
                    e.Use();
                }
                else if (e.delta.y > 0 && _activeLayer < _layerCount - 1)
                {
                    _activeLayer++;
                    e.Use();
                }
            }

            if (e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.P:
                    _eraseMode = false;
                    e.Use();
                    break;
                case KeyCode.E:
                    _eraseMode = true;
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    if (_activeLayer < _layerCount - 1) { _activeLayer++; e.Use(); }
                    break;
                case KeyCode.DownArrow:
                    if (_activeLayer > 0) { _activeLayer--; e.Use(); }
                    break;
            }
            Repaint();
        }

        private void ImportFromLevel()
        {
            if (_level == null || _level.EditorActiveTiles.Count == 0) return;

            int maxCol = 0, maxRow = 0, maxLayer = 0;
            foreach (var t in _level.EditorActiveTiles)
            {
                int c = Mathf.RoundToInt(t.visualPosition.x / 0.5f);
                int r = Mathf.RoundToInt(t.visualPosition.y / 0.5f);
                int l = Mathf.RoundToInt(t.visualPosition.z);
                maxCol   = Mathf.Max(maxCol, c);
                maxRow   = Mathf.Max(maxRow, r);
                maxLayer = Mathf.Max(maxLayer, l);
            }

            _gridCols   = maxCol + 1;
            _gridRows   = maxRow + 1;
            _layerCount = maxLayer + 1;
            InitCells();

            foreach (var t in _level.EditorActiveTiles)
            {
                int c = Mathf.RoundToInt(t.visualPosition.x / 0.5f);
                int r = Mathf.RoundToInt(t.visualPosition.y / 0.5f);
                int l = Mathf.RoundToInt(t.visualPosition.z);
                if (c < _gridCols && r < _gridRows && l < _layerCount)
                    _cells[l][c, r] = t.typeID;
            }
        }

        private void CreateNewLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create LevelData", "LevelData_01", "asset", "Choose save location");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            
            RefreshLevelList();
            _level = asset;
            SyncSelectedLevelIndex();
        }

        private void CreateNextLevel()
        {
            string folderPath = "Assets/_Project/Resources/Levels";
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Levels")) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Levels");

            int nextIndex = _allLevels.Count + 1;
            int highest = 0;
            foreach (var l in _allLevels)
            {
                var match = System.Text.RegularExpressions.Regex.Match(l.name, @"\d+");
                if (match.Success)
                {
                    if (int.TryParse(match.Value, out int val) && val > highest) highest = val;
                }
            }
            if (highest > 0) nextIndex = highest + 1;

            string assetName = $"LevelData_{nextIndex:D2}.asset";
            string fullPath = $"{folderPath}/{assetName}";
            
            LevelData newLevel = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(newLevel, fullPath);
            AssetDatabase.SaveAssets();

            RefreshLevelList();
            
            _level = newLevel;
            SyncSelectedLevelIndex();
        }

        private void RefreshLevelList()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            _allLevels.Clear();
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var l = AssetDatabase.LoadAssetAtPath<LevelData>(p);
                if (l != null) _allLevels.Add(l);
            }
            
            _allLevels.Sort((a, b) => string.Compare(a.name, b.name));

            _levelNames = new string[_allLevels.Count];
            for (int i = 0; i < _allLevels.Count; i++)
            {
                _levelNames[i] = _allLevels[i].name;
            }

            SyncSelectedLevelIndex();
        }

        private void SyncSelectedLevelIndex()
        {
            _selectedLevelIndex = -1;
            if (_level != null)
            {
                _selectedLevelIndex = _allLevels.IndexOf(_level);
            }
        }
    }
}
