using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame.Attacks
{
    /// <summary>
    /// 飛び道具本体の移動と衝突処理
    /// </summary>
    public class FireShotBullet : MonoBehaviour
    {
        /// <summary>
        /// 弾スプライト
        /// </summary>
        [SerializeField] SpriteRenderer _spriteRenderer;

        /// <summary>
        /// 物理本体
        /// </summary>
        private Rigidbody2D _rigidbody2D;
        /// <summary>
        /// 非ダメージ対象への衝突回数
        /// </summary>
        private int _hitCount = 0;
        /// <summary>
        /// 初期化済み判定
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 弾初期化
        /// </summary>
        public void Initialize(Vector2 initialVelocity, float damage)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _rigidbody2D = GetComponent<Rigidbody2D>();

            _rigidbody2D.linearVelocity = initialVelocity;

            _spriteRenderer.gameObject.SetActive(true);

            this.UpdateAsObservable()
                .Subscribe(_ => transform.right = _rigidbody2D.linearVelocity.normalized)
                .AddTo(this);

            this.OnCollisionEnter2DAsObservable()
                .Subscribe(c =>
                {
                    if (c.gameObject.TryGetComponent<IDamageable>(out var damageable))
                    {
                        // 発射直後の自機接触を防ぎつつ、跳ね返り後のみ自機被弾を許可する
                        // ノーバウンド状態でPlayerに当たった場合は無視する
                        // （仕込んではみたものの、最終的にCollider側の設定でプレイヤーと衝突しないようにした）
                        if (_hitCount == 0 && damageable.Type == AttackerType.Player) return;

                        // なんかわからないのにあたったら無視
                        if (damageable.Type == AttackerType.Unknown) return;
                        
                        var direction = damageable.GetDirection(transform.position);
                        // 相手との相対位置から左右の吹き飛び方向のみを決め、常に上方向成分を加える。
                        var futtobi = new Vector2(direction.x > 0 ? -1 : 1, 1).normalized;
                        damageable.OnDamaged(new Damage(AttackerType.Player, damage, futtobi));

                        Destroy(gameObject);
                    }
                    else
                    {
                        if (_hitCount++ == 3)
                        {
                            Destroy(gameObject);
                        }
                    }
                })
                .AddTo(this);

            Destroy(gameObject, 3f);
        }
    }
}
