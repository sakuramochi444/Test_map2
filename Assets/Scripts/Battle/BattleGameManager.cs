// BattleGameManager.cs

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// [追加] シリアル通信に必要なライブラリ
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class BattleGameManager : MonoBehaviour
{
    // ... (スライム ～ rodLHealAmount までのインスペクタ設定は変更なし) ...
    // ... (slimes ～ rodLHealAmount) ...

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


    // --- [追加] シリアル通信設定 (インスペクタから設定) ---
    [Header("シリアルポート設定")]
    [Tooltip("マイコンが接続されているCOMポート名 (例: COM3)")]
    public string portName = "COM256";
    [Tooltip("ボーレート (マイコン側の Serial.begin() と合わせる)")]
    public int baudRate = 115200;
    // -------------------------------------------------


    // ... (currentEnemies ～ gameOverSceneName は変更なし) ...
    private CharacterStats[] currentEnemies = new CharacterStats[4];
    private float[] enemyActionCounters;
    private AudioSource audioSource;
    private Dictionary<GameObject, Coroutine> activeEffectCoroutines = new Dictionary<GameObject, Coroutine>();
    private bool isRodKAttackBoosted = false;
    private bool hasUsedRodLHeal = false;
    private bool isBattleEnding = false;
    private AudioClip lastPlayedAttackSound = null;
    public string gameOverSceneName = "MainScene";


    private FlagManager flagManager;

    // --- [追加] シリアル通信用の内部変数 ---
    private SerialPort serialPort;
    private Thread readThread;
    private bool isThreadRunning = false;
    // スレッドセーフなキュー (サブスレッド -> メインスレッド(Update) へのデータ受け渡し用)
    private ConcurrentQueue<string> receivedDataQueue = new ConcurrentQueue<string>();
    // --------------------------------------


    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // ... (FlagManager, 武器非表示, enemyActionCounters の設定は変更なし) ...
        flagManager = FlagManager.instance;
        if (flagManager == null)
        {
            Debug.LogError("FlagManager.instance が見つかりません！ MainSceneから正しくロードされていない可能性があります。");
        }
        foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
        foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }
        enemyActionCounters = new float[4];


        // ... (playerStats の初期化処理は変更なし) ...
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

        // ... (武器表示、エフェクト非表示、フラグ初期化は変更なし) ...
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


        // --- [追加] シリアルポートを開く ---
        OpenPort();
        // ---------------------------------
    }

    // [追加] アプリケーション終了時にポートを閉じる
    void OnApplicationQuit()
    {
        ClosePort();
    }

    // [追加] (シーン切り替え時など、このオブジェクトが破棄される時にも呼ぶ)
    void OnDestroy()
    {
        ClosePort();
    }


    // ... (HideAllMonsters, InitializeEnemies は変更なし) ...
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


    // --- [ここから修正 (Update メソッド)] ---
    void Update()
    {
        if (isBattleEnding) return;

        // --- [追加] シリアル入力処理 ---
        bool serialAttackInput = false;
        while (receivedDataQueue.TryDequeue(out string data))
        {
            // マイコンからは ReadLine() で読み取るため、余分な改行コードや空白を除去
            string trimmedData = data.Trim();

            if (trimmedData == "s")
            {
                serialAttackInput = true;
                Debug.Log("マイコンから 's' を受信しました。");
                // 1フレームで複数の 's' を処理しないよう、1つ見つけたらキューの処理を中断
                break;
            }
            else
            {
                // 's' 以外（改行コードのみなど）は無視
                if (!string.IsNullOrEmpty(trimmedData))
                {
                    Debug.Log("シリアル受信 (無視): " + data);
                }
            }
        }
        // --- [追加ここまで] ---


        bool playerActed = false;

        // --- [修正] ---
        // 変数宣言を if/else if チェーンの *前* に移動
        bool isRoto = (GameManager.instance != null) ? GameManager.instance.isRotoActive : true;
        bool rotoAttackInput = (isRoto && serialAttackInput); // 's' は Roto の時だけ攻撃トリガーになる
        // --- [修正ここまで] ---


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

        // --- [修正] ---
        // (if/else if チェーンが途切れないように、間の変数宣言を上に移動させた)
        // 1c. 「攻撃」 (J, K, Lキー、または Roto時のシリアル入力)
        else if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.L) || rotoAttackInput)
        // --- [修正ここまで] ---
        {
            if (playerStats == null) { return; }

            lastPlayedAttackSound = null;

            int activeRotoIndex = (GameManager.instance != null) ? GameManager.instance.currentWeaponDirectionIndex : 0;
            if (activeRotoIndex < 0 || activeRotoIndex >= 4) { return; }

            // (isRoto は上で取得済み)

            // J, L キーは敵の存在に関わらず発動 (Rod使用時のみ)
            if (!isRoto && Input.GetKeyDown(KeyCode.J))
            {
                playerActed = HandleRodJKey(activeRotoIndex);
            }
            else if (!isRoto && Input.GetKeyDown(KeyCode.L))
            {
                playerActed = HandleRodLKey(activeRotoIndex);
            }
            // Kキー (または Roto使用時のシリアル入力、または Roto使用時の J, Lキー) は、敵の存在チェックが必要
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
                    }
                    else
                    {
                        // 1b. 敵がいて、生存している (攻撃実行)
                        if (isRoto)
                        {
                            // Roto (K または シリアル's')
                            // [変更] Kキー または Roto時のシリアル入力
                            if (Input.GetKeyDown(KeyCode.K) || rotoAttackInput)
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
                            // [変更] Kキーのみ (シリアル入力はRoto専用)
                            if (Input.GetKeyDown(KeyCode.K))
                            {
                                playerActed = PerformAttack(targetEnemy, false, 1, activeRotoIndex);
                            }
                        }
                    }
                }
                else
                {
                    // 2. 攻撃対象（敵）が「いない」場合 (Kキーまたはシリアル's')
                    // [変更] KキーまたはRoto時のシリアル入力の場合のみログを出す
                    if (Input.GetKeyDown(KeyCode.K) || rotoAttackInput)
                    {
                        Debug.Log($"スロット {activeRotoIndex} には現在、敵がいません。");
                    }
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
    // --- [Update メソッドの修正ここまで] ---


    // ... (SetActiveWeaponDisplay ～ CheckForBattleEndCoroutine は変更なし) ...
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


    // ... (HandleRodJKey, HandleRodLKey, PerformAttack, PlayEffectAndSound, HideEffectAfterDelay は変更なし) ...
    // (これらのメソッドは、今回のロジック変更の影響を受けないため、そのまま)
    private bool HandleRodJKey(int slotIndex)
    {
        GameObject effectPrefab = null;
        if (rodAttackEffectPrefabs_J != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_J.Length)
        {
            effectPrefab = rodAttackEffectPrefabs_J[slotIndex];
        }
        AudioClip soundClip = rodAttackSound_J;
        isRodKAttackBoosted = true;
        Debug.Log("Rod (K) 攻撃が強化された！");
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 0);
        return true;
    }
    private bool HandleRodLKey(int slotIndex)
    {
        bool actionTakesTurn = true;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;
        if (rodAttackEffectPrefabs_L != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_L.Length)
        {
            effectPrefab = rodAttackEffectPrefabs_L[slotIndex];
        }
        if (hasUsedRodLHeal)
        {
            Debug.Log("Rod (L) の回復は戦闘中1回しか使えない。");
            if (ineffectiveAttackSound != null)
            {
                soundClip = ineffectiveAttackSound;
            }
            actionTakesTurn = false;
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
        }
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 2);
        return actionTakesTurn;
    }
    private bool PerformAttack(CharacterStats targetEnemy, bool isRoto, int attackTypeIndex, int slotIndex)
    {
        bool actionTakesTurn = true;
        int finalDamage = 0;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;

        if (isRoto)
        {
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
                if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                {
                    targetEnemy.gameObject.SetActive(false);
                    Debug.Log($"{targetEnemy.gameObject.name} (スロット) を倒した！");
                    if (GameManager.instance != null)
                    {
                        GameManager.instance.NotifyEnemyDefeated();
                    }
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
                    if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                    {
                        targetEnemy.gameObject.SetActive(false);
                        Debug.Log($"{targetEnemy.gameObject.name} (スロット) を倒した！");
                        if (GameManager.instance != null)
                        {
                            GameManager.instance.NotifyEnemyDefeated();
                        }
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
                    Debug.Log($"Rod (K) 攻撃は {targetEnemy.name} に無効だ。");
                }
            }
            else
            {
                Debug.LogWarning($"PerformAttack(Rod) が不明な attackTypeIndex: {attackTypeIndex} で呼ばれました。");
                actionTakesTurn = false;
            }
        }
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, attackTypeIndex);

        return actionTakesTurn;
    }
    private void PlayEffectAndSound(GameObject effectPrefab, AudioClip soundClip, int slotIndex, int attackTypeIndex)
    {
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
            Debug.LogWarning($"スロット {slotIndex} (タイプ {attackTypeIndex}) に対応するエフェクトがインスペクタで設定されていません。");
        }
        if (audioSource != null && soundClip != null)
        {
            audioSource.PlayOneShot(soundClip);
        }
    }
    private System.Collections.IEnumerator HideEffectAfterDelay(GameObject effect, float delay)
    {
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


    // --- [ここから追加 (シリアル通信メソッド)] ---

    /// <summary>
    /// シリアルポートを開き、読み取りスレッドを開始します。
    /// </summary>
    private void OpenPort()
    {
        serialPort = new SerialPort(portName, baudRate);
        serialPort.ReadTimeout = 1000; // 読み取りタイムアウト（ミリ秒）

        try
        {
            serialPort.Open();
            isThreadRunning = true;

            // 読み取り用のサブスレッドを開始
            readThread = new Thread(ReadSerialData);
            readThread.IsBackground = true; // メインスレッド終了時に自動終了させる
            readThread.Start();

            Debug.Log("シリアルポートを開きました: " + portName);
        }
        catch (System.Exception e)
        {
            Debug.LogError("ポートを開けませんでした: " + e.Message);
            // (ポートが開けなくてもゲームは続行する)
        }
    }

    /// <summary>
    /// シリアルポートを閉じ、読み取りスレッドを停止します。
    /// </summary>
    private void ClosePort()
    {
        // スレッドを停止
        isThreadRunning = false;

        // スレッドが終了するのを待つ (Join)
        if (readThread != null && readThread.IsAlive)
        {
            try
            {
                readThread.Join();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("シリアル読み取りスレッドの停止中にエラー: " + e.Message);
            }
        }
        readThread = null;

        // ポートを閉じる
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Close();
                serialPort.Dispose();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("シリアルポートの切断中にエラー: " + e.Message);
            }

            serialPort = null;
            Debug.Log("シリアルポートを閉じました。");
        }
    }

    /// <summary>
    /// [サブスレッドで実行] シリアルデータを読み取り、キューに追加します。
    /// </summary>
    private void ReadSerialData()
    {
        while (isThreadRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                // データを1行読み取る (ReadLineはデータが来るまで待機する)
                // マイコン側で Serial.println("s") を使うことを想定
                string data = serialPort.ReadLine();

                // 読み取ったデータをスレッドセーフなキューに入れる
                // (Update側で処理される)
                receivedDataQueue.Enqueue(data);
            }
            catch (System.TimeoutException)
            {
                // タイムアウトは正常な動作なので無視
            }
            catch (System.Exception e)
            {
                // ポートが閉じられた、USBが抜かれたなどの場合
                if (isThreadRunning) // (ClosePort() が呼ばれた時以外のエラー)
                {
                    Debug.LogError("データ読み取りエラー: " + e.Message);
                    // (スレッドを停止させるか、再接続を試みるなどの処理も可能)
                }
            }
        }
        Debug.Log("シリアル読み取りスレッドを終了します。");
    }
    // --- [追加ここまで] ---
}