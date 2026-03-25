using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.CreateObservables
{
    public sealed class FromUpdate : MonoBehaviour
    {
        private IDisposable _subscription;

        private void Start()
        {
            _subscription = this.UpdateAsObservable()
                .Subscribe(_ => OnUpdateFromR3());
        }

        private void OnUpdateFromR3()
        {
            // 結果として、このメソッドが毎フレーム実行される
        }

        private void OnDestroy()
        {
            // 購読終了
            _subscription?.Dispose();
        }
    }
}