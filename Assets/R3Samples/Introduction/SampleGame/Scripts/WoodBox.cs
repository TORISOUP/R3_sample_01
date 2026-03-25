using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 箱の被弾応答と衝突音再生
    /// </summary>
    public class WoodBox : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// 衝突音一覧
        /// </summary>
        [SerializeField] private AudioClip[] _crashClips;
        /// <summary>
        /// 衝突音再生判定速度
        /// </summary>
        [SerializeField] private float _crashMagnitudeThreshold = 4.0f;

        /// <summary>
        /// 物理本体
        /// </summary>
        private Rigidbody2D _rigidbody;
        /// <summary>
        /// 効果音再生元
        /// </summary>
        private AudioSource _audioSource;

        public AttackerType Type => AttackerType.Unknown;

        /// <summary>
        /// 参照取得と衝突監視
        /// </summary>
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _rigidbody = GetComponent<Rigidbody2D>();

            this.OnCollisionEnter2DAsObservable()
                .Where(_ => _rigidbody.linearVelocity.magnitude > _crashMagnitudeThreshold)
                .ThrottleFirst(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    PlayCrashSound();
                })
                .AddTo(this);
        }

        /// <summary>
        /// 指定座標との相対方向取得
        /// </summary>
        public Vector2 GetDirection(Vector2 position)
        {
            return position - (Vector2)transform.position;
        }

        /// <summary>
        /// 被弾時の吹き飛び反映
        /// </summary>
        public void OnDamaged(Damage damage)
        {
            _rigidbody.linearVelocity = damage.Velocity;

            PlayCrashSound();
        }

        /// <summary>
        /// 衝突音ランダム再生
        /// </summary>
        private void PlayCrashSound()
        {
            var clip = _crashClips[UnityEngine.Random.Range(0, _crashClips.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
}
