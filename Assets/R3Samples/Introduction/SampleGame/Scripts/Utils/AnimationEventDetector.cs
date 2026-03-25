using R3;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// アニメーションイベント通知橋渡し
    /// </summary>
    public class AnimationEventDetector : MonoBehaviour
    {
        /// <summary>
        /// 任意イベント通知
        /// </summary>
        private readonly Subject<Unit> _animationEventSubject = new Subject<Unit>();
        /// <summary>
        /// 終了イベント通知
        /// </summary>
        private readonly Subject<Unit> _animationEndSubject = new Subject<Unit>();

        public Observable<Unit> AnimationEvent => _animationEventSubject;
        public Observable<Unit> AnimationEnded => _animationEndSubject;

        /// <summary>
        /// 購読破棄登録
        /// </summary>
        private void Start()
        {
            _animationEndSubject.AddTo(this);
            _animationEventSubject.AddTo(this);
        }

        /// <summary>
        /// アニメーションイベント通知
        /// </summary>
        public void OnAnimationEvent()
        {
            _animationEventSubject?.OnNext(Unit.Default);
        }

        /// <summary>
        /// アニメーション終了通知
        /// </summary>
        public void OnAnimationEnded()
        {
            _animationEndSubject?.OnNext(Unit.Default);
        }
    }
}
