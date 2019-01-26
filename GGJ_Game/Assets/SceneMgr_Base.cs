using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMgr_Base : MonoBehaviour
{
    public int SceneID = 1;
    public Label_TimeStep dialogUI;
    bool dialogEnd = false;


    float time = 0;
    bool canHandleKey = false;



    // Start is called before the first frame update
    protected void Start()
    {
        canHandleKey = true;
    }
    // Update is called once per frame
    protected void Update()
    {
        //键盘响应间隔
        if (!canHandleKey)
        {
            time += Time.deltaTime;
            if (time > 0.5f)
            {
                time = 0;
                canHandleKey = true;
            }
        }

        // 处理空格
        if (canHandleKey && Input.GetKey(KeyCode.Space))
        {
            canHandleKey = false;
            if (!dialogEnd)
                PlayDialog();
            else
                HandleDialogEnd();
        }
    }

    void PlayDialog()
    {
        dialogEnd = !dialogUI.HandleSpace();
        if (dialogEnd)
            HandleDialogEnd();
    }

    void HandleDialogEnd()
    {
        SwitchScene();
    }

    void SwitchScene()
    {
        SceneManager.LoadScene(SceneID + 1);
    }
}
