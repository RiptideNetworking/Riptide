using UnityEngine;
using UnityEngine.UI;

namespace Riptide.Demos.PlayerHosted
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager _singleton;
        public static UIManager Singleton
        {
            get => _singleton;
            private set
            {
                if (_singleton == null)
                    _singleton = value;
                else if (_singleton != value)
                {
                    Debug.Log($"{nameof(UIManager)} instance already exists, destroying object!");
                    Destroy(value);
                }
            }
        }

        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject gameMenu;
        [SerializeField] private InputField usernameField;
        [SerializeField] private InputField hostIPField;

        internal string Username => usernameField.text;

        private void Awake()
        {
            Singleton = this;
        }

        public void HostClicked()
        {
            mainMenu.SetActive(false);
            gameMenu.SetActive(true);

            NetworkManager.Singleton.StartHost();
        }

        public void JoinClicked()
        {
            if (string.IsNullOrEmpty(hostIPField.text))
            {
                Debug.Log("Enter an IP!");
                return;
            }

            NetworkManager.Singleton.JoinGame(hostIPField.text);
            mainMenu.SetActive(false);
            gameMenu.SetActive(true);
        }

        public void LeaveClicked()
        {
            NetworkManager.Singleton.LeaveGame();
            BackToMain();
        }

        internal void BackToMain()
        {
            mainMenu.SetActive(true);
            gameMenu.SetActive(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        internal void UpdateUIVisibility()
        {
            if (Cursor.lockState == CursorLockMode.None)
                gameMenu.SetActive(true);
            else
                gameMenu.SetActive(false);
        }
    }
}
