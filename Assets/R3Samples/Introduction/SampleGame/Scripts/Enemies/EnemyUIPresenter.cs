using UnityEngine;
using UnityEngine.UI;
using R3;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// 敵体力UI反映
    /// </summary>
    public sealed class EnemyUIPresenter : MonoBehaviour
    {
        /// <summary>
        /// 体力スライダー
        /// </summary>
        [SerializeField] private Slider _healthSlider;

        /// <summary>
        /// UI購読登録
        /// </summary>
        private void Start()
        {
            var core = GetComponent<EnemyCore>();

            core.MaxHealth.Subscribe(v =>
            {
                // 最大値が更新されたらヘルスバーは一旦最大値表示にする
                _healthSlider.value = 1.0f;
                
                // 最大値に合わせてバーの長さを変える
                _healthSlider.GetComponent<RectTransform>()
                    .sizeDelta = new Vector2(v * 300, 200);

            }).AddTo(this);

            core.CurrentHealth.Subscribe(v =>
                {
                    var next = Mathf.Clamp01(v / core.MaxHealth.CurrentValue);
                    _healthSlider.value = next;

                    if (v <= 0)
                    {
                        // 非表示にする
                        _healthSlider.gameObject.SetActive(false);
                    }
                })
                .AddTo(this);
        }
    }
}
