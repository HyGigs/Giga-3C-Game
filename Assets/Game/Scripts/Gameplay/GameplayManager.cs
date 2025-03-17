using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private InputManager _input;

    private void Start()
    {
        _input.OnMainMenuInput += BackToMainMenu;
    }

    private void BackToMainMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        _input.OnMainMenuInput -= BackToMainMenu;
    }
}
