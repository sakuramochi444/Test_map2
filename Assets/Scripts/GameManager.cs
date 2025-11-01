// GameManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // ▼▼▼ 以下をすべて追加・修正 ▼▼▼

    // ゲームの状態を保存するための変数
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public List<Vector3> enemyPositions = new List<Vector3>();
    public int[,] mapData;

    public List<int> validCombatDirections = new List<int>();

    // プレイヤーが戦闘を開始したことを示すフレーム単位のフラグ
    public bool combatInitiatedThisFrame = false;

    // 戦闘になった敵の位置情報
    private Vector3 positionOfEnemyInCombat;

    // 戦闘から戻ってきたかを判断するためのフラグ
    private bool returnedFromBattle = false;

    // あなたのシーン名に合わせてください
    private string mainSceneName = "MainScene";     // 通常シーンの名前
    private string battleSceneName = "BattleScene"; // 戦闘シーンの名前

    void Awake()
    {
        // シングルトンの設定（シーンをまたいでインスタンスを維持する）
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // このオブジェクトをシーン遷移時に破棄しない
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void LateUpdate()
    {
        // このフラグは1フレームの間だけ有効にする
        combatInitiatedThisFrame = false;
    }

    public void PlayerCaughtByEnemy(GameObject enemyInCombat, List<int> validDirections)
    {
        Debug.Log("敵に捕まった！戦闘シーンへ移行します。");

        // 1. 現在のゲーム状態を保存する
        // ▼▼▼ 引数を SaveGameState にも渡す ▼▼▼
        SaveGameState(enemyInCombat, validDirections);

        // 2. 戦闘シーンをロードする
        SceneManager.LoadScene(battleSceneName);
    }

    /// <summary>
    /// 戦闘シーンからメインシーンへ戻るメソッド
    /// </summary>
    public void ReturnToMainScene()
    {
        Debug.Log("メインシーンへ戻ります。");
        returnedFromBattle = true; // メインシーンに戻ったことを記録
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// 現在のゲームの状態を保存する
    /// </summary>
    private void SaveGameState(GameObject enemyInCombat, List<int> validDirections)
    {
        // プレイヤーの位置と向きを保存
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerPosition = player.transform.position;
            playerRotation = player.transform.rotation;
        }

        // 戦闘になった敵の位置を保存
        positionOfEnemyInCombat = enemyInCombat.transform.position;

        // すべての敵の位置を保存
        enemyPositions.Clear();
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            enemyPositions.Add(enemy.transform.position);
        }

        // 現在のマップデータ（宝箱の状態など）を保存
        mapData = (int[,])MapGenerator.map.Clone(); // 配列を値渡しでコピー

        // ▼▼▼ 追加 ▼▼▼
        // 戦闘時の有効な方向を保存
        validCombatDirections.Clear();
        if (validDirections != null)
        {
            validCombatDirections.AddRange(validDirections);
        }
        // ▲▲▲ 追加 ▲▲▲
    }

    /// <summary>
    /// メインシーンがロードされた後にゲームの状態を復元する
    /// </summary>
    private IEnumerator RestoreGameStateAfterLoad()
    {
        // シーン内のオブジェクトがすべて読み込まれるのを1フレーム待つ
        yield return null;

        Debug.Log("ゲームの状態を復元します。");

        // プレイヤーの位置と向きを復元
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // CharacterControllerはワープの邪魔になることがあるので一時的に無効化
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = playerPosition;
            player.transform.rotation = playerRotation;
            if (cc != null) cc.enabled = true;
        }

        // マップの状態（宝箱など）を復元
        MapGenerator.instance.ChangeMap(mapData);

        // 現在シーンの敵をすべて削除
        GameObject[] existingEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in existingEnemies)
        {
            Destroy(enemy);
        }

        // 保存しておいた位置に敵を再配置する
        foreach (Vector3 pos in enemyPositions)
        {
            // 戦闘した敵の位置と非常に近い場合は、再配置しない（=破壊したことになる）
            if (Vector3.Distance(pos, positionOfEnemyInCombat) < 0.1f)
            {
                continue;
            }
            Instantiate(MapGenerator.instance.EnemyPrefab, pos, Quaternion.identity);
        }

        returnedFromBattle = false; // フラグをリセット
    }

    // シーンがロードされた時に呼ばれるイベントハンドラ
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // メインシーンに、かつ戦闘から戻ってきた場合に限り、状態を復元する
        if (scene.name == mainSceneName && returnedFromBattle)
        {
            StartCoroutine(RestoreGameStateAfterLoad());
        }
    }

    // オブジェクトが有効になった時にイベント登録
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // オブジェクトが無効になった時にイベント解除
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}