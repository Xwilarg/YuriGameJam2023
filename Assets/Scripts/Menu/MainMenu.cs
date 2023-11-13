﻿using UnityEngine;
using UnityEngine.SceneManagement;
using YuriGameJam2023.Achievement;
using YuriGameJam2023.Persistency;

namespace YuriGameJam2023.Menu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private GameObject _oldVersion, _newVersion;

        private void Awake()
        {
            SceneManager.LoadScene("VN", LoadSceneMode.Additive);
            if (PersistencyManager.Instance.SaveData.CurrentLevel == 5)
            {
                _newVersion.gameObject.SetActive(true);
            }
            else
            {
                _oldVersion.gameObject.SetActive(true);
            }
        }

        private void Start()
        {
            if (PersistencyManager.Instance.SaveData.CurrentLevel == 5)
            {
                AchievementManager.Instance.Unlock(AchievementID.CompleteGame);
            }
        }

        public void Play()
        {
            if (PersistencyManager.Instance.SaveData.CurrentLevel == 5)
            {
                PersistencyManager.Instance.DeleteSaveFolder();
            }
            SceneManager.LoadScene("Main");
        }
    }
}
