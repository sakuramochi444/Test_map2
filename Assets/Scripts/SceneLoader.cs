using System.Collections; // コルーチンのために必要
using UnityEngine;
using UnityEngine.SceneManagement;

// このスクリプトがアタッチされたGameObjectに
// AudioSourceコンポーネントを必須にする
[RequireComponent(typeof(AudioSource))]
public class SceneLoader : MonoBehaviour
{
    private AudioSource audioSource;

    // ゲーム開始時に1回だけ呼ばれる
    void Awake()
    {
        // このGameObjectに付いているAudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();

        // Play On AwakeがONだとシーン開始時に鳴ってしまうので、
        // インスペクタでOFFにしておいてください。
    }

    // ★ボタンのOn Click()から呼び出すためのメソッド
    public void LoadSceneWithSound(string sceneName)
    {
        // 直接シーンをロードする代わりに、コルーチンを開始する
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    // 音を鳴らして待機し、シーンをロードする一連の流れ
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        // 1. AudioSourceに設定された音を再生
        audioSource.Play();

        // 2. 音が鳴り終わるまで待機
        // （クリック音が一瞬の場合、0.3秒などの固定値でもOK）
        // yield return new WaitForSeconds(0.3f); 

        // または、クリップの長さだけ正確に待つ場合
        yield return new WaitForSeconds(audioSource.clip.length);

        // 3. 待機後、シーンをロード
        SceneManager.LoadScene(sceneName);
    }
}