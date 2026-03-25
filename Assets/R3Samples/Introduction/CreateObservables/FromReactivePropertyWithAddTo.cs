using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace R3Samples.Introduction.CreateObservables
{
    public sealed class FromReactivePropertyWithAddTo : MonoBehaviour
    {
        // フィールドにint型のReactivePropertyを定義、初期値は0にしておく
        private readonly ReactiveProperty<int> _count = new(0);

        private void Start()
        {
            // OnDestroy()時にReactivePropertyを破棄する
            _count.AddTo(this);
            
            Debug.Log($"初期値は[{_count.CurrentValue}]です。");

            _count.Subscribe(value =>
            {
                Debug.Log(value);
            }).AddTo(this); // OnDestroy時に購読終了

            LoopAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _count.Value++;

                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
            }
        }
    }
}