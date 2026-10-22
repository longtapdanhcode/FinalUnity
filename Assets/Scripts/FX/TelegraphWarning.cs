using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vanguard.FX
{
    public enum TelegraphShape
    {
        Circle,
        Sector,
        Line
    }

    /// <summary>Mức nguy hiểm quyết định màu cảnh báo (Sprint 8 - 8.4).</summary>
    public enum TelegraphDanger
    {
        Friendly, // đòn của phe người chơi (cyan)
        Low,      // vàng
        Medium,   // cam
        High      // đỏ
    }

    /// <summary>
    /// Vùng cảnh báo nguy hiểm phẳng trên mặt sàn (Sprint 5 - Card 5.4): khung nền mờ thể hiện phạm vi tối đa
    /// và lớp nạp đầy (fill) lớn dần cho tới khi lấp đầy 100% thì đòn đánh kích hoạt.
    /// Nên lấy qua TelegraphManager (có Object Pool) thay vì Instantiate trực tiếp.
    /// </summary>
    public class TelegraphWarning : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private TelegraphShape shape = TelegraphShape.Circle;

        [Header("Layers")]
        [Tooltip("Khung nền: phạm vi tối đa của đòn đánh")]
        [SerializeField] private MeshRenderer baseRenderer = null;
        [Tooltip("Lớp nạp đầy: lớn dần từ 0 tới 100% theo thời gian")]
        [SerializeField] private MeshRenderer fillRenderer = null;
        [Tooltip("Mức nguy hiểm mặc định; có thể đổi lúc gọi Show qua TelegraphManager")]
        [SerializeField] private TelegraphDanger danger = TelegraphDanger.Medium;

        [Header("Placement")]
        [Tooltip("Độ cao so với mặt sàn để tránh Z-fighting")]
        [SerializeField] private float groundOffset = 0.02f;
        [SerializeField] private float fillLayerOffset = 0.005f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LayerId = Shader.PropertyToID("_Layer");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static int groundMask = -1;
        private static Material fallbackMaterial;

        private MeshFilter baseFilter;
        private MeshFilter fillFilter;
        private Transform baseTransform;
        private Transform fillTransform;
        private MaterialPropertyBlock block;
        private bool visualsReady;

        private TelegraphShape currentShape;
        private Vector3 fullScale;
        private float duration;
        private float elapsed;
        private Action onComplete;

        internal TelegraphManager Owner;
        internal int PoolIndex;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Tăng sau mỗi lần Show / kết thúc, để TelegraphHandle không hủy nhầm khi đối tượng đã được Pool tái sử dụng.
        /// </summary>
        public int Ticket { get; private set; }

        public TelegraphShape DefaultShape => shape;

        /// <summary>Đổi mức nguy hiểm (màu) cho lần hiển thị tiếp theo hoặc đang chạy.</summary>
        public void SetDanger(TelegraphDanger level)
        {
            danger = level;
            if (visualsReady) ApplyDangerColors();
        }

        private void Awake()
        {
            EnsureVisuals();
            gameObject.SetActive(false);
        }

        #region Public API
        /// <summary>
        /// Cảnh báo hình tròn (AOE) tại position, nạp đầy sau duration giây rồi gọi onComplete.
        /// </summary>
        public void ShowCircle(Vector3 position, float radius, float duration, Action onComplete = null)
        {
            Begin(TelegraphShape.Circle, position, Quaternion.identity, new Vector3(radius, 1f, radius),
                0f, duration, onComplete);
        }

        /// <summary>
        /// Cảnh báo hình quạt: đỉnh quạt tại position, hướng forward, góc angle (độ), cự ly radius.
        /// </summary>
        public void ShowSector(Vector3 position, Vector3 forward, float angle, float radius, float duration,
            Action onComplete = null)
        {
            Begin(TelegraphShape.Sector, position, FlatRotation(forward), new Vector3(radius, 1f, radius),
                angle, duration, onComplete);
        }

        /// <summary>
        /// Cảnh báo vệt thẳng: bắt đầu tại position, hướng forward, dài length, rộng width.
        /// </summary>
        public void ShowLine(Vector3 position, Vector3 forward, float length, float width, float duration,
            Action onComplete = null)
        {
            Begin(TelegraphShape.Line, position, FlatRotation(forward), new Vector3(width, 1f, length),
                0f, duration, onComplete);
        }

        /// <summary>
        /// Ẩn ngay lập tức và trả về Pool mà không gọi onComplete.
        /// </summary>
        public void Cancel()
        {
            if (IsPlaying) Finish(false);
        }

        /// <summary>
        /// Chỉ hủy nếu ticket còn khớp (cùng lần Show), tránh hủy nhầm khi đã được tái sử dụng.
        /// </summary>
        public void Cancel(int ticket)
        {
            if (ticket == Ticket) Cancel();
        }
        #endregion

        private void Begin(TelegraphShape newShape, Vector3 position, Quaternion rotation, Vector3 scale,
            float sectorAngle, float time, Action callback)
        {
            EnsureVisuals();

            currentShape = newShape;
            fullScale = scale;
            duration = Mathf.Max(0.01f, time);
            elapsed = 0f;
            onComplete = callback;

            Mesh mesh = TelegraphMeshes.Get(newShape, sectorAngle);
            baseFilter.sharedMesh = mesh;
            fillFilter.sharedMesh = mesh;

            transform.SetPositionAndRotation(
                new Vector3(position.x, FindGroundY(position) + groundOffset, position.z), rotation);
            baseTransform.localScale = scale;
            ApplyShapeParams(newShape, sectorAngle);
            ApplyDangerColors();
            ApplyFill(0f);

            Ticket++;
            IsPlaying = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsPlaying) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyFill(t);

            if (t >= 1f) Finish(true);
        }

        private void ApplyFill(float t)
        {
            Vector3 scale = fullScale;
            if (currentShape == TelegraphShape.Line)
            {
                scale.z *= t; // Vệt thẳng lớn dần theo chiều dài
            }
            else
            {
                scale.x *= t; // Tròn / quạt lớn dần từ tâm (đỉnh quạt) ra ngoài
                scale.z *= t;
            }
            fillTransform.localScale = scale;

            fillRenderer.GetPropertyBlock(block);
            block.SetFloat(ProgressId, t);
            fillRenderer.SetPropertyBlock(block);
        }

        private void Finish(bool invokeCallback)
        {
            IsPlaying = false;
            Ticket++;

            Action callback = invokeCallback ? onComplete : null;
            onComplete = null;

            gameObject.SetActive(false);
            if (Owner != null) Owner.Release(this);

            callback?.Invoke();
        }

        private void OnDisable()
        {
            // Bị tắt từ bên ngoài (đổi Scene, tắt manager...) -> không giữ trạng thái đang chạy
            if (IsPlaying)
            {
                IsPlaying = false;
                Ticket++;
                onComplete = null;
            }
        }

        #region Visual setup
        private void EnsureVisuals()
        {
            if (visualsReady) return;
            visualsReady = true;

            if (baseRenderer == null) baseRenderer = CreateLayer("Base", 0, 0f);
            if (fillRenderer == null) fillRenderer = CreateLayer("Fill", 1, fillLayerOffset);

            baseTransform = baseRenderer.transform;
            fillTransform = fillRenderer.transform;
            baseFilter = GetOrAdd<MeshFilter>(baseRenderer.gameObject);
            fillFilter = GetOrAdd<MeshFilter>(fillRenderer.gameObject);
            fillTransform.localPosition = new Vector3(0f, fillLayerOffset, 0f);

            if (baseRenderer.sharedMaterial == null) baseRenderer.sharedMaterial = GetFallbackMaterial();
            if (fillRenderer.sharedMaterial == null) fillRenderer.sharedMaterial = GetFallbackMaterial();

            // Lớp fill vẽ sau lớp nền để luôn nằm phía trên khi sắp xếp vật thể trong suốt
            baseRenderer.sortingOrder = 0;
            fillRenderer.sortingOrder = 1;

            block = new MaterialPropertyBlock();
            SetFloat(baseRenderer, LayerId, 0f);
            SetFloat(fillRenderer, LayerId, 1f);
            ApplyDangerColors();
        }

        private void SetFloat(Renderer renderer, int id, float value)
        {
            renderer.GetPropertyBlock(block);
            block.SetFloat(id, value);
            renderer.SetPropertyBlock(block);
        }

        private void ApplyShapeParams(TelegraphShape newShape, float sectorAngle)
        {
            float halfAngle = newShape == TelegraphShape.Sector ? sectorAngle * 0.5f * Mathf.Deg2Rad : Mathf.PI;
            SetShape(baseRenderer, newShape, halfAngle);
            SetShape(fillRenderer, newShape, halfAngle);
        }

        private void SetShape(Renderer renderer, TelegraphShape newShape, float halfAngle)
        {
            renderer.GetPropertyBlock(block);
            block.SetFloat(ShapeId, (float)(int)newShape);
            block.SetFloat(HalfAngleId, halfAngle);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Màu HDR theo mức nguy hiểm: nền mờ hơn, lớp nạp đầy đậm hơn để bloom nổi trên sàn tối.</summary>
        private void ApplyDangerColors()
        {
            Color hue;
            switch (danger)
            {
                case TelegraphDanger.Friendly: hue = new Color(0.1f, 0.85f, 1f); break;
                case TelegraphDanger.Low: hue = new Color(1f, 0.75f, 0.1f); break;
                case TelegraphDanger.High: hue = new Color(1f, 0.04f, 0.12f); break;
                default: hue = new Color(1f, 0.35f, 0.05f); break;
            }
            ApplyColor(baseRenderer, new Color(hue.r * 1.4f, hue.g * 1.4f, hue.b * 1.4f, 0.6f));
            ApplyColor(fillRenderer, new Color(hue.r * 2.2f, hue.g * 2.2f, hue.b * 2.2f, 0.9f));
        }

        private MeshRenderer CreateLayer(string layerName, int sortingOrder, float yOffset)
        {
            GameObject go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        private static Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            fallbackMaterial = new Material(shader) { name = "M_Telegraph_Fallback" };
            fallbackMaterial.SetColor(BaseColorId, new Color(1f, 0.1f, 0.1f, 0.45f));
            fallbackMaterial.SetFloat("_Surface", 1f);
            fallbackMaterial.SetFloat("_Blend", 0f);
            fallbackMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fallbackMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fallbackMaterial.SetFloat("_ZWrite", 0f);
            fallbackMaterial.SetOverrideTag("RenderType", "Transparent");
            fallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fallbackMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return fallbackMaterial;
        }
        #endregion

        #region Helpers
        private static Quaternion FlatRotation(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        }

        /// <summary>
        /// Tìm độ cao mặt sàn dưới position (bỏ qua quái, Player và trigger); không có thì dùng nguyên position.y.
        /// </summary>
        private static float FindGroundY(Vector3 position)
        {
            if (groundMask == -1)
            {
                groundMask = ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast");
            }

            if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 20f, groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return position.y;
        }
        #endregion
    }

    /// <summary>
    /// Tay cầm nhẹ tới một lần hiển thị telegraph; Cancel() an toàn kể cả khi telegraph đã xong và được Pool tái sử dụng.
    /// </summary>
    public readonly struct TelegraphHandle
    {
        private readonly TelegraphWarning warning;
        private readonly int ticket;

        public TelegraphHandle(TelegraphWarning warning)
        {
            this.warning = warning;
            ticket = warning != null ? warning.Ticket : 0;
        }

        public bool IsActive => warning != null && warning.IsPlaying && warning.Ticket == ticket;

        public void Cancel()
        {
            if (warning != null) warning.Cancel(ticket);
        }
    }

    /// <summary>
    /// Mesh phẳng (mặt trên hướng lên) dùng chung cho mọi telegraph, tạo một lần rồi lưu cache.
    /// </summary>
    internal static class TelegraphMeshes
    {
        private const int CircleSegments = 48;
        private const int SectorSegments = 24;

        private static Mesh disc;
        private static Mesh line;
        private static readonly Dictionary<int, Mesh> Sectors = new Dictionary<int, Mesh>();

        public static Mesh Get(TelegraphShape shape, float sectorAngle)
        {
            switch (shape)
            {
                case TelegraphShape.Sector:
                    return GetSector(sectorAngle);
                case TelegraphShape.Line:
                    return line != null ? line : (line = BuildLine());
                default:
                    return disc != null ? disc : (disc = BuildFan(360f, CircleSegments, "TelegraphDisc"));
            }
        }

        private static Mesh GetSector(float angle)
        {
            int key = Mathf.Clamp(Mathf.RoundToInt(angle), 1, 360);
            if (!Sectors.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = BuildFan(key, SectorSegments, $"TelegraphSector_{key}");
                Sectors[key] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// Quạt bán kính 1, đỉnh tại gốc, hướng +Z, đối xứng qua trục Z.
        /// </summary>
        private static Mesh BuildFan(float angleDegrees, int segments, string meshName)
        {
            var vertices = new Vector3[segments + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float half = angleDegrees * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)segments);
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

            return CreateMesh(meshName, vertices, normals, triangles);
        }

        /// <summary>
        /// Hình chữ nhật rộng 1 (trục X, từ -0.5 đến 0.5), dài 1 (trục Z, từ 0 đến 1).
        /// </summary>
        private static Mesh BuildLine()
        {
            var vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f)
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return CreateMesh("TelegraphLine", vertices, normals, triangles);
        }

        private static Mesh CreateMesh(string meshName, Vector3[] vertices, Vector3[] normals, int[] triangles)
        {
            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
