﻿using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YuriGameJam2023.Persistency;
using YuriGameJam2023.VN;

namespace YuriGameJam2023.Campfire
{
    public class CampfireManager : MonoBehaviour
    {
        [SerializeField]
        private Couple[] _couples;

        [SerializeField]
        private CharacterCamp[] _characters;

        /// <summary>
        /// Character we clicked on with the mouse, considered as 'selected'
        /// </summary>
        private CharacterCamp _current;
        /// <summary>
        /// Character we are currently hovering with our mouse
        /// </summary>
        private CharacterCamp _hovered;

        private void Awake()
        {
            SceneManager.LoadScene("VN", LoadSceneMode.Additive);

            /// DEBUG !!!
            PersistencyManager.Instance.SaveData.UnlockSupport(GetSupportKey(_couples[0].A, _couples[0].B));
        }

        private void Update()
        {
            if (VNManager.Instance.IsPlayingStory)
            {
                return;
            }

            // Beginning of the loop, removing light if it's not the one of the selected character
            if (_hovered != null && _hovered != _current)
            {
                _hovered.ToggleLight(false);
                // We only remove interaction hints if we didn't select a character
                if (_current == null)
                {
                    foreach (var c in _characters)
                    {
                        c.ToggleInteraction(false);
                    }
                }
            }

            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit) && hit.collider.CompareTag("Player"))
            {
                _hovered = hit.collider.GetComponent<CharacterCamp>();
                _hovered.ToggleLight(true);

                // If a character is already selected we don't do anything
                if (_current == null)
                {
                    UpdateSupportDisplay(_hovered);
                }
            }
            else
            {
                _hovered = null;
            }
        }

        /// <summary>
        /// For each other character we check if we can display an interaction
        /// </summary>
        private void UpdateSupportDisplay(CharacterCamp target)
        {
            foreach (var couple in _couples)
            {
                // Current iteration doesn't contain target
                if (couple.A != target && couple.B != target)
                {
                    couple.A.ToggleInteraction(false);
                    couple.B.ToggleInteraction(false);
                    continue;
                }

                var key = GetSupportKey(couple.A, couple.B);
                var level = PersistencyManager.Instance.SaveData.GetCurrentSupportLevel(key);

                if (PersistencyManager.Instance.SaveData.CanPlaySupport(key, level))
                {
                    if (couple.A == target)
                    {
                        couple.A.ToggleInteraction(false);
                        couple.B.ToggleInteraction(true);
                    }
                    else
                    {
                        couple.A.ToggleInteraction(true);
                        couple.B.ToggleInteraction(false);
                    }
                }
                else
                {
                    couple.A.ToggleInteraction(false);
                    couple.B.ToggleInteraction(false);
                }
            }
        }

        public void OnClick(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                if (VNManager.Instance.IsPlayingStory)
                {
                    VNManager.Instance.DisplayNextDialogue();
                }
                else
                {
                    if (_hovered != null) // We clicked on a character
                    {
                        // Already clicked on another character
                        if (_current != null)
                        {
                            var key = GetSupportKey(_hovered, _current);
                            var level = PersistencyManager.Instance.SaveData.GetCurrentSupportLevel(key);
                            if (PersistencyManager.Instance.SaveData.CanPlaySupport(key, level))
                            {
                                // Hide all characters not concerned by story
                                foreach (var c in _characters)
                                {
                                    c.ToggleLight(false);
                                    c.ToggleInteraction(false);
                                    if (c != _current && c != _hovered)
                                    {
                                        c.gameObject.SetActive(false);
                                    }
                                }

                                VNManager.Instance.ShowStory(_couples.First(x => (x.A == _current && x.B == _hovered) || (x.A == _hovered && x.B == _current)).Stories[level], () =>
                                {
                                    foreach (var c in _characters)
                                    {
                                        c.gameObject.SetActive(true);
                                    }

                                    _current = null;

                                    PersistencyManager.Instance.SaveData.PlaySupport(key);
                                    PersistencyManager.Instance.Save();
                                });
                            }
                            else
                            {
                                _current.ToggleLight(false);
                                _current = _hovered;
                                UpdateSupportDisplay(_current);
                            }
                        }
                        else
                        {
                            _current = _hovered;
                        }
                    }
                    else
                    {
                        // We clicked on nothing so we remove the light of a possibly selected character
                        if (_current != null)
                        {
                            _current.ToggleLight(false);
                        }
                        _current = null;
                    }

                    _hovered = null;
                }
            }
        }

        public string GetSupportKey(CharacterCamp a, CharacterCamp b)
        {
            var name1 = a.name;
            var name2 = b.name;

            if (name1.CompareTo(name2) < 0) return $"{name1}{name2}";
            return $"{name2}{name1}";
        }
    }
}
