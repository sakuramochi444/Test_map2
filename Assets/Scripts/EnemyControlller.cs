using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Listが空でないことを確認するために使用

public class EnemyController : MonoBehaviour
{
    // プレイヤーオブジェクトの参照を保持する変数
    private Transform playerTransform;
    private CharacterController cc; // 敵自身のCharacterController

    void Start()
    {
        // プレイヤーのTransformを取得して保持する
        // プレイヤーは "Player" タグがついている前提
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // 自身のCharacterControllerを取得
        cc = GetComponent<CharacterController>();
    }

    // ▼▼▼ Update() メソッドを削除、またはコメントアウトします ▼▼▼
    /*
    void Update()
    {
        // プレイヤーが既に戦闘を開始しているフレームでは、敵は行動しない
        if (GameManager.instance != null && GameManager.instance.combatInitiatedThisFrame)
        {
            return;
        }

        // W, A, S, D いずれかのキーが押された瞬間を検知
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
        {
            DecideActionWithProbability();
        }
    }
    */
    // ▲▲▲ ここまで削除 ▲▲▲


    // ▼▼▼ このメソッドを新しく追加します ▼▼▼
    /// <summary>
    /// プレイヤーの行動後に呼び出され、敵の行動（追跡またはランダム移動）を実行する
    /// </summary>
    public void ExecuteTurn(Vector3 playerPosBeforeMove)
    {
        // (戦闘開始フラグのチェックは変更なし)
        if (GameManager.instance != null && GameManager.instance.combatInitiatedThisFrame)
        {
            return;
        }

        // プレイヤーが戦闘を開始しなかったので、敵は行動を決定する
        // ▼▼▼ 修正: 引数を DecideActionWithProbability へ渡す ▼▼▼
        DecideActionWithProbability(playerPosBeforeMove);
    }
    // ▲▲▲ ここまで追加 ▲▲▲


    /// <summary>
    /// 確率に基づいて「留まる」か「移動する」かを決定する
    /// </summary>
    void DecideActionWithProbability(Vector3 playerPosForChaseCheck)
    {
        // 0. プレイヤーの追跡を試みる
        // ▼▼▼ 修正: 引数を TryChasePlayer へ渡す ▼▼▼
        if (TryChasePlayer(playerPosForChaseCheck))
        {
            // 追跡に成功した（プレイヤーの方向に移動した）場合は、ここで処理終了
            return;
        }

        // (ランダム移動のロジックは変更なし)
        // ...
        float randomValue = Random.value;
        if (randomValue < 0.1f)
        {
            return;
        }
        else
        {
            MoveToRandomValidSpot();
        }
    }


    /// <summary>
    /// プレイヤーが前方にいるか確認し、いる場合はその方向へ移動を試みる
    /// </summary>
    /// <returns>追跡移動に成功した場合はtrue、そうでない場合はfalse</returns>
    bool TryChasePlayer(Vector3 playerPosForChaseCheck)
    {
        /* (playerTransform のチェックは不要になります)
        if (playerTransform == null)
        {
            return false;
        }
        */

        // 1. 敵の「前方」ベクトルをオイラー角(Y)に基づいて決定する (変更なし)
        float yAngle = transform.rotation.eulerAngles.y;
        Vector3 forwardDir = Vector3.zero;
        if (Mathf.Abs(yAngle - 0) < 1.0f || Mathf.Abs(yAngle - 360) < 1.0f) { forwardDir = new Vector3(0, 0, 1); }
        else if (Mathf.Abs(yAngle - 90) < 1.0f) { forwardDir = new Vector3(1, 0, 0); }
        else if (Mathf.Abs(yAngle - 180) < 1.0f) { forwardDir = new Vector3(0, 0, -1); }
        else if (Mathf.Abs(yAngle - 270) < 1.0f || Mathf.Abs(yAngle - (-90)) < 1.0f) { forwardDir = new Vector3(-1, 0, 0); }
        else { forwardDir = GetRoundedDirection(transform.forward); }

        // ▼▼▼ 修正ここから ▼▼▼
        // 2. プレイヤーの相対位置を「移動前の位置」から取得
        // (playerTransform.position の代わりに引数 playerPosForChaseCheck を使う)
        Vector3 playerRelativePos = playerPosForChaseCheck - transform.position;
        // ▲▲▲ 修正ここまで ▲▲▲

        // 3. プレイヤーが「前方」のマスにいるかチェック (変更なし)
        if (IsPlayerInDirection(playerRelativePos, forwardDir))
        {
            // (移動先のチェックロジックは変更なし)
            // ...
            Vector3 targetPosition = transform.position + forwardDir;
            int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
            int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);
            if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16) { return false; }
            int targetCellType = MapGenerator.map[mapZ, mapX];
            if (targetCellType == 1 || targetCellType == 2) { return false; }
            if (IsEnemyAt(targetPosition)) { return false; }

