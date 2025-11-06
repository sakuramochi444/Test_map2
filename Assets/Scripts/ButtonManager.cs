using UnityEngine;
using UnityEngine.SceneManagement; // シーン管理に必要

/// <summary>
/// UIボタンのイベント（OnClick）から呼び出される各種機能を提供します。
/// ポーズメニューやデバッグメニューのCanvasなどにアタッチして使用します。
/// </summary>
public class ButtonManager : MonoBehaviour
{
    [Header("シーン名設定")]
    [Tooltip("リセット時に戻るスタートシーンの名前")]
    public string startSceneName = "StartScene";

    // === ゲームのリセット ===

    /// <summary>
    /// すべての状態をリセットし、指定された "StartScene" に戻ります。
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void ResetAndReturnToStart()
    {
        Debug.Log($"ゲームの状態をリセットし、{startSceneName} に戻ります。");

        // 1. GameManager (DontDestroyOnLoad) を破棄する
        //    これにより、次回StartSceneからMainSceneに移行した際に、
        //    新しいGameManagerがクリーンな状態で作成されます。
        if (GameManager.instance != null)
        {
            Destroy(GameManager.instance.gameObject);
            Debug.Log("GameManagerインスタンスを破棄しました。");
        }

        // 2. MapGeneratorの静的データ(mapLevels)は、
        //    Awake()の if (mapLevels.Count == 0) ブロックによって
        //    再ロード時に自動的に処理されるため、ここでリセットする必要はありません。

        // 3. StartSceneをロードする
        SceneManager.LoadScene(startSceneName);
    }

    // === ゲームの終了 ===

    /// <summary>
    /// ゲームを終了します。（ビルド版でのみ有効）
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します...");

        // ゲームを終了する
        Application.Quit();

#if UNITY_EDITOR
        // Unityエディタ実行中の場合は、再生を停止（ビルド版では無視されます）
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // === HPの全回復 ===

    /// <summary>
    /// プレイヤーのHPを（GameManagerと現在のシーンの両方で）全回復します。
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void HealPlayerToFull()
    {
        Debug.Log("プレイヤーのHPを全回復します。");

        // 1. GameManagerのデータを回復
        if (GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
        {
            // GameManagerに保存されているHP値を最大値に設定
            GameManager.instance.playerCurrentHealth = GameManager.instance.playerMaxHealth;
            Debug.Log($"GameManagerのHPを回復: {GameManager.instance.playerCurrentHealth}/{GameManager.instance.playerMaxHealth}");
        }
        else
        {
            Debug.LogWarning("GameManagerが見つからないか、ステータスが未初期化のため、GameManager上のHPは回復できませんでした。");
            // GameManagerがなくても、シーン上のプレイヤーの回復は試みる
        }

        // 2. 現在のシーンのプレイヤーのHPを即時回復 (シーンによって対象が異なる)

        // BattleSceneにいるか？ (BattleGameManagerを探す)

        // ▼▼▼ 警告箇所を修正 ▼▼▼
        // BattleGameManager b_gm = FindObjectOfType<BattleGameManager>(); // 旧
        BattleGameManager b_gm = FindFirstObjectByType<BattleGameManager>(); // 新
        // ▲▲▲ 警告箇所を修正 ▲▲▲

        if (b_gm != null && b_gm.playerStats != null)
        {
            // BattleSceneのプレイヤーのHealメソッドを呼ぶ
            // (CharacterStats.Heal()は自動的にmaxHealthでクランプしてくれます)
            b_gm.playerStats.Heal(b_gm.playerStats.maxHealth);
            Debug.Log($"BattleSceneのプレイヤーHPを回復: {b_gm.playerStats.currentHealth}/{b_gm.playerStats.maxHealth}");

            // OnDamagedイベントを発行してUI（HPバーなど）を更新させる
            b_gm.playerStats.OnDamaged?.Invoke();
            return; // BattleSceneの処理が終わったらMainSceneの処理は不要
        }

        // MainSceneにいるか？ ("Player"タグを探す)
        // (BattleSceneにいなかった場合に実行される)
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            CharacterStats mainPlayerStats = playerObj.GetComponent<CharacterStats>();
            if (mainPlayerStats != null)
            {
                // MainSceneのプレイヤーのHealメソッドを呼ぶ
                mainPlayerStats.Heal(mainPlayerStats.maxHealth);
                Debug.Log($"MainSceneのプレイヤーHPを回復: {mainPlayerStats.currentHealth}/{mainPlayerStats.maxHealth}");

                // OnDamagedイベントを発行してUI（HPバーなど）を更新させる
                mainPlayerStats.OnDamaged?.Invoke();
            }
        }
    }

    /// <summary>
    /// 最後の階層（GameManagerが記憶している戦闘突入前の状態）からやり直します。
    /// DeathScene のボタン OnClick() イベントに設定してください。
    /// </summary>
    public void RestartFromLastFloor()
    {
        if (GameManager.instance != null)
        {
            Debug.Log("現在の階層を最初からやり直します。");

            // ▼▼▼ 修正箇所 ▼▼▼
            // (旧) GameManager.instance.ReturnToMainScene(); 
            // (新) 新しく作成した RestartCurrentLevel メソッドを呼び出す
            GameManager.instance.RestartCurrentLevel();
            // ▲▲▲ 修正箇所 ▲▲▲
        }
        else
        {
            // GameManager が何らかの理由で存在しない場合のフォールバック
            Debug.LogWarning("GameManagerが見つからないため、StartSceneに戻ります。");
            ResetAndReturnToStart();
        }
    }
}