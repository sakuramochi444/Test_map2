// BattleGameManager.cs

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AudioSource))]
public class BattleGameManager : MonoBehaviour
{
    // ... (中略: インスペクタ設定 (slimes ～ rodLHealAmount) は変更なし) ...
    [Header("スロット別モンスター (インスペクタから設定)")]
    public CharacterStats[] slimes = new CharacterStats[4];
    public CharacterStats[] skeletons = new CharacterStats[4];
    public CharacterStats[] golems = new CharacterStats[4];
    public CharacterStats[] ghosts = new CharacterStats[4];

    [Header("武器オブジェクト (インスペクタから設定)")]
    public GameObject[] rotos = new GameObject[4];
    public GameObject[] rods = new GameObject[4];

    [Header("プレイヤー (ヒエラルキーから設定)")]
    public CharacterStats playerStats;

    [Header("攻撃エフェクト (インスペクタから設定)")]
    public GameObject[] rotoAttackEffectPrefabs = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_J = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_K = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_L = new GameObject[4];

    [Header("攻撃サウンド (インスペクタから設定)")]
    public AudioClip rotoAttackSound;
    [Tooltip("Rod (Jキー) 強化用サウンド")]
    public AudioClip rodAttackSound_J;
    [Tooltip("Rod (Kキー) 攻撃用サウンド")]
    public AudioClip rodAttackSound_K;
    [Tooltip("Rod (Lキー) 回復用サウンド")]
    public AudioClip rodAttackSound_L;

    [Tooltip("攻撃が無効だった時 (はじかれた時) のサウンド")]
    public AudioClip ineffectiveAttackSound;

    [Header("Rod特殊効果 設定")]
    [Tooltip("Rod (Lキー) での体力回復量 (戦闘中1回のみ)")]
    public int rodLHealAmount = 20;


    // ... (中略: currentEnemies ～ gameOverSceneName は変更なし) ...
    private CharacterStats[] currentEnemies = new CharacterStats[4];
    private float[] enemyActionCounters;
    private AudioSource audioSource;
    private Dictionary<GameObject, Coroutine> activeEffectCoroutines = new Dictionary<GameObject, Coroutine>();
    private bool isRodKAttackBoosted = false;
    private bool hasUsedRodLHeal = false;
    private bool isBattleEnding = false;
    private AudioClip lastPlayedAttackSound = null;
    public string gameOverSceneName = "MainScene"; // (現在は未使用)


    // FlagManager への参照を保持する変数
    private FlagManager flagManager;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // MainSceneから持ち越されたシングルトンインスタンスを直接参照する
        flagManager = FlagManager.instance;

        if (flagManager == null)
        {
            // (旧) Debug.LogError("BattleGameManager に FlagManager がアタッチされていません！");
            Debug.LogError("FlagManager.instance が見つかりません！ MainSceneから正しくロードされていない可能性があります。");
        }

        foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
        foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }

        enemyActionCounters = new float[4];

        if (playerStats == null)
        {
            Debug.LogError("BattleGameManagerの 'Player Stats' がインスペクタから設定されていません！");
        }
        else
        {
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
                Debug.LogWarning("GameManager が見つからないか、ステータスが未初期化です。BattlePlayerのデフォルトステータス（インスペクタの値）で開始します。");
            }

            if (GameManager.instance != null)
            {
                CharacterController cc = playerStats.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerStats.transform.position = GameManager.instance.playerPosition;
                playerStats.transform.rotation = GameManager.instance.playerRotation;
                if (cc != null) cc.enabled = true;
                Debug.Log($"プレイヤーの位置を {GameManager.instance.playerPosition} に復元しました。");
            }

            playerStats.OnDied.AddListener(OnPlayerDied);
        }

        InitializeEnemies();

        if (GameManager.instance != null)
        {
            SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);
        }
        else
        {
            SetActiveWeaponDisplay(0, true);
        }

        foreach (var effect in rotoAttackEffectPrefabs) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_J) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_K) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_L) { if (effect != null) effect.SetActive(false); }

        isRodKAttackBoosted = false;
        hasUsedRodLHeal = false;
    }

    void HideAllMonsters()
    {
        for (int i = 0; i < currentEnemies.Length; i++) { currentEnemies[i] = null; }
        foreach (var monster in slimes) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in skeletons) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in golems) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in ghosts) { if (monster != null) monster.gameObject.SetActive(false); }
    }

    void InitializeEnemies()
    {
        HideAllMonsters();
        for (int i = 0; i < enemyActionCounters.Length; i++) { enemyActionCounters[i] = 0f; }
        List<int> validDirections = new List<int>();
        if (GameManager.instance != null) { validDirections = GameManager.instance.validCombatDirections; }
        else { Debug.LogWarning("GameManager が見つかりません。デフォルトの方向（全て）で敵を配置します。"); validDirections = new List<int> { 0, 1, 2, 3 }; }
        List<int> availableIndices = new List<int>();
        foreach (int index in validDirections) { if (index >= 0 && index < 4) { availableIndices.Add(index); } }
        if (availableIndices.Count == 0) { Debug.LogWarning("有効な戦闘方向がありません。デフォルトでA方向(index 0)に配置します。"); availableIndices.Add(0); }
        int count = Random.Range(1, availableIndices.Count + 1);
        Debug.Log($"戦闘可能な {availableIndices.Count} 方向のうち、ランダムで {count} 体の敵が出現します。");
        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break;
            int listIndex = Random.Range(0, availableIndices.Count);
            int slotIndex = availableIndices[listIndex];
            availableIndices.RemoveAt(listIndex);
            int monsterTypeIndex = Random.Range(0, 4);
            CharacterStats monsterToActivate = null;
            switch (monsterTypeIndex)
            {
                case 0: monsterToActivate = (slimes.Length > slotIndex && slimes[slotIndex] != null) ? slimes[slotIndex] : null; break;
                case 1: monsterToActivate = (skeletons.Length > slotIndex && skeletons[slotIndex] != null) ? skeletons[slotIndex] : null; break;
                case 2: monsterToActivate = (golems.Length > slotIndex && golems[slotIndex] != null) ? golems[slotIndex] : null; break;
                case 3: monsterToActivate = (ghosts.Length > slotIndex && ghosts[slotIndex] != null) ? ghosts[slotIndex] : null; break;
            }
            if (monsterToActivate != null)
            {
                monsterToActivate.ResetHealth();
                monsterToActivate.gameObject.SetActive(true);
                currentEnemies[slotIndex] = monsterToActivate;
                Debug.Log($"スロット {slotIndex} に {monsterToActivate.gameObject.name} を配置しました。");
            }
            else { Debug.LogWarning($"スロット {slotIndex} に対応する モンスタータイプ {monsterTypeIndex} (0:S, 1:Sk, 2:G, 3:Gh) がインスペクタで設定されていません。"); }
        }
    }


    // --- [ここから変更] ---
    void Update()
    {
        if (isBattleEnding) return;

        bool playerActed = false;

        // 1a. 武器の「向き」変更 (WASDキー)
        if (Input.GetKeyDown(KeyCode.W)) { SetActiveWeaponDisplay(3, true); }
        else if (Input.GetKeyDown(KeyCode.S)) { SetActiveWeaponDisplay(2, true); }
        else if (Input.GetKeyDown(KeyCode.A)) { SetActiveWeaponDisplay(0, true); }
        else if (Input.GetKeyDown(KeyCode.D)) { SetActiveWeaponDisplay(1, true); }

        // 1b. 武器の「種類」切り替え (Cキー)
        else if (Input.GetKeyDown(KeyCode.C))
        {
            if (GameManager.instance != null)
            {
                GameManager.instance.isRotoActive = !GameManager.instance.isRotoActive;
                SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);
                Debug.Log(GameManager.instance.isRotoActive ? "武器を Roto に切り替えました。" : "武器を Rod に切り替えました。");
            }
        }

        // 1c. 「攻撃」 (J, K, Lキー)
        else if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.L))
        {
            if (playerStats == null) { return; }

            lastPlayedAttackSound = null;

            int activeRotoIndex = (GameManager.instance != null) ? GameManager.instance.currentWeaponDirectionIndex : 0;
            if (activeRotoIndex < 0 || activeRotoIndex >= 4) { return; }

            // 武器がRotoかRodかを取得
            bool isRoto = (GameManager.instance != null) ? GameManager.instance.isRotoActive : true;

            // J, L キーは敵の存在に関わらず発動 (Rod使用時のみ)
            if (!isRoto && Input.GetKeyDown(KeyCode.J))
            {
                // [新設] Jキー (Rod) の処理
                playerActed = HandleRodJKey(activeRotoIndex);
            }
            else if (!isRoto && Input.GetKeyDown(KeyCode.L))
            {
                // [新設] Lキー (Rod) の処理
                playerActed = HandleRodLKey(activeRotoIndex);
            }
            // Kキー (または Roto使用時の J, Lキー) は、敵の存在チェックが必要
            else
            {
                // 1. 攻撃対象（敵）が存在するかチェック
                if (activeRotoIndex < currentEnemies.Length && currentEnemies[activeRotoIndex] != null)
                {
                    CharacterStats targetEnemy = currentEnemies[activeRotoIndex];

                    // 1a. 敵はいるが、すでに倒されているか
                    if (targetEnemy.currentHealth <= 0 || !targetEnemy.gameObject.activeSelf)
                    {
                        Debug.Log($"スロット {activeRotoIndex + 1} の敵は倒されているか、そこにはいない。");
                        // (playerActed は false のまま)
                    }
                    else
                    {
                        // 1b. 敵がいて、生存している (攻撃実行)
                        if (isRoto)
                        {
                            // Roto (K)
                            if (Input.GetKeyDown(KeyCode.K))
                            {
                                playerActed = PerformAttack(targetEnemy, true, 1, activeRotoIndex);
                            }
                            else
                            {
                                // Rotoで J または L を押した場合 (無効)
                                Debug.Log("Roto 使用中は J, L キーは無効です。");
                            }
                        }
                        else // Rod
                        {
                            // Rod (K) のみ (J, L は上で処理済みのため)
                            if (Input.GetKeyDown(KeyCode.K))
                            {
                                playerActed = PerformAttack(targetEnemy, false, 1, activeRotoIndex);
                            }
                        }
                    }
                }
                else
                {
                    // 2. 攻撃対象（敵）が「いない」場合 (Kキー)
                    Debug.Log($"スロット {activeRotoIndex} には現在、敵がいません。");
                    // (playerActed は false のまま)
                }
            }

            StartCoroutine(CheckForBattleEndCoroutine());
        }

        // 2. 敵のターン処理
        if (playerActed && !isBattleEnding)
        {
            if (playerStats != null && playerStats.currentHealth > 0)
            {
                ProcessEnemyTurns();
            }
        }
    }
    // --- [変更ここまで] ---


    // ... (中略: SetActiveWeaponDisplay, ProcessEnemyTurns, EnemyAct, OnPlayerDied, CheckForBattleEndCoroutine は変更なし) ...
    void SetActiveWeaponDisplay(int index, bool updateGameManager)
    {
        if (GameManager.instance == null) { foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); } foreach (var rod in rods) { if (rod != null) rod.SetActive(false); } return; }
        if (updateGameManager) { GameManager.instance.currentWeaponDirectionIndex = index; }
        if (index < 0 || index >= 4) { foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); } foreach (var rod in rods) { if (rod != null) rod.SetActive(false); } return; }
        bool isRoto = GameManager.instance.isRotoActive;
        int directionIndex = GameManager.instance.currentWeaponDirectionIndex;
        for (int i = 0; i < 4; i++) { if (rotos[i] != null) { rotos[i].SetActive(isRoto && (i == directionIndex)); } if (rods[i] != null) { rods[i].SetActive(!isRoto && (i == directionIndex)); } }
    }

    void ProcessEnemyTurns()
    {
        if (playerStats == null || playerStats.speed <= 0) return;
        Debug.Log("--- 敵のターン開始 ---");
        for (int i = 0; i < currentEnemies.Length; i++)
        {
            CharacterStats enemy = currentEnemies[i];
            if (enemy == null || !enemy.gameObject.activeSelf || enemy.currentHealth <= 0) { continue; }
            if (enemy.speed <= 0) { Debug.LogWarning($"{enemy.name} の速度が0以下なため、行動できません。"); continue; }
            enemyActionCounters[i] += 1.0f;
            float actionRatio = (float)playerStats.speed / (float)enemy.speed;
            while (enemyActionCounters[i] >= actionRatio)
            {
                EnemyAct(enemy);
                enemyActionCounters[i] -= actionRatio;
                if (playerStats.currentHealth <= 0 || isBattleEnding)
                {
                    Debug.Log("敵のターン中にプレイヤーが倒れたか、戦闘が終了した。");
                    return;
                }
            }
        }
        Debug.Log("--- 敵のターン終了 ---");
    }

    void EnemyAct(CharacterStats enemy)
    {
        if (playerStats == null || playerStats.currentHealth <= 0) { return; }
        int damage = enemy.attack - playerStats.defense;
        damage = Mathf.Max(1, damage);
        Debug.Log($"<color=red>{enemy.gameObject.name} の攻撃！ Player に {damage} のダメージ！</color>");
        playerStats.TakeDamage(damage);
    }


    void OnPlayerDied()
    {
        if (isBattleEnding) return;
        isBattleEnding = true;

        Debug.Log("<color=red>プレイヤーは倒れてしまった... ゲームオーバー</color>");

        if (GameManager.instance != null)
        {
            GameManager.instance.GoToDeathScene();
        }
    }

    IEnumerator CheckForBattleEndCoroutine()
    {
        yield return null;

        if (isBattleEnding) yield break;

        bool anyEnemyActive = currentEnemies.Any(e => e != null && e.currentHealth > 0 && e.gameObject.activeSelf);

        if (!anyEnemyActive)
        {
            isBattleEnding = true;
            Debug.Log("すべての敵を倒した！戦闘終了。");

            float waitTime = 0f;
            if (lastPlayedAttackSound != null)
            {
                waitTime = lastPlayedAttackSound.length;
                Debug.Log($"サウンド {lastPlayedAttackSound.name} の再生完了 ( {waitTime} 秒) を待機します。");
            }
            else
            {
                Debug.Log("待機するサウンドがありません。すぐにシーンを遷移します。");
            }

            if (GameManager.instance != null && playerStats != null)
            {
                GameManager.instance.playerCurrentHealth = playerStats.currentHealth;
                Debug.Log($"戦闘終了時のHP {playerStats.currentHealth} をGameManagerに保存しました。");
            }

            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }

            if (GameManager.instance != null)
            {
                GameManager.instance.ReturnToMainScene();
            }
        }
    }


    // --- [ここから追加] ---
    /// <summary>
    /// Rod (Jキー) の特殊効果（強化）を実行します。
    /// （PerformAttack の case 0 からロジックを移動）
    /// </summary>
    /// <param name="slotIndex">エフェクトを表示するスロット番号</param>
    /// <returns>ターンを消費する場合 true</returns>
    private bool HandleRodJKey(int slotIndex)
    {
        // 1. エフェクトとサウンドの決定
        GameObject effectPrefab = null;
        if (rodAttackEffectPrefabs_J != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_J.Length)
        {
            effectPrefab = rodAttackEffectPrefabs_J[slotIndex];
        }

        AudioClip soundClip = rodAttackSound_J;

        // 2. 効果の発動
        isRodKAttackBoosted = true;
        Debug.Log("Rod (K) 攻撃が強化された！");

        // 3. 共通エフェクト・サウンド再生
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 0); // (type 0 = J)

        // 4. ターン消費
        return true;
    }

    /// <summary>
    /// Rod (Lキー) の特殊効果（回復）を実行します。
    /// （PerformAttack の case 2 からロジックを移動）
    /// </summary>
    /// <param name="slotIndex">エフェクトを表示するスロット番号</param>
    /// <returns>ターンを消費する場合 true</returns>
    private bool HandleRodLKey(int slotIndex)
    {
        bool actionTakesTurn = true;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;

        // 1. エフェクトの決定
        if (rodAttackEffectPrefabs_L != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_L.Length)
        {
            effectPrefab = rodAttackEffectPrefabs_L[slotIndex];
        }

        // 2. 効果の発動（使用制限チェック）
        if (hasUsedRodLHeal)
        {
            Debug.Log("Rod (L) の回復は戦闘中1回しか使えない。");
            if (ineffectiveAttackSound != null)
            {
                soundClip = ineffectiveAttackSound;
            }
            actionTakesTurn = false; // 2回目以降はターン消費しない
        }
        else
        {
            soundClip = rodAttackSound_L;

            if (playerStats != null)
            {
                playerStats.Heal(rodLHealAmount);
                hasUsedRodLHeal = true;
                Debug.Log($"Rod (L) で {rodLHealAmount} HP回復した。 (現在HP: {playerStats.currentHealth})");
            }
            else
            {
                Debug.LogError("playerStats が null のため回復できませんでした。");
            }
            // (actionTakesTurn はデフォルトで true)
        }

        // 3. 共通エフェクト・サウンド再生
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 2); // (type 2 = L)

        // 4. ターン消費
        return actionTakesTurn;
    }
    // --- [追加ここまで] ---


    /// <summary>
    /// プレイヤーの攻撃を実行します（ダメージ計算、エフェクト再生、サウンド再生）
    /// </summary>
    /// <returns>ダメージが適用された場合 (敵のターン処理に進むべき場合) true、無効だった場合 false</returns>
    // --- [ここから変更] ---
    private bool PerformAttack(CharacterStats targetEnemy, bool isRoto, int attackTypeIndex, int slotIndex)
    {
        // J(0) と L(2) のロジックは HandleRodJKey / HandleRodLKey に移動しました

        bool actionTakesTurn = true;
        int finalDamage = 0;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;

        if (isRoto)
        {
            // --- 1. Roto (Kキー) の処理 ---
            // (Rotoの場合、attackTypeIndex は常に 1 (K) のはず)
            if (rotoAttackEffectPrefabs != null && slotIndex >= 0 && slotIndex < rotoAttackEffectPrefabs.Length)
            {
                effectPrefab = rotoAttackEffectPrefabs[slotIndex];
            }

            string enemyName = targetEnemy.gameObject.name.ToLower();
            bool isEffective = false;
            if (enemyName.Contains("slime") || enemyName.Contains("skeleton"))
            {
                isEffective = true;
            }

            if (isEffective)
            {
                int damage = playerStats.attack - targetEnemy.defense;
                finalDamage = Mathf.Max(1, damage);
                soundClip = rotoAttackSound;
                targetEnemy.TakeDamage(finalDamage);

                // 敵のHPが0以下になったかチェック
                if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                {
                    targetEnemy.gameObject.SetActive(false);
                    Debug.Log($"{targetEnemy.gameObject.name} (スロット) を倒した！");

                    // --- [ここから修正] ---
                    // 敵を倒したので、FlagManager のメソッドを呼び出す
                    if (flagManager != null)
                    {
                        flagManager.NotifyEnemyDefeated();
                    }
                    // --- [修正ここまで] ---
                }
            }
            else
            {
                finalDamage = 0;
                if (ineffectiveAttackSound != null)
                {
                    soundClip = ineffectiveAttackSound;
                }
                actionTakesTurn = false;
                Debug.Log($"Roto 攻撃は {targetEnemy.name} に無効だ。");
            }
        }
        else // Rod
        {
            // --- 2. Rod (K) の処理 ---
            // (J, L は Update() 側で分岐されたため、ここに来るのは K(1) のみ)

            if (attackTypeIndex == 1) // Kキー
            {
                if (rodAttackEffectPrefabs_K != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_K.Length)
                {
                    effectPrefab = rodAttackEffectPrefabs_K[slotIndex];
                }
                soundClip = rodAttackSound_K;

                string enemyName = targetEnemy.gameObject.name.ToLower();
                bool isEffective = false;
                if (enemyName.Contains("golem") || enemyName.Contains("ghost"))
                {
                    isEffective = true;
                }

                if (isEffective)
                {
                    int damage = playerStats.attack - targetEnemy.defense;
                    damage = Mathf.Max(1, damage);

                    if (isRodKAttackBoosted)
                    {
                        finalDamage = damage * 3;
                        isRodKAttackBoosted = false;
                        Debug.Log($"Rod (K) 強化攻撃！ {finalDamage} のダメージ！");
                    }
                    else
                    {
                        finalDamage = damage;
                        Debug.Log($"Rod (K) 攻撃！ {finalDamage} のダメージ！");
                    }
                    targetEnemy.TakeDamage(finalDamage);

                    // 敵のHPが0以下になったかチェック
                    if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                    {
                        targetEnemy.gameObject.SetActive(false);
                        Debug.Log($"{targetEnemy.gameObject.name} (スロット) を倒した！");

                        // --- [ここから修正] ---
                        // 敵を倒したので、FlagManager のメソッドを呼び出す
                        if (flagManager != null)
                        {
                            flagManager.NotifyEnemyDefeated();
                        }
                        // --- [修正ここまで] ---
                    }
                }
                else // 無効な対象
                {
                    finalDamage = 0;
                    if (ineffectiveAttackSound != null)
                    {
                        soundClip = ineffectiveAttackSound;
                    }
                    actionTakesTurn = false;
                    Debug.Log($"Rod (K) 攻撃は {targetEnemy.name} に無効だ。");
                }
            }
            else
            {
                Debug.LogWarning($"PerformAttack(Rod) が不明な attackTypeIndex: {attackTypeIndex} で呼ばれました。");
                actionTakesTurn = false;
            }
        }

        // --- 3. 共通処理 (エフェクトとサウンドの再生) ---
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, attackTypeIndex);

        return actionTakesTurn;
    }
    // --- [変更ここまで] ---


    // --- [ここから追加] ---
    /// <summary>
    /// エフェクトとサウンドを再生する共通ヘルパーメソッド
    /// （PerformAttack の 共通処理(3) を分離）
    /// </summary>
    private void PlayEffectAndSound(GameObject effectPrefab, AudioClip soundClip, int slotIndex, int attackTypeIndex)
    {
        // 3a. エフェクト再生
        if (effectPrefab != null)
        {
            if (activeEffectCoroutines.ContainsKey(effectPrefab) && activeEffectCoroutines[effectPrefab] != null)
            {
                StopCoroutine(activeEffectCoroutines[effectPrefab]);
                activeEffectCoroutines.Remove(effectPrefab);
            }
            effectPrefab.SetActive(true);
            Coroutine newCoroutine = StartCoroutine(HideEffectAfterDelay(effectPrefab, 1.0f));
            activeEffectCoroutines[effectPrefab] = newCoroutine;
        }
        else
        {
            // J, K, L のどれでも、設定されていなければ警告
            Debug.LogWarning($"スロット {slotIndex} (タイプ {attackTypeIndex}) に対応するエフェクトがインスペクタで設定されていません。");
        }

        // 3b. サウンド再生 (無効な攻撃でも鳴らす)
        if (audioSource != null && soundClip != null)
        {
            audioSource.PlayOneShot(soundClip);
        }
    }
    // --- [追加ここまで] ---


    /// <summary>
    /// 指定された時間(delay)が経過した後、エフェクトオブジェクトを非表示にします。
    /// </summary>
    private System.Collections.IEnumerator HideEffectAfterDelay(GameObject effect, float delay)
    {
        // ... (中略: このメソッド自体は変更なし) ...
        yield return new WaitForSeconds(delay);
        if (effect != null)
        {
            effect.SetActive(false);
        }
        if (activeEffectCoroutines.ContainsKey(effect))
        {
            activeEffectCoroutines.Remove(effect);
        }
    }
}