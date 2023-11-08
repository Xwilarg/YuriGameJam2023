using Cinemachine;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YuriGameJam2023.Effect;
using YuriGameJam2023.Persistency;
using YuriGameJam2023.Player;

namespace YuriGameJam2023
{
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [Header("Players")]
        [SerializeField]
        private SO.CharacterInfo[] _players;

        [SerializeField]
        private GameObject _playerPrefab;

        [Header("UI")]
        [SerializeField]
        [Tooltip("Text used to display the distance the character can still walk")]
        private TMP_Text _distanceText;

        [SerializeField]
        [Tooltip("Text used to display the amount of actions left")]
        private TMP_Text _actionCountText;

        [SerializeField]
        [Tooltip("Tooltip that confirm if the player want to end his action")]
        private GameObject _disablePopup;

        [SerializeField]
        [Tooltip("Tooltip that confirm if the player want to end his turn")]
        private GameObject _endTurnPopup;

        [SerializeField]
        private Transform _skillBar;

        [SerializeField]
        private GameObject _skillPrefab;

        [Header("Camera & Effects")]
        [SerializeField]
        [Tooltip("Cinemachine virtual camera")]
        private CinemachineVirtualCamera _vCamCloseup;

        [SerializeField]
        private Transform _cameraDefaultLookAtWorld;

        [SerializeField]
        private GameObject _worldView, _closeupView;

        [SerializeField]
        [Tooltip("Object used to display spell Area Of Effect hints")]
        private AoeHint _aoeHint;

        [SerializeField]
        private SpellDesc _spellDesc;

        [SerializeField]
        private GameObject _gameOver;

        [SerializeField]
        private TMP_Text _gameOverReasonText;

        private Vector3 _camMov;

        private Character _currentPlayer;

        private int _totalActionCount;
        private bool _isPlayerTurn;

        private readonly List<Character> _characters = new();

        private bool IsUIActive => _disablePopup.activeInHierarchy || _endTurnPopup.activeInHierarchy || _gameOver.activeInHierarchy;

