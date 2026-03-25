using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤーの体力、被ダメージ状態、一時無敵、被弾後の復帰処理を担当する。
    /// ダメージ適用時の判定とノックバック開始もここで管理する。
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerDamageHandler : MonoBehaviour
    {
        /// <summary>
        /// ダメージ適用結果
        /// </summary>
        public readonly struct DamageResult
        {
            /// <summary>
            /// ダメージを実際に受けた
            /// </summary>
            public bool Accepted { get; }

            /// <summary>
            /// プレイヤーが振り向く方向
            /// </summary>
            public PlayerDirection NextDirection { get; }

            public DamageResult(bool accepted, PlayerDirection nextDirection)
            {
                Accepted = accepted;
                NextDirection = nextDirection;
            }
        }

        /// <summary>
        /// 体力状態
        /// </summary>
        private readonly ReactiveProperty<int> _playerHealth = new(3);
        /// <summary>
        /// ダメージ状態
        /// </summary>
        private readonly ReactiveProperty<PlayerDamageState> _playerDamageState = new(PlayerDamageState.None);
        /// <summary>
        /// 無敵状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isPlayerInvincible = new(false);
        /// <summary>
        /// ダメージ拘束状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isLockedByDamage = new(false);

        /// <summary>
        /// 移動制御
        /// </summary>
        private PlayerMover _playerMover;
        private CancellationTokenSource _damageRecoveryCts;

        /// <summary>
        /// Playerの体力
        /// </summary>
        public ReadOnlyReactiveProperty<int> PlayerHealth => _playerHealth;

        /// <summary>
        /// プレイヤーのダメージ状態
        /// </summary>
        public ReadOnlyReactiveProperty<PlayerDamageState> DamageState => _playerDamageState;

        /// <summary>
        /// 無敵状態
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsPlayerInvincible => _isPlayerInvincible;
 
        /// <summary>
        /// ダメージ硬直による操作不能状態か
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsLockedByDamage => _isLockedByDamage;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _playerMover = GetComponent<PlayerMover>();
        }

        /// <summary>
        /// 状態購読登録
        /// </summary>
        private void Start()
        {
            _playerHealth.AddTo(this);
            _playerDamageState.AddTo(this);
            _isPlayerInvincible.AddTo(this);
            _isLockedByDamage.AddTo(this);

            // 「ダメージ状態」だったら一定時間後にダメージ状態を解除する
            // 「気絶」したら何もしない
            _isLockedByDamage
                .Where(x => x && _playerDamageState.Value == PlayerDamageState.Damaged)
                .SubscribeAwait(async (_, ct) =>
                {
                    await RecoverFromDamageAsync(ct);
                }, AwaitOperation.Switch) // Switchなので仮にダメージ中に再度ダメージを受けてもたぶん問題ない
                .AddTo(this);
        }

        /// <summary>
        /// ダメージ判定と被弾リアクションを実行し、呼び出し側が反映する結果を返す。
        /// </summary>
        public DamageResult ApplyDamage(Damage damage, Action cancelAttack)
        {
            // ダメージで吹っ飛ぶ方向とは反対方向を向く
            var nextDirection = damage.Velocity.x > 0 ? PlayerDirection.Left : PlayerDirection.Right;

            if (_isPlayerInvincible.Value)
            {
                // 無敵状態なら何もせず終わり
                return new DamageResult(false, nextDirection);
            }

            // ダメージノックバック中でない　かつ　気絶中でない
            if (_playerDamageState.CurrentValue == PlayerDamageState.None)
            {
                // 自分自身からの攻撃が仮に存在するとしたらそれはダメージは受けない（ノックバックはする）
                var damageValue = damage.Type != AttackerType.Player ? damage.DamageValue : 0;

                if (damageValue > 0)
                {
                    // ダメージを受けたら無敵状態になる
                    _isPlayerInvincible.Value = true;
                }
                
                _playerHealth.Value = (int)Mathf.Max(0, _playerHealth.Value - damageValue);
                _playerDamageState.Value = PlayerDamageState.Damaged;
                
                // ノックバック中は一時操作不能
                _isLockedByDamage.Value = true;
            }

            // 攻撃中の処理があったら止める
            cancelAttack?.Invoke();
            
            // ノックバック実行
            _playerMover.UpdateHorizontalMove(0); // コントローラー入力をリセット
            _playerMover.Launch(damage.Velocity);

            // ノックバック解除処理は RecoverFromDamageAsync へ続く
            
            return new DamageResult(true, nextDirection);
        }

        /// <summary>
        /// ノックバックと一時操作不能と無敵を解除する
        /// </summary>
        /// <param name="ct"></param>
        private async UniTask RecoverFromDamageAsync(CancellationToken ct)
        {
            /*
             * ここに到達した段階でプレイヤーは「ダメージアニメーション」で吹っ飛んでいる状態
             */
            
            // 0.5秒はダメージノックバック状態を維持
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);

            // 吹っ飛んでまだ空中にいるなら着地するまでノックバック硬直は解けない
            if (!_playerMover.IsGrounded.CurrentValue)
            {
                await _playerMover.IsGrounded.FirstOrDefaultAsync(x => x, cancellationToken: ct)
                    .AsUniTask()
                    // 引っかかった時を考慮して3秒で強制的に次に進ませる
                    .TimeoutWithoutException(TimeSpan.FromSeconds(3));
            }

            // コントローラー入力はリセットしておく
            _playerMover.UpdateHorizontalMove(0);

            // 着地時に体力がゼロだったら「気絶」して終了
            if (_playerHealth.CurrentValue <= 0)
            {
                _playerDamageState.Value = PlayerDamageState.Fainted;
                return;
            }

            // 操作不能状態を解除
            _isLockedByDamage.Value = false;
            _playerDamageState.Value = PlayerDamageState.None;

            // 無敵状態はしばらく維持してから解除
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);
            _isPlayerInvincible.Value = false;
        }

        /// <summary>
        /// 後始末
        /// </summary>
        private void OnDestroy()
        {
            _damageRecoveryCts?.Cancel();
            _damageRecoveryCts?.Dispose();
            _damageRecoveryCts = null;
        }
    }
}
