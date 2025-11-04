// BattleGameManager.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq; // .Any() (敵が残っているか確認する) のために使用

// 戦闘シーンにおけるゲームの進行（敵の配置、プレイヤーの攻撃、敵のターン処理、戦闘の終了）を管理します。
public class BattleGameManager : MonoBehaviour
{
    [Header("スロット別モンスター (インスペクタから設定)")]
    // 0=A, 1=D, 2=S, 3=W のスロットに対応するモンスターのCharacterStats
    public CharacterStats[] slimes = new CharacterStats[4];
    public CharacterStats[] skeletons = new CharacterStats[4];
    public CharacterStats[] golems = new CharacterStats[4];
    public CharacterStats[] ghosts = new CharacterStats[4];

    [Header("武器オブジェクト (インスペクタから設定)")]
    // 0=A, 1=D, 2=S, 3=W のスロットに対応する武器モデル
    public GameObject[] rotos = new GameObject[4];
    public GameObject[] rods = new GameObject[4];

    [Header("プレイヤー (ヒエラルキーから設定)")]
    // 戦闘シーン用のプレイヤーオブジェクト（の CharacterStats）
    public CharacterStats playerStats;

    // 現在戦闘中の敵（4スロット分）。存在しないスロットは null。
    private CharacterStats[] currentEnemies = new CharacterStats[4];
    // 敵の行動ゲージ（速度ベースのターン計算用）
    private float[] enemyActionCounters;

    [Header("ゲームオーバー設定")]
    public string gameOverSceneName = "MainScene"; // (現在は未使用)

    void Start()
    {
        // 1. すべての武器モデルを非表示にする
        foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
        foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }

        // 2. 敵の行動カウンター配列(4スロット分)を初期化
        enemyActionCounters = new float[4];

        // 3. プレイヤーのステータスと位置を GameManager から引き継ぐ
        if (playerStats == null)
        {
            Debug.LogError("BattleGameManagerの 'Player Stats' がインスペクタから設定されていません！");
        }
        else
        {
            // 3a. ステータス（HP, Attackなど）を引き継ぐ
            if (GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
            {
                playerStats.InitializeStats(
                    GameManager.instance.playerMaxHealth,
                    GameManager.instance.playerCurrentHealth,
                    GameManager.instance.playerAttack,
                    GameManager.instance.playerDefense,
                    GameManager.instance.playerSpeed
                );
                Debug.Log($"GameManagerからステータスを引き継ぎました。HP: {playerStats.currentHealth}/{playerStats.maxHealth}");
            }
            else
            {
                // GameManager がない場合 (戦闘シーン単体テストなど)
                Debug.LogWarning("GameManager が見つからないか、ステータスが未初期化です。BattlePlayerのデフォルトステータス（インスペクタの値）で開始します。");
            }

            // 3b. プレイヤーの位置・向きを MainScene で保存された状態に復元
            // (MainSceneと同じ位置・向きで戦闘を開始するため)
            if (GameManager.instance != null)
            {
                CharacterController cc = playerStats.GetComponent<CharacterController>();
                // transform.position を直接設定するため、CharacterControllerを一時的に無効化
                if (cc != null) cc.enabled = false;

                // GameManagerに保存されている位置と向きを設定
                playerStats.transform.position = GameManager.instance.playerPosition;
                playerStats.transform.rotation = GameManager.instance.playerRotation;

                if (cc != null) cc.enabled = true; // CharacterControllerを再度有効化

                Debug.Log($"プレイヤーの位置を {GameManager.instance.playerPosition} に復元しました。");
            }

            // 3c. プレイヤーのOnDiedイベント（HPが0になったら）に、OnPlayerDiedメソッドを登録
            playerStats.OnDied.AddListener(OnPlayerDied);
        }

        // 4. 敵を初期配置する
        InitializeEnemies();

        // 5. 武器の初期表示を設定
        // (MainSceneで向いていた方向、使っていた武器をGameManagerから読み込む)
        if (GameManager.instance != null)
        {
            SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false); // false = GameManagerの値は変更しない
        }
        else
        {
            SetActiveWeaponDisplay(0, true); // GameManagerがいない場合はデフォルト(A向き)
        }
    }

    // 登録されている全ての種類のモンスターを非表示にし、戦闘スロット(currentEnemies)をクリアします。
    void HideAllMonsters()
    {
        // 戦闘スロット配列をクリア
        for (int i = 0; i < currentEnemies.Length; i++) { currentEnemies[i] = null; }

        // インスペクタで設定されたすべてのモンスターオブジェクトを非アクティブ化
        foreach (var monster in slimes) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in skeletons) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in golems) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in ghosts) { if (monster != null) monster.gameObject.SetActive(false); }
    }


    // 敵を戦闘スロットに初期配置します。
    // GameManagerから渡された「戦闘可能な方向」にのみ、ランダムな種類の敵をランダムな数だけ配置します。
    void InitializeEnemies()
    {
        // まず全てのモンスターを隠し、カウンターをリセット
        HideAllMonsters();
        for (int i = 0; i < enemyActionCounters.Length; i++) { enemyActionCounters[i] = 0f; }

        // 1. GameManagerから戦闘可能な方向(validDirections)を取得
        List<int> validDirections = new List<int>();
        if (GameManager.instance != null)
        {
            validDirections = GameManager.instance.validCombatDirections;
        }
        else
        {
            Debug.LogWarning("GameManager が見つかりません。デフォルトの方向（全て）で敵を配置します。");
            validDirections = new List<int> { 0, 1, 2, 3 }; // 0=A, 1=D, 2=S, 3=W
        }

        // 有効な方向（スロットインデックス）のリストを作成
        List<int> availableIndices = new List<int>();
        foreach (int index in validDirections)
        {
            if (index >= 0 && index < 4)
            {
                availableIndices.Add(index);
            }
        }

        // もし有効な方向が0なら、保険としてA方向(0)を追加
        if (availableIndices.Count == 0)
        {
            Debug.LogWarning("有効な戦闘方向がありません。デフォルトでA方向(index 0)に配置します。");
            availableIndices.Add(0);
        }

        // 2. 出現する敵の「数」をランダムに決定 (1体 ～ 有効な方向の最大数)
        int count = Random.Range(1, availableIndices.Count + 1);
        Debug.Log($"戦闘可能な {availableIndices.Count} 方向のうち、ランダムで {count} 体の敵が出現します。");

        // 3. 敵を配置
        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break; // 配置するスロットがなくなったら終了

            // 3a. 配置する「スロット」をランダムに決定
            int listIndex = Random.Range(0, availableIndices.Count);
            int slotIndex = availableIndices[listIndex];
            availableIndices.RemoveAt(listIndex); // 決定したスロットはリストから削除

            // 3b. 配置する「モンスターの種類」をランダムに決定 (0～3)
            int monsterTypeIndex = Random.Range(0, 4);
            CharacterStats monsterToActivate = null;

            // 3c. 種類(monsterTypeIndex)とスロット(slotIndex)に応じて、
            //     インスペクタで設定されたモンスター配列から対象を取得
            switch (monsterTypeIndex)
            {
                case 0: monsterToActivate = (slimes.Length > slotIndex && slimes[slotIndex] != null) ? slimes[slotIndex] : null; break;
                case 1: monsterToActivate = (skeletons.Length > slotIndex && skeletons[slotIndex] != null) ? skeletons[slotIndex] : null; break;
                case 2: monsterToActivate = (golems.Length > slotIndex && golems[slotIndex] != null) ? golems[slotIndex] : null; break;
                case 3: monsterToActivate = (ghosts.Length > slotIndex && ghosts[slotIndex] != null) ? ghosts[slotIndex] : null; break;
            }

            // 3d. 対象が見つかったら、HPをリセットして表示し、
            //     currentEnemies 配列に登録する
            if (monsterToActivate != null)
            {
                monsterToActivate.ResetHealth(); // HPを最大値に戻す
                monsterToActivate.gameObject.SetActive(true);
                currentEnemies[slotIndex] = monsterToActivate;
                Debug.Log($"スロット {slotIndex} に {monsterToActivate.gameObject.name} を配置しました。");
            }
            else
            {
                Debug.LogWarning($"スロット {slotIndex} に対応する モンスタータイプ {monsterTypeIndex} (0:S, 1:Sk, 2:G, 3:Gh) がインスペクタで設定されていません。");
            }
        }
    }


    void Update()
    {
        // このフレームでプレイヤーが行動（攻撃）したか
        bool playerActed = false;

        // --- 1. プレイヤーの入力処理 ---

        // 1a. 武器の「向き」変更 (WASDキー)
        if (Input.GetKeyDown(KeyCode.W))
        {
            SetActiveWeaponDisplay(3, true); // W (index 3) をセット
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            SetActiveWeaponDisplay(2, true); // S (index 2) をセット
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            SetActiveWeaponDisplay(0, true); // A (index 0) をセット
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            SetActiveWeaponDisplay(1, true); // D (index 1) をセット
        }

        // 1b. 武器の「種類」切り替え (Cキー)
        else if (Input.GetKeyDown(KeyCode.C))
        {
            if (GameManager.instance != null)
            {
                // GameManagerの状態(isRotoActive)を反転
                GameManager.instance.isRotoActive = !GameManager.instance.isRotoActive;
                // 表示を更新 (向きは変更しない)
                SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);
                Debug.Log(GameManager.instance.isRotoActive ? "武器を Roto に切り替えました。" : "武器を Rod に切り替えました。");
            }
        }

        // 1c. 「攻撃」 (Kキー)
        else if (Input.GetKeyDown(KeyCode.K))
        {
            if (playerStats == null) { return; }

            // GameManagerから現在の武器の「向き」（＝攻撃するスロット）を取得
            int activeRotoIndex = (GameManager.instance != null) ? GameManager.instance.currentWeaponDirectionIndex : 0;
            if (activeRotoIndex < 0 || activeRotoIndex >= 4) { return; }

            // 攻撃対象（向いているスロットの敵）を取得
            if (activeRotoIndex < currentEnemies.Length && currentEnemies[activeRotoIndex] != null)
            {
                CharacterStats targetEnemy = currentEnemies[activeRotoIndex];

                // 敵が既に倒されているかチェック
                if (targetEnemy.currentHealth <= 0 || !targetEnemy.gameObject.activeSelf)
                {
                    Debug.Log($"スロット {activeRotoIndex + 1} の敵は倒されているか、そこにはいない。");
                }
                else
                {
                    // --- ダメージ計算 ---
                    int damage = playerStats.attack - targetEnemy.defense;
                    damage = Mathf.Max(1, damage); // 最低でも1ダメージ

                    // --- 武器の相性（特攻）チェック ---
                    float damageMultiplier = 1.0f;
                    string enemyName = targetEnemy.gameObject.name.ToLower();
                    bool isRoto = (GameManager.instance != null) ? GameManager.instance.isRotoActive : true;

                    if (isRoto) // Rotoの場合
                    {
                        // Slime または Skeleton に特攻 (2倍ダメージ)
                        if (enemyName.Contains("slime") || enemyName.Contains("skeleton"))
                        {
                            damageMultiplier = 2.0f;
                            Debug.Log("Roto 特攻ボーナス！ (Slime/Skeleton)");
                        }
                    }
                    else // Rodの場合
                    {
                        // Golem または Ghost に特攻 (2倍ダメージ)
                        if (enemyName.Contains("golem") || enemyName.Contains("ghost"))
                        {
                            damageMultiplier = 2.0f;
                            Debug.Log("Rod 特攻ボーナス！ (Golem/Ghost)");
                        }
                    }

                    // 最終ダメージを計算して適用
                    int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);
                    Debug.Log($"Player が {targetEnemy.gameObject.name} (スロット {activeRotoIndex + 1}) に攻撃！ {finalDamage} ダメージ。");
                    targetEnemy.TakeDamage(finalDamage);

                    // 敵を倒したかチェック
                    if (targetEnemy.currentHealth <= 0)
                    {
                        targetEnemy.gameObject.SetActive(false);
                        Debug.Log($"{targetEnemy.gameObject.name} (スロット {activeRotoIndex + 1}) を倒した！");
                    }

                    // プレイヤーが行動したフラグを立てる
                    playerActed = true;
                }
            }
            else
            {
                Debug.Log($"スロット {activeRotoIndex} には現在、敵がいません。");
            }

            // 攻撃後、戦闘が終了したか（すべての敵を倒したか）チェック
            CheckForBattleEnd();
        }

        // --- 2. 敵のターン処理 ---
        // プレイヤーが行動（攻撃）した場合のみ、敵のターンを実行
        if (playerActed)
        {
            // プレイヤーが生存している場合のみ
            if (playerStats != null && playerStats.currentHealth > 0)
            {
                ProcessEnemyTurns();
            }
        }
    }

    // 指定されたインデックスの武器（RotoまたはRod）のみをアクティブにします。
    // index: 向き (0=A, 1=D, 2=S, 3=W)
    // updateGameManager: GameManagerの状態も更新するか
    void SetActiveWeaponDisplay(int index, bool updateGameManager)
    {
        // GameManagerがいない場合は、何も表示せず終了
        if (GameManager.instance == null)
        {
            foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
            foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }
            return;
        }

        // (必要なら) GameManagerの「現在の向き」の値を更新
        if (updateGameManager)
        {
            GameManager.instance.currentWeaponDirectionIndex = index;
        }

        // 無効なインデックスなら、すべて非表示にして終了
        if (index < 0 || index >= 4)
        {
            foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
            foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }
            return;
        }

        // GameManagerから現在の武器状態（Roto/Rod、向き）を読み取る
        bool isRoto = GameManager.instance.isRotoActive;
        int directionIndex = GameManager.instance.currentWeaponDirectionIndex;

        // 4スロット（A,D,S,W）すべてをチェックし、
        // 該当する武器モデルのみを SetActive(true) にする
        for (int i = 0; i < 4; i++)
        {
            if (rotos[i] != null)
            {
                rotos[i].SetActive(isRoto && (i == directionIndex));
            }
            if (rods[i] != null)
            {
                rods[i].SetActive(!isRoto && (i == directionIndex));
            }
        }
    }


    // プレイヤーが行動した後に呼び出され、敵の行動を処理します（速度ベース）。
    void ProcessEnemyTurns()
    {
        if (playerStats == null || playerStats.speed <= 0) return;
        Debug.Log("--- 敵のターン開始 ---");

        // 4スロットすべての敵をチェック
        for (int i = 0; i < currentEnemies.Length; i++)
        {
            CharacterStats enemy = currentEnemies[i];
            // 敵がいない、または既に倒されている場合はスキップ
            if (enemy == null || !enemy.gameObject.activeSelf || enemy.currentHealth <= 0) { continue; }
            if (enemy.speed <= 0) { Debug.LogWarning($"{enemy.name} の速度が0以下なため、行動できません。"); continue; }

            // 1. 行動ゲージ(enemyActionCounters)を 1.0f (プレイヤー1回行動分) 増やす
            enemyActionCounters[i] += 1.0f;

            // 2. 敵が1回行動するために必要なプレイヤーの行動回数（比率）を計算
            // (例: Player Speed 10 / Enemy Speed 5 = 2.0f -> プレイヤーが2回動く間に敵は1回動く)
            // (例: Player Speed 10 / Enemy Speed 20 = 0.5f -> プレイヤーが1回動く間に敵は2回動く)
            float actionRatio = (float)playerStats.speed / (float)enemy.speed;

            // 3. 行動ゲージ(counter)が、必要な比率(actionRatio)を超える限り、敵は行動する
            while (enemyActionCounters[i] >= actionRatio)
            {
                EnemyAct(enemy); // 敵の攻撃実行
                enemyActionCounters[i] -= actionRatio; // 行動ゲージを消費

                // 敵の行動中にプレイヤーが倒れたら、残りの敵のターンは中断
                if (playerStats.currentHealth <= 0)
                {
                    Debug.Log("敵のターン中にプレイヤーが倒れた。");
                    return;
                }
            }
        }
        Debug.Log("--- 敵のターン終了 ---");
    }

    // 敵がプレイヤーに攻撃する実際の処理
    void EnemyAct(CharacterStats enemy)
    {
        if (playerStats == null || playerStats.currentHealth <= 0) { return; }

        // ダメージ計算（敵の攻撃力 - プレイヤーの防御力、最低1ダメージ）
        int damage = enemy.attack - playerStats.defense;
        damage = Mathf.Max(1, damage);

        Debug.Log($"<color=red>{enemy.gameObject.name} の攻撃！ Player に {damage} のダメージ！</color>");

        // プレイヤーにダメージを与える
        playerStats.TakeDamage(damage);
    }

    // プレイヤーのHPが0になった時（CharacterStats の OnDiedイベントから）呼び出されます。
    void OnPlayerDied()
    {
        Debug.Log("<color=red>プレイヤーは倒れてしまった... ゲームオーバー</color>");

        // このマネージャーの Update() などを停止
        this.enabled = false;

        // GameManagerにメインシーンへ戻るよう指示
        if (GameManager.instance != null)
        {
            GameManager.instance.ReturnToMainScene();
        }
    }

    // （プレイヤーの攻撃後）アクティブな敵が残っているか確認し、
    // もしいなければ戦闘を終了します。
    void CheckForBattleEnd()
    {
        // Linqの .Any() を使用して、
        // currentEnemies 配列内に「生存している(HP>0)」敵が「1体でもいるか」をチェック
        bool anyEnemyActive = currentEnemies.Any(enemy =>
            enemy != null &&
            enemy.currentHealth > 0 &&
            enemy.gameObject.activeSelf
        );

        // もし生存している敵が「いない」(!anyEnemyActive) なら
        if (!anyEnemyActive)
        {
            Debug.Log("すべての敵を倒した！戦闘終了。");

            // 勝利した場合、現在のプレイヤーのHPをGameManagerに保存する
            if (GameManager.instance != null && playerStats != null)
            {
                GameManager.instance.playerCurrentHealth = playerStats.currentHealth;
                Debug.Log($"戦闘終了時のHP {playerStats.currentHealth} をGameManagerに保存しました。");
            }

            // GameManagerにメインシーンへ戻るよう指示
            if (GameManager.instance != null)
            {
                GameManager.instance.ReturnToMainScene();
            }
        }
    }
}