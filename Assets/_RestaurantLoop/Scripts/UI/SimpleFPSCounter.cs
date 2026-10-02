using UnityEngine;

public class SimpleFPSCounter : MonoBehaviour
{
    private static SimpleFPSCounter Instance;

    private float deltaTime;

    private void Awake()
    {
        // Prevent duplicate counters when returning to main menu
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Disable vSync to enforce target frame rate on mobile devices
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    // private void Update()
    // {
    //     // Smooth out the delta time to avoid erratic jumps
    //     deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    // }
    //
    // private void OnGUI()
    // {
    //     float msec = deltaTime * 1000.0f;
    //     float fps = 1.0f / deltaTime;
    //
    //     GUIStyle style = new GUIStyle();
    //     style.alignment = TextAnchor.UpperLeft;
    //     style.fontSize = Screen.height * 2 / 75;
    //     style.normal.textColor = (fps >= 55) ? Color.green : (fps >= 30 ? Color.yellow : Color.red);
    //
    //     Rect rect = new Rect(30, 30, Screen.width, Screen.height * 2 / 100);
    //     GUI.Label(rect, $"{fps:0.} FPS ({msec:0.0} ms)", style);
    // }
}