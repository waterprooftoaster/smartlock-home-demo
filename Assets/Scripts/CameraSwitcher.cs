using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraSwitcher : MonoBehaviour
{
    public Button mainCameraButton;
    public Button sideCameraButton;

    public Camera mainCamera;
    public Camera sideCamera;

    void Start()
    {
        // Find the cameras by name
        mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        sideCamera = GameObject.Find("Side Camera").GetComponent<Camera>();

        // Ensure the main camera is enabled and the side camera is disabled at the start
        mainCamera.enabled = true;
        sideCamera.enabled = false;

        // Assign button functions
        mainCameraButton.onClick.AddListener(SwitchToMainCamera);
        sideCameraButton.onClick.AddListener(SwitchToSideCamera);
    }

    public void SwitchToMainCamera()
    {
        mainCamera.enabled = true;
        sideCamera.enabled = false;
    }

    public void SwitchToSideCamera()
    {
        mainCamera.enabled = false;
        sideCamera.enabled = true;
    }
}

