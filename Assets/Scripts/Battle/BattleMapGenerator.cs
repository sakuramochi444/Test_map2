// BattleMapGenerator.cs

using UnityEngine;
using System.Collections.Generic;

public class BattleMapGenerator : MonoBehaviour
{
    // ▼▼▼ インスペクタから設定するプレハブを追加 ▼▼▼
    public GameObject wallPrefab;
    public GameObject wallPrefabView; // 追加
    public GameObject stairsPrefab;   // 追加
    public GameObject chestPrefab;
    public GameObject enemyPrefab;
    public GameObject playerPrefab;
    public GameObject playerPrefabView;

    // ▼▼▼ 必要な変数を追加 ▼▼▼
    // 生成したマップオブジェクトの親オブジェクト
    private GameObject mapHolder;
    // 生成した宝箱オブジェクトを管理するための辞書
    private Dictionary<Vector2Int, List<GameObject>> chestObjectLists = new Dictionary<Vector2Int, List<GameObject>>();


    void Start()
    {
        if (GameManager.instance == null)
        {
            Debug.LogError("GameManagerが見つかりません。MainSceneから開始してください。");
            return;
        }

        // GameManagerからデータを受け取り、シーンを構築
        if (GameManager.instance.mapData != null)
        {
            GenerateMapFromData(GameManager.instance.mapData);
        }
        PlaceObjects("Enemy", enemyPrefab, GameManager.instance.enemyPositions);
        PlacePlayer(GameManager.instance.playerPosition, GameManager.instance.playerRotation);
    }

    void GenerateMapFromData(int[,] map)
    {
        mapHolder = new GameObject("BattleMapHolder");
        chestObjectLists.Clear(); // 念のためリストをクリア

        // 高さ1の壁を生成
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (map[z, x] == 1)
                {
                    float posX = x - 7.5f;
                    float posZ = z - 7.5f;
                    Vector3 wallPosition = new Vector3(posX, 1f, posZ);
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
                else if (map[z, x] == 3)
                {
                    Vector2Int key = new Vector2Int(x, z);
                    if (!chestObjectLists.ContainsKey(key))
                    {
                        chestObjectLists[key] = new List<GameObject>();
                    }

                    float posX = x - 7.5f;
                    float posZ = z - 7.5f;
                    Vector3 chestPosition = new Vector3(posX, 4.5f, posZ);
                    GameObject chestInstance = Instantiate(chestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);

                    chestObjectLists[key].Add(chestInstance);
                }
                // 敵(4)の生成はPlaceObjectsメソッドに任せるので、ここでは処理しない
            }
        }

        // 高さ0の床と階段を生成
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 0f, posZ);
                Vector3 stairsPosition = new Vector3(posX, -0.55f, posZ);
                if (map[z, x] == 2)
                {
                    Instantiate(stairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                else
                {
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }

        // 高さ2の天井を生成
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 2f, posZ);
                Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
            }
        }

        // 高さ6の壁を生成
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (map[z, x] == 1)
                {
                    float posX = x - 7.5f;
                    float posZ = z - 7.5f;
                    Vector3 wallPosition = new Vector3(posX, 6f, posZ);
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
                else if (map[z, x] == 3)
                {
                    Vector2Int key = new Vector2Int(x, z);
                    if (!chestObjectLists.ContainsKey(key))
                    {
                        chestObjectLists[key] = new List<GameObject>();
                    }

                    float posX = x - 7.5f;
                    float posZ = z - 7.5f;
                    Vector3 chestPosition = new Vector3(posX, 9.5f, posZ);
                    GameObject chestInstance = Instantiate(chestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);

                    chestObjectLists[key].Add(chestInstance);
                }
            }
        }

        // 高さ5の床/天井と、見えない階段を生成
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 5f, posZ);
                Vector3 stairsPosition = new Vector3(posX, -4.55f, posZ);
                if (map[z, x] == 2)
                {
                    Instantiate(stairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                else
                {
                    Instantiate(wallPrefabView, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }
    }

    /// <summary>
    /// 指定されたプレハブを、指定された位置リストに基づいて配置する
    /// </summary>
    void PlaceObjects(string tag, GameObject prefab, List<Vector3> positions)
    {
        if (positions == null || prefab == null) return;
        var objectHolder = new GameObject(tag + "Holder");
        foreach (var pos in positions)
        {
            Instantiate(prefab, pos, Quaternion.identity, objectHolder.transform);
        }
    }

    /// <summary>
    /// プレイヤーを指定された位置と向きで配置する
    /// </summary>
    void PlacePlayer(Vector3 position, Quaternion rotation)
    {
        // ▼▼▼ ここから修正 ▼▼▼
        if (playerPrefab != null)
        {
            // Playerを指定された位置に生成
            Instantiate(playerPrefab, position, rotation);
        }

        if (playerPrefabView != null)
        {
            // Playerの位置からY軸方向に5ずらした位置を計算
            Vector3 viewPosition = position + new Vector3(0, 5f, 0);

            // PlayerViewを計算した位置に生成
            Instantiate(playerPrefabView, viewPosition, rotation);
        }
        // ▲▲▲ ここまで修正 ▲▲▲
    }
}