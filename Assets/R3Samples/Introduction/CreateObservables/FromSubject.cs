using R3;
using UnityEngine;

namespace R3Samples.Introduction.CreateObservables
{
    public sealed class FromSubject : MonoBehaviour
    {
        private void Start()
        {
            // int型のSubjectを準備
            var subject = new Subject<int>();

            // Subject<T> は Observable<T> を継承しているので
            // そのままObservableとして扱うことができる
            Observable<int> observable = subject;

            // メッセージが発行されたらログに出す
            var subscription = observable.Subscribe(
                onNext: value => Debug.Log(value),
                onErrorResume: error => Debug.LogException(error),
                onCompleted: _ => Debug.Log("OnCompleted!")
            );

            // 1, 2, 3 を発行
            subject.OnNext(1);
            subject.OnNext(2);
            subject.OnNext(3);

            // OnErrorResumeを発行
            subject.OnErrorResume(new System.ArgumentException("引数が不正です"));

            // OnCompleted(Success)を発行
            subject.OnCompleted();

            // Subjectを破棄
            // （まだOnCompletedが発行されていなかったらこのタイミングで自動発行される）
            subject.Dispose();
            
            // 購読を破棄
            subscription.Dispose();
        }
    }
}