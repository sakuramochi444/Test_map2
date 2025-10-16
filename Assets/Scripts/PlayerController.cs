using UnityEngine;

// ファイル名は PlayerController.cs ですが、クラス名は PlayerViewController になっていますね。
// このまま動作しますが、もし意図しないものであればファイル名とクラス名を合わせることをお勧めします。
public class PlayerController : MonoBehaviour
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
        // ↓↓↓↓ ここを修正 ↓↓↓↓
        else if (targetCellType == 2) // 2は階段
        {
            // マップジェネレーターにlevel2のマップを生成するように指示する
            MapGenerator.instance.ChangeMap(MapGenerator.level2); // ← この行を追加

            // プレイヤーを新しいマップの初期位置に移動させる
            SetInitialPosition(false);
        }
        // ↑↑↑↑ ここまで修正 ↑↑↑↑
    }

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
            // 新しいマップ(level2)のスタート位置
            newPosition = new Vector3(-6.5f, transform.position.y, -6.5f);
        }

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        transform.position = newPosition;

        if (cc != null)
        {
            cc.enabled = true;
        }
    }
}