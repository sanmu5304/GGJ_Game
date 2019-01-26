using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Label_TimeStep : MonoBehaviour
{

    public string[] dialogs;
    private int nextDialogID = 1;

    private string msg = "123456789";
    public Text label;

    public float allTime = 5.0f;

    float time;
    public int i = 1;
    public bool isPlaying = false;
    // Start is called before the first frame update
    void Start()
    {
        isPlaying = true;
        time = 0;
        msg = dialogs[0];
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlaying)
        {
            if (time > allTime / msg.Length)
            {
                time = 0;
                i++;
                if (i >= msg.Length)
                {
                    i = msg.Length;
                    isPlaying = false;
                }
            }
            time += Time.deltaTime;
            label.text = msg.Substring(0, i);
        }
    }

    public bool HandleSpace()
    {
        Debug.Log("ss");
        if (isPlaying)
        {
            i = msg.Length;
        }
        else
        {
            isPlaying = true;
            i = 0;

            if (nextDialogID >= dialogs.Length)
            {
                nextDialogID = dialogs.Length;
                isPlaying = false;
                return false;
            }
            msg = dialogs[nextDialogID];
            nextDialogID++;
        }
        return true;
    }
}
