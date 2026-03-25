using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 敵射出砲台の生成制御
    /// </summary>
    public class CannonCore : MonoBehaviour
    {
        /// <summary>
        /// 敵管理
        /// </summary>
        [SerializeField] private EnemyManager _enemyManager;
        /// <summary>
        /// 衝撃波判定
        /// </summary>
        [SerializeField] private Collider2D _shockWaveCollider2D;
        /// <summary>
        /// 発射位置
        /// </summary>
        [SerializeField] private Transform _muzzle;

        /// <summary>
        /// 発射準備音
        /// </summary>
        [SerializeField] private AudioClip _prepareShotClip;
        /// <summary>
        /// 発射音
        /// </summary>
        [SerializeField] private AudioClip _shotClip;

        /// <summary>
        /// 発射最低速度
        /// </summary>
        [SerializeField] private float _shotMinVelocity = 7;
        /// <summary>
        /// 発射最高速度
        /// </summary>
        [SerializeField] private float _shotMaxVelocity = 9;

        /// <summary>
        /// 効果音再生元
        /// </summary>
        private AudioSource _audioSource;
        /// <summary>
        /// アニメーションイベント検知
        /// </summary>
        private AnimationEventDetector _animationEventDetector;
        /// <summary>
        /// アニメーター
        /// </summary>
        private Animator _animator;

        /// <summary>
        /// 現在出現設定
        /// </summary>
        private EnemyPattern _currentPattern;
        /// <summary>
        /// 衝撃波除外対象
        /// </summary>
        private GameObject _shockwaveIgnoreTarget;

        /// <summary>
        /// 参照取得
        /// </summary>
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _animator = GetComponent<Animator>();
            _animationEventDetector = GetComponent<AnimationEventDetector>();
        }

        /// <summary>
        /// 生成ループ開始と衝撃波監視
        /// </summary>
        private void Start()
        {
            EnemyLoopAsync(destroyCancellationToken).Forget();

            // 大砲発射時、大砲に近いオブジェクトを吹き飛ばす
            _shockWaveCollider2D.OnTriggerEnter2DAsObservable()
                .Subscribe(x =>
                {
                    if (x.gameObject == _shockwaveIgnoreTarget) return;
                    if (!x.gameObject.TryGetComponent<IDamageable>(out var damageable)) return;
                    var dir = new Vector2(-1, 1).normalized;
                    damageable.OnDamaged(new Damage(AttackerType.Unknown, 0, dir * 30));
                })
                .AddTo(this);
        }

        /// <summary>
        /// 初期パターン設定
        /// </summary>
        public void Initialize(EnemyPattern initialPattern)
        {
            _currentPattern = initialPattern;
        }

        /// <summary>
        /// 敵パターン更新
        /// </summary>
        public void UpdateEnemyPatter(EnemyPattern pattern)
        {
            _currentPattern = pattern;
        }

        /// <summary>
        /// 敵生成ループ
        /// </summary>
        private async UniTaskVoid EnemyLoopAsync(CancellationToken token)
        {
            // 最初はちょっと待つ
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
            
            while (!token.IsCancellationRequested)
            {
                // フィールド上の敵の数が目標数より減ってるなら生成
                while (!token.IsCancellationRequested &&
                       _enemyManager.CurrentEnemyCount.CurrentValue < _currentPattern.EnemyCount)
                {
                    await CreateEnemyAsync(token);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: token);
            }
        }

        /// <summary>
        /// 単体敵生成
        /// </summary>
        private async UniTask CreateEnemyAsync(CancellationToken ct)
        {
            // 発射アニメーション再生
            _animator.Play("CannonShot");
            _audioSource.PlayOneShot(_prepareShotClip);

            // 発射タイミングがくるまで待機
            await _animationEventDetector.AnimationEvent.FirstOrDefaultAsync(cancellationToken: ct);

            // 新しく敵を生成
            var e = _enemyManager.CreateEnemy(_currentPattern.MaxHealth, _muzzle.position);

            // 敵の発射方向とかを設定
            var direction = new Vector2(-10f, Random.Range(0, 4f)).normalized;
            var power = Random.Range(_shotMinVelocity, _shotMaxVelocity);
            e.InitialLaunch(direction * power);

            // 生成した敵は衝撃波の対象外とする
            _shockwaveIgnoreTarget = e.gameObject;

            // 発射直後のみ衝撃波判定を短時間有効化し、砲口付近のオブジェクトへ吹き飛びを与える。
            UniTask.Void(async ct2 =>
            {
                _shockWaveCollider2D.enabled = true;
                await UniTask.DelayFrame(3, PlayerLoopTiming.FixedUpdate, ct2);
                _shockWaveCollider2D.enabled = false;
            }, destroyCancellationToken);

            _audioSource.PlayOneShot(_shotClip);

            // アニメーション再生完了を待機
            await _animationEventDetector.AnimationEnded.FirstAsync(ct);

            _shockwaveIgnoreTarget = null;
            _animator.Rebind();
            _animator.Update(0);
        }
    }
}