            // 5. 移動可能！ (変更なし)
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;
            Debug.Log("追従します。");

            return true; // 追跡成功
        }

        return false; // プレイヤーが前方にいなかった
    }

    /// <summary>
    /// プレイヤーが指定した方向の隣接マスにいるかチェック
    /// (Y軸の高さを無視して判定するように修正)
    /// </summary>
    bool IsPlayerInDirection(Vector3 playerRelativePos, Vector3 direction)
    {
        // (*** この関数の中身は変更ありません ***)
        Vector3 relativePosXZ = new Vector3(playerRelativePos.x, 0, playerRelativePos.z);
        if (Vector3.Distance(relativePosXZ, direction) < 0.1f)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// transform.forwardなどから、(1,0,0) や (0,0,-1) のような
    /// 軸に沿ったきれいなベクトルを取得する (予備として残します)
    /// </summary>
    Vector3 GetRoundedDirection(Vector3 direction)
    {
        // (*** この関数の中身は変更ありません ***)
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return new Vector3(Mathf.Sign(direction.x), 0, 0);
        }
        else
        {
            return new Vector3(0, 0, Mathf.Sign(direction.z));
        }
    }


    /// <summary>
    /// 移動可能なマスの中からランダムに１つを選んで移動し、その方向を向く
    /// (この関数は「移動する」と決まった後に呼ばれる)
    /// </summary>
    void MoveToRandomValidSpot()
    {
        // (*** CharacterControllerの有効/無効化以外、中身は変更ありません ***)
        List<Vector3> possibleMoveDestinations = GetPossibleMoveDestinations();

        if (possibleMoveDestinations.Any())
        {
            int randomIndex = Random.Range(0, possibleMoveDestinations.Count);
            Vector3 chosenDestination = possibleMoveDestinations[randomIndex];
            Vector3 moveDirection = chosenDestination - transform.position;

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = targetRotation;
            }

            if (cc != null) cc.enabled = false;
            transform.position = chosenDestination;
            if (cc != null) cc.enabled = true;
        }
    }

    /// <summary>
    /// 現在地から移動可能な「行き先」（現在地は含まない）をリストで返す
    /// </summary>
    /// <returns>移動可能な座標(Vector3)のリスト</returns>
    List<Vector3> GetPossibleMoveDestinations()
    {
        // (*** この関数の中身は変更ありません ***)
        List<Vector3> destinations = new List<Vector3>();
        Vector3[] directions = {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };

        foreach (var dir in directions)
        {
            Vector3 targetPosition = transform.position + dir;
            int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
            int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);
            if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16) { continue; }
            int targetCellType = MapGenerator.map[mapZ, mapX];
            if (targetCellType == 1 || targetCellType == 2) { continue; }
            if (IsEnemyAt(targetPosition)) { continue; }
            destinations.Add(targetPosition);
        }
        return destinations;
    }

    /// <summary>
    /// 指定した座標に (自分以外の) Enemyタグのオブジェクトがあるかチェックする
    /// </summary>
    bool IsEnemyAt(Vector3 position)
    {
        // (*** この関数の中身は変更ありません ***)
        Collider[] hitColliders = Physics.OverlapSphere(position, 0.4f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy") && hitCollider.gameObject != this.gameObject)
            {
                return true;
            }
        }
        return false;
    }
}