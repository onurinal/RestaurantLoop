using UnityEngine;

public class SimpleFPSCounter : MonoBehaviour
{
    private float _deltaTime;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        
        // Unlock to 60 FPS (or Match device refresh rate)
        Application.targetFrameRate = 60;
    }

    private void Update()
    {
        // Smooth out the delta time to avoid erratic jumps
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        float msec = _deltaTime * 1000.0f;
        float fps = 1.0f / _deltaTime;
        
        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = Screen.height * 2 / 75;
        style.normal.textColor = (fps >= 55) ? Color.green : (fps >= 30 ? Color.yellow : Color.red);

        Rect rect = new Rect(30, 30, Screen.width, Screen.height * 2 / 100);
        GUI.Label(rect, $"{fps:0.} FPS ({msec:0.0} ms)", style);
    }
}