using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.Operators
{
    public sealed class ThrottleFirstSample : MonoBehaviour
    {
        private void Start()
        {
            this.UpdateAsObservable()
                // スペースキーが押されたフレームだけ通す
                .Where(_ => Input.GetKeyDown(KeyCode.Space))
                // 0.5秒以内の連打は無視
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ =>
                {
                    Debug.Log("スペースキーが押されました");
                })
                .AddTo(this);
        }
    }
}
