using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vanguard.Spawning;

namespace Vanguard.Waves
{
    /// <summary>
    /// Dữ liệu cấu hình cho một Wave (Sprint 3 - Card 3.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Wave_", menuName = "Vanguard/Wave Config")]
    public class WaveConfigSO : ScriptableObject
    {
        public enum WaveClearCondition
        {
            ClearAllEnemies,
            SurvivalTimer,
            ClearOrTimer
        }

        [System.Serializable]
        public struct EnemyGroup
        {
            public EnemyType enemyType;
            [Min(0)] public int count;
        }

        [Header("Wave Info")]
        public int waveNumber = 1;
        public string waveTitle = "Wave";

        [Tooltip("Nhãn phụ hiển thị trên HUD sau số wave, ví dụ MINI-BOSS (để trống nếu không cần)")]
        public string hudTag = "";

        [Header("Clear Condition")]
        public WaveClearCondition clearCondition = WaveClearCondition.ClearAllEnemies;

        [Tooltip("Thời gian đếm ngược (giây) nếu là wave SurvivalTimer / ClearOrTimer")]
        public float survivalTime = 60f;

        [Header("Spawning")]
        [Tooltip("Khoảng cách giữa các nhịp nhả quái (giây)")]
        public float spawnInterval = 0.8f;

        public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();

        [Header("Environment")]
        [Tooltip("Bật bẫy xoay tâm sàn (từ wave 8)")]
        public bool enableCenterTrap = false;

        public int TotalEnemyCount => enemyGroups.Sum(g => g.count);

        public bool UsesTimer => clearCondition != WaveClearCondition.ClearAllEnemies;
        public bool EndsWhenCleared => clearCondition != WaveClearCondition.SurvivalTimer;
    }
}
