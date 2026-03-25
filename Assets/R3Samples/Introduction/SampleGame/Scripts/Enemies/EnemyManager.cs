using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 敵生成数と撃破数の管理
    /// </summary>
    public sealed class EnemyManager : MonoBehaviour
    {
        /// <summary>
        /// プレイヤー座標参照
        /// </summary>
        [SerializeField] private Transform _playerTransform;
        /// <summary>
        /// 敵プレハブ
        /// </summary>
        [SerializeField] private EnemyCore _enemyPrefab;
        /// <summary>
        /// 敵共通音声出力
        /// </summary>
        [SerializeField] private AudioSource _enemyAudioSource;

        /// <summary>
        /// 現在敵数
        /// </summary>
        private readonly ReactiveProperty<int> _currentEnemyCount = new(0);
        /// <summary>
        /// 撃破数
        /// </summary>
        private readonly ReactiveProperty<int> _killCount = new(0);

        public ReadOnlyReactiveProperty<int> CurrentEnemyCount => _currentEnemyCount;
        public ReadOnlyReactiveProperty<int> KillCount => _killCount;

        /// <summary>
        /// 敵生成
        /// </summary>
        public EnemyCore CreateEnemy(int maxHealth, Vector2 positon)
        {
            var enemy = Instantiate(_enemyPrefab, positon, Quaternion.identity);
            enemy.Initialize(maxHealth, _playerTransform, _enemyAudioSource);

            _currentEnemyCount.Value++;

            // 敵オブジェクト破棄ではなく死亡完了を待つことで、死亡演出終了後に撃破数を加算する
            UniTask.Void(async ct =>
            {
                await enemy.DeadAsync.AttachExternalCancellation(ct);
                _killCount.Value++;
            }, destroyCancellationToken);

            enemy.OnDestroyAsObservable()
                .Take(1)
                .Subscribe(_ => _currentEnemyCount.Value--)
                .AddTo(this);

            return enemy;
        }
    }
}
