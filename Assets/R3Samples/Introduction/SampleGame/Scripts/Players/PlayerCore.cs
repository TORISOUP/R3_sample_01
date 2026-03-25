using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Serialization;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤー全体の公開状態を集約し、各プレイヤー用コンポーネントを束ねる責務を持つ
    /// 向き、行動状態、被ダメージ窓口を統合して、外部から参照しやすい形で公開する
    /// </summary>
    [RequireComponent(typeof(PlayerAttack))]
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerDamageHandler))]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class PlayerCore : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// 入力状態供給元
        /// </summary>
        [FormerlySerializedAs("_inputEventProvider")] [SerializeField]
        private InputSystemInputEventProvider inputSystemInputEventProvider;

        /// <summary>
        /// 向き状態
        /// </summary>
        private readonly ReactiveProperty<PlayerDirection> _playerDirection = new(PlayerDirection.Right);

        /// <summary>
        /// 行動状態
        /// </summary>
        private readonly ReactiveProperty<PlayerActionState> _playerActionState = new(PlayerActionState.Controllable);

        /// <summary>
        /// 初期化完了通知
        /// </summary>
        private readonly UniTaskCompletionSource _initializedCompletionSource = new();

        private PlayerMover _playerMover;
        private PlayerDamageHandler _playerDamageHandler;
        private PlayerAttackController _playerAttackController;

        public AttackerType Type => AttackerType.Player;

        /// <summary>
        /// 向き
        /// </summary>
        public ReadOnlyReactiveProperty<PlayerDirection> Direction => _playerDirection;

        /// <summary>
        /// 現在の体力
        /// </summary>
        public ReadOnlyReactiveProperty<int> PlayerHealth => _playerDamageHandler.PlayerHealth;

        /// <summary>
        /// ダメージ状態
        /// </summary>
        public ReadOnlyReactiveProperty<PlayerDamageState> DamageState => _playerDamageHandler.DamageState;

        /// <summary>
        /// 無敵状態
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsPlayerInvincible => _playerDamageHandler.IsPlayerInvincible;

        public UniTask InitializedAsync() => _initializedCompletionSource.Task;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _playerMover = GetComponent<PlayerMover>();
            _playerDamageHandler = GetComponent<PlayerDamageHandler>();
            _playerAttackController = GetComponent<PlayerAttackController>();
        }

        /// <summary>
        /// 状態購読と初期化完了通知
        /// </summary>
        private void Start()
        {
            _playerDirection.AddTo(this);
            _playerActionState.AddTo(this);
            _playerAttackController.SetFacingDirection(_playerDirection.CurrentValue);

            // 操作可能なときだけ入力で向きを更新する
            inputSystemInputEventProvider.Direction
                .Where(_ => IsPlayerControllable())
                .Subscribe(UpdateDirectionFromInput)
                .AddTo(this);

            // 各コンポーネントが持つ状態から、プレイヤー全体の行動状態を導出する
            // 気絶状態、ダメージ状態、攻撃中は操作不能にしたいのでその判定処理
            Observable.CombineLatest(
                    _playerAttackController.IsAttackLocked,
                    _playerDamageHandler.IsLockedByDamage,
                    _playerDamageHandler.DamageState,
                    DetermineActionState
                )
                // 攻撃拘束とダメージ拘束を 1 つの行動状態に集約する。
                .Subscribe(ApplyActionState)
                .AddTo(this);

            // 気絶状態になったら着地時に座標を固定する
            _playerDamageHandler.DamageState
                .Where(x => x == PlayerDamageState.Fainted)
                .Take(1)
                .SubscribeAwait(async (_, ct) =>
                {
                    await _playerMover.IsGrounded.FirstOrDefaultAsync(x => x, cancellationToken: ct);
                    _playerMover.SetVelocityZero();
                    _playerMover.UpdateHorizontalMove(0);
                    _playerMover.Lock();
                })
                .AddTo(this);

            _initializedCompletionSource.TrySetResult();
        }

        /// <summary>
        /// 指定座標との相対方向取得
        /// </summary>
        public Vector2 GetDirection(Vector2 position)
        {
            return position - (Vector2)transform.position;
        }

        /// <summary>
        /// 被ダメージ受付
        /// </summary>
        public void OnDamaged(Damage damage)
        {
            // ダメージ処理を委譲
            var result = _playerDamageHandler.ApplyDamage(damage, CancelAttack);
            // ダメージが通らなかったら何もしない
            if (!result.Accepted) return;

            // ダメージを受けたなら、プレイヤーの向きを上書きする
            ApplyDirection(result.NextDirection);
        }

        /// <summary>
        /// 初期化待機キャンセル
        /// </summary>
        private void OnDestroy()
        {
            _initializedCompletionSource.TrySetCanceled();
        }

        /// <summary>
        /// 操作可能判定
        /// </summary>
        private bool IsPlayerControllable()
        {
            return _playerActionState.CurrentValue == PlayerActionState.Controllable;
        }

        /// <summary>
        /// 複数のフラグより現在の状態を決定する
        /// </summary>
        private static PlayerActionState DetermineActionState(
            bool isLockedByAttack,
            bool isLockedByDamage,
            PlayerDamageState damageState)
        {
            // 気絶は最優先で扱う。
            if (damageState == PlayerDamageState.Fainted)
            {
                return PlayerActionState.Fainted;
            }

            // 被弾拘束中は攻撃拘束よりも強い状態として扱う。
            if (isLockedByDamage)
            {
                return PlayerActionState.DamageLocked;
            }

            if (isLockedByAttack)
            {
                return PlayerActionState.AttackLocked;
            }

            return PlayerActionState.Controllable;
        }

        /// <summary>
        /// プレイヤーの向きを更新し、AttackControllerにも共有
        /// </summary>
        private void ApplyDirection(PlayerDirection direction)
        {
            _playerDirection.Value = direction;
            _playerAttackController.SetFacingDirection(direction);
        }

        /// <summary>
        /// 入力から向き更新
        /// </summary>
        private void UpdateDirectionFromInput(InputDirection inputDirection)
        {
            if (inputDirection is InputDirection.Left or InputDirection.UpLeft or InputDirection.DownLeft)
            {
                ApplyDirection(PlayerDirection.Left);
            }
            else if (inputDirection is InputDirection.Right or InputDirection.UpRight or InputDirection.DownRight)
            {
                ApplyDirection(PlayerDirection.Right);
            }
        }

        /// <summary>
        /// 行動状態反映
        /// </summary>
        private void ApplyActionState(PlayerActionState state)
        {
            _playerActionState.Value = state;

            // 通常操作可能なときだけ PlayerMover が入力を受け付ける。
            _playerMover.SetInputEnabled(state == PlayerActionState.Controllable);

            if (state == PlayerActionState.Controllable)
            {
                SyncDirectionFromCurrentInput();
            }
        }

        /// <summary>
        /// 現在入力から向き同期
        /// </summary>
        private void SyncDirectionFromCurrentInput()
        {
            UpdateDirectionFromInput(inputSystemInputEventProvider.Direction.CurrentValue);
        }

        /// <summary>
        /// 攻撃中断
        /// </summary>
        private void CancelAttack()
        {
            _playerAttackController.CancelCurrentAttack();
        }
    }

    /// <summary>
    /// プレイヤー向き
    /// </summary>
    public enum PlayerDirection
    {
        Right,
        Left
    }

    /// <summary>
    /// プレイヤーダメージ状態
    /// </summary>
    public enum PlayerDamageState
    {
        /// <summary>
        /// 通常状態、攻撃や操作可能
        /// </summary>
        None,

        /// <summary>
        /// ダメージで吹っ飛び状態、操作不能
        /// </summary>
        Damaged,

        /// <summary>
        /// 気絶、操作不能
        /// </summary>
        Fainted
    }

    /// <summary>
    /// プレイヤー行動状態
    /// </summary>
    public enum PlayerActionState
    {
        /// <summary>
        /// 通常操作が可能な状態
        /// </summary>
        Controllable,

        /// <summary>
        /// 攻撃モーション中などで操作を拘束している状態
        /// </summary>
        AttackLocked,

        /// <summary>
        /// 被弾中で操作を拘束している状態
        /// </summary>
        DamageLocked,

        /// <summary>
        /// 気絶して操作不能な状態
        /// </summary>
        Fainted
    }

    /// <summary>
    /// プレイヤー空中状態
    /// </summary>
    public enum PlayerAerial
    {
        Grounded,
        Rising,
        Falling,
    }
}