using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExitApplication : MonoBehaviour
{
    public void ExitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
