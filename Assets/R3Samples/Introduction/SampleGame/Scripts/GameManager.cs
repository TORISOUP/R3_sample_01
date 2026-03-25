using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// ゲーム全体の進行管理
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// 敵射出砲台
        /// </summary>
        [SerializeField] CannonCore _cannonCore;
        /// <summary>
        /// 敵管理
        /// </summary>
        [SerializeField] private EnemyManager _enemyManager;
        /// <summary>
        /// プレイヤー本体
        /// </summary>
        [SerializeField] private PlayerCore _playerCore;
        /// <summary>
        /// 入力状態供給元
        /// </summary>
        [SerializeField] private InputSystemInputEventProvider inputSystemInputEventProvider;

        /// <summary>
        /// スコア保持
        /// </summary>
        private readonly ReactiveProperty<int> _score = new(0);
        /// <summary>
        /// レベル保持
        /// </summary>
        private readonly ReactiveProperty<int> _level = new(0);
        /// <summary>
        /// ゲームオーバー状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isGameOver = new(false);
        /// <summary>
        /// 次ゲーム遷移可能状態
        /// </summary>
        private readonly ReactiveProperty<bool> _canNextGame = new(false);
        public ReadOnlyReactiveProperty<int> Score => _score;
        public ReadOnlyReactiveProperty<int> Level => _level;
        public ReadOnlyReactiveProperty<bool> IsGameOver => _isGameOver;
        public ReadOnlyReactiveProperty<bool> CanNextGame => _canNextGame;

        // KeyはKillCount
        private readonly Dictionary<int, EnemyPattern> _enemyPatternSettings = new();

        /// <summary>
        /// 初期設定と購読登録
        /// </summary>
        private void Awake()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;

            _level.AddTo(this);
            _score.AddTo(this);

            _enemyPatternSettings.Add(0, new EnemyPattern(1, 1));
            _enemyPatternSettings.Add(2, new EnemyPattern(1, 2));
            _enemyPatternSettings.Add(5, new EnemyPattern(2, 1));
            _enemyPatternSettings.Add(7, new EnemyPattern(2, 2));
            _enemyPatternSettings.Add(10, new EnemyPattern(3, 2));
            _enemyPatternSettings.Add(13, new EnemyPattern(1, 3));
            _enemyPatternSettings.Add(15, new EnemyPattern(2, 3));
            _enemyPatternSettings.Add(18, new EnemyPattern(3, 3));
            _enemyPatternSettings.Add(21, new EnemyPattern(4, 3));

            
            // 初期設定
            InitializeEnemyPattern();

            // 初期化が終わるまで待つ
            _playerCore
                .InitializedAsync()
                .ContinueWith(() =>
                {
                    // プレイヤーが気絶したら終了
                    _playerCore.DamageState
                        .Where(x => x == PlayerDamageState.Fainted)
                        .Take(1)
                        .SubscribeAwait(async (_, ct) =>
                        {
                            _isGameOver.Value = true;
                            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);
                            _canNextGame.Value = true;
                        })
                        .AddTo(this);
                });

            _enemyManager.KillCount
                // 初期状態は明示的に適用済みなので、以後のキル数変化だけを見る
                .Skip(1)
                // やられてるならカウントしない
                .Where(_ => !_isGameOver.CurrentValue)
                .Subscribe(killCount =>
                {
                    // 敵を倒した数 = スコア
                    _score.Value = killCount;

                    ApplyEnemyPatternForKillCount(killCount);
                })
                .AddTo(this);

            // ゲームオーバーになって次のゲームに進行する処理
            _canNextGame
                .Where(x => x)
                .Take(1)
                .SubscribeAwait(async (_, ct) =>
                {
                    // ボタンが押されるまで待つ
                    await Observable.Merge(
                            inputSystemInputEventProvider.IsAttackButton.DistinctUntilChanged().Skip(1),
                            inputSystemInputEventProvider.IsJumpButton.DistinctUntilChanged().Skip(1)
                        )
                        .FirstOrDefaultAsync(x => x, cancellationToken: ct);

                    // シーンの再読み込みでゲームの強制リセット（！）
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                })
                .AddTo(this);
        }

        /// <summary>
        /// 初期敵出現パターン適用
        /// </summary>
        private void InitializeEnemyPattern()
        {
            var initialPattern = _enemyPatternSettings[0];
            _level.Value = 1;
            _cannonCore.Initialize(initialPattern);
        }

        /// <summary>
        /// 撃破数に応じた敵パターン更新
        /// </summary>
        private void ApplyEnemyPatternForKillCount(int killCount)
        {
            if (!_enemyPatternSettings.TryGetValue(killCount, out var pattern)) return;

            _level.Value++;
            _cannonCore.UpdateEnemyPatter(pattern);
        }
    }

    /// <summary>
    /// 敵出現設定
    /// </summary>
    /// <param name="EnemyCount">同時出現数</param>
    /// <param name="MaxHealth">敵最大体力</param>
    public record EnemyPattern(int EnemyCount, int MaxHealth);
}
