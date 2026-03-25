using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;

namespace R3Samples.Introduction.SampleGame
{
    /// <summary>
    /// ゲームオーバーUI表示制御
    /// </summary>
    public class GameOverPresenter : MonoBehaviour
    {
        /// <summary>
        /// ゲーム進行参照
        /// </summary>
        [SerializeField] private GameManager _gameManager;
        /// <summary>
        /// 暗転表示
        /// </summary>
        [SerializeField] private GameObject _cover;
        /// <summary>
        /// 最終スコア表示
        /// </summary>
        [SerializeField] private TMP_Text _finalScoreResultText;
        /// <summary>
        /// 再開案内表示
        /// </summary>
        [SerializeField] private GameObject _canNextGame;

        /// <summary>
        /// 初期表示設定
        /// </summary>
        private void Start()
        {
            _cover.SetActive(false);
            _finalScoreResultText.gameObject.SetActive(false);
            _canNextGame.SetActive(false);
            
            WaitForGameOverAsync(destroyCancellationToken).Forget();
        }

        /// <summary>
        /// ゲームオーバー演出進行
        /// </summary>
        private async UniTaskVoid WaitForGameOverAsync(CancellationToken ct)
        {
            // ゲームオーバーを待つ
            await _gameManager.IsGameOver.FirstOrDefaultAsync(x => x, cancellationToken: ct);
            
            // ちょっと待って暗転
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
            _cover.SetActive(true);
            
            // スコア表示
            _finalScoreResultText.text = $"たおしたかず\n{_gameManager.Score.CurrentValue}";
            _finalScoreResultText.gameObject.SetActive(true);
            
            // リトライ可能になるまで待つ
            await _gameManager.CanNextGame.FirstOrDefaultAsync(x => x, cancellationToken: ct);
            _canNextGame.gameObject.SetActive(true);
        }
    }
}
