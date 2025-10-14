using UnityEngine;

public class PlayerViewController : MonoBehaviour
{
    void Start()
    {
        // ゲーム開始時にプレイヤーを初期位置へ
        SetInitialPosition(true);
    }

    void Update()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W))
        {
            moveDirection.x = 1; // X軸 正方向
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            moveDirection.x = -1; // X軸 負方向
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            moveDirection.z = 1; // Z軸 正方向
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            moveDirection.z = -1; // Z軸 負方向
        }

        if (moveDirection != Vector3.zero)
        {
            TryMove(moveDirection);
        }
    }

    void TryMove(Vector3 direction)
    {
        Vector3 targetPosition = transform.position + direction;

        int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
        int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);

        if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16)
        {
            return;
        }

        int targetCellType = MapGenerator.map[mapZ, mapX];

        if (targetCellType == 0) // 0は道
        {
            transform.position = targetPosition;
        }
        else if (targetCellType == 2) // 2は階段
        {
            SetInitialPosition(false);
        }
    }

    // ↓↓↓↓ ここを修正 ↓↓↓↓
    // プレイヤーを初期位置に設定するメソッド
    void SetInitialPosition(bool isFirstTime)
    {
        Vector3 newPosition;

        if (isFirstTime)
        {
            // 最初のマップ(level1)のスタート位置
            newPosition = new Vector3(6.5f, transform.position.y, -6.5f);
        }
        else
        {
            // 新しいマップ(level_new)のスタート位置
            newPosition = new Vector3(-6.5f, transform.position.y, -6.5f);
        }

        // --- ここからが修正箇所 ---
        // CharacterControllerがアタッチされている場合、一度無効にしてから座標をセットする
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // 座標を更新
        transform.position = newPosition;

        // CharacterControllerを再度有効にする
        if (cc != null)
        {
            cc.enabled = true;
        }
        // --- ここまで ---
    }
}