using R3;
using UnityEngine;
using UnityEngine.UI;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// プレイヤー体力UI反映
    /// </summary>
    public class PlayerPresenter : MonoBehaviour
    {
        /// <summary>
        /// プレイヤー参照
        /// </summary>
        [SerializeField] private PlayerCore _playerCore;
        /// <summary>
        /// ハート表示ルート
        /// </summary>
        [SerializeField] private GameObject _playerHearts;

        /// <summary>
        /// ハート画像一覧
        /// </summary>
        private Image[] _heartImages;

        /// <summary>
        /// UI購読登録
        /// </summary>
        private void Start()
        {
            _heartImages = _playerHearts.GetComponentsInChildren<Image>();

            _playerCore.PlayerHealth
                .Subscribe(health =>
                {
                    for (var i = 0; i < _heartImages.Length; i++)
                    {
                        _heartImages[i].color = i < health ? Color.white : Color.black;
                    }
                })
                .AddTo(this);
        }
    }
}
