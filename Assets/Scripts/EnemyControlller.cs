using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Listが空でないことを確認するために使用

public class EnemyController : MonoBehaviour
{
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

    /// <summary>
    /// 確率に基づいて「留まる」か「移動する」かを決定する
    /// </summary>
    void DecideActionWithProbability()
    {
        // 0.0から1.0の間のランダムな値を取得
        float randomValue = Random.value;

        // 1. 10%の確率で「留まる」
        if (randomValue < 0.1f) // 0.1f は 10%
        {
            // 何もせず、現在の位置と向きを維持する
            return;
        }
        // 2. 残りの90%の確率で「移動する」
        else
        {
            MoveToRandomValidSpot();
        }
    }

    /// <summary>
    /// 移動可能なマスの中からランダムに１つを選んで移動し、その方向を向く
    /// (この関数は「移動する」と決まった後に呼ばれる)
    /// </summary>
    void MoveToRandomValidSpot()
    {
        // 1. 移動可能な「行き先」のリストを取得する（このリストに現在地は含まない）
        List<Vector3> possibleMoveDestinations = GetPossibleMoveDestinations();

        // 2. 移動先が1つ以上ある場合のみ、移動処理を行う
        if (possibleMoveDestinations.Any()) // .Any()はリストに要素が1つ以上あるかを確認するのに便利
        {
            // 3. リストの中からランダムに1つの座標を選ぶ
            int randomIndex = Random.Range(0, possibleMoveDestinations.Count);
            Vector3 chosenDestination = possibleMoveDestinations[randomIndex];

            // 4. 現在地から目的地までの移動方向ベクトルを計算する
            Vector3 moveDirection = chosenDestination - transform.position;

            // 5. 移動方向がある場合、向きを変える
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = targetRotation;
            }

            // 6. 選ばれた座標へ移動する
            transform.position = chosenDestination;
        }
        // 7. 移動可能なマスがない場合（袋小路など）は、結果的に「留まる」ことになる
    }

    /// <summary>
    /// 現在地から移動可能な「行き先」（現在地は含まない）をリストで返す
    /// </summary>
    /// <returns>移動可能な座標(Vector3)のリスト</returns>
    List<Vector3> GetPossibleMoveDestinations()
    {
        List<Vector3> destinations = new List<Vector3>();

        // 上下左右4方向のベクトルを定義
        Vector3[] directions = {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };

        // 各方向をチェックする
        foreach (var dir in directions)
        {
            Vector3 targetPosition = transform.position + dir;
            int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
            int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);

            if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16)
            {
                continue;
            }

            int targetCellType = MapGenerator.map[mapZ, mapX];

            if (targetCellType == 0 || targetCellType == 3)
            {
                destinations.Add(targetPosition);
            }
        }
        return destinations;
    }
}