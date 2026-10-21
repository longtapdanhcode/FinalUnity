using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vanguard.Enemy;

namespace Vanguard.Spawning
{
    /// <summary>
    /// Các loại quái mà EnemySpawner có thể sinh ra.
    /// </summary>
    public enum EnemyType
    {
        Crawler,
        VoidCaster,
        VolatileHusk,
        Gargoyle,
        VoidWarden
    }

    /// <summary>
    /// Cấu hình Pool cho một loại quái.
    /// </summary>
    [Serializable]
    public class EnemyPoolConfig
    {
        public EnemyType type;
        public GameObject prefab;

        [Tooltip("Số lượng quái khởi tạo sẵn khi vào trận (tránh Instantiate giữa combat)")]
        [Min(0)] public int prewarmCount = 6;
    }

    /// <summary>
    /// Enemy Spawner (Sprint 3 - Card 3.2): Object Pooling theo từng loại quái + sinh quái tại SpawnPoint ngẫu nhiên.
    /// Cung cấp API (ActiveEnemyCount, TotalEnemiesSpawned, OnAllEnemiesCleared...) cho Game Director / Wave Manager (Card 3.3).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Pools")]
        [SerializeField] private EnemyPoolConfig[] enemyPools =
        {
            new EnemyPoolConfig { type = EnemyType.Crawler },
            new EnemyPoolConfig { type = EnemyType.VoidCaster },
            new EnemyPoolConfig { type = EnemyType.VolatileHusk },
            new EnemyPoolConfig { type = EnemyType.Gargoyle, prewarmCount = 2 },
            new EnemyPoolConfig { type = EnemyType.VoidWarden, prewarmCount = 1 }
        };

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Tâm đấu trường dùng để tự sinh SpawnPoint dự phòng")]
        [SerializeField] private Vector3 arenaCenter = Vector3.zero;

        [Tooltip("Bán kính vòng SpawnPoint dự phòng khi spawnPoints rỗng (mét)")]
        [SerializeField] private float fallbackSpawnRadius = 20f;

        [Tooltip("Số SpawnPoint dự phòng rải đều quanh vòng tròn")]
        [SerializeField, Min(1)] private int fallbackSpawnPointCount = 8;

        [Header("Spawn Settings")]
        [Tooltip("Khoảng giãn cách giữa mỗi con quái trong một đợt (giây)")]
        [SerializeField, Min(0f)] private float spawnDelay = 0.3f;

        [Tooltip("Bán kính tìm điểm hợp lệ trên NavMesh quanh vị trí spawn (mét)")]
        [SerializeField] private float navMeshSampleRadius = 4f;

        [Tooltip("Tránh spawn 2 con liên tiếp tại cùng một SpawnPoint")]
        [SerializeField] private bool avoidRepeatSpawnPoint = true;

        // Pool runtime
        private readonly Dictionary<EnemyType, Queue<EnemyBase>> pools = new Dictionary<EnemyType, Queue<EnemyBase>>();
        private readonly Dictionary<EnemyType, GameObject> prefabs = new Dictionary<EnemyType, GameObject>();
        private readonly Dictionary<EnemyBase, EnemyType> ownedEnemies = new Dictionary<EnemyBase, EnemyType>();
        private readonly HashSet<EnemyBase> pooledEnemies = new HashSet<EnemyBase>();
        private readonly HashSet<EnemyBase> aliveEnemies = new HashSet<EnemyBase>();
        private readonly List<EnemyBase> despawnBuffer = new List<EnemyBase>();

        // Delegate cache (0 GC Alloc khi đăng ký sự kiện cho quái mới)
        private Action<EnemyBase> deathHandler;
        private Action<EnemyBase> despawnHandler;

        private WaitForSeconds spawnWait;
        private float cachedSpawnDelay = -1f;
        private int lastSpawnPointIndex = -1;
        private int pendingWaveSpawns;
        private int runningWaves;
        private int waveGeneration;
        private bool isInitialized;

        #region Properties & Events
        /// <summary>Số quái còn sống trên đấu trường.</summary>
        public int ActiveEnemyCount => aliveEnemies.Count;

        /// <summary>Tổng số quái đã spawn từ đầu trận.</summary>
        public int TotalEnemiesSpawned { get; private set; }

        /// <summary>Tổng số quái đã bị tiêu diệt từ đầu trận.</summary>
        public int TotalEnemiesKilled { get; private set; }

        /// <summary>Số quái còn đang chờ spawn trong các đợt đang chạy.</summary>
        public int PendingSpawnCount => pendingWaveSpawns;

        /// <summary>Đang có ít nhất một SpawnWaveRoutine chạy.</summary>
        public bool IsSpawningWave => runningWaves > 0;

        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;

        public event Action<EnemyBase, EnemyType> OnEnemySpawned;
        public event Action<EnemyBase, EnemyType> OnEnemyKilled;

        /// <summary>Phát ra khi không còn quái sống và không còn quái nào chờ spawn.</summary>
        public event Action OnAllEnemiesCleared;
        #endregion

        private void Awake()
        {
            Initialize();
            SetupPhysicsLayers();
        }

        /// <summary>
        /// Cho phép Player và Enemy đi xuyên qua nhau về mặt vật lý (tránh bị kẹt cứng giữa đàn quái).
        /// Đòn chém / AOE dùng Physics.Overlap* theo LayerMask nên không bị ảnh hưởng.
        /// </summary>
        private static void SetupPhysicsLayers()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer != -1 && enemyLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private void ApplyEnemyLayer(EnemyBase enemy)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                SetLayerRecursively(enemy.transform, enemyLayer);
            }
        }

        private void Start()
        {
            Prewarm();
        }

        private void OnDestroy()
        {
            foreach (var pair in ownedEnemies)
            {
                if (pair.Key == null) continue;
                pair.Key.OnEnemyDeath -= deathHandler;
                pair.Key.OnDespawnRequested -= despawnHandler;
            }
        }

        private void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            deathHandler = HandleEnemyDeath;
            despawnHandler = HandleDespawnRequested;

            if (enemyPools != null)
            {
                for (int i = 0; i < enemyPools.Length; i++)
                {
                    EnemyPoolConfig config = enemyPools[i];
                    if (config == null || config.prefab == null) continue;

                    if (config.prefab.GetComponent<EnemyBase>() == null)
                    {
                        Debug.LogError($"[EnemySpawner] Prefab '{config.prefab.name}' ({config.type}) không có component EnemyBase!", this);
                        continue;
                    }

                    prefabs[config.type] = config.prefab;
                    if (!pools.ContainsKey(config.type))
                    {
                        pools[config.type] = new Queue<EnemyBase>();
                    }
                }
            }

            EnsureSpawnPoints();
        }

        #region Spawn Points
        /// <summary>
        /// Phòng ngừa lỗi: loại bỏ phần tử null; nếu rỗng thì dùng các GameObject con,
        /// nếu vẫn rỗng thì tự sinh các điểm rải đều quanh vòng tròn rìa đấu trường.
        /// </summary>
        private void EnsureSpawnPoints()
        {
            spawnPoints = FilterValid(spawnPoints);
            if (spawnPoints.Length > 0) return;

            if (transform.childCount > 0)
            {
                var children = new Transform[transform.childCount];
                for (int i = 0; i < children.Length; i++)
                {
                    children[i] = transform.GetChild(i);
                }
                spawnPoints = children;
                Debug.LogWarning($"[EnemySpawner] spawnPoints rỗng, dùng {spawnPoints.Length} GameObject con làm SpawnPoint.", this);
                return;
            }

            GameObject root = new GameObject("GeneratedSpawnPoints");
            root.transform.SetParent(transform, false);

            int count = Mathf.Max(1, fallbackSpawnPointCount);
            spawnPoints = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 pos = new Vector3(
                    arenaCenter.x + Mathf.Cos(angle) * fallbackSpawnRadius,
                    0f,
                    arenaCenter.z + Mathf.Sin(angle) * fallbackSpawnRadius
                );

                GameObject point = new GameObject($"SpawnPoint_{i:00}");
                point.transform.SetParent(root.transform, false);
                point.transform.position = pos;
                // Hướng mặt vào tâm đấu trường
                point.transform.rotation = Quaternion.LookRotation(new Vector3(arenaCenter.x - pos.x, 0f, arenaCenter.z - pos.z));
                spawnPoints[i] = point.transform;
            }

            Debug.LogWarning($"[EnemySpawner] spawnPoints rỗng, tự sinh {count} điểm quanh vòng tròn bán kính {fallbackSpawnRadius}m.", this);
        }

        private static Transform[] FilterValid(Transform[] points)
        {
            if (points == null) return Array.Empty<Transform>();

            int validCount = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null) validCount++;
            }
            if (validCount == points.Length) return points;

            var result = new Transform[validCount];
            int index = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null) result[index++] = points[i];
            }
            return result;
        }

        private Transform GetRandomSpawnPoint()
        {
            int count = spawnPoints.Length;
            int index = UnityEngine.Random.Range(0, count);

            if (avoidRepeatSpawnPoint && count > 1 && index == lastSpawnPointIndex)
            {
                index = (index + UnityEngine.Random.Range(1, count)) % count;
            }

            lastSpawnPointIndex = index;
            return spawnPoints[index];
        }

        private Vector3 SnapToNavMesh(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)
                ? hit.position
                : position;
        }
        #endregion

        #region Pooling
        /// <summary>
        /// Khởi tạo sẵn quái theo prewarmCount để tránh Instantiate trong lúc combat.
        /// </summary>
        public void Prewarm()
        {
            Initialize();
            if (enemyPools == null) return;

            for (int i = 0; i < enemyPools.Length; i++)
            {
                EnemyPoolConfig config = enemyPools[i];
                if (config == null || !prefabs.ContainsKey(config.type)) continue;

                Queue<EnemyBase> pool = pools[config.type];
                while (pool.Count < config.prewarmCount)
                {
                    EnemyBase enemy = CreateInstance(config.type);
                    if (enemy == null) break;
                    enemy.gameObject.SetActive(false);
                    pool.Enqueue(enemy);
                    pooledEnemies.Add(enemy);
                }
            }
        }

        private EnemyBase CreateInstance(EnemyType type)
        {
            if (!prefabs.TryGetValue(type, out GameObject prefab)) return null;

            // Khởi tạo tại SpawnPoint trên NavMesh để NavMeshAgent không báo lỗi "not close enough to the NavMesh".
            // KHÔNG gán parent: PlayerCombat/SoulBurst/VolatileHusk lọc mục tiêu theo transform.root,
            // gom quái vào chung một GameObject cha sẽ khiến đòn AOE chỉ trúng 1 con.
            Vector3 position = SnapToNavMesh(spawnPoints[0].position);
            GameObject obj = Instantiate(prefab, position, Quaternion.identity);
            obj.name = prefab.name;

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            ApplyEnemyLayer(enemy);
            enemy.OnEnemyDeath += deathHandler;
            enemy.OnDespawnRequested += despawnHandler;
            ownedEnemies[enemy] = type;
            return enemy;
        }

        private EnemyBase GetFromPool(EnemyType type)
        {
            if (pools.TryGetValue(type, out Queue<EnemyBase> pool))
            {
                while (pool.Count > 0)
                {
                    EnemyBase enemy = pool.Dequeue();
                    if (enemy == null) continue; // Bị Destroy từ bên ngoài
                    pooledEnemies.Remove(enemy);
                    return enemy;
                }
            }

            return CreateInstance(type);
        }

        /// <summary>
        /// Thu hồi quái về Pool (SetActive false). Gọi được cả với quái còn sống (ví dụ dọn đấu trường).
        /// </summary>
        public void ReturnToPool(GameObject enemyObj)
        {
            if (enemyObj == null) return;

            if (!enemyObj.TryGetComponent(out EnemyBase enemy) || !ownedEnemies.ContainsKey(enemy))
            {
                Debug.LogWarning($"[EnemySpawner] '{enemyObj.name}' không thuộc Pool của Spawner này, Destroy thay thế.", this);
                Destroy(enemyObj);
                return;
            }

            if (ReturnToPoolInternal(enemy))
            {
                CheckAllCleared();
            }
        }

        /// <summary>Trả về true nếu quái còn sống bị thu hồi (làm thay đổi ActiveEnemyCount).</summary>
        private bool ReturnToPoolInternal(EnemyBase enemy)
        {
            if (pooledEnemies.Contains(enemy)) return false; // Chống thu hồi 2 lần

            bool wasAlive = aliveEnemies.Remove(enemy);
            enemy.gameObject.SetActive(false);
            pooledEnemies.Add(enemy);
            pools[ownedEnemies[enemy]].Enqueue(enemy);
            return wasAlive;
        }

        /// <summary>
        /// Thu hồi toàn bộ quái đang có trên đấu trường và dừng các đợt spawn (dùng khi Restart / chuyển màn).
        /// Không phát OnAllEnemiesCleared.
        /// </summary>
        public void DespawnAll()
        {
            StopAllWaves();

            despawnBuffer.Clear();
            foreach (var pair in ownedEnemies)
            {
                if (pair.Key != null && !pooledEnemies.Contains(pair.Key))
                {
                    despawnBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < despawnBuffer.Count; i++)
            {
                ReturnToPoolInternal(despawnBuffer[i]);
            }
            despawnBuffer.Clear();
        }

        private void HandleEnemyDeath(EnemyBase enemy)
        {
            if (!aliveEnemies.Remove(enemy)) return;

            TotalEnemiesKilled++;
            OnEnemyKilled?.Invoke(enemy, ownedEnemies[enemy]);
            CheckAllCleared();
        }

        private void HandleDespawnRequested(EnemyBase enemy)
        {
            ReturnToPoolInternal(enemy);
        }

        private void CheckAllCleared()
        {
            if (aliveEnemies.Count == 0 && pendingWaveSpawns == 0)
            {
                OnAllEnemiesCleared?.Invoke();
            }
        }
        #endregion

        #region Spawn API
        /// <summary>
        /// Lấy một con quái từ Pool và đặt tại SpawnPoint ngẫu nhiên (hoặc customPosition).
        /// Trả về null nếu loại quái chưa được cấu hình Prefab.
        /// </summary>
        public GameObject SpawnEnemy(EnemyType type, Vector3? customPosition = null)
        {
            Initialize();

            if (!prefabs.ContainsKey(type))
            {
                Debug.LogWarning($"[EnemySpawner] Chưa gán Prefab cho loại quái {type}!", this);
                return null;
            }

            Vector3 position;
            Quaternion rotation;
            if (customPosition.HasValue)
            {
                position = customPosition.Value;
                Vector3 toCenter = arenaCenter - position;
                toCenter.y = 0f;
                rotation = toCenter.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
            }
            else
            {
                Transform point = GetRandomSpawnPoint();
                position = point.position;
                rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
            }
            position = SnapToNavMesh(position);

            EnemyBase enemy = GetFromPool(type);
            if (enemy == null) return null;

            // Đặt vị trí TRƯỚC khi kích hoạt để NavMeshAgent bám đúng chỗ ngay khi OnEnable
            enemy.transform.SetPositionAndRotation(position, rotation);
            ApplyEnemyLayer(enemy);
            enemy.gameObject.SetActive(true);
            enemy.ResetForSpawn(position, rotation);

            aliveEnemies.Add(enemy);
            TotalEnemiesSpawned++;
            OnEnemySpawned?.Invoke(enemy, type);

            return enemy.gameObject;
        }

        /// <summary>
        /// Bắt đầu một đợt quái theo danh sách loại, giãn cách spawnDelay giữa mỗi con.
        /// </summary>
        public Coroutine SpawnWave(IReadOnlyList<EnemyType> enemies)
        {
            if (enemies == null || enemies.Count == 0) return null;
            return StartCoroutine(SpawnWaveRoutine(enemies));
        }

        /// <summary>
        /// Bắt đầu một đợt gồm count con quái cùng loại, giãn cách spawnDelay giữa mỗi con.
        /// </summary>
        public Coroutine SpawnWave(EnemyType type, int count)
        {
            if (count <= 0) return null;
            return StartCoroutine(SpawnWaveRoutine(type, count));
        }

        /// <summary>
        /// Coroutine spawn một đợt quái theo danh sách. Wave Manager có thể StartCoroutine trực tiếp
        /// hoặc yield return để chờ đợt spawn xong.
        /// </summary>
        public IEnumerator SpawnWaveRoutine(IReadOnlyList<EnemyType> enemies)
        {
            if (enemies == null || enemies.Count == 0) yield break;
            yield return RunWave(enemies, default, enemies.Count);
        }

        /// <summary>
        /// Coroutine spawn count con quái cùng loại.
        /// </summary>
        public IEnumerator SpawnWaveRoutine(EnemyType type, int count)
        {
            if (count <= 0) yield break;
            yield return RunWave(null, type, count);
        }

        private IEnumerator RunWave(IReadOnlyList<EnemyType> enemies, EnemyType singleType, int count)
        {
            int generation = waveGeneration;
            runningWaves++;
            pendingWaveSpawns += count;

            for (int i = 0; i < count; i++)
            {
                if (i > 0) yield return GetSpawnWait();

                // StopAllWaves() / DespawnAll() đã hủy đợt này trong lúc chờ
                if (generation != waveGeneration) yield break;

                pendingWaveSpawns--;
                SpawnEnemy(enemies != null ? enemies[i] : singleType);
            }

            runningWaves--;
            CheckAllCleared();
        }

        /// <summary>
        /// Dừng mọi đợt spawn đang chạy (kể cả đợt chạy trên MonoBehaviour khác) và xóa số quái đang chờ.
        /// </summary>
        public void StopAllWaves()
        {
            StopAllCoroutines();
            waveGeneration++;
            runningWaves = 0;
            pendingWaveSpawns = 0;
        }

        private WaitForSeconds GetSpawnWait()
        {
            if (spawnWait == null || !Mathf.Approximately(cachedSpawnDelay, spawnDelay))
            {
                cachedSpawnDelay = spawnDelay;
                spawnWait = new WaitForSeconds(spawnDelay);
            }
            return spawnWait;
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 1f);

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] == null) continue;
                    Gizmos.DrawWireSphere(spawnPoints[i].position, 0.6f);
                    Gizmos.DrawRay(spawnPoints[i].position, spawnPoints[i].forward * 1.5f);
                }
            }
            else
            {
                // Xem trước vòng SpawnPoint dự phòng
                int count = Mathf.Max(1, fallbackSpawnPointCount);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 pos = new Vector3(
                        arenaCenter.x + Mathf.Cos(angle) * fallbackSpawnRadius,
                        0f,
                        arenaCenter.z + Mathf.Sin(angle) * fallbackSpawnRadius
                    );
                    Gizmos.DrawWireSphere(pos, 0.6f);
                }
            }
        }
#endif
    }
}
