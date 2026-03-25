using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using R3Samples.Introduction.SampleGame;
using UnityEngine;
using Random = UnityEngine.Random;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 敵本体の状態管理と挙動制御
    /// </summary>
    public class EnemyCore : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// 向き状態
        /// </summary>
        private readonly ReactiveProperty<EnemyDirection> _direction = new(EnemyDirection.Left);
        /// <summary>
        /// 最大体力
        /// </summary>
        private readonly ReactiveProperty<float> _maxHealth = new(1);
        /// <summary>
        /// 現在体力
        /// </summary>
        private readonly ReactiveProperty<float> _currentHealth = new(1);
        /// <summary>
        /// 行動状態
        /// </summary>
        private readonly ReactiveProperty<EnemyStatus> _status = new(EnemyStatus.InitialLaunch);
        /// <summary>
        /// ジャンプ通知
        /// </summary>
        private readonly Subject<Unit> _jumped = new();
        /// <summary>
        /// 死亡完了通知
        /// </summary>
        private readonly UniTaskCompletionSource _isDead = new();
        /// <summary>
        /// 死亡アニメーション完了通知
        /// </summary>
        private readonly UniTaskCompletionSource _onDeadAnimationEnded = new();

        /// <summary>
        /// 移動制御
        /// </summary>
        private CharacterController2D _characterController2D;
        /// <summary>
        /// プレイヤー座標参照
        /// </summary>
        private Transform _playerTransform;

        public AttackerType Type => AttackerType.Enemy;
        public ReadOnlyReactiveProperty<EnemyDirection> Direction => _direction;
        public ReadOnlyReactiveProperty<float> MaxHealth => _maxHealth;
        public ReadOnlyReactiveProperty<float> CurrentHealth => _currentHealth;
        public ReadOnlyReactiveProperty<EnemyStatus> Status => _status;
        public Observable<Unit> Jumped => _jumped;
        public UniTask DeadAsync => _isDead.Task;
        public AudioSource AudioSource { get; private set; }

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _characterController2D = GetComponent<CharacterController2D>();
        }

        /// <summary>
        /// 状態購読と各ループ開始
        /// </summary>
        private void Start()
        {
            _currentHealth.AddTo(this);
            _maxHealth.AddTo(this);
            _status.AddTo(this);
            _jumped.AddTo(this);

            // 敵がダメージを受けて吹っ飛んでる時の処理
            _status
                .SubscribeAwait(async (x, ct) =>
                {
                    // ダメージ状態じゃないなら何もしない
                    if (x is not EnemyStatus.Damaged) return;
                    
                    // ダメージを受けたらまず0.25秒は最低でも吹っ飛び状態を維持
                    await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);

                    // 吹っ飛んだら地面に着くまで待つ or 1秒経過するまで待つ
                    await _characterController2D.IsGrounded
                        .FirstOrDefaultAsync(x => x, cancellationToken: ct)
                        .AsUniTask()
                        .TimeoutWithoutException(TimeSpan.FromSeconds(1f));

                    // 操作可能状態に変更
                    _characterController2D.UnlockControl();

                    // 状態を通常に戻す
                    if (_status.CurrentValue == EnemyStatus.Damaged)
                    {
                        _status.Value = EnemyStatus.Normal;
                    }
                    // ダメージ処理中に次のダメージ処理が来ても問題なく動くように
                    // AwaitOperation.Switchを使う
                }, AwaitOperation.Switch) 
                .AddTo(this);

            // EnemyStatusは最初「InitialLaunch」から始まる
            // それが解除されたら各ループを開始する
            _status.Where(x => x != EnemyStatus.InitialLaunch)
                .Take(1)
                .Subscribe(_ =>
                {
                    DetectPlayerAsync(destroyCancellationToken).Forget();
                    MoveAsync(destroyCancellationToken).Forget();
                })
                .AddTo(this);

            // なにかぶつかってきたら攻撃する
            this.OnTriggerEnter2DAsObservable()
                .Where(_ => _currentHealth.CurrentValue > 0)
                .Subscribe(x =>
                {
                    if (!x.TryGetComponent<IDamageable>(out var damageable)) return;
                    if (damageable.Type != AttackerType.Player) return;

                    var direction = damageable.GetDirection(transform.position);
                    var futtobi = new Vector2(direction.x > 0 ? -1 : 1, 2).normalized;
                    damageable.OnDamaged(new Damage(AttackerType.Enemy, 1, futtobi * 5f));
                })
                .AddTo(this);

            WaitForDeadAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 敵初期化
        /// </summary>
        public void Initialize(float maxHealth, Transform playerTransform, AudioSource audioSource)
        {
            // AudioSourceは外から注入されたものを使う
            // Enemyが死亡したときに効果音を鳴らしたいが、
            // EnemyのGameObjectにAudioSourceをつけるとDestroyで音が止まってしまうため
            AudioSource = audioSource;
            
            _playerTransform = playerTransform;
            _maxHealth.Value = maxHealth;
            _currentHealth.Value = maxHealth;
            _characterController2D.moveSpeed = maxHealth switch
            {
                3 => 2.5f,
                2 => 2.0f,
                _ => 1.5f
            };
        }

        /// <summary>
        /// 指定座標との相対方向取得
        /// </summary>
        public Vector2 GetDirection(Vector2 position)
        {
            return position - (Vector2)transform.position;
        }

        /// <summary>
        /// 最初の射出
        /// </summary>
        public void InitialLaunch(Vector2 velocity)
        {
            // 大砲から発射された直後は初速を与えて操作不能にする
            _characterController2D.LockControl();
            _characterController2D.Launch(velocity);
            _direction.Value = velocity.x > 0 ? EnemyDirection.Right : EnemyDirection.Left;

            UniTask.Void(async ct =>
            {
                // 最低でも0.2秒以上飛んで、着地したら操作可能にして通常状態に以降
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: ct);
                await _characterController2D.IsGrounded.FirstOrDefaultAsync(x => x, cancellationToken: ct);
                if (_status.CurrentValue == EnemyStatus.InitialLaunch)
                {
                    _characterController2D.UnlockControl();
                    _status.Value = EnemyStatus.Normal;
                }
            }, destroyCancellationToken);
        }

        /// <summary>
        /// 被ダメージ反映
        /// </summary>
        public void OnDamaged(Damage damage)
        {
            if (_status.Value is EnemyStatus.Damaged or EnemyStatus.Dead) return;

            _currentHealth.Value -= damage.DamageValue;
            _status.OnNext(_currentHealth.Value > 0 ? EnemyStatus.Damaged : EnemyStatus.Dead);

            _characterController2D.LockControl();
            _characterController2D.Launch(damage.Velocity);
        }

        /// <summary>
        /// 死亡アニメーション終了通知
        /// </summary>
        public void OnDeadAnimationEnded()
        {
            _onDeadAnimationEnded.TrySetResult();
        }

        /// <summary>
        /// 後始末
        /// </summary>
        private void OnDestroy()
        {
            _onDeadAnimationEnded.TrySetCanceled();
            _isDead.TrySetCanceled();
        }

        /// <summary>
        /// 向き更新ループ
        /// </summary>
        private async UniTaskVoid DetectPlayerAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!_characterController2D.IsGrounded.CurrentValue)
                {
                    await UniTask.Yield(ct);
                    continue;
                }

                // 半々でプレイヤー追従とランダム転向を切り替え、単純すぎない挙動にしている
                if (Random.Range(0, 100) < 50)
                {
                    var isRight = transform.position.x < _playerTransform.position.x;
                    _direction.Value = isRight ? EnemyDirection.Right : EnemyDirection.Left;
                }
                else
                {
                    _direction.Value = Random.Range(0, 100) < 50 ? EnemyDirection.Right : EnemyDirection.Left;
                }

                var randomSeconds = Random.Range(1, 3);
                await UniTask.Delay(TimeSpan.FromSeconds(randomSeconds), cancellationToken: ct);
            }
        }

        /// <summary>
        /// 移動制御ループ
        /// </summary>
        private async UniTaskVoid MoveAsync(CancellationToken ct)
        {
            // 向いている方向に進行しつつ、時よりジャンプする
            var lastJumpSeconds = Time.timeAsDouble;
            while (!ct.IsCancellationRequested)
            {
                if (_status.CurrentValue == EnemyStatus.Normal)
                {
                    var moveDir = _direction.Value == EnemyDirection.Right ? 1 : -1;
                    _characterController2D.Move(moveDir);

                    var elapsedTime = Time.timeAsDouble - lastJumpSeconds;
                    
                    // 最後にジャンプしてから３秒以上経ったらランダムでジャンプする
                    if (_characterController2D.IsGrounded.CurrentValue && elapsedTime > 3f)
                    {
                        if (Random.Range(0, 100) < 2f)
                        {
                            lastJumpSeconds = Time.timeAsDouble;
                            _characterController2D.AddVelocity(Vector2.up * Random.Range(5, 10));
                            _jumped.OnNext(Unit.Default);
                        }
                    }
                }

                await UniTask.Yield(ct);
            }
        }

        /// <summary>
        /// 死亡完了待機
        /// </summary>
        private async UniTaskVoid WaitForDeadAsync(CancellationToken ct)
        {
            await _status.FirstOrDefaultAsync(x => x == EnemyStatus.Dead, cancellationToken: ct);
            await _onDeadAnimationEnded.Task;
            _isDead.TrySetResult();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 敵向き
    /// </summary>
    public enum EnemyDirection
    {
        Right,
        Left
    }

    /// <summary>
    /// 敵状態
    /// </summary>
    public enum EnemyStatus
    {
        InitialLaunch,
        Normal,
        Damaged,
        Dead
    }
}
