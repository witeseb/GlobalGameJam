using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private const string GameSceneName = "Cutscene";

    private void Awake()
    {
        startButton.onClick.AddListener(OnStartClicked);
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Quit();
        }
    }

    /// <summary>Loads the main game scene.</summary>
    public void OnStartClicked()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    /// <summary>Exits the application.</summary>
    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}



