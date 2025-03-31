using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private InputManager _input;
    [SerializeField] private string _sceneName;

    private void Start()
    {
        _input.OnMainMenuInput += BackToMainMenu;
    }

    private void BackToMainMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(_sceneName);
    }

    private void OnDestroy()
    {
        _input.OnMainMenuInput -= BackToMainMenu;
    }
}
