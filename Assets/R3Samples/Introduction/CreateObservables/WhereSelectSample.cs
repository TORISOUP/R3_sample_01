using R3;
using R3.Triggers;
using UnityEngine;

namespace R3Samples.Introduction.Operators
{
    public sealed class WhereSelectSample : MonoBehaviour
    {
        private void Start()
        {
            // 衝突対象がPlayerならRigidbodyを取り出してAddForceする
            this.OnCollisionEnterAsObservable()
                // 衝突対象がPlayerタグであるか？
                .Where(col => col.gameObject.CompareTag("Player"))
                // Rigidbodyを取り出す
                .Select(col => col.rigidbody)
                // Rigidbodyがnullでないか？
                .Where(rig => rig != null)
                // Rigidbodyに力を加える
                .Subscribe(rig =>
                {
                    rig.AddForce(Vector3.up);
                })
                .AddTo(this);
        }
    }
}