using UnityEngine;
using System.Collections;
using System.Collections.Generic; // <--- 追加
using System.Linq; // <--- 追加

// ファイル名は PlayerController.cs ですが、クラス名は PlayerViewController になっていますね。
// このまま動作しますが、もし意図しないものであればファイル名とクラス名を合わせることをお勧めします。
public class PlayerController : MonoBehaviour
{
    public GameObject roto1;
    public GameObject roto2;
    public GameObject roto3;
    public GameObject roto4;
    public GameObject tanni1;
    public GameObject tanni2;
    public GameObject tanni3;
    public GameObject tanni4;

    // 実行中のコルーチンを管理するための変数
    private Coroutine showTanniCoroutine;

    void Start()
    {
        // (*** この関数の中身は変更ありません ***)
        SetInitialPosition(true);
        roto1.SetActive(true);
        roto2.SetActive(false);
        roto3.SetActive(false);
        roto4.SetActive(false);

        tanni1.SetActive(false);
        tanni2.SetActive(false);
        tanni3.SetActive(false);
        tanni4.SetActive(false);
    }

    void Update()
    {
        Vector3 moveDirection = Vector3.zero;
        bool keyPressed = false; // <--- 追加

        if (Input.GetKeyDown(KeyCode.W))
        {
            moveDirection.x = 1; // X軸 正方向
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(true);
            keyPressed = true; // <--- 追加
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            moveDirection.x = -1; // X軸 負方向
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(true);
            roto4.SetActive(false);
            keyPressed = true; // <--- 追加
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            moveDirection.z = 1; // Z軸 正方向
            roto1.SetActive(true);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(false);
            keyPressed = true; // <--- 追加
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            moveDirection.z = -1; // Z軸 負方向
            roto1.SetActive(false);
            roto2.SetActive(true);
            roto3.SetActive(false);
            roto4.SetActive(false);
            keyPressed = true; // <--- 追加
        }

        Vector3 playerPosBeforeMove = transform.position;

        // 2. プレイヤーの移動処理（接触判定を含む）を先に実行
        if (moveDirection != Vector3.zero)
        {
            TryMove(moveDirection);
        }

        // 3. キーが押されていた場合、敵のターンを実行
        if (keyPressed)
        {
            EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
            foreach (EnemyController enemy in allEnemies)
            {
                // 4. 敵の追跡判定用に、プレイヤーの「移動前の位置」を渡す
                enemy.ExecuteTurn(playerPosBeforeMove);
            }
        }
        // ▲▲▲ 修正ここまで ▲▲▲
    }

    // TryMoveメソッドを以下のように変更
    void TryMove(Vector3 direction)
    {
        // (*** この関数の中身は変更ありません ***)

        // 移動前に CharacterController を取得
        CharacterController cc = GetComponent<CharacterController>();

        Vector3 targetPosition = transform.position + direction;

        // --- 敵との接触判定をここで行う ---
        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, 0.4f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                // 敵を発見した場合

                // 1. プレイヤーを敵のいたマスへ移動させる (ワープ処理)
                if (cc != null) cc.enabled = false;
                transform.position = targetPosition;
                if (cc != null) cc.enabled = true;

                // 2. GameManagerにフラグを立てさせ、他の敵の動きを封じる
                if (GameManager.instance != null)
                {
                    GameManager.instance.combatInitiatedThisFrame = true;
                }

                // 3. 戦闘を開始する (周囲のマップ情報を渡す)
                // (突っ込んだ場合は、現在地 = 敵の1マス手前 の周囲情報を渡す)
                List<int> validDirections = GetValidCombatDirections(transform.position);
                GameManager.instance.PlayerCaughtByEnemy(hitCollider.gameObject, validDirections);

                // 4. これ以上このメソッドの処理は行わない
                return;
            }
        }

        // --- 敵がいない場合、通常の移動処理 ---
        int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
        int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);

        if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16)
        {
            return;
        }

        int targetCellType = MapGenerator.map[mapZ, mapX];

        if (targetCellType == 0 || targetCellType == 4) // 道または何もない敵の出現ポイント
        {
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;
        }
        else if (targetCellType == 2) // 階段
        {
            MapGenerator.instance.ChangeMap(MapGenerator.level2);
            SetInitialPosition(false);
        }
        else if (targetCellType == 3) // 宝箱
        {
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;

            MapGenerator.map[mapZ, mapX] = 0;
            MapGenerator.instance.RemoveChestObjectsAt(mapX, mapZ);

            GameObject tanniToShow = null;
            if (direction.x > 0) { tanniToShow = tanni4; }
            else if (direction.x < 0) { tanniToShow = tanni3; }
            else if (direction.z > 0) { tanniToShow = tanni1; }
            else if (direction.z < 0) { tanniToShow = tanni2; }

            if (tanniToShow != null)
            {
                if (showTanniCoroutine != null)
                {
                    StopCoroutine(showTanniCoroutine);
                }
                showTanniCoroutine = StartCoroutine(ShowTanniAndHide(tanniToShow, 3f));
            }
        }
    }

    /// <summary>
    /// 指定されたGameObjectを一定時間表示した後に非表示にするコルーチン
    /// </summary>
    private IEnumerator ShowTanniAndHide(GameObject tanniObject, float duration)
    {
        // (*** この関数の中身は変更ありません ***)
        tanni1.SetActive(false);
        tanni2.SetActive(false);
        tanni3.SetActive(false);
        tanni4.SetActive(false);
        tanniObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        tanniObject.SetActive(false);
    }

    // プレイヤーを初期位置に設定するメソッド
    void SetInitialPosition(bool isFirstTime)
    {
        // (*** この関数の中身は変更ありません ***)
        Vector3 newPosition;
        if (isFirstTime) { newPosition = new Vector3(6.5f, transform.position.y, -6.5f); }
        else { newPosition = new Vector3(-6.5f, transform.position.y, -6.5f); }

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) { cc.enabled = false; }
        transform.position = newPosition;
        if (cc != null) { cc.enabled = true; }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            // ▼▼▼ 修正 ▼▼▼
            // (捕まった場合は、現在地 = 敵と同じマス の周囲情報を渡す)
            List<int> validDirections = GetValidCombatDirections(transform.position);
            GameManager.instance.PlayerCaughtByEnemy(other.gameObject, validDirections);
            // ▲▲▲ 修正 ▲▲▲
        }
    }

    private List<int> GetValidCombatDirections(Vector3 currentPos)
    {
        List<int> directions = new List<int>();

        // 現在のマップ座標を計算
        int mapX = Mathf.RoundToInt(currentPos.x + 7.5f);
        int mapZ = Mathf.RoundToInt(currentPos.z + 7.5f);

        // 各方向の座標とタイルの種類をチェック
        // [0] = roto1 (Aキー対応) -> MainSceneの Z+1 方向
        // [1] = roto2 (Dキー対応) -> MainSceneの Z-1 方向
        // [2] = roto3 (Sキー対応) -> MainSceneの X-1 方向
        // [3] = roto4 (Wキー対応) -> MainSceneの X+1 方向

        // チェック (Aキー方向: Z+1)
        if (IsValidTile(mapX, mapZ + 1)) directions.Add(0);

        // チェック (Dキー方向: Z-1)
        if (IsValidTile(mapX, mapZ - 1)) directions.Add(1);

        // チェック (Sキー方向: X-1)
        if (IsValidTile(mapX - 1, mapZ)) directions.Add(2);

        // チェック (Wキー方向: X+1)
        if (IsValidTile(mapX + 1, mapZ)) directions.Add(3);

        return directions;
    }

    private bool IsValidTile(int x, int z)
    {
        // マップ範囲外チェック
        if (x < 0 || x >= 16 || z < 0 || z >= 16)
        {
            return false;
        }

        // MapGeneratorが利用可能か確認 (シーン遷移時など)
        if (MapGenerator.map == null) return false;

        // マップデータを取得
        int tileType = MapGenerator.map[z, x];

        // 0 (道), 3 (宝箱), 4 (敵) かどうか
        return tileType == 0 || tileType == 3 || tileType == 4;
    }
}