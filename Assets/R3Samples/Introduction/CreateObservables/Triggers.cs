using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.CreateObservables
{
    public sealed class Triggers : MonoBehaviour
    {
        private void Start()
        {
            // Update()の変換
            this.UpdateAsObservable()
                .Subscribe()
                // AddTo はDispose実行をこのGameObjectのDestroyに連動させる機能
                .AddTo(this);
            
            // FixedUpdate()
            this.FixedUpdateAsObservable()
                .Subscribe()
                .AddTo(this);
            
            // OnTriggerEnter
            this.OnTriggerEnterAsObservable()
                .Subscribe()
                .AddTo(this);
            
            // OnCollisionEnter
            this.OnCollisionEnterAsObservable()
                .Subscribe(collision =>
                {
                    // ぶつかった対象のGameObjectの
                    // Update()にHookする、みたいなこともできる
                    collision.gameObject
                        .UpdateAsObservable()
                        .Subscribe()
                        .AddTo(this);
                })
                .AddTo(this);
        }
    }
}