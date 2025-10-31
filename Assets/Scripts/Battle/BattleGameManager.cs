// BattlePlayerController.cs

using UnityEngine;

public class BattleGameManager : MonoBehaviour
{
    public GameObject roto1;
    public GameObject roto2;
    public GameObject roto3;
    public GameObject roto4;

    void Start()
    {
        roto1.SetActive(false);
        roto2.SetActive(false);
        roto3.SetActive(false);
        roto4.SetActive(false);
    }

    void Update()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W))
        {
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(true);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            roto1.SetActive(false);
            roto2.SetActive(false);
            roto3.SetActive(true);
            roto4.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            roto1.SetActive(true);
            roto2.SetActive(false);
            roto3.SetActive(false);
            roto4.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            roto1.SetActive(false);
            roto2.SetActive(true);
            roto3.SetActive(false);
            roto4.SetActive(false);
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            if (GameManager.instance != null)
            {
                GameManager.instance.ReturnToMainScene();
            }
        }
    }
}