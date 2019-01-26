using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame_BtnHandler : MonoBehaviour
{
    public Button btn;
    public Image img;
    public bool isLighting = true;
    public MiniGame gameMgr;
    public int row = -1;
    public int colume = -1;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HandleClicked()
    {
        gameMgr.OnBtnClick(row, colume);
    }

    public void ChangeState()
    {
        isLighting = !isLighting;
        if (isLighting)
            img.color = Color.white;
        else
            img.color = Color.black;
    }

    public void TurnOff()
    {
        isLighting = false;
        img.color = Color.black;
    }

    public void TurnOn()
    {
        isLighting = true;
        img.color = Color.white;
    }
}
