using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using R3Samples.Introduction.SampleGame.Attacks;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤーの各攻撃アニメーションと当たり判定の実行を担当する
    /// 各技の制御とヒットボックス制御を持つ
    /// </summary>
    public sealed class PlayerAttack : MonoBehaviour
    {
        /// <summary>
        /// 攻撃当たり判定設定
        /// </summary>
        private readonly struct AttackHitboxSettings
        {
            /// <summary>
            /// 当たり判定コライダー
            /// </summary>
            public Collider2D Collider { get; }
            /// <summary>
            /// ダメージ量
            /// </summary>
            public float Damage { get; }
            /// <summary>
            /// ノックバック方向
            /// </summary>
            public Vector2 KnockbackDirection { get; }
            /// <summary>
            /// ノックバック強さ
            /// </summary>
            public float KnockbackPower { get; }

            /// <summary>
            /// 設定生成
            /// </summary>
            public AttackHitboxSettings(
                Collider2D collider,
                float damage,
                Vector2 knockbackDirection,
                float knockbackPower)
            {
                Collider = collider;
                Damage = damage;
                KnockbackDirection = knockbackDirection;
                KnockbackPower = knockbackPower;
            }
        }

        /// <summary>
        /// 通常攻撃判定
        /// </summary>
        [SerializeField] private Collider2D _punchAttackCollider;
        /// <summary>
        /// 昇竜拳判定
        /// </summary>
        [SerializeField] private Collider2D _upperPunchAttackCollider;
        /// <summary>
        /// 回転攻撃判定
        /// </summary>
        [SerializeField] private Collider2D _spinAttackCollider;

        /// <summary>
        /// 通常攻撃威力
        /// </summary>
        [SerializeField] private float _punchAttackDamage = 1f;
        /// <summary>
        /// 昇竜拳威力
        /// </summary>
        [SerializeField] private float _upperPunchAttackDamage = 2f;
        /// <summary>
        /// 回転攻撃威力
        /// </summary>
        [SerializeField] private float _spinAttackDamage = 3f;

        /// <summary>
        /// 昇竜拳上昇速度
        /// </summary>
        [SerializeField] private float _upperPunchJumpSpeed = 12f;
        /// <summary>
        /// 昇竜拳水平速度
        /// </summary>
        [SerializeField] private float _upperPunchMoveSpeed = 2.5f;
        /// <summary>
        /// 回転攻撃水平速度
        /// </summary>
        [SerializeField] private float _spinAttackSpeed = 4.0f;

        /// <summary>
        /// 飛び道具プレハブ
        /// </summary>
        [SerializeField] private FireShotBullet _fireShotBulletPrefab;
        /// <summary>
        /// 飛び道具発射位置
        /// </summary>
        [SerializeField] private Transform _fireShotMuzzlePosition;

        private PlayerAnimation _playerAnimation;
        private PlayerMover _playerMover;

        private AttackHitboxSettings _punchHitbox;
        private AttackHitboxSettings _upperPunchHitbox;
        private AttackHitboxSettings _spinHitbox;

        /// <summary>
        /// 初期化と当たり判定登録
        /// </summary>
        private void Start()
        {
            _playerAnimation = GetComponent<PlayerAnimation>();
            _playerMover = GetComponent<PlayerMover>();

            _punchHitbox = new AttackHitboxSettings(_punchAttackCollider, _punchAttackDamage, new Vector2(1f, 1f), 8f);
            _upperPunchHitbox = new AttackHitboxSettings(_upperPunchAttackCollider, _upperPunchAttackDamage,
                new Vector2(1f, 3f), 10f);
            _spinHitbox = new AttackHitboxSettings(_spinAttackCollider, _spinAttackDamage, new Vector2(1f, 10f), 15f);

            RegisterAttackHitbox(_punchHitbox);
            RegisterAttackHitbox(_upperPunchHitbox);
            RegisterAttackHitbox(_spinHitbox);

            InitializeHitbox(_punchHitbox.Collider);
            InitializeHitbox(_upperPunchHitbox.Collider);
            InitializeHitbox(_spinHitbox.Collider);
        }

        /// <summary>
        /// 当たり判定購読登録
        /// </summary>
        private void RegisterAttackHitbox(AttackHitboxSettings settings)
        {
            settings.Collider.OnTriggerEnter2DAsObservable()
                .Subscribe(c =>
                {
                    if (!c.gameObject.TryGetComponent<IDamageable>(out var damageable)) return;

                    // 相手との相対位置から左右を反転させ、技ごとの既定ベクトルでノックバックを作る。
                    var knockback = CreateKnockback(damageable.GetDirection(transform.position), settings);
                    damageable.OnDamaged(new Damage(AttackerType.Player, settings.Damage, knockback));
                })
                .AddTo(this);
        }

        /// <summary>
        /// 弱攻撃
        /// </summary>
        public async UniTask PunchAttackAsync(CancellationToken ct)
        {
            await WithHitboxEnabledAsync(_punchHitbox.Collider, async () =>
            {
                _playerMover.UpdateHorizontalMove(0);
                _playerAnimation.StartUnManagedPunchAnimation();

                // パンチの持続は2F(!)
                await UniTask.Delay(TimeSpan.FromSeconds(2f / 60), DelayType.Realtime, cancellationToken: ct);
                
                // ヒットボックス無効化
                SetColliderEnable(_punchHitbox.Collider, false);
                
                // 後隙15F
                await UniTask.Delay(TimeSpan.FromSeconds(15f / 60), DelayType.Realtime, cancellationToken: ct);
                _playerAnimation.ResetAnimation();
            });
        }

        /// <summary>
        /// しょうりゅうけん
        /// 上昇、落下、着地隙
        /// </summary>
        public async UniTask UpperPunchAttackAsync(PlayerDirection direction, CancellationToken ct)
        {
            await WithHitboxEnabledAsync(_upperPunchHitbox.Collider, async () =>
            {
                // 向いている方向の斜め上にジャンプ
                var x = direction == PlayerDirection.Left ? -1f : 1f;
                _playerMover.SetConstantX(x * _upperPunchMoveSpeed);
                _playerMover.Jump(_upperPunchJumpSpeed);

                // しょうりゅうけんアニメーション
                _playerAnimation.StartUpperPunchAnimation();

                // 上昇が終わるまで待機
                await UniTask.WaitUntil(_playerMover, m => m.CurrentVelocity.y < 0, cancellationToken: ct);

                // 落下が始まったらヒットボックス無効化、水平速度をゼロにして垂直落下
                SetColliderEnable(_upperPunchHitbox.Collider, false);
                _playerMover.UpdateHorizontalMove(0);
                _playerAnimation.StartUnManagedFallDownAnimation();

                // 着地するまで待つ
                await UniTask.WhenAny(_playerMover.IsGrounded
                        .FirstAsync(isGrounded => isGrounded, cancellationToken: ct)
                        .AsUniTask(),
                    UniTask.Delay(TimeSpan.FromSeconds(120f / 60), DelayType.Realtime, cancellationToken: ct)
                );

                // 着地アニメーション再生、依然として水平速度はゼロを維持
                _playerMover.UpdateHorizontalMove(0);
                _playerAnimation.StartUnManagedLandingAnimation();

                // 着地隙 20F
                await UniTask.Delay(TimeSpan.FromSeconds(20f / 60), DelayType.Realtime, cancellationToken: ct);

                _playerAnimation.ResetAnimation();
            });
        }

        /// <summary>
        /// はどうけん
        /// </summary>
        public async UniTask FireShotAsync(PlayerDirection direction, CancellationToken ct)
        {
            _playerMover.UpdateHorizontalMove(0);

            _playerAnimation.StartUnManagedShotAnimation();

            // 弾を発射
            var bullet = Instantiate(_fireShotBulletPrefab, _fireShotMuzzlePosition.position, Quaternion.identity);
            var x = direction == PlayerDirection.Left ? -1f : 1f;
            var initialVelocity = new Vector2(x, -0.5f).normalized;

            bullet.Initialize(initialVelocity * 5f, 1);

            // 後隙20F
            await UniTask.Delay(TimeSpan.FromSeconds(20f / 60), DelayType.Realtime, cancellationToken: ct);

            _playerAnimation.ResetAnimation();
        }

        /// <summary>
        /// たつまきせんぷうきゃく
        /// </summary>
        public async UniTask SpinAttackAsync(PlayerDirection direction, CancellationToken ct)
        {
            await WithHitboxEnabledAsync(_spinHitbox.Collider, async () =>
            {
                // 向いている方向に定速で強制移動
                var x = direction == PlayerDirection.Left ? -1f : 1f;
                _playerMover.SetConstantX(x * _spinAttackSpeed);

                // スピンアタックアニメーション開始
                await _playerAnimation.StartSpinAttackAsync(ct);

                // アニメーションが終わったらヒットボックス無効化
                SetColliderEnable(_spinHitbox.Collider, false);

                // 後隙アニメーションを再生して、向いている方向に軽く吹っ飛ぶ
                _playerAnimation.StartUnManagedStoppingAnimation();
                _playerMover.SetConstantX(x * 4f);
                _playerMover.Jump(6f);

                // 30Fの後隙、それでも着地しない場合は着地するまで待つ
                await UniTask.Delay(TimeSpan.FromSeconds(30f / 60), DelayType.Realtime, cancellationToken: ct);
                await _playerMover.IsGrounded.FirstOrDefaultAsync(s => s, cancellationToken: ct);
                _playerMover.UpdateHorizontalMove(0);

                _playerAnimation.ResetAnimation();
            });
        }

        /// <summary>
        /// 当たり判定付き処理実行
        /// ヒットボックスを有効化して、確実に無効化する
        /// </summary>
        private async UniTask WithHitboxEnabledAsync(Collider2D targetCollider, Func<UniTask> action)
        {
            try
            {
                SetColliderEnable(targetCollider, true);
                await action();
            }
            finally
            {
                SetColliderEnable(targetCollider, false);
            }
        }

        /// <summary>
        /// ノックバック生成
        /// </summary>
        private static Vector2 CreateKnockback(Vector2 direction, AttackHitboxSettings settings)
        {
            var horizontal = direction.x > 0 ? -1f : 1f;
            return new Vector2(horizontal * settings.KnockbackDirection.x, settings.KnockbackDirection.y).normalized
                   * settings.KnockbackPower;
        }

        /// <summary>
        /// 当たり判定初期化
        /// </summary>
        private void InitializeHitbox(Collider2D targetCollider)
        {
            SetColliderEnable(targetCollider, false);
        }

        /// <summary>
        /// 判定表示切替
        /// </summary>
        private static void SetColliderEnable(Collider2D collider, bool enabled)
        {
            collider.enabled = enabled;
            collider.GetComponentInChildren<SpriteRenderer>().enabled = enabled;
        }
    }
}