        private readonly Dictionary<Tuple<Character, Character>, int> _love = new();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var spawns = GameObject.FindGameObjectsWithTag("PlayerSpawn");
            Assert.GreaterOrEqual(spawns.Length, _players.Length, "Not enough spawn points for the whole team");
            for (int i = 0; i < _players.Length; i++)
            {
                var go = Instantiate(_playerPrefab, spawns[i].transform.position + Vector3.up, Quaternion.identity);
                go.GetComponent<Character>().Info = _players[i];
            }
        }

        private void Update()
        {
            if (_isPlayerTurn && !IsUIActive)
            {
                var move3d = _camMov * Time.deltaTime * 10f;
                _cameraDefaultLookAtWorld.Translate(move3d);
            }
        }

        public void GameOver(string reason)
        {
            if (!_isPlayerTurn)
            {
                EndTurn();
            }
            _gameOver.SetActive(true);
            _gameOverReasonText.text = reason;
        }

        public void ReloadGame()
        {
            SceneManager.LoadScene("Main");
        }

        private bool _gameStarted;
        public void RegisterCharacter(Character c)
        {
            _characters.Add(c);

            if (!_gameStarted && _characters.Count(x => x is PlayerController) == _players.Length)
            {
                _gameStarted = true;
                EndTurn();
            }
        }
        public void UnregisterCharacter(Character c)
        {
            _characters.Remove(c);

            if (!_characters.Any(x => x is PlayerController))
            {
                GameOver("All your characters collapsed");
            }
            else if (!_characters.Any(x => x is EnemyController))
            {
                UnlockSupport();
                StartCoroutine(WaitAndLoadCampfire());
            }
        }

        private IEnumerator WaitAndLoadCampfire()
        {
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("Campfire");
        }

        private void UnlockSupport()
        {
            var couple = _love.OrderByDescending(x => x.Value).FirstOrDefault();

            // Not much love during this game
            if (couple.Key == default)
            {
                return;
            }

            var name1 = couple.Key.Item1.Info.Name;
            var name2 = couple.Key.Item2.Info.Name;

            string key = name1.CompareTo(name2) < 0 ? $"{name1}{name2}" : $"{name2}{name1}";
            PersistencyManager.Instance.SaveData.UnlockSupport(key);
            PersistencyManager.Instance.Save();
        }

        public bool AmIActive(Character c)
            => _currentPlayer != null && _currentPlayer.gameObject.GetInstanceID() == c.gameObject.GetInstanceID();

        /// <summary>
        /// Deselect a player (aka we are not controlling it anymore)
        /// </summary>
        public void UnsetPlayer()
        {
            _currentPlayer = null;
            _worldView.SetActive(true);
            _closeupView.SetActive(false);
        }

        /// <summary>
        /// Show the distance the player can still walk during this turn
        /// </summary>
        public void DisplayDistanceText(float value)
        {
            _distanceText.text = $"Distance: {value:N1}";
        }

        /// <summary>
        /// Remove an action, when a player is out of actions, his turn ends
        /// </summary>
        public void RemoveAction()
        {
            for (int i = 0; i < _skillBar.childCount; i++) Destroy(_skillBar.GetChild(i).gameObject);
            _spellDesc.Hide();

            _totalActionCount--;
            Debug.Log($"[=/=] Ending turn, action left: {_totalActionCount}");
            _actionCountText.text = $"Actions Left: {_totalActionCount}";
            if (_totalActionCount == 0)
            {
                EndTurn();
            }
            else if (!_isPlayerTurn)
            {
                EnemyManager.Instance.DoAction();
            }
        }

        public void EndTurn()
        {
            if (_isPlayerTurn && _gameOver.activeInHierarchy)
            {
                return; // Make sure player turn can't end when we lost
            }

            var currCharacters = _characters.Where(x => _isPlayerTurn ? x is PlayerController : x is EnemyController);
            for (int i = currCharacters.Count() - 1; i >= 0; i--)
            {
                currCharacters.ElementAt(i).EndTurn();
            }
            foreach (var c in currCharacters)
            {
                c.CanBePlayed = true;
            }

            _isPlayerTurn = !_isPlayerTurn;

            currCharacters = _characters.Where(x => _isPlayerTurn ? x is PlayerController : x is EnemyController);
            _totalActionCount = currCharacters.Count();

            _actionCountText.text = $"Actions Left: {_totalActionCount}";

            Debug.Log($"===== Starting {(_isPlayerTurn ? "Player" : "Enemy")} turn =====");

            if (!_isPlayerTurn)
            {
                EnemyManager.Instance.StartTurn();
            }
        }

        public void StartTurn(Character c)
        {
            _spellDesc.Show();
            Debug.Log($"[{c}] Starting turn");
            for (int i = 0; i < c.Info.Skills.Length; i++)
            {
                var skill = c.Info.Skills[i];
                var go = Instantiate(_skillPrefab, _skillBar);
                go.transform.GetChild(0).GetComponent<Image>().sprite = skill.Sprite;
                go.GetComponentInChildren<TMP_Text>().text = $"{i + 1}";

                if (i == 0) // Display selected hint on first skill
                {
                    var hint = go.transform.GetComponent<Image>();
                    hint.color = new(hint.color.r, hint.color.g, hint.color.b, 1f);
                    _spellDesc.SetSpell(c.Info.Skills[i]);
                }
            }

            _currentPlayer = c;
            _currentPlayer.Enable();

            _worldView.SetActive(false);
            _closeupView.SetActive(true);
            _vCamCloseup.LookAt = _currentPlayer.transform;
            _vCamCloseup.Follow = _currentPlayer.transform;
        }

        /// <summary>
        /// Disable the display of all effects
        /// </summary>
        public void ResetEffectDisplay()
        {
            _aoeHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// Show a hint on the floor that represent where an attack will land
        /// </summary>
        public void ShowAoeHint(Vector3 pos, int radius)
        {
            _aoeHint.gameObject.SetActive(true);
            _aoeHint.Show(pos, radius);
        }

        public Character GetClosestCharacter<T>(Transform transform)
            where T : Character
        {
            return _characters
                .Where(x => x is T)
                .OrderBy(x => Vector3.Distance(transform.position, x.transform.position))
                .ElementAt(0);
        }

        public IEnumerable<T> GetCharacters<T>()
        {
            return _characters
                .Where(x => x is T)
                .Cast<T>();
        }

        public void DisableConfirm()
        {
            _currentPlayer.Disable();
        }

        public void DisableDecline() {
            _spellDesc.Show();
        }

        public void IncreaseLove(Character a, Character b, int love)
        {
            var couple = _love
                .FirstOrDefault(x => (x.Key.Item1 == a || x.Key.Item1 == b) &&
                    (x.Key.Item2 == a || x.Key.Item2 == b));

            if (couple.Key == default)
            {
                _love.Add(new Tuple<Character, Character>(a, b), love);
                return;
            }

            _love[couple.Key] += love;
        }

        public void OnClick()
        {
            if (_isPlayerTurn && !IsUIActive)
            {
                if (_currentPlayer == null) // We aren't controlling a player...
                {
                    // ... so if we click on one we take possession of it
                    if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit) && hit.collider.CompareTag("Player"))
                    {
                        var c = hit.collider.GetComponent<PlayerController>();
                        if (c.CanBePlayed)
                        {
                            StartTurn(c);
                        }
                    }
                }
                else
                {
                    if (((PlayerController)_currentPlayer).CanAttack)
                    {
                        _currentPlayer.Attack();
                    }
                }
            }
        }

        public void OnClickCancel()
        {
            if (_isPlayerTurn)
            {
                if (_currentPlayer != null && _isPlayerTurn)
                {
                    ((PlayerController)_currentPlayer).Mov = Vector2.zero;
                }

                if (_gameOver.activeInHierarchy)
                {
                    return;
                }

                // Close popups
                if (_endTurnPopup.activeInHierarchy)
                {
                    _endTurnPopup.SetActive(false);
                }
                else if (_disablePopup.activeInHierarchy)
                {
                    _disablePopup.SetActive(false);
                    _spellDesc.Show();
                }
                // Open popup to end turn
                else if (_currentPlayer == null)
                {
                    _endTurnPopup.SetActive(true);
                }
                // If we aren't already disabling the player, open popup to end the current action
                else if (_currentPlayer != null && !_currentPlayer.PendingAutoDisable)
                {
                    _disablePopup.SetActive(true);
                    _spellDesc.Hide();
                }
            }
        }

        public void OnMovement(Vector2 mov)
        {
            _camMov = new Vector3(mov.x, 0f, mov.y);
            if (_isPlayerTurn && !IsUIActive && _currentPlayer != null)
            {
                ((PlayerController)_currentPlayer).Mov = mov;
            }
        }

        public void OnSkillSelected(int id)
        {
            id--;
            if (_currentPlayer != null && id < _currentPlayer.Info.Skills.Length)
            {
                var hint = _skillBar.GetChild(_currentPlayer.CurrentSkill).transform.GetComponent<Image>();
                hint.color = new(hint.color.r, hint.color.g, hint.color.b, 0f);
                _currentPlayer.CurrentSkill = id;
                hint = _skillBar.GetChild(_currentPlayer.CurrentSkill).transform.GetComponent<Image>();
                hint.color = new(hint.color.r, hint.color.g, hint.color.b, 1f);
                _spellDesc.SetSpell(_currentPlayer.Info.Skills[_currentPlayer.CurrentSkill]);
            }
        }
    }
}
