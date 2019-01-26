using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame : MonoBehaviour
{
    //btnsMatrix[i][j] : i:colume  j:row
    public GameObject[][] btnsMatrix
        = new GameObject[5][] { new GameObject[5], new GameObject[5] , new GameObject[5] , new GameObject[5] , new GameObject[5] };
    public GameObject btnPrefab;
    public GameObject bgPlane;
    public int stepNum = 1;
    // Start is called before the first frame update
    void Start()
    {
        CreateButtons(bgPlane.transform);
    }

    // Update is called once per frame
    void Update()
    {

    }
    public float spaceSize = 200.0f;
    public Vector3 Offset = Vector3.zero;
    void CreateButtons(Transform bgPlane)
    {
        Vector3 originalPos = Offset;
        for (int i = 0; i < 5; i++)
        {
            for(int j=0;j<5; j++)
            {
                GameObject btn = GameObject.Instantiate(btnPrefab,bgPlane);
                btn.hideFlags = HideFlags.DontSave;
                Vector3 pos = originalPos + i * spaceSize * Vector3.right + j*(-1) * spaceSize * Vector3.up;
                btn.transform.localPosition = pos;
                btn.GetComponent<MiniGame_BtnHandler>().gameMgr = this;
                btn.GetComponent<MiniGame_BtnHandler>().row = j;
                btn.GetComponent<MiniGame_BtnHandler>().colume = i;
                btnsMatrix[i][j] = btn;

            }
        }
        InitRandom();
        InitState();
    }

    public List<Vector2> steps = new List<Vector2>();


    void InitRandom()
    {
        for (int i = 0; i < stepNum; i++)
        {
            int randomX = Mathf.FloorToInt(Random.Range(0, 4.99999f));
            int randomY = Mathf.FloorToInt(Random.Range(0, 4.99999f));
            steps.Add(new Vector2(randomX, randomY));
        }
    }

    void InitState()
    {
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                btnsMatrix[i][j].GetComponent<MiniGame_BtnHandler>().TurnOn();
            }
        }
        for(int i = 0; i < stepNum; i++)
        {
            OnBtnClick((int)steps[i].x, (int)steps[i].y);
        }
    }

    public void OnBtnClick(int row,int colume)
    {
        Debug.Log(row + "_" + colume + "  is click");
        btnsMatrix[colume][row].GetComponent<MiniGame_BtnHandler>().ChangeState();
        for (int i=0;i<5;i++)
        {
            btnsMatrix[i][row].GetComponent<MiniGame_BtnHandler>().ChangeState();
        }
        for(int j = 0;j<5; j++)
        {
            btnsMatrix[colume][j].GetComponent<MiniGame_BtnHandler>().ChangeState();
        }
        //Application.LoadLevel(level);
    }

    public void TurnOffLights(int row, int colume)
    {
        Debug.Log(row + "_" + colume + "  is click");
        btnsMatrix[colume][row].GetComponent<MiniGame_BtnHandler>().TurnOff();
        for (int i = 0; i < 5; i++)
        {
            btnsMatrix[i][row].GetComponent<MiniGame_BtnHandler>().TurnOff();
        }
        for (int j = 0; j < 5; j++)
        {
            btnsMatrix[colume][j].GetComponent<MiniGame_BtnHandler>().TurnOff();
        }
        //Application.LoadLevel(level);
    }

    public void OnResetBtn()
    {
        InitState();
    }

    

    public void OnTipsBtn()
    {
        InitState();
    }
}
