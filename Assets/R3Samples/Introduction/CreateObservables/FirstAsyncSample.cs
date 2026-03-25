using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.AwaitObservable
{
    public sealed class FirstAsyncSample : MonoBehaviour
    {
        private void Start()
        {
            // async/awaitの待機を開始
            WaitPlayerFallenAsync(destroyCancellationToken).Forget();
        }

        // 奈落（yが-5m以下)に落ちるまで待機する
        private async UniTaskVoid WaitPlayerFallenAsync(CancellationToken ct)
        {
            // 毎フレームチェック
            await this.UpdateAsObservable()
                // 座標に変換
                .Select(_ => transform.position)
                // y座標が-5以下という条件を最初に満たすまで待機
                .FirstAsync(
                    pos => pos.y <= -5f, cancellationToken: ct);
            
            // 奈落に落ちたので破棄する
            Destroy(gameObject);
        }
    }
}