using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 敵アニメーション反映
    /// </summary>
    public sealed class EnemyAnimation : MonoBehaviour
    {
        /// <summary>
        /// 見た目ルート
        /// </summary>
        [SerializeField] private Transform _spriteRoot;

        /// <summary>
        /// 敵本体
        /// </summary>
        private EnemyCore _enemyCore;
        /// <summary>
        /// アニメーター
        /// </summary>
        private Animator _animator;
        /// <summary>
        /// アニメーションイベント検知
        /// </summary>
        private AnimationEventDetector _animationEventDetector;

        /// <summary>
        /// 参照取得と購読登録
        /// </summary>
        private void Start()
        {
            _animator = _spriteRoot.GetComponentInChildren<Animator>();
            _enemyCore = GetComponent<EnemyCore>();
            _animationEventDetector = _spriteRoot.GetComponentInChildren<AnimationEventDetector>();

            // 向きを反映
            _enemyCore.Direction
                .Subscribe(x =>
                {
                    _spriteRoot.localScale = new Vector3(x == EnemyDirection.Left ? 1f : -1f, 1f, 1f);
                })
                .AddTo(this);

            // 敵のダメージ状態を反映
            _enemyCore.Status
                .Subscribe(x =>
                {
                    switch (x)
                    {
                        case EnemyStatus.Normal:
                        case EnemyStatus.InitialLaunch:
                            _animator.Play("EnemyNormal");
                            break;
                        case EnemyStatus.Damaged:
                            _animator.Play("EnemyDamaged");
                            break;
                        case EnemyStatus.Dead:
                            _animator.Play("EnemyDead");
                            WaitForAnimationEndAsync(destroyCancellationToken).Forget();
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(x), x, null);
                    }
                }).AddTo(this);
        }

        /// <summary>
        /// 死亡アニメーション終了待機
        /// </summary>
        private async UniTaskVoid WaitForAnimationEndAsync(CancellationToken token)
        {
            await _animationEventDetector.AnimationEnded.FirstAsync(token);
            _enemyCore.OnDeadAnimationEnded();
        }
    }
}
