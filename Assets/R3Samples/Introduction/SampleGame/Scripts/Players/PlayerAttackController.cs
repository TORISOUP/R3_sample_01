using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 攻撃入力の解釈、コマンド判定、攻撃開始と攻撃拘束状態の管理を担当する
    /// 入力からどの技を出すかを決めて PlayerAttack に委譲する
    /// </summary>
    [RequireComponent(typeof(PlayerAttack))]
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerDamageHandler))]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        /// <summary>
        /// 検知済みコマンド情報
        /// </summary>
        private record DetectedCommand(CommandMove Move, long Timestamp)
        {
            public static readonly DetectedCommand None = new(CommandMove.None, 0);
        }

        /// <summary>
        /// 攻撃入力情報
        /// </summary>
        private record AttackInput(long Timestamp);

        /// <summary>
        /// 攻撃判定用入力束
        /// </summary>
        private record AttackRequest(AttackInput Input, DetectedCommand Command);

        /// <summary>
        /// コマンド成立後に攻撃ボタンを押すまでの猶予(30F)
        /// </summary>
        private static readonly TimeSpan CommandAttackWindow = TimeSpan.FromSeconds(30f / 60);

        /// <summary>
        /// 入力状態供給元
        /// </summary>
        [SerializeField] private InputSystemInputEventProvider inputSystemInputEventProvider;

        /// <summary>
        /// 攻撃拘束状態
        /// </summary>
        private readonly ReactiveProperty<bool> _isAttackLocked = new(false);
        /// <summary>
        /// 攻撃開始通知
        /// </summary>
        private readonly Subject<PlayerAttackType> _attackStarted = new();

        private PlayerAttack _playerAttack;
        private PlayerMover _playerMover;
        private PlayerDamageHandler _playerDamageHandler;
        private PlayerCommandDetector _playerCommandDetector;
        private CancellationTokenSource _attackCancellationSource;
        private PlayerDirection _currentDirection = PlayerDirection.Right;

        /// <summary>
        /// 攻撃モーション中による操作不能状態
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsAttackLocked => _isAttackLocked;

        public Observable<PlayerAttackType> AttackStarted => _attackStarted;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _playerAttack = GetComponent<PlayerAttack>();
            _playerMover = GetComponent<PlayerMover>();
            _playerDamageHandler = GetComponent<PlayerDamageHandler>();
            inputSystemInputEventProvider = inputSystemInputEventProvider != null
                ? inputSystemInputEventProvider
                : GetComponent<InputSystemInputEventProvider>();
        }

        /// <summary>
        /// 初期化と購読登録
        /// </summary>
        private void Start()
        {
            _isAttackLocked.AddTo(this);
            _attackStarted.AddTo(this);

            // コマンド入力の解釈を委譲
            _playerCommandDetector = new PlayerCommandDetector(inputSystemInputEventProvider);
            _playerCommandDetector.AddTo(this);

            SetUpAttack();
        }

        /// <summary>
        /// 後始末
        /// </summary>
        private void OnDestroy()
        {
            _attackCancellationSource?.Cancel();
            _attackCancellationSource?.Dispose();
            _attackCancellationSource = null;
        }

        /// <summary>
        /// 現在の向きを更新する
        /// 攻撃実行時はこの向きを使って技の進行方向を決める
        /// </summary>
        public void SetFacingDirection(PlayerDirection direction)
        {
            _currentDirection = direction;
        }

        /// <summary>
        /// 被弾時などに実行中の攻撃を中断する
        /// </summary>
        public void CancelCurrentAttack()
        {
            _attackCancellationSource?.Cancel();
        }

        /// <summary>
        /// 攻撃開始可否判定
        /// </summary>
        private bool CanStartAttack()
        {
            // 攻撃は地上にいて、攻撃拘束中でもダメージ拘束中でもないときだけ開始する。
            return _playerMover.IsGrounded.CurrentValue
                   && !_isAttackLocked.CurrentValue
                   && !_playerDamageHandler.IsLockedByDamage.CurrentValue
                   && _playerDamageHandler.DamageState.CurrentValue != PlayerDamageState.Fainted;
        }

        /// <summary>
        /// 攻撃入力購読設定
        /// </summary>
        private void SetUpAttack()
        {
            var timeProvider = UnityTimeProvider.Update;

            var detectedCommands = _playerCommandDetector.CommandMove
                .Where(move => move != CommandMove.None)
                // コマンド入力が成立した瞬間、コマンド内容とそのときの時刻を保持する
                .Timestamp(timeProvider)
                .Select(x => new DetectedCommand(x.Value, x.Timestamp))
                .Prepend(DetectedCommand.None);

            var attackPressed = inputSystemInputEventProvider.IsAttackButton
                .DistinctUntilChanged()
                .Where(x => x)
                // 攻撃ボタンが押された時刻を付与
                .Timestamp(timeProvider)
                .Select(x => new AttackInput(x.Timestamp));

            var attackRequests = attackPressed
                // 攻撃開始は通常操作可能かつ接地中のときだけ許可
                .Where(_ => CanStartAttack())
                // 攻撃ボタンが押されたら直近のコマンド成立状態をもってくる
                .WithLatestFrom(detectedCommands, (input, command) => new AttackRequest(input, command));

            var attackTypes = attackRequests
                .Select(request => ResolveAttackType(request, timeProvider));

            attackTypes
                // 技が完遂するからキャンセルされるまで次の攻撃操作は無視(AwaitOperation.Drop)
                .SubscribeAwait(async (attackType, _) =>
                {
                    // 新しい攻撃を始める前に、前回の攻撃処理を中断できるトークンを作り直す。
                    using var attackCancellation = CreateAttackCancellationTokenSource();

                    switch (attackType)
                    {
                        case PlayerAttackType.Punch:
                            await NormalPunchAsync(attackCancellation.Token);
                            break;
                        case PlayerAttackType.UpperPunch:
                            await UpperPunchAsync(attackCancellation.Token);
                            break;
                        case PlayerAttackType.SpinAttack:
                            await SpinAttackAsync(attackCancellation.Token);
                            break;
                        case PlayerAttackType.FireShot:
                            await FireShotAsync(attackCancellation.Token);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }, AwaitOperation.Drop)
                .AddTo(this);
        }

        /// <summary>
        /// 攻撃種別解決
        /// </summary>
        private static PlayerAttackType ResolveAttackType(AttackRequest request, TimeProvider timeProvider)
        {
            var elapsed = 
                timeProvider.GetElapsedTime(request.Command.Timestamp, request.Input.Timestamp);
            
            // 指定の技が入力猶予フレーム以内に入力できていたか？
            // できてないならただのパンチ
            if (elapsed > CommandAttackWindow)
            {
                return PlayerAttackType.Punch;
            }

            // 入力猶予フレーム以内ならそれぞれの技の発動を許可する
            return request.Command.Move switch
            {
                CommandMove.UpperPunch => PlayerAttackType.UpperPunch,
                CommandMove.SpinAttack => PlayerAttackType.SpinAttack,
                CommandMove.FireShot => PlayerAttackType.FireShot,
                _ => PlayerAttackType.Punch
            };
        }

        /// <summary>
        /// 実行中の攻撃を中断できるCancellationTokenをつくる
        /// </summary>
        private CancellationTokenSource CreateAttackCancellationTokenSource()
        {
            _attackCancellationSource?.Cancel();
            _attackCancellationSource?.Dispose();
            _attackCancellationSource = new CancellationTokenSource();
            return CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken,
                _attackCancellationSource.Token);
        }

        /// <summary>
        /// 通常攻撃実行
        /// </summary>
        private async UniTask NormalPunchAsync(CancellationToken ct)
        {
            try
            {
                _isAttackLocked.Value = true;
                _attackStarted.OnNext(PlayerAttackType.Punch);
                await _playerAttack.PunchAttackAsync(ct);
            }
            finally
            {
                // 気絶していない場合だけ攻撃拘束を解除する
                if (_playerDamageHandler.DamageState.CurrentValue != PlayerDamageState.Fainted)
                {
                    _isAttackLocked.Value = false;
                }
            }
        }

        /// <summary>
        /// 昇竜拳実行
        /// </summary>
        private async UniTask UpperPunchAsync(CancellationToken ct)
        {
            try
            {
                _isAttackLocked.Value = true;
                _attackStarted.OnNext(PlayerAttackType.UpperPunch);
                await _playerAttack.UpperPunchAttackAsync(_currentDirection, ct);
            }
            finally
            {
                // 気絶していない場合だけ攻撃拘束を解除する
                if (_playerDamageHandler.DamageState.CurrentValue != PlayerDamageState.Fainted)
                {
                    _isAttackLocked.Value = false;
                }
            }
        }

        /// <summary>
        /// 回転攻撃実行
        /// </summary>
        private async UniTask SpinAttackAsync(CancellationToken ct)
        {
            try
            {
                _isAttackLocked.Value = true;
                _attackStarted.OnNext(PlayerAttackType.SpinAttack);
                await _playerAttack.SpinAttackAsync(_currentDirection, ct);
            }
            finally
            {
                // 気絶していない場合だけ攻撃拘束を解除する
                if (_playerDamageHandler.DamageState.CurrentValue != PlayerDamageState.Fainted)
                {
                    _isAttackLocked.Value = false;
                }
            }
        }

        /// <summary>
        /// 飛び道具実行
        /// </summary>
        private async UniTask FireShotAsync(CancellationToken ct)
        {
            try
            {
                _isAttackLocked.Value = true;
                _attackStarted.OnNext(PlayerAttackType.FireShot);
                await _playerAttack.FireShotAsync(_currentDirection, ct);
            }
            finally
            {
                // 気絶していない場合だけ攻撃拘束を解除する。
                if (_playerDamageHandler.DamageState.CurrentValue != PlayerDamageState.Fainted)
                {
                    _isAttackLocked.Value = false;
                }
            }
        }
    }

    /// <summary>
    /// プレイヤー攻撃種別
    /// </summary>
    public enum PlayerAttackType
    {
        Punch,
        UpperPunch,
        SpinAttack,
        FireShot
    }
}
