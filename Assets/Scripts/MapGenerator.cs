using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    // このクラスのインスタンスをどこからでも参照できるようにするための静的変数
    public static MapGenerator instance;

    // インスペクタから設定するプレハブ
    public GameObject wallPrefab;
    public GameObject wallPrefabView;
    public GameObject StairsPrefab;
    public GameObject ChestPrefab;
    public GameObject EnemyPrefab;

    // 生成した宝箱オブジェクトを座標と紐づけて管理するための辞書
    // 1つの座標に複数のオブジェクト（例：高さの違う宝箱）がある可能性を考慮し、Listで管理します
    private Dictionary<Vector2Int, List<GameObject>> chestObjectLists = new Dictionary<Vector2Int, List<GameObject>>();

    // 現在のマップデータを保持する静的配列
    public static int[,] map = new int[16, 16];

    // レベルごとのマップデータ
    public static int[,] level1 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,1,0,0,0,0,0,0,0,1},
        {1,0,1,1,1,1,0,1,0,1,1,1,1,1,0,1},
        {1,0,1,0,0,0,0,0,0,1,0,0,0,0,0,1},
        {1,0,1,0,1,1,1,1,1,1,0,1,1,1,0,1},
        {1,0,0,0,1,4,0,0,0,0,3,1,0,0,4,1},
        {1,1,1,0,1,4,1,1,1,1,1,1,0,1,0,1},
        {1,0,0,0,1,0,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,1,1,1,0,1,0,1,1,1,0,1},
        {1,0,0,0,0,0,0,1,0,0,0,1,0,0,0,1},
        {1,0,1,1,1,0,1,1,1,1,0,1,0,1,1,1},
        {1,0,1,0,0,0,0,0,0,1,0,1,0,1,0,1},
        {1,0,1,1,0,1,1,1,0,0,0,0,1,1,0,1},
        {1,4,0,0,0,1,0,0,0,0,2,0,0,0,0,1}, // level1のゴール(2)
        {1,1,1,1,0,1,3,1,1,0,0,0,1,1,3,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
    };

    public static int[,] level2 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,1,0,0,0,0,1,0,0,0,0,0,1},
        {1,0,1,0,1,0,1,1,0,1,0,1,1,1,0,1},
        {1,0,1,1,1,0,1,0,0,1,0,0,0,1,0,1},
        {1,0,0,0,0,0,1,0,1,1,1,1,0,1,0,1},
        {1,1,1,1,1,1,1,0,0,0,0,1,0,1,0,1},
        {1,0,0,0,0,0,3,0,1,1,0,1,3,1,0,1},
        {1,0,1,1,1,1,1,1,1,1,0,1,1,1,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,1,1,1,1,1,1,1,1,0,1,1,1},
        {1,0,1,0,1,0,0,0,0,1,0,1,0,1,0,1},
        {1,0,0,0,1,0,1,1,0,1,0,1,0,1,0,1},
        {1,1,1,0,1,0,1,0,0,1,0,1,0,0,0,1},
        {1,0,0,0,1,0,1,0,1,1,0,1,0,2,0,1},
        {1,3,1,1,1,0,0,0,1,0,0,0,0,0,0,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
    };

    // 生成したマップオブジェクトの親オブジェクト
    private GameObject mapHolder;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        map = level1;
        GenerateMap();
    }

    public void ChangeMap(int[,] newMap)
    {
        if (mapHolder != null)
        {
            Destroy(mapHolder);
        }
        // 新しいマップを生成する前に、宝箱の管理リストをクリアする
        chestObjectLists.Clear();
        map = newMap;
        GenerateMap();
    }

    void GenerateMap()
    {
        mapHolder = new GameObject("Map Holder");

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
                    GameObject chestInstance = Instantiate(ChestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);

                    // 生成した宝箱オブジェクトを辞書に追加
                    chestObjectLists[key].Add(chestInstance);
                }
                else if (map[z, x] == 4)
                {
                    float posX = x - 7.5f;
                    float posZ = z - 7.5f;
                    Vector3 enemyPosition = new Vector3(posX, 0.5f, posZ);
                    Instantiate(EnemyPrefab, enemyPosition, Quaternion.identity, mapHolder.transform);
                }
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
                    Instantiate(StairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                else
                {
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }

        // ↓↓↓↓ ここを修正 ↓↓↓↓
        // 高さ2の天井を生成（壁の上のみ）
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
        // ↑↑↑↑ ここまで修正 ↑↑↑↑

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
                    GameObject chestInstance = Instantiate(ChestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);

                    // こちらの宝箱も辞書に追加
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
                Vector3 stairsPosition = new Vector3(posX, -4.55f, posZ); // y座標は画面外なので影響しない
                if (map[z, x] == 2)
                {
                    Instantiate(StairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                else
                {
                    Instantiate(wallPrefabView, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }
    }

    public void RemoveChestObjectsAt(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);
        if (chestObjectLists.ContainsKey(key))
        {
            // その座標にある宝箱オブジェクトをすべて破壊する
            foreach (GameObject chest in chestObjectLists[key])
            {
                if (chest != null)
                {
                    Destroy(chest);
                }
            }
            // 辞書からその座標の情報を削除する
            chestObjectLists.Remove(key);
        }
    }
}