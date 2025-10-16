using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    // このクラスのインスタンスをどこからでも参照できるようにするための静的変数
    public static MapGenerator instance;

    // インスペクタから設定するプレハブ
    public GameObject wallPrefab;
    public GameObject wallPrefabView;
    public GameObject StairsPrefab;

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
        {1,0,0,0,1,0,0,0,0,0,0,1,0,0,0,1},
        {1,1,1,0,1,0,1,1,1,1,1,1,0,1,0,1},
        {1,0,0,0,1,0,0,0,0,1,0,0,0,1,0,1},
        {1,0,1,1,1,1,1,1,0,1,0,1,1,1,0,1},
        {1,0,0,0,0,0,0,1,0,0,0,1,0,0,0,1},
        {1,0,1,1,1,0,1,1,1,1,0,1,0,1,1,1},
        {1,0,1,0,0,0,0,0,0,1,0,1,0,1,0,1},
        {1,0,1,1,0,1,1,1,0,0,0,0,1,1,0,1},
        {1,0,0,0,0,1,0,0,0,0,2,0,0,0,0,1}, // level1のゴール(2)
        {1,1,1,1,0,1,0,1,1,0,0,0,1,1,0,1},
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
        {1,0,0,0,0,0,0,0,1,1,0,1,0,1,0,1},
        {1,0,1,1,1,1,1,1,1,1,0,1,1,1,0,1},
        {1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,0,1,1,1,1,1,1,1,1,0,1,1,1},
        {1,0,1,0,1,0,0,0,0,1,0,1,0,1,0,1},
        {1,0,0,0,1,0,1,1,0,1,0,1,0,1,0,1},
        {1,1,1,0,1,0,1,0,0,1,0,1,0,0,0,1},
        {1,0,0,0,1,0,1,0,1,1,0,1,0,2,0,1},
        {1,0,1,1,1,0,0,0,1,0,0,0,0,0,0,1},
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
}