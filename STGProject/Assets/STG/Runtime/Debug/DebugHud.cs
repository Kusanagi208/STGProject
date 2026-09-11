#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Supplies allocation-free runtime geometry and counters to the development Debug HUD.
    /// </summary>
    internal interface IDebugHudDataSource
    {
        /// <summary>Gets the maximum geometry and counter capacities for the current stage.</summary>
        DebugHudCapacities Capacities { get; }

        /// <summary>Writes the current stage diagnostics into a reusable frame buffer.</summary>
        void Populate(DebugHudFrameBuffer frameBuffer, GameSetting gameSetting);
    }

    /// <summary>
    /// Identifies a Debug HUD primitive's visual meaning.
    /// </summary>
    internal enum DebugHudVisual : byte
    {
        DamageRect,
        PickupRect,
        FirePoint,
        Spawner,
        TriggeredSpawner,
        MoveArea,
        EnemyRecycleArea,
        BulletRecycleArea,
        WeaponRecycleArea,
        PickupRecycleArea
    }

    internal enum DebugHudPrimitiveType : byte
    {
        Rect,
        Point,
        Line
    }

    /// <summary>
    /// Defines fixed upper bounds used by one stage's Debug HUD.
    /// </summary>
    internal readonly struct DebugHudCapacities
    {
        internal DebugHudCapacities(
            int primitiveCapacity,
            int enemyCapacity,
            int bulletCapacity,
            int weaponCapacity,
            int pickupCapacity,
            int spawnerCapacity)
        {
            PrimitiveCapacity = Mathf.Max(1, primitiveCapacity);
            EnemyCapacity = Mathf.Max(0, enemyCapacity);
            BulletCapacity = Mathf.Max(0, bulletCapacity);
            WeaponCapacity = Mathf.Max(0, weaponCapacity);
            PickupCapacity = Mathf.Max(0, pickupCapacity);
            SpawnerCapacity = Mathf.Max(0, spawnerCapacity);
        }

        internal int PrimitiveCapacity { get; }
        internal int EnemyCapacity { get; }
        internal int BulletCapacity { get; }
        internal int WeaponCapacity { get; }
        internal int PickupCapacity { get; }
        internal int SpawnerCapacity { get; }
    }

    /// <summary>
    /// Contains the changing object counts displayed by the Debug HUD legend.
    /// </summary>
    internal readonly struct DebugHudCounts
    {
        internal DebugHudCounts(
            int enemyCount,
            int bulletCount,
            int weaponCount,
            int pickupCount,
            int triggeredSpawnerCount,
            int spawnerCount)
        {
            EnemyCount = enemyCount;
            BulletCount = bulletCount;
            WeaponCount = weaponCount;
            PickupCount = pickupCount;
            TriggeredSpawnerCount = triggeredSpawnerCount;
            SpawnerCount = spawnerCount;
        }

        internal int EnemyCount { get; }
        internal int BulletCount { get; }
        internal int WeaponCount { get; }
        internal int PickupCount { get; }
        internal int TriggeredSpawnerCount { get; }
        internal int SpawnerCount { get; }
    }

    internal readonly struct DebugHudPrimitive
    {
        internal DebugHudPrimitive(
            DebugHudPrimitiveType type,
            Vector3 start,
            Vector3 end,
            Rect rect,
            DebugHudVisual visual,
            bool dashed)
        {
            Type = type;
            Start = start;
            End = end;
            Rect = rect;
            Visual = visual;
            Dashed = dashed;
        }

        internal DebugHudPrimitiveType Type { get; }
        internal Vector3 Start { get; }
        internal Vector3 End { get; }
        internal Rect Rect { get; }
        internal DebugHudVisual Visual { get; }
        internal bool Dashed { get; }
    }

    /// <summary>
    /// Reuses a fixed primitive array while collecting one rendered Debug HUD frame.
    /// </summary>
    internal sealed class DebugHudFrameBuffer
    {
        private DebugHudPrimitive[] m_primitives;
        private int m_count;

        internal DebugHudFrameBuffer(int capacity)
        {
            m_primitives = new DebugHudPrimitive[Mathf.Max(1, capacity)];
        }

        internal int Count => m_count;

        internal DebugHudCounts Counts { get; set; }

        internal DebugHudPrimitive this[int index] => m_primitives[index];

        /// <summary>Reallocates the backing array once when a new stage is bound.</summary>
        internal void SetCapacity(int capacity)
        {
            int resolvedCapacity = Mathf.Max(1, capacity);
            if (m_primitives.Length == resolvedCapacity)
            {
                return;
            }

            m_primitives = new DebugHudPrimitive[resolvedCapacity];
            m_count = 0;
        }

        /// <summary>Clears the logical contents without clearing or reallocating the backing array.</summary>
        internal void Clear()
        {
            m_count = 0;
            Counts = default;
        }

        /// <summary>Adds a world-space rectangle when fixed capacity remains.</summary>
        internal void AddRect(Rect rect, DebugHudVisual visual, bool dashed)
        {
            Add(new DebugHudPrimitive(DebugHudPrimitiveType.Rect, default, default, rect, visual, dashed));
        }

        /// <summary>Adds a world-space point marker when fixed capacity remains.</summary>
        internal void AddPoint(Vector3 position, DebugHudVisual visual)
        {
            Add(new DebugHudPrimitive(DebugHudPrimitiveType.Point, position, default, default, visual, false));
        }

        /// <summary>Adds a world-space line when fixed capacity remains.</summary>
        internal void AddLine(Vector3 start, Vector3 end, DebugHudVisual visual, bool dashed)
        {
            Add(new DebugHudPrimitive(DebugHudPrimitiveType.Line, start, end, default, visual, dashed));
        }

        private void Add(DebugHudPrimitive primitive)
        {
            if (m_count >= m_primitives.Length)
            {
                return;
            }

            m_primitives[m_count++] = primitive;
        }
    }

    /// <summary>
    /// Owns the persistent Development Build Debug HUD and its stage binding.
    /// </summary>
    internal sealed class DebugHudController : IDisposable
    {
        private const int k_initialPrimitiveCapacity = 16;

        private readonly GameSetting m_gameSetting;
        private readonly GameObject m_root;
        private readonly DebugHudGraphic m_graphic;
        private readonly DebugHudFrameBuffer m_frameBuffer;
        private readonly DebugHudCountLabel m_enemyCount;
        private readonly DebugHudCountLabel m_bulletCount;
        private readonly DebugHudCountLabel m_weaponCount;
        private readonly DebugHudCountLabel m_pickupCount;
        private readonly DebugHudCountLabel m_triggeredSpawnerCount;
        private readonly DebugHudCountLabel m_spawnerCount;

        private IDebugHudDataSource m_dataSource;
        private bool m_isVisible;

        private DebugHudController(Camera camera, Canvas canvas, GameSetting gameSetting)
        {
            m_gameSetting = gameSetting;
            m_frameBuffer = new DebugHudFrameBuffer(k_initialPrimitiveCapacity);
            m_root = new GameObject("DebugHud", typeof(RectTransform));
            RectTransform rootRect = (RectTransform)m_root.transform;
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsLastSibling();

            GameObject graphicObject = new GameObject(
                "Geometry",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DebugHudGraphic));
            RectTransform graphicRect = (RectTransform)graphicObject.transform;
            graphicRect.SetParent(rootRect, false);
            graphicRect.anchorMin = Vector2.zero;
            graphicRect.anchorMax = Vector2.one;
            graphicRect.offsetMin = Vector2.zero;
            graphicRect.offsetMax = Vector2.zero;
            m_graphic = graphicObject.GetComponent<DebugHudGraphic>();
            m_graphic.Initialize(camera, m_frameBuffer);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform panel = CreatePanel(rootRect);
            CreateText(panel, "F1  DEBUG HUD", new Vector2(4f, -2f), new Vector2(82f, 10f), font, Color.white);
            CreateText(panel, "DamageRect", new Vector2(4f, -13f), new Vector2(82f, 9f), font, DebugHudGraphic.DamageColor);
            CreateText(panel, "PickupRect", new Vector2(4f, -22f), new Vector2(82f, 9f), font, DebugHudGraphic.PickupColor);
            CreateText(panel, "FirePoint", new Vector2(4f, -31f), new Vector2(82f, 9f), font, DebugHudGraphic.FirePointColor);
            CreateText(panel, "Spawner", new Vector2(4f, -40f), new Vector2(82f, 9f), font, DebugHudGraphic.SpawnerColor);
            CreateText(panel, "Move Area", new Vector2(4f, -49f), new Vector2(82f, 9f), font, DebugHudGraphic.MoveAreaColor);
            CreateText(panel, "Enemy Area", new Vector2(4f, -58f), new Vector2(82f, 9f), font, DebugHudGraphic.EnemyRecycleColor);
            CreateText(panel, "Bullet Area", new Vector2(4f, -67f), new Vector2(82f, 9f), font, DebugHudGraphic.BulletRecycleColor);
            CreateText(panel, "Weapon Area", new Vector2(4f, -76f), new Vector2(82f, 9f), font, DebugHudGraphic.WeaponRecycleColor);
            CreateText(panel, "Pickup Area", new Vector2(4f, -85f), new Vector2(82f, 9f), font, DebugHudGraphic.PickupRecycleColor);

            m_enemyCount = CreateCountRow(panel, "Enemies", 92f, -4f, font);
            m_bulletCount = CreateCountRow(panel, "Bullets", 92f, -16f, font);
            m_weaponCount = CreateCountRow(panel, "Weapons", 92f, -28f, font);
            m_pickupCount = CreateCountRow(panel, "Pickups", 92f, -40f, font);
            m_triggeredSpawnerCount = CreateCountRow(panel, "Triggered", 92f, -52f, font);
            m_spawnerCount = CreateCountRow(panel, "Spawners", 92f, -64f, font);

            ConfigureCountCapacities(default);
            m_root.SetActive(false);
        }

        /// <summary>Creates the HUD when its serialized camera and Canvas dependencies are valid.</summary>
        internal static DebugHudController TryCreate(
            Camera camera,
            Canvas canvas,
            GameSetting gameSetting,
            UnityEngine.Object logContext)
        {
            if (camera == null || canvas == null)
            {
                Debug.LogWarning("Development Debug HUD requires serialized Camera and Canvas references.", logContext);
                return null;
            }

            return new DebugHudController(camera, canvas, gameSetting);
        }

        /// <summary>Binds the loaded stage as the HUD's allocation-free data source.</summary>
        internal void Bind(IDebugHudDataSource dataSource)
        {
            m_dataSource = dataSource;
            DebugHudCapacities capacities = dataSource.Capacities;
            m_frameBuffer.SetCapacity(capacities.PrimitiveCapacity);
            ConfigureCountCapacities(capacities);
        }

        /// <summary>Unbinds the specified stage if it is still the active data source.</summary>
        internal void Unbind(IDebugHudDataSource dataSource)
        {
            if (!ReferenceEquals(m_dataSource, dataSource))
            {
                return;
            }

            m_dataSource = null;
            m_frameBuffer.Clear();
            m_graphic.SetVerticesDirty();
            UpdateCounts(default);
        }

        /// <summary>Toggles Debug HUD visibility for the current application session.</summary>
        internal void Toggle()
        {
            m_isVisible = !m_isVisible;
            m_root.SetActive(m_isVisible);
        }

        /// <summary>Refreshes visible geometry and counters once per rendered frame.</summary>
        internal void Refresh()
        {
            if (!m_isVisible)
            {
                return;
            }

            m_frameBuffer.Clear();
            m_dataSource?.Populate(m_frameBuffer, m_gameSetting);
            m_graphic.SetVerticesDirty();
            UpdateCounts(m_frameBuffer.Counts);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            m_dataSource = null;
            if (m_root != null)
            {
                UnityEngine.Object.Destroy(m_root);
            }
        }

        private void ConfigureCountCapacities(DebugHudCapacities capacities)
        {
            m_enemyCount.Configure(capacities.EnemyCapacity);
            m_bulletCount.Configure(capacities.BulletCapacity);
            m_weaponCount.Configure(capacities.WeaponCapacity);
            m_pickupCount.Configure(capacities.PickupCapacity);
            m_triggeredSpawnerCount.Configure(capacities.SpawnerCapacity);
            m_spawnerCount.Configure(capacities.SpawnerCapacity);
        }

        private void UpdateCounts(DebugHudCounts counts)
        {
            m_enemyCount.SetValue(counts.EnemyCount);
            m_bulletCount.SetValue(counts.BulletCount);
            m_weaponCount.SetValue(counts.WeaponCount);
            m_pickupCount.SetValue(counts.PickupCount);
            m_triggeredSpawnerCount.SetValue(counts.TriggeredSpawnerCount);
            m_spawnerCount.SetValue(counts.SpawnerCount);
        }

        private static RectTransform CreatePanel(RectTransform parent)
        {
            GameObject panelObject = new GameObject("Legend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = (RectTransform)panelObject.transform;
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(3f, -3f);
            panelRect.sizeDelta = new Vector2(181f, 96f);
            Image image = panelObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = false;
            return panelRect;
        }

        private static DebugHudCountLabel CreateCountRow(
            RectTransform parent,
            string label,
            float x,
            float y,
            Font font)
        {
            CreateText(parent, label, new Vector2(x, y), new Vector2(60f, 10f), font, Color.white);
            Text valueText = CreateText(
                parent,
                string.Empty,
                new Vector2(x + 62f, y),
                new Vector2(22f, 10f),
                font,
                Color.white);
            valueText.alignment = TextAnchor.UpperRight;
            return new DebugHudCountLabel(valueText);
        }

        private static Text CreateText(
            RectTransform parent,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            Font font,
            Color color)
        {
            GameObject textObject = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 7;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }
    }

    internal sealed class DebugHudCountLabel
    {
        private readonly Text m_text;
        private string[] m_cachedValues;
        private int m_currentValue = -1;

        internal DebugHudCountLabel(Text text)
        {
            m_text = text;
        }

        /// <summary>Prebuilds every valid counter string for the bound pool capacity.</summary>
        internal void Configure(int maximumValue)
        {
            int resolvedMaximum = Mathf.Max(0, maximumValue);
            m_cachedValues = new string[resolvedMaximum + 1];
            for (int index = 0; index <= resolvedMaximum; index++)
            {
                m_cachedValues[index] = index.ToString(CultureInfo.InvariantCulture);
            }

            m_currentValue = -1;
            SetValue(0);
        }

        /// <summary>Applies a cached counter string only when the value changes.</summary>
        internal void SetValue(int value)
        {
            int safeValue = Mathf.Clamp(value, 0, m_cachedValues.Length - 1);
            if (safeValue == m_currentValue)
            {
                return;
            }

            m_currentValue = safeValue;
            m_text.text = m_cachedValues[safeValue];
        }
    }

    /// <summary>
    /// Renders all Debug HUD world primitives through one uGUI geometry batch.
    /// </summary>
    internal sealed class DebugHudGraphic : Graphic
    {
        private const float k_lineThickness = 0.8f;
        private const float k_pointHalfSize = 2.5f;
        private const float k_dashLength = 3f;
        private const float k_dashGap = 2f;

        private static readonly Color32 s_damageColor = new Color32(255, 64, 64, 235);
        private static readonly Color32 s_pickupColor = new Color32(64, 239, 255, 235);
        private static readonly Color32 s_firePointColor = new Color32(255, 224, 48, 255);
        private static readonly Color32 s_spawnerColor = new Color32(255, 64, 224, 235);
        private static readonly Color32 s_moveAreaColor = new Color32(80, 255, 112, 210);
        private static readonly Color32 s_enemyRecycleColor = new Color32(255, 144, 48, 210);
        private static readonly Color32 s_triggeredSpawnerColor = new Color32(144, 144, 144, 210);
        private static readonly Color32 s_bulletRecycleColor = new Color32(176, 96, 255, 210);
        private static readonly Color32 s_weaponRecycleColor = new Color32(176, 255, 64, 210);
        private static readonly Color32 s_pickupRecycleColor = new Color32(64, 144, 255, 210);

        /// <summary>Gets the DamageRect legend color.</summary>
        internal static Color32 DamageColor => s_damageColor;

        /// <summary>Gets the PickupRect legend color.</summary>
        internal static Color32 PickupColor => s_pickupColor;

        /// <summary>Gets the FirePoint legend color.</summary>
        internal static Color32 FirePointColor => s_firePointColor;

        /// <summary>Gets the active spawner legend color.</summary>
        internal static Color32 SpawnerColor => s_spawnerColor;

        /// <summary>Gets the player movement-area legend color.</summary>
        internal static Color32 MoveAreaColor => s_moveAreaColor;

        /// <summary>Gets the enemy recycle-area legend color.</summary>
        internal static Color32 EnemyRecycleColor => s_enemyRecycleColor;

        /// <summary>Gets the enemy-bullet recycle-area legend color.</summary>
        internal static Color32 BulletRecycleColor => s_bulletRecycleColor;

        /// <summary>Gets the player-weapon recycle-area legend color.</summary>
        internal static Color32 WeaponRecycleColor => s_weaponRecycleColor;

        /// <summary>Gets the pickup recycle-area legend color.</summary>
        internal static Color32 PickupRecycleColor => s_pickupRecycleColor;

        private Camera m_camera;
        private DebugHudFrameBuffer m_frameBuffer;

        /// <summary>Initializes the renderer with persistent camera and frame-buffer references.</summary>
        internal void Initialize(Camera camera, DebugHudFrameBuffer frameBuffer)
        {
            m_camera = camera;
            m_frameBuffer = frameBuffer;
            raycastTarget = false;
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (m_camera == null || m_frameBuffer == null)
            {
                return;
            }

            Rect viewportRect = rectTransform.rect;
            for (int index = 0; index < m_frameBuffer.Count; index++)
            {
                DebugHudPrimitive primitive = m_frameBuffer[index];
                Color32 color = GetColor(primitive.Visual);
                switch (primitive.Type)
                {
                    case DebugHudPrimitiveType.Rect:
                        DrawRect(vertexHelper, primitive.Rect, viewportRect, color, primitive.Dashed);
                        break;
                    case DebugHudPrimitiveType.Point:
                        DrawPoint(vertexHelper, primitive.Start, viewportRect, color);
                        break;
                    case DebugHudPrimitiveType.Line:
                        DrawWorldLine(
                            vertexHelper,
                            primitive.Start,
                            primitive.End,
                            viewportRect,
                            color,
                            primitive.Dashed);
                        break;
                }
            }
        }

        /// <summary>Clips a local-space line to the supplied viewport rectangle.</summary>
        internal static bool TryClipLine(Rect clipRect, ref Vector2 start, ref Vector2 end)
        {
            int startCode = GetOutCode(clipRect, start);
            int endCode = GetOutCode(clipRect, end);
            while (true)
            {
                if ((startCode | endCode) == 0)
                {
                    return true;
                }

                if ((startCode & endCode) != 0)
                {
                    return false;
                }

                int outCode = startCode != 0 ? startCode : endCode;
                Vector2 clipped = default;
                if ((outCode & 8) != 0)
                {
                    float deltaY = end.y - start.y;
                    if (Mathf.Approximately(deltaY, 0f))
                    {
                        return false;
                    }

                    clipped.x = start.x + ((end.x - start.x) * (clipRect.yMax - start.y) / deltaY);
                    clipped.y = clipRect.yMax;
                }
                else if ((outCode & 4) != 0)
                {
                    float deltaY = end.y - start.y;
                    if (Mathf.Approximately(deltaY, 0f))
                    {
                        return false;
                    }

                    clipped.x = start.x + ((end.x - start.x) * (clipRect.yMin - start.y) / deltaY);
                    clipped.y = clipRect.yMin;
                }
                else if ((outCode & 2) != 0)
                {
                    float deltaX = end.x - start.x;
                    if (Mathf.Approximately(deltaX, 0f))
                    {
                        return false;
                    }

                    clipped.y = start.y + ((end.y - start.y) * (clipRect.xMax - start.x) / deltaX);
                    clipped.x = clipRect.xMax;
                }
                else
                {
                    float deltaX = end.x - start.x;
                    if (Mathf.Approximately(deltaX, 0f))
                    {
                        return false;
                    }

                    clipped.y = start.y + ((end.y - start.y) * (clipRect.xMin - start.x) / deltaX);
                    clipped.x = clipRect.xMin;
                }

                if (outCode == startCode)
                {
                    start = clipped;
                    startCode = GetOutCode(clipRect, start);
                }
                else
                {
                    end = clipped;
                    endCode = GetOutCode(clipRect, end);
                }
            }
        }

        private void DrawRect(
            VertexHelper vertexHelper,
            Rect worldRect,
            Rect viewportRect,
            Color32 color,
            bool dashed)
        {
            if (!TryWorldToLocal(new Vector3(worldRect.xMin, worldRect.yMin, 0f), viewportRect, out Vector2 min) ||
                !TryWorldToLocal(new Vector3(worldRect.xMax, worldRect.yMax, 0f), viewportRect, out Vector2 max))
            {
                return;
            }

            Vector2 bottomRight = new Vector2(max.x, min.y);
            Vector2 topLeft = new Vector2(min.x, max.y);
            DrawLocalLine(vertexHelper, min, bottomRight, viewportRect, color, dashed);
            DrawLocalLine(vertexHelper, bottomRight, max, viewportRect, color, dashed);
            DrawLocalLine(vertexHelper, max, topLeft, viewportRect, color, dashed);
            DrawLocalLine(vertexHelper, topLeft, min, viewportRect, color, dashed);
        }

        private void DrawPoint(VertexHelper vertexHelper, Vector3 worldPoint, Rect viewportRect, Color32 color)
        {
            if (!TryWorldToLocal(worldPoint, viewportRect, out Vector2 point))
            {
                return;
            }

            DrawLocalLine(
                vertexHelper,
                new Vector2(point.x - k_pointHalfSize, point.y),
                new Vector2(point.x + k_pointHalfSize, point.y),
                viewportRect,
                color,
                false);
            DrawLocalLine(
                vertexHelper,
                new Vector2(point.x, point.y - k_pointHalfSize),
                new Vector2(point.x, point.y + k_pointHalfSize),
                viewportRect,
                color,
                false);
        }

        private void DrawWorldLine(
            VertexHelper vertexHelper,
            Vector3 worldStart,
            Vector3 worldEnd,
            Rect viewportRect,
            Color32 color,
            bool dashed)
        {
            if (!TryWorldToLocal(worldStart, viewportRect, out Vector2 start) ||
                !TryWorldToLocal(worldEnd, viewportRect, out Vector2 end))
            {
                return;
            }

            DrawLocalLine(vertexHelper, start, end, viewportRect, color, dashed);
        }

        private void DrawLocalLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            Rect viewportRect,
            Color32 color,
            bool dashed)
        {
            if (!TryClipLine(viewportRect, ref start, ref end))
            {
                return;
            }

            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            if (!dashed)
            {
                AddLineQuad(vertexHelper, start, end, color);
                return;
            }

            Vector2 direction = delta / length;
            float distance = 0f;
            while (distance < length)
            {
                float segmentEnd = Mathf.Min(distance + k_dashLength, length);
                AddLineQuad(
                    vertexHelper,
                    start + (direction * distance),
                    start + (direction * segmentEnd),
                    color);
                distance += k_dashLength + k_dashGap;
            }
        }

        private static void AddLineQuad(VertexHelper vertexHelper, Vector2 start, Vector2 end, Color32 color)
        {
            Vector2 direction = end - start;
            float inverseLength = 1f / direction.magnitude;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (k_lineThickness * 0.5f * inverseLength);
            int firstVertex = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = start - normal;
            vertexHelper.AddVert(vertex);
            vertex.position = start + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end - normal;
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
            vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
        }

        private bool TryWorldToLocal(Vector3 worldPosition, Rect viewportRect, out Vector2 localPosition)
        {
            Vector3 viewportPosition = m_camera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0f)
            {
                localPosition = default;
                return false;
            }

            localPosition = new Vector2(
                viewportRect.xMin + (viewportPosition.x * viewportRect.width),
                viewportRect.yMin + (viewportPosition.y * viewportRect.height));
            return true;
        }

        private static int GetOutCode(Rect rect, Vector2 point)
        {
            int code = 0;
            if (point.x < rect.xMin)
            {
                code |= 1;
            }
            else if (point.x > rect.xMax)
            {
                code |= 2;
            }

            if (point.y < rect.yMin)
            {
                code |= 4;
            }
            else if (point.y > rect.yMax)
            {
                code |= 8;
            }

            return code;
        }

        private static Color32 GetColor(DebugHudVisual visual)
        {
            switch (visual)
            {
                case DebugHudVisual.DamageRect:
                    return s_damageColor;
                case DebugHudVisual.PickupRect:
                    return s_pickupColor;
                case DebugHudVisual.FirePoint:
                    return s_firePointColor;
                case DebugHudVisual.Spawner:
                    return s_spawnerColor;
                case DebugHudVisual.TriggeredSpawner:
                    return s_triggeredSpawnerColor;
                case DebugHudVisual.MoveArea:
                    return s_moveAreaColor;
                case DebugHudVisual.EnemyRecycleArea:
                    return s_enemyRecycleColor;
                case DebugHudVisual.BulletRecycleArea:
                    return s_bulletRecycleColor;
                case DebugHudVisual.WeaponRecycleArea:
                    return s_weaponRecycleColor;
                case DebugHudVisual.PickupRecycleArea:
                    return s_pickupRecycleColor;
                default:
                    return Color.white;
            }
        }
    }
}
#endif
