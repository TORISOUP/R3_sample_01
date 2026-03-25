using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using Random = UnityEngine.Random;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤーの見た目とアニメーション遷移を担当する
    /// 向き、歩行、空中状態、被ダメージ状態をアニメーターへ反映する
    /// </summary>
    public sealed class PlayerAnimation : MonoBehaviour
    {
        /// <summary>
        /// 見た目ルート
        /// </summary>
        [SerializeField] private Transform _spriteRoot;

        /// <summary>
        /// プレイヤー本体
        /// </summary>
        private PlayerCore _playerCore;
        /// <summary>
        /// 移動制御
        /// </summary>
        private PlayerMover _playerMover;
        /// <summary>
        /// アニメーター
        /// </summary>
        private Animator _animator;
        /// <summary>
        /// アニメーションイベント検知
        /// </summary>
        private AnimationEventDetector _animationEventDetector;
        /// <summary>
        /// スプライト描画
        /// </summary>
        private SpriteRenderer _spriteRenderer;

        private readonly string IsWalking = "IsWalking";
        private readonly string JumpUpTrigger = "JumpUp";
        private readonly string IsFalling = "IsFalling";
        private readonly string IsGrounded = "IsGrounded";

        /// <summary>
        /// 参照取得と購読登録
        /// </summary>
        private void Start()
        {
            _animationEventDetector = GetComponentInChildren<AnimationEventDetector>();
            _spriteRenderer = _spriteRoot.GetComponentInChildren<SpriteRenderer>();
            _playerCore = GetComponent<PlayerCore>();
            _animator = GetComponentInChildren<Animator>();
            _playerMover = GetComponent<PlayerMover>();

            // 向き
            _playerCore.Direction
                .Subscribe(x =>
                {
                    _spriteRoot.localScale =
                        x == PlayerDirection.Left ? new Vector3(-1, 1, 1) : new Vector3(1, 1, 1);
                })
                .AddTo(this);

            // 歩きモーション反映
            _playerMover.IsWalking
                .Subscribe(x => _animator.SetBool(IsWalking, x))
                .AddTo(this);

            // 空中状態反映
            _playerMover.Aerial.Subscribe(x =>
            {
                switch (x)
                {
                    case PlayerAerial.Grounded:
                        _animator.SetBool(IsGrounded, true);
                        _animator.SetBool(IsFalling, false);
                        break;
                    case PlayerAerial.Rising:
                        _animator.SetBool(IsGrounded, false);
                        _animator.SetTrigger(JumpUpTrigger);
                        break;
                    case PlayerAerial.Falling:
                        _animator.SetBool(IsGrounded, false);
                        _animator.SetBool(IsFalling, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(x), x, null);
                }
            }).AddTo(this);

            // やられモーション
            _playerCore.DamageState
                .Subscribe(x =>
                {
                    switch (x)
                    {
                        case PlayerDamageState.None:
                            ResetAnimation();
                            break;
                        case PlayerDamageState.Damaged:
                            _animator.Play("PlayerDamaged");
                            break;
                        case PlayerDamageState.Fainted:
                            _animator.Play("PlayerFainted");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(x), x, null);
                    }
                })
                .AddTo(this);

            // 無敵状態なら半透明にする
            // ただしくらいモーション中は半透明ではない
            _playerCore.IsPlayerInvincible
                .CombineLatest(_playerCore.DamageState,
                    ((isInvincible, damageState) => isInvincible && damageState == PlayerDamageState.None))
                .Subscribe(x =>
                {
                    // trueの時は半透明にする
                    _spriteRenderer.color = x ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
                })
                .AddTo(this);
        }

        /// <summary>
        /// 回転攻撃アニメーション開始
        /// </summary>
        public async UniTask StartSpinAttackAsync(CancellationToken token)
        {
            _animator.Play("PlayerSpinAttack");
            await _animationEventDetector.AnimationEnded.FirstAsync(token);
        }

        /// <summary>
        /// 昇竜拳アニメーション開始
        /// </summary>
        public void StartUpperPunchAnimation()
        {
            var random = Random.Range(0, 3);
            switch (random)
            {
                case 0:
                    _animator.Play("PlayerJumpAttack1");
                    break;
                case 1:
                    _animator.Play("PlayerJumpAttack2");
                    break;
                default:
                    _animator.Play("PlayerJumpAttack3");
                    break;
            }
        }

        /// <summary>
        /// 通常攻撃アニメーション開始
        /// </summary>
        public void StartUnManagedPunchAnimation()
        {
            _animator.Play("PlayerAttack");
        }

        /// <summary>
        /// 落下アニメーション開始
        /// </summary>
        public void StartUnManagedFallDownAnimation()
        {
            _animator.Play("PlayerFallDown_UnManaged");
        }

        /// <summary>
        /// 着地アニメーション開始
        /// </summary>
        public void StartUnManagedLandingAnimation()
        {
            _animator.Play("PlayerLanding");
        }

        /// <summary>
        /// 停止アニメーション開始
        /// </summary>
        public void StartUnManagedStoppingAnimation()
        {
            _animator.Play("PlayerStopping");
        }

        /// <summary>
        /// 飛び道具アニメーション開始
        /// </summary>
        public void StartUnManagedShotAnimation()
        {
            _animator.Play("PlayerShot");
        }

        /// <summary>
        /// アニメーション初期化
        /// </summary>
        public void ResetAnimation()
        {
            _animator.Rebind();
            _animator.Update(0);
        }
    }
}
