using System.Collections.Generic;
using System.Linq;
using TileMatch.Model;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Editor
{
    public class LevelDesignTool : EditorWindow
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const float CellSize         = 36f;
        private const float HalfCell         = CellSize * 0.5f;
        private const float GridPadding      = 8f;
        private const float PaletteIconSize  = 48f;
        private const float SidebarWidth     = 220f;
        private const float ToolbarHeight    = 26f;
        private const int   MaxTypeID        = 15;
        private const string SpritesPath     = "Assets/_Project/Sprites";

        // ── State: target asset ─────────────────────────────────────────────
        private LevelData _level;

        // ── State: grid configuration ───────────────────────────────────────
        private int _gridCols  = 8;
        private int _gridRows  = 8;
        private int _layerCount = 4;

        // ── State: design-time tile grid ────────────────────────────────────
        // _cells[layer][col, row] = typeID (0 = empty)
        private int[][,] _cells;

        // ── State: layer navigation & tools ─────────────────────────────────
        private int  _activeLayer   = 0;
        private int  _selectedType  = 1;
        private bool _eraseMode     = false;
        private bool _showBlocking  = false;

        // Post-bake blocking map: key = tileID, value = is directly clickable
        private HashSet<int> _bakedClickable = new HashSet<int>();

        // ── State: copy/paste ────────────────────────────────────────────────
        private int[,] _clipboard;

        // ── State: validation ────────────────────────────────────────────────
        private LevelValidator.ValidationResult _lastValidation;

        // ── State: orders ────────────────────────────────────────────────────
        private Vector2 _orderScrollPos;
        private bool    _orderFoldout = true;

        // ── State: palette sprites ───────────────────────────────────────────
        private Sprite[] _palette;
        private bool     _paletteLoaded = false;

        // ── State: scroll ─────────────────────────────────────────────────
        private Vector2 _gridScrollPos;
        private Vector2 _mainScrollPos;

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
                fontSize  = 13,
                alignment = TextAnchor.MiddleLeft
            };

            _labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = false
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
                EditorGUILayout.Space(6);
                DrawPaletteSection();
                GUILayout.FlexibleSpace();
                DrawBakeSection();
                EditorGUILayout.Space(4);
            }
        }

        private void DrawTargetAssetSection()
        {
            GUILayout.Label("Target Level", _headerStyle);
            EditorGUI.BeginChangeCheck();
            _level = (LevelData)EditorGUILayout.ObjectField(_level, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck() && _level != null)
                ImportFromLevel();

            if (_level == null)
            {
                EditorGUILayout.HelpBox("Assign a LevelData asset to begin.", MessageType.Info);
                if (GUILayout.Button("Create New LevelData"))
                    CreateNewLevel();
            }
        }

        private void DrawGridConfigSection()
        {
            GUILayout.Label("Grid Config", _headerStyle);

            EditorGUI.BeginChangeCheck();
            _gridCols   = Mathf.Clamp(EditorGUILayout.IntField("Columns",  _gridCols),  2, 20);
            _gridRows   = Mathf.Clamp(EditorGUILayout.IntField("Rows",     _gridRows),  2, 20);
            _layerCount = Mathf.Clamp(EditorGUILayout.IntField("Z Layers", _layerCount), 1, 12);
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
        }

        private void DrawPaletteSection()
        {
            GUILayout.Label("Tile Type", _headerStyle);

            if (!_paletteLoaded) return;

            int perRow = Mathf.Max(1, Mathf.FloorToInt(SidebarWidth / (PaletteIconSize + 4)));

            EditorGUILayout.BeginVertical();
            for (int i = 1; i <= MaxTypeID; i += perRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < perRow && (i + j) <= MaxTypeID; j++)
                {
                    int typeID = i + j;
                    bool selected = _selectedType == typeID;

                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = selected ? Color.yellow : prev;

                    GUIContent content = _palette[typeID] != null
                        ? new GUIContent(_palette[typeID].texture, $"Type {typeID}")
                        : new GUIContent(typeID.ToString());

                    if (GUILayout.Button(content, GUILayout.Width(PaletteIconSize), GUILayout.Height(PaletteIconSize)))
                    {
                        _selectedType = typeID;
                        _eraseMode    = false;
                    }

                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
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
                DrawGrid();
                EditorGUILayout.Space(12);
                DrawOrderEditor();
                DrawValidationPanel();
                EditorGUILayout.EndScrollView();
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
                GUILayout.Label($"Grid: {_gridCols}×{_gridRows}  |  Active: Z{_activeLayer}", EditorStyles.miniLabel);
                GUILayout.Space(8);
            }
        }

        private void DrawGrid()
        {
            if (_cells == null || _cells.Length != _layerCount) return;

            float totalW = _gridCols * CellSize + GridPadding * 2;
            float totalH = _gridRows * CellSize + GridPadding * 2;

            _gridScrollPos = EditorGUILayout.BeginScrollView(_gridScrollPos,
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

                        Rect cellRect = GetCellRect(canvas, c, r);
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

                    Rect cellRect = GetCellRect(canvas, c, r);

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
        }

        private void HandleGridInput(Rect canvas)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (!canvas.Contains(e.mousePosition)) return;
            if (e.button != 0) return;

            int col = Mathf.FloorToInt((e.mousePosition.x - canvas.x - GridPadding) / CellSize);
            int row = Mathf.FloorToInt((e.mousePosition.y - canvas.y - GridPadding) / CellSize);

            if (col < 0 || col >= _gridCols || row < 0 || row >= _gridRows) return;

            int newValue = _eraseMode ? 0 : _selectedType;
            if (_cells[_activeLayer][col, row] == newValue) return;

            Undo.RecordObject(this, _eraseMode ? "Erase Tile" : "Paint Tile");
            _cells[_activeLayer][col, row] = newValue;

            _showBlocking   = false;
            _lastValidation = null;

            e.Use();
            Repaint();
        }

        // ══════════════════════════════════════════════════════════════════════
        // ORDER EDITOR
        // ══════════════════════════════════════════════════════════════════════
        private void DrawOrderEditor()
        {
            if (_level == null) return;

            _orderFoldout = EditorGUILayout.Foldout(_orderFoldout, "Pending Orders", true, EditorStyles.foldoutHeader);
            if (!_orderFoldout) return;

            EditorGUI.indentLevel++;
            _orderScrollPos = EditorGUILayout.BeginScrollView(_orderScrollPos, GUILayout.MaxHeight(180));

            for (int i = 0; i < _level.pendingOrders.Count; i++)
            {
                OrderData order = _level.pendingOrders[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"Order {i + 1}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            Undo.RecordObject(_level, "Remove Order");
                            _level.pendingOrders.RemoveAt(i);
                            EditorUtility.SetDirty(_level);
                            break;
                        }
                    }

                    for (int j = 0; j < order.requiredTypeIDs.Count; j++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            int newType = EditorGUILayout.IntSlider($"  Slot {j + 1}", order.requiredTypeIDs[j], 1, MaxTypeID);
                            if (newType != order.requiredTypeIDs[j])
                            {
                                Undo.RecordObject(_level, "Edit Order Slot");
                                order.requiredTypeIDs[j] = newType;
                                EditorUtility.SetDirty(_level);
                            }

                            if (_palette != null && newType >= 1 && newType <= MaxTypeID && _palette[newType] != null)
                                GUILayout.Label(new GUIContent(_palette[newType].texture), GUILayout.Width(22), GUILayout.Height(22));

                            if (GUILayout.Button("-", GUILayout.Width(22)))
                            {
                                Undo.RecordObject(_level, "Remove Order Slot");
                                order.requiredTypeIDs.RemoveAt(j);
                                EditorUtility.SetDirty(_level);
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Add Slot"))
                    {
                        Undo.RecordObject(_level, "Add Order Slot");
                        order.requiredTypeIDs.Add(1);
                        EditorUtility.SetDirty(_level);
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Order"))
            {
                Undo.RecordObject(_level, "Add Order");
                _level.pendingOrders.Add(new OrderData());
                EditorUtility.SetDirty(_level);
            }

            EditorGUI.indentLevel--;
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

            List<TileSaveData> allTiles = BuildTileList();
            BakeBlockingRelationships(allTiles);

            _level.activeTiles = allTiles;
            EditorUtility.SetDirty(_level);
            AssetDatabase.SaveAssets();

            _bakedClickable.Clear();
            foreach (var t in allTiles)
                if (t.blockingTileIDs.Count == 0)
                    _bakedClickable.Add(t.tileID);

            _showBlocking   = true;
            _lastValidation = LevelValidator.Validate(_level);

            Debug.Log($"[LevelDesignTool] Baked {allTiles.Count} tiles. " +
                      $"Clickable: {_bakedClickable.Count}. " +
                      $"Solvable: {_lastValidation.IsSolvable}");

            Repaint();
        }

        private List<TileSaveData> BuildTileList()
        {
            var list  = new List<TileSaveData>();
            int nextID = 1;

            for (int l = 0; l < _layerCount; l++)
            {
                for (int c = 0; c < _gridCols; c++)
                {
                    for (int r = 0; r < _gridRows; r++)
                    {
                        int typeID = _cells[l][c, r];
                        if (typeID == 0) continue;

                        list.Add(new TileSaveData
                        {
                            tileID          = nextID++,
                            typeID          = typeID,
                            visualPosition  = new Vector3(c * 0.5f, r * 0.5f, l),
                            blockingTileIDs = new List<int>()
                        });
                    }
                }
            }

            return list;
        }

        private void BakeBlockingRelationships(List<TileSaveData> tiles)
        {
            var byLayer = new Dictionary<int, List<TileSaveData>>();
            foreach (var t in tiles)
            {
                int layer = Mathf.RoundToInt(t.visualPosition.z);
                if (!byLayer.ContainsKey(layer))
                    byLayer[layer] = new List<TileSaveData>();
                byLayer[layer].Add(t);
            }

            // Footprint: each tile occupies [x-0.5, x+0.5] x [y-0.5, y+0.5] in world space
            const float half    = 0.5f;
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

                    bool overlapsX = lx + half - ux > epsilon && ux + half - lx > epsilon;
                    bool overlapsY = ly + half - uy > epsilon && uy + half - ly > epsilon;

                    if (overlapsX && overlapsY)
                        lower.blockingTileIDs.Add(upper.tileID);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private Rect GetCellRect(Rect canvas, int col, int row)
        {
            return new Rect(
                canvas.x + GridPadding + col * CellSize,
                canvas.y + GridPadding + row * CellSize,
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
            int oldLayers = old?.Length ?? 0;

            _cells = new int[_layerCount][,];
            for (int l = 0; l < _layerCount; l++)
            {
                _cells[l] = new int[_gridCols, _gridRows];
                if (l < oldLayers && old[l] != null)
                {
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
            if (_level == null || _level.activeTiles.Count == 0) return;

            int maxCol = 0, maxRow = 0, maxLayer = 0;
            foreach (var t in _level.activeTiles)
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

            foreach (var t in _level.activeTiles)
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
            _level = asset;
        }
    }
}
