using System.Diagnostics;
using System.Text;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// メインUI反映
    /// </summary>
    public class MainUIPresenter : MonoBehaviour
    {
        /// <summary>
        /// ゲーム進行参照
        /// </summary>
        [SerializeField] private GameManager _gameManager;

        /// <summary>
        /// レベル表示
        /// </summary>
        [SerializeField] private TMP_Text _levelText;

        /// <summary>
        /// スコア表示
        /// </summary>
        [SerializeField] private TMP_Text _scoreText;

        /// <summary>
        /// 入力履歴表示
        /// </summary>
        [SerializeField] private Text _inputList;

        /// <summary>
        /// 入力状態供給元
        /// </summary>
        [SerializeField] private InputSystemInputEventProvider inputSystemInputEventProvider;

        /// <summary>
        /// 入力履歴上限
        /// </summary>
        private const int MaxInputHistory = 6;

        /// <summary>
        /// 直近入力履歴
        /// </summary>
        private readonly string[] _recentInputs = new string[MaxInputHistory];

        /// <summary>
        /// 現在履歴数
        /// </summary>
        private int _recentInputCount;

        /// <summary>
        /// UI購読登録
        /// </summary>
        private void Start()
        {
            _gameManager.Level
                .Subscribe(level => _levelText.text = $"レベル: {level}")
                .AddTo(this);

            _gameManager.Score
                .Subscribe(score => _scoreText.text = $"たおした: {score}")
                .AddTo(this);

            DebugDisplay();
        }

        [Conditional("UNITY_EDITOR")]
        private void DebugDisplay()
        {
            // デバッグ表示
            Observable.Merge(
                    inputSystemInputEventProvider.Direction
                        .DistinctUntilChanged()
                        .Where(direction => direction != InputDirection.None)
                        .Select(DirectionToText),
                    inputSystemInputEventProvider.IsAttackButton
                        .DistinctUntilChanged()
                        .Where(isPressed => isPressed)
                        .Select(_ => "P"))
                .Subscribe(AddInputHistory)
                .AddTo(this);
        }

        /// <summary>
        /// 入力履歴追加
        /// </summary>
        private void AddInputHistory(string input)
        {
            if (_recentInputCount < MaxInputHistory)
            {
                _recentInputs[_recentInputCount] = input;
                _recentInputCount++;
            }
            else
            {
                for (var i = 1; i < MaxInputHistory; i++)
                {
                    _recentInputs[i - 1] = _recentInputs[i];
                }

                _recentInputs[MaxInputHistory - 1] = input;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < _recentInputCount; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(_recentInputs[i]);
            }

            _inputList.text = builder.ToString();
        }

        /// <summary>
        /// 方向入力記号変換
        /// </summary>
        private static string DirectionToText(InputDirection direction)
        {
            return direction switch
            {
                InputDirection.Up => "↑",
                InputDirection.UpRight => "↗",
                InputDirection.Right => "→",
                InputDirection.DownRight => "↘",
                InputDirection.Down => "↓",
                InputDirection.DownLeft => "↙",
                InputDirection.Left => "←",
                InputDirection.UpLeft => "↖",
                _ => "-"
            };
        }
    }
}