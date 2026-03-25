using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤーの行動イベントを監視し、効果音再生を一元管理する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(PlayerCore))]
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class PlayerSoundEffectPlayer : MonoBehaviour
    {
        /// <summary>
        /// 効果音再生元
        /// </summary>
        [SerializeField] private AudioSource _audioSource;
        /// <summary>
        /// ジャンプ音
        /// </summary>
        [SerializeField] private AudioClip _jumpClip;
        /// <summary>
        /// 着地音
        /// </summary>
        [SerializeField] private AudioClip _landingClip;
        /// <summary>
        /// 通常攻撃音
        /// </summary>
        [SerializeField] private AudioClip _punchClip;
        /// <summary>
        /// 昇竜拳音
        /// </summary>
        [SerializeField] private AudioClip _upperPunchClip;
        /// <summary>
        /// 回転攻撃音
        /// </summary>
        [SerializeField] private AudioClip _spinAttackClip;
        /// <summary>
        /// 飛び道具音
        /// </summary>
        [SerializeField] private AudioClip _fireShotClip;
        /// <summary>
        /// 被弾音
        /// </summary>
        [SerializeField] private AudioClip _damageClip;
        /// <summary>
        /// 気絶音
        /// </summary>
        [SerializeField] private AudioClip _faintedClip;

        /// <summary>
        /// プレイヤー本体
        /// </summary>
        private PlayerCore _playerCore;
        /// <summary>
        /// 移動制御
        /// </summary>
        private PlayerMover _playerMover;
        /// <summary>
        /// 攻撃制御
        /// </summary>
        private PlayerAttackController _playerAttackController;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _playerCore = GetComponent<PlayerCore>();
            _playerMover = GetComponent<PlayerMover>();
            _playerAttackController = GetComponent<PlayerAttackController>();
        }

        /// <summary>
        /// 効果音購読登録
        /// </summary>
        private void Start()
        {
            _playerMover.Jumped
                .Subscribe(_ => PlayOneShot(_jumpClip))
                .AddTo(this);

            _playerMover.IsGrounded
                .DistinctUntilChanged()
                .Where(isGrounded => isGrounded)
                .Skip(1)
                .Subscribe(_ => PlayOneShot(_landingClip))
                .AddTo(this);

            _playerAttackController.AttackStarted
                .Subscribe(attackType =>
                {
                    switch (attackType)
                    {
                        case PlayerAttackType.Punch:
                            PlayOneShot(_punchClip);
                            break;
                        case PlayerAttackType.UpperPunch:
                            PlayOneShot(_upperPunchClip);
                            break;
                        case PlayerAttackType.SpinAttack:
                            PlayOneShot(_spinAttackClip);
                            break;
                        case PlayerAttackType.FireShot:
                            PlayOneShot(_fireShotClip);
                            break;
                    }
                })
                .AddTo(this);

            _playerCore.DamageState
                .DistinctUntilChanged()
                .Subscribe(damageState =>
                {
                    switch (damageState)
                    {
                        case PlayerDamageState.Damaged:
                            PlayOneShot(_damageClip);
                            break;
                        case PlayerDamageState.Fainted:
                            PlayOneShot(_faintedClip);
                            break;
                    }
                })
                .AddTo(this);
        }

        /// <summary>
        /// 効果音単発再生
        /// </summary>
        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource == null || clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip);
        }
    }
}
