using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// Enemy の行動に応じた効果音再生を一元管理する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(EnemyCore))]
    public sealed class EnemySoundEffectPlayer : MonoBehaviour
    {
        /// <summary>
        /// ジャンプ音
        /// </summary>
        [SerializeField] private AudioClip _jumpClip;
        /// <summary>
        /// 被弾音
        /// </summary>
        [SerializeField] private AudioClip _damagedClip;
        /// <summary>
        /// 死亡音
        /// </summary>
        [SerializeField] private AudioClip _deadClip;

        /// <summary>
        /// 敵本体
        /// </summary>
        private EnemyCore _enemyCore;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _enemyCore = GetComponent<EnemyCore>();
        }

        /// <summary>
        /// 効果音購読登録
        /// </summary>
        private void Start()
        {
            _enemyCore.Jumped
                .Subscribe(_ => PlayOneShot(_jumpClip))
                .AddTo(this);

            _enemyCore.Status
                .DistinctUntilChanged()
                .Skip(1)
                .Subscribe(status =>
                {
                    switch (status)
                    {
                        case EnemyStatus.Damaged:
                            PlayOneShot(_damagedClip);
                            break;
                        case EnemyStatus.Dead:
                            PlayOneShot(_deadClip);
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
            if (_enemyCore.AudioSource == null || clip == null)
            {
                return;
            }

            _enemyCore.AudioSource.PlayOneShot(clip);
        }
    }
}
