using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace R3Samples.Introduction.CreateObservables
{
    public sealed class FromReactiveProperty : MonoBehaviour
    {
        // 読み取りのみが可能なReactivePropertyを公開する
        public ReadOnlyReactiveProperty<int> Count => _count;
        
        // フィールドにint型のReactivePropertyを定義、初期値は0にしておく
        private readonly ReactiveProperty<int> _count = new(0);
 
        private IDisposable _subscription;

        private void Start()
        {
            // ReactivePropertyの現在の値はプロパティから参照可能
            Debug.Log($"初期値は[{_count.CurrentValue}]です。");

            // _count の値が変化を購読
            _subscription = _count.Subscribe(value =>
            {
                // 値が変化したらそれをログに書き出す
                Debug.Log(value);
            });

            // ループ処理を起動
            LoopAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// ループ処理
        /// </summary>
        private async UniTaskVoid LoopAsync(CancellationToken token)
        {
            // 1秒に1回、_countをインクリメントする
            while (!token.IsCancellationRequested)
            {
                _count.Value++;

                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
            }
        }

        private void OnDestroy()
        {
            // ReactivePropertyを破棄
            // 同時にOnCompletedが発行される
            _count?.Dispose();
            
            // 購読の終了
            _subscription?.Dispose();
        }
    }
}