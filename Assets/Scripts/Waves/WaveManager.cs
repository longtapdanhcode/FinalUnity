using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Vanguard.Enemy;
using Vanguard.Environment;
using Vanguard.Spawning;

namespace Vanguard.Waves
{
    /// <summary>
    /// Quản lý vòng lặp 10 Wave (Sprint 3 - Card 3.3): rải quái qua EnemySpawner, theo dõi điều kiện
    /// hoàn thành, bật/tắt bẫy tâm sàn và phát sự kiện cho hệ thống Upgrade Card (Sprint 4).
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        public static event Action<int> OnWaveStarted;
        public static event Action<int> OnWaveCompleted;
        public static event Action OnAllWavesCleared;

        /// <summary>Phát khi số quái còn lại hoặc tổng số quái của wave thay đổi (remaining, total).</summary>
        public static event Action<int, int> OnEnemiesRemainingChanged;

        [Header("References")]
        [SerializeField] private List<WaveConfigSO> waveConfigs = new List<WaveConfigSO>();
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private CenterRotatingTrap centerTrap;

        [Header("Flow")]
        [SerializeField] private bool autoStartFirstWave = true;
        [Tooltip("Nghỉ giải lao giữa các wave (giây)")]
        [SerializeField] private float waveRestDuration = 3f;
        [Tooltip("Nếu bật: sau mỗi wave chờ ResumeNextWave() (ví dụ người chơi chọn xong card nâng cấp) thay vì tự đếm giờ nghỉ")]
        [SerializeField] private bool waitForExternalResume = false;

        private int currentWaveIndex;
        private bool isWaveActive;
        private float currentWaveTimer;
        private int enemiesRemainingInWave;
        private int totalEnemiesInWave;
        private string currentWaveTitle = string.Empty;
        private string currentWaveTag = string.Empty;

        private bool skipRequested;
        private bool resumeRequested;
        private bool allWavesCleared;
        private Coroutine flowCoroutine;

        // Bộ nhớ đệm tái sử dụng (0 GC Alloc giữa các wave)
        private readonly List<EnemyType> spawnQueue = new List<EnemyType>(64);

        #region Properties
        public int CurrentWaveNumber => currentWaveIndex + 1;
        public int TotalWaves => waveConfigs.Count;
        public bool IsWaveActive => isWaveActive;
        public float CurrentWaveTimer => currentWaveTimer;
        public int EnemiesRemainingInWave => enemiesRemainingInWave;
        public int TotalEnemiesInWave => totalEnemiesInWave;
        public string CurrentWaveTitle => currentWaveTitle;

        /// <summary>Nhãn phụ của wave hiện tại (ví dụ MINI-BOSS), rỗng nếu không có.</summary>
        public string CurrentWaveTag => currentWaveTag;
        public bool AllWavesCleared => allWavesCleared;

        /// <summary>
        /// Bật để sau mỗi wave chờ ResumeNextWave() (ví dụ người chơi chọn xong thẻ nâng cấp) thay vì tự đếm giờ nghỉ.
        /// </summary>
        public bool WaitForExternalResume
        {
            get => waitForExternalResume;
            set => waitForExternalResume = value;
        }
        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WaveManager] Đã có WaveManager khác trong Scene, hủy bản sao này.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (centerTrap == null) centerTrap = FindAnyObjectByType<CenterRotatingTrap>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (Instance != this) return;

            if (spawner == null)
            {
                Debug.LogError("[WaveManager] Thiếu EnemySpawner, không thể chạy wave!", this);
                return;
            }
            if (waveConfigs.Count == 0)
            {
                Debug.LogError("[WaveManager] waveConfigs trống! Chạy menu Vanguard > Generate 10 Waves Data.", this);
                return;
            }

            if (autoStartFirstWave) StartWaves();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
            {
                SkipCurrentWave();
            }
        }

        #region Public API
        /// <summary>Bắt đầu chuỗi wave từ wave 1.</summary>
        public void StartWaves()
        {
            if (spawner == null || waveConfigs.Count == 0) return;
            if (flowCoroutine != null) StopCoroutine(flowCoroutine);

            allWavesCleared = false;
            currentWaveIndex = 0;
            flowCoroutine = StartCoroutine(WaveFlowRoutine());
        }

        /// <summary>Dọn sạch wave hiện tại ngay lập tức để sang wave sau (phím N).</summary>
        public void SkipCurrentWave()
        {
            if (!isWaveActive) return;
            skipRequested = true;
        }

        /// <summary>Cho phép sang wave kế tiếp khi waitForExternalResume = true (ví dụ sau khi chọn xong card).</summary>
        public void ResumeNextWave()
        {
            resumeRequested = true;
        }
        #endregion

        private IEnumerator WaveFlowRoutine()
        {
            while (currentWaveIndex < waveConfigs.Count)
            {
                yield return StartWaveRoutine(currentWaveIndex);

                if (currentWaveIndex >= waveConfigs.Count - 1) break;

                // Nghỉ giải lao / chờ người chơi chọn card nâng cấp
                if (waitForExternalResume)
                {
                    // resumeRequested được reset lúc bắt đầu wave, nên ResumeNextWave() gọi ngay trong
                    // sự kiện OnWaveCompleted (ví dụ không có thẻ để chọn) vẫn không bị mất
                    yield return new WaitUntil(() => resumeRequested);
                }
                else
                {
                    yield return new WaitForSeconds(waveRestDuration);
                }

                currentWaveIndex++;
            }

            allWavesCleared = true;
            flowCoroutine = null;
            Debug.Log("[WaveManager] HOÀN THÀNH TOÀN BỘ 10 WAVE!");
            OnAllWavesCleared?.Invoke();
        }

        private IEnumerator StartWaveRoutine(int index)
        {
            WaveConfigSO config = waveConfigs[index];
            if (config == null)
            {
                Debug.LogWarning($"[WaveManager] waveConfigs[{index}] bị null, bỏ qua wave này.", this);
                yield break;
            }

            int waveNumber = index + 1;
            BuildSpawnQueue(config);

            currentWaveTitle = config.waveTitle;
            currentWaveTag = config.hudTag ?? string.Empty;
            totalEnemiesInWave = spawnQueue.Count;
            enemiesRemainingInWave = spawnQueue.Count;
            currentWaveTimer = config.UsesTimer ? config.survivalTime : 0f;
            skipRequested = false;
            resumeRequested = false;
            isWaveActive = true;

            if (centerTrap != null) centerTrap.SetTrapActive(config.enableCenterTrap);

            Debug.Log($"[WaveManager] BẮT ĐẦU WAVE {waveNumber}: {config.waveTitle} ({enemiesRemainingInWave} quái)");
            OnWaveStarted?.Invoke(waveNumber);
            OnEnemiesRemainingChanged?.Invoke(enemiesRemainingInWave, totalEnemiesInWave);

            // Rải quái dần dần theo nhịp spawnInterval, đồng thời đếm giờ / kiểm tra điều kiện hoàn thành
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, config.spawnInterval));
            int spawnIndex = 0;
            float nextSpawnTime = 0f;
            float elapsed = 0f;

            while (isWaveActive)
            {
                if (skipRequested) break;

                bool allSpawned = spawnIndex >= spawnQueue.Count;

                // Nhả quái theo nhịp
                if (!allSpawned && elapsed >= nextSpawnTime)
                {
                    // Nếu prefab chưa gán, SpawnEnemy trả null: con này không được tính nên wave không bị kẹt
                    spawner.SpawnEnemy(spawnQueue[spawnIndex]);
                    spawnIndex++;
                    nextSpawnTime += config.spawnInterval;
                    allSpawned = spawnIndex >= spawnQueue.Count;
                }

                // Quái còn lại = quái chưa nhả + quái đang sống (tính cả quái do Boss triệu hồi giữa trận)
                int remaining = (spawnQueue.Count - spawnIndex) + spawner.ActiveEnemyCount;
                if (remaining != enemiesRemainingInWave)
                {
                    enemiesRemainingInWave = remaining;
                    // Quái do Boss triệu hồi làm tổng số tăng thêm
                    totalEnemiesInWave = Mathf.Max(totalEnemiesInWave, remaining);
                    OnEnemiesRemainingChanged?.Invoke(enemiesRemainingInWave, totalEnemiesInWave);
                }

                // Đếm thời gian
                elapsed += Time.deltaTime;
                if (config.UsesTimer)
                {
                    currentWaveTimer = Mathf.Max(0f, config.survivalTime - elapsed);
                    if (currentWaveTimer <= 0f) break;
                }
                else
                {
                    currentWaveTimer = elapsed;
                }

                if (config.EndsWhenCleared && allSpawned && enemiesRemainingInWave <= 0) break;

                yield return null;
            }

            CompleteWave(waveNumber);
        }

        private void CompleteWave(int waveNumber)
        {
            isWaveActive = false;
            enemiesRemainingInWave = 0;
            OnEnemiesRemainingChanged?.Invoke(0, totalEnemiesInWave);

            // Thu hồi quái còn sống (hết giờ / bỏ qua) và dừng nhả quái
            spawner.DespawnAll();

            Debug.Log($"[WaveManager] HOÀN THÀNH WAVE {waveNumber}!");
            OnWaveCompleted?.Invoke(waveNumber);
        }

        /// <summary>Gộp các EnemyGroup thành danh sách phẳng rồi xáo trộn để các loại quái xen kẽ nhau.</summary>
        private void BuildSpawnQueue(WaveConfigSO config)
        {
            spawnQueue.Clear();
            for (int g = 0; g < config.enemyGroups.Count; g++)
            {
                WaveConfigSO.EnemyGroup group = config.enemyGroups[g];
                for (int i = 0; i < group.count; i++) spawnQueue.Add(group.enemyType);
            }

            for (int i = spawnQueue.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (spawnQueue[i], spawnQueue[j]) = (spawnQueue[j], spawnQueue[i]);
            }
        }
    }
}
