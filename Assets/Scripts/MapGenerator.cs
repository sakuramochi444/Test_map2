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
        {1,0,0,0,1,0,0,0,0,1,0,0,0,0,0,1}, // level_newのスタート地点(1,1) -> (-6.5f, y, -6.5f)
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
        // シングルトンパターンの実装
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
        // 初期マップをlevel1に設定して生成
        map = level1;
        GenerateMap();
    }

    // マップを切り替えて再生成する公開メソッド
    public void ChangeMap(int[,] newMap)
    {
        // 既にマップが生成されている場合は、古いマップを破棄する
        if (mapHolder != null)
        {
            Destroy(mapHolder);
        }

        // 新しいマップデータをセット
        map = newMap;

        // 新しいマップを生成
        GenerateMap();
    }

    void GenerateMap()
    {
        // 生成した壁をまとめるための親オブジェクトを作成
        mapHolder = new GameObject("Map Holder");

        // (元のGenerateMapメソッドの中身は変更ありません。以下にそのまま記述します)
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