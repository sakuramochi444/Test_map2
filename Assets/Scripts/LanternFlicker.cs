using UnityEngine;

public class LanternFlicker : MonoBehaviour
{
    // publicにするとインスペクタから調整できる
    public float minIntensity = 0.8f; // 明るさの最小値
    public float maxIntensity = 1.2f; // 明るさの最大値
    public float flickerSpeed = 0.5f; // 揺らぐ速さ

    private Light lanternLight;
    private float randomOffset;

    void Start()
    {
        // 同じオブジェクトについているLightコンポーネントを取得
        lanternLight = GetComponent<Light>();
        // 揺らぎのパターンにランダム性を加える
        randomOffset = Random.Range(0f, 65535f);
    }

    void Update()
    {
        // PerlinNoiseを使うと、カクカクしない滑らかな揺らぎを表現できる
        float noise = Mathf.PerlinNoise(randomOffset, Time.time * flickerSpeed);

        // 明るさを最小値と最大値の間で滑らかに変化させる
        lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}