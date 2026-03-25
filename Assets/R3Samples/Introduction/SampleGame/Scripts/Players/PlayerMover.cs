using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using R3Samples.Introduction.SampleGame;
using UnityEngine;
using UnityEngine.Serialization;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤーの通常移動、ジャンプ、ノックバックなど移動そのものを担当する
    /// 接地状態、歩行状態、空中状態の公開もここで行う
    /// </summary>
    [RequireComponent(typeof(InputSystemInputEventProvider))]
    [RequireComponent(typeof(CharacterController2D))]
    public sealed class PlayerMover : MonoBehaviour
    {
        /// <summary>
        /// 通常ジャンプ速度
        /// </summary>
        [SerializeField] private float JumpSpeed = 10f;
        /// <summary>
        /// 入力状態供給元
        /// </summary>
        [FormerlySerializedAs("_inputEventProvider")] [SerializeField] private InputSystemInputEventProvider inputSystemInputEventProvider;

        /// <summary>
        /// 歩行状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isWalking = new(false);
        /// <summary>
        /// 実水平移動状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isActuallyMovingHorizontally = new(false);
        /// <summary>
        /// 接地状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isGrounded = new(false);
        /// <summary>
        /// 空中状態
        /// </summary>
        private readonly ReactiveProperty<PlayerAerial> _playerAerial = new(PlayerAerial.Grounded);
        /// <summary>
        /// ジャンプ通知
        /// </summary>
        private readonly Subject<Unit> _jumped = new();

        /// <summary>
        /// 2D移動制御
        /// </summary>
        private CharacterController2D _characterController2D;
        /// <summary>
        /// 入力受付可否
        /// </summary>
        private bool _isInputEnabled = true;

        public ReadOnlyReactiveProperty<bool> IsGrounded => _isGrounded;
        public ReadOnlyReactiveProperty<PlayerAerial> Aerial => _playerAerial;
        public ReadOnlyReactiveProperty<bool> IsWalking => _isWalking;
        public Observable<Unit> Jumped => _jumped;
        public Vector2 CurrentVelocity => _characterController2D.CurrentVelocity;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            inputSystemInputEventProvider = inputSystemInputEventProvider != null
                ? inputSystemInputEventProvider
                : GetComponent<InputSystemInputEventProvider>();
            _characterController2D = GetComponent<CharacterController2D>();
        }

        /// <summary>
        /// 状態購読と入力処理開始
        /// </summary>
        private void Start()
        {
            _isActuallyMovingHorizontally.AddTo(this);
            _isGrounded.AddTo(this);
            _jumped.AddTo(this);

            _characterController2D.IsGrounded.Subscribe(x => _isGrounded.Value = x)
                .AddTo(this);

            // 接地中かつ移動しているなら歩行状態
            Observable.CombineLatest(_isGrounded, _isActuallyMovingHorizontally)
                .Subscribe(v => _isWalking.Value = v[0] && v[1])
                .AddTo(this);

            this.UpdateAsObservable()
                // 通常移動入力は操作可能な状態のときだけ反映する。
                .Where(_ => _isInputEnabled)
                .Subscribe(_ => ApplyMoveInput())
                .AddTo(this);

            this.UpdateAsObservable()
                // ジャンプは操作可能かつ接地中のときだけ受け付ける。
                .Where(_ => _isInputEnabled)
                .Where(_ => inputSystemInputEventProvider.IsJumpButton.CurrentValue && IsGrounded.CurrentValue)
                .ThrottleFirstFrame(10)
                .Subscribe(_ => Jump())
                .AddTo(this);

            // 空中にいるかの判定
            CheckPlayerAerialAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// 通常移動入力を受け付けるかどうかを切り替える。
        /// 拘束中は入力を止め、水平移動も即座にリセットする。
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _isInputEnabled = enabled;
            if (!enabled)
            {
                UpdateHorizontalMove(0);
            }
        }

        /// <summary>
        /// 水平移動更新
        /// </summary>
        public void UpdateHorizontalMove(float x)
        {
            if (x == 0)
            {
                _characterController2D.Move(0);
                _isActuallyMovingHorizontally.Value = false;
            }
            else
            {
                _characterController2D.Move(x);
                _isActuallyMovingHorizontally.Value = true;
            }
        }

        /// <summary>
        /// 通常ジャンプ
        /// </summary>
        public void Jump()
        {
            _characterController2D.AddVelocity(Vector2.up * JumpSpeed);
            _jumped.OnNext(Unit.Default);
        }

        /// <summary>
        /// 指定速度ジャンプ
        /// </summary>
        public void Jump(float speed)
        {
            _characterController2D.AddVelocity(Vector2.up * speed);
        }

        /// <summary>
        /// 速度初期化
        /// </summary>
        public void SetVelocityZero()
        {
            _characterController2D.ClearConstantVelocityX();
            _characterController2D.OverrideVelocity(Vector2.zero);
        }

        /// <summary>
        /// 射出速度適用
        /// </summary>
        public void Launch(Vector2 velocity)
        {
            _characterController2D.Launch(velocity);
        }

        /// <summary>
        /// 操作ロック
        /// </summary>
        public void Lock()
        {
            _characterController2D.LockControl();
        }

        /// <summary>
        /// 操作ロック解除
        /// </summary>
        public void Unlock()
        {
            _characterController2D.UnlockControl();
        }

        /// <summary>
        /// 水平固定速度設定
        /// </summary>
        public void SetConstantX(float xSpeed)
        {
            if (xSpeed == 0)
            {
                _characterController2D.ClearConstantVelocityX();
                _isActuallyMovingHorizontally.Value = false;
            }
            else
            {
                _characterController2D.SetConstantVelocityX(xSpeed);
                _isActuallyMovingHorizontally.Value = true;
            }
        }

        /// <summary>
        /// 現在の方向入力をそのまま水平移動へ変換する。
        /// </summary>
        private void ApplyMoveInput()
        {
            var dir = inputSystemInputEventProvider.Direction.CurrentValue;

            if (dir is InputDirection.Left or InputDirection.UpLeft or InputDirection.DownLeft)
            {
                UpdateHorizontalMove(-1);
            }
            else if (dir is InputDirection.Right or InputDirection.UpRight or InputDirection.DownRight)
            {
                UpdateHorizontalMove(1);
            }
            else
            {
                UpdateHorizontalMove(0);
            }
        }

        // Playerの接地・上昇中・下降中の判定を行う
        /// <summary>
        /// 空中状態判定ループ
        /// </summary>
        private async UniTaskVoid CheckPlayerAerialAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // 着地まで待つ
                await IsGrounded.FirstAsync(x => x, ct);

                // 着地したのでGrounded
                _playerAerial.Value = PlayerAerial.Grounded;

                // 地面から離れるまで待つ
                await IsGrounded.FirstAsync(x => !x, ct);

                // 離れたときの速度が上向きなら
                if (CurrentVelocity.y > 0f)
                {
                    // 上昇中扱いにする
                    _playerAerial.Value = PlayerAerial.Rising;

                    // 落下し始めるまで待つ
                    await UniTask.WaitWhile(IsStillRising, cancellationToken: ct);

                    // 着地判定が発生していたら終了
                    if (IsGrounded.CurrentValue)
                    {
                        continue;
                    }
                }

                // 落下開始
                _playerAerial.Value = PlayerAerial.Falling;
            }

            return;

            bool IsStillRising()
            {
                return CurrentVelocity.y >= 0f
                       && !IsGrounded.CurrentValue;
            }
        }
    }
}
