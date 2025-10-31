using UnityEngine;
using System.Collections; // コルーチンを使用するために追加

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
        // ゲーム開始時にプレイヤーを初期位置へ
        SetInitialPosition(true);
        roto1.SetActive(true);
        roto2.SetActive(false);
        roto3.SetActive(false);
        roto4.SetActive(false);

        // ▼▼▼ 追加 ▼▼▼
        // 最初はすべてのtanniテキストを非表示にしておく
        tanni1.SetActive(false);
        tanni2.SetActive(false);
        tanni3.SetActive(false);
        tanni4.SetActive(false);
        // ▲▲▲ 追加 ▲▲▲
    }

    void Update()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W))
        {
            moveDirection.x = 1; // X軸 正方向
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(true);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            moveDirection.x = -1; // X軸 負方向
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(true);
            roto4.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            moveDirection.z = 1; // Z軸 正方向
            roto1.SetActive(true);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            moveDirection.z = -1; // Z軸 負方向
            roto1.SetActive(false);
            roto2.SetActive(true);
            roto3.SetActive(false);
            roto4.SetActive(false);
        }

        if (moveDirection != Vector3.zero)
        {
            TryMove(moveDirection);
        }
    }

    // TryMoveメソッドを以下のように変更

    void TryMove(Vector3 direction)
    {
        Vector3 targetPosition = transform.position + direction;

        // --- 敵との接触判定をここで行う ---
        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, 0.4f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                // 敵を発見した場合
                // 1. プレイヤーを敵のいたマスへ移動させる
                transform.position = targetPosition;

                // 2. GameManagerにフラグを立てさせ、他の敵の動きを封じる
                if (GameManager.instance != null)
                {
                    GameManager.instance.combatInitiatedThisFrame = true;
                }

                // 3. 戦闘を開始する
                GameManager.instance.PlayerCaughtByEnemy(hitCollider.gameObject);

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
            transform.position = targetPosition;
        }
        else if (targetCellType == 2) // 階段
        {
            MapGenerator.instance.ChangeMap(MapGenerator.level2);
            SetInitialPosition(false);
        }
        else if (targetCellType == 3) // 宝箱
        {
            transform.position = targetPosition;
            MapGenerator.map[mapZ, mapX] = 0;
            MapGenerator.instance.RemoveChestObjectsAt(mapX, mapZ);

            GameObject tanniToShow = null;
            if (direction.x > 0)
            {
                tanniToShow = tanni4;
            }
            else if (direction.x < 0)
            {
                tanniToShow = tanni3;
            }
            else if (direction.z > 0)
            {
                tanniToShow = tanni1;
            }
            else if (direction.z < 0)
            {
                tanniToShow = tanni2;
            }

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

    // ▼▼▼ 追加 ▼▼▼
    /// <summary>
    /// 指定されたGameObjectを一定時間表示した後に非表示にするコルーチン
    /// </summary>
    /// <param name="tanniObject">表示するテキストオブジェクト</param>
    /// <param name="duration">表示する時間（秒）</param>
    private IEnumerator ShowTanniAndHide(GameObject tanniObject, float duration)
    {
        // 念のため、すべてのテキストを一旦非表示にする
        tanni1.SetActive(false);
        tanni2.SetActive(false);
        tanni3.SetActive(false);
        tanni4.SetActive(false);

        // 対象のテキストを表示する
        tanniObject.SetActive(true);

        // 指定された秒数だけ待機する
        yield return new WaitForSeconds(duration);

        // 対象のテキストを非表示にする
        tanniObject.SetActive(false);
    }
    // ▲▲▲ 追加 ▲▲▲

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

    private void OnTriggerEnter(Collider other)
    {
        // 接触した相手のオブジェクトが "Enemy" タグを持っているか確認
        if (other.CompareTag("Enemy"))
        {
            // GameManagerにイベント発生を通知し、接触した敵オブジェクトを渡す
            GameManager.instance.PlayerCaughtByEnemy(other.gameObject);
        }
    }
}