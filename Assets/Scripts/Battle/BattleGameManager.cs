// BattleGameManager.cs

using UnityEngine;
using System.Collections.Generic; // ListやLinqのために追加
using System.Linq; // Linq (Any) を使うために追加

public class BattleGameManager : MonoBehaviour
{
    /// <summary>
    /// プレイヤーの向きを示す Roto オブジェクト (roto1, roto2, roto3, roto4 の順で設定)
    /// [0] = roto1 (Aキー対応)
    /// [1] = roto2 (Dキー対応)
    /// [2] = roto3 (Sキー対応)
    /// [3] = roto4 (Wキー対応)
    /// </summary>
    public GameObject[] rotos = new GameObject[4];

    /// <summary>
    /// 敵のスライム オブジェクト (slime1, slime2, slime3, slime4 の順で設定)
    /// [0] = slime1 (roto1の攻撃対象)
    /// [1] = slime2 (roto2の攻撃対象)
    /// [2] = slime3 (roto3の攻撃対象)
    /// [3] = slime4 (roto4の攻撃対象)
    /// </summary>
    public GameObject[] slimes = new GameObject[4];


    void Start()
    {
        // 1. Rotoをすべて非表示にする (既存のロジック)
        foreach (var roto in rotos)
        {
            if (roto != null)
            {
                roto.SetActive(false);
            }
        }

        // 2. スライムを初期配置する
        InitializeSlimes();
    }

    /// <summary>
    /// スライムを初期配置する
    /// (GameManagerから渡された有効な方向のみを対象とする)
    /// </summary>
    void InitializeSlimes()
    {
        // 1. 全てのスライムを非表示にする
        foreach (var slime in slimes)
        {
            if (slime != null)
            {
                slime.SetActive(false);
            }
        }

        // 2. GameManagerから有効な方向のリストを取得
        List<int> validDirections = new List<int>();
        if (GameManager.instance != null)
        {
            validDirections = GameManager.instance.validCombatDirections;
            Debug.Log("GameManagerから有効な方向を取得しました: " + string.Join(",", validDirections));
        }
        else
        {
            Debug.LogError("GameManagerが見つかりません。MainSceneから開始してください。");
            // GameManagerがいない場合、スライムは出現しない
        }

        // 3. 有効な方向リストに基づいて、表示対象となるスライムのインデックスリストを作成
        List<int> availableIndices = new List<int>();
        foreach (int index in validDirections)
        {
            // インデックスが 0〜3 の範囲内であり、
            // かつ Slimes[index] がインスペクタで設定されているか確認
            if (index >= 0 && index < slimes.Length && slimes[index] != null)
            {
                availableIndices.Add(index);
            }
            else if (index >= 0 && index < slimes.Length && slimes[index] == null)
            {
                // ▼▼▼「表示されません」対策のデバッグログ▼▼▼
                Debug.LogWarning($"インデックス {index} は有効な方向として指定されましたが、BattleGameManagerのSlimes配列のElement {index} が設定されていません。");
            }
        }

        // 4. 表示対象のスライムがいない場合
        if (availableIndices.Count == 0)
        {
            if (validDirections.Count == 0 && GameManager.instance != null)
            {
                Debug.Log("GameManagerから有効な方向が指定されませんでした（全方向が壁など）。スライムは出現しません。");
            }
            else if (validDirections.Count > 0)
            {
                Debug.LogError("「表示されません」エラーの可能性：有効な方向はありましたが、対応するSlimes配列がすべて未設定です。インスペクタを確認してください。");
            }
            // スライムが0体なので、すぐに戦闘終了チェックを行う
            CheckForBattleEnd();
            return;
        }

        // 5. 表示するスライムの数 (1〜最大でavailableIndices.Count) を決定
        // (有効な方向のリストから、ランダムに1〜N個選ぶ)

        // 最大4方向というご要望ですが、そもそも戦闘可能方向が4つより多い場合は
        // availableIndices.Count が最大値となります。
        // ここでは「戦闘可能方向の数」を上限としてランダムに選びます。
        int count = Random.Range(1, availableIndices.Count + 1); // 1 から (有効なスライム数)

        // 6. リストからランダムに 'count' 個選んで表示する
        Debug.Log($"戦闘可能な {availableIndices.Count} 方向のうち、ランダムで {count} 体のスライムが出現します。");

        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break; // 念のため

            // リスト (availableIndices) からランダムなインデックスを選ぶ
            int listIndex = Random.Range(0, availableIndices.Count);
            // そのリストのインデックスに格納されているスライムの番号 (0〜3) を取得
            int slimeIndex = availableIndices[listIndex];

            if (slimes[slimeIndex] != null)
            {
                slimes[slimeIndex].SetActive(true);
            }

            // 一度選んだスライムはリストから削除し、重複して選ばれないようにする
            availableIndices.RemoveAt(listIndex);
        }
    }


    void Update()
    {
        // 1. プレイヤーの向き選択 (Rotoの切り替え)
        if (Input.GetKeyDown(KeyCode.W))
        {
            SetActiveRoto(3); // roto4
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            SetActiveRoto(2); // roto3
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            SetActiveRoto(0); // roto1
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            SetActiveRoto(1); // roto2
        }

        // 2. 攻撃 (Kキー)
        else if (Input.GetKeyDown(KeyCode.K))
        {
            int activeRotoIndex = -1;
            for (int i = 0; i < rotos.Length; i++)
            {
                if (rotos[i] != null && rotos[i].activeSelf)
                {
                    activeRotoIndex = i;
                    break;
                }
            }

            if (activeRotoIndex != -1)
            {
                if (activeRotoIndex < slimes.Length && slimes[activeRotoIndex] != null)
                {
                    if (slimes[activeRotoIndex].activeSelf)
                    {
                        slimes[activeRotoIndex].SetActive(false);
                        Debug.Log($"Slime {activeRotoIndex + 1} を倒した！");
                    }
                    else
                    {
                        Debug.Log($"Slime {activeRotoIndex + 1} は既に倒されている。");
                    }
                }
            }
            else
            {
                Debug.Log("攻撃する向きを選択してください (A, S, D, W キー)。");
                return;
            }

            // 2-3. 攻撃後、スライムが全滅したかチェック
            CheckForBattleEnd();
        }
    }

    /// <summary>
    /// 指定されたインデックスのRotoのみをアクティブにする
    /// </summary>
    void SetActiveRoto(int index)
    {
        if (index < 0 || index >= rotos.Length || rotos[index] == null)
        {
            foreach (var roto in rotos)
            {
                if (roto != null) roto.SetActive(false);
            }
            return;
        }

        for (int i = 0; i < rotos.Length; i++)
        {
            if (rotos[i] != null)
            {
                rotos[i].SetActive(i == index);
            }
        }
    }

    /// <summary>
    /// アクティブなスライムが残っているか確認し、いなければ戦闘を終了する
    /// </summary>
    void CheckForBattleEnd()
    {
        // slimes配列をチェックし、1つでも activeSelf が true のものがあるか
        bool anySlimeActive = slimes.Any(slime => slime != null && slime.activeSelf);

        // 1つもアクティブなスライムがなかった場合
        if (!anySlimeActive)
        {
            Debug.Log("すべてのスライムを倒した！戦闘終了。");
            if (GameManager.instance != null)
            {
                GameManager.instance.ReturnToMainScene();
            }
        }
    }
}