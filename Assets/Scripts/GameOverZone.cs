﻿using UnityEngine;

namespace YuriGameJam2023
{
    public class GameOverZone : MonoBehaviour
    {
        public void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Enemy"))
            {
                other.GetComponent<EnemyController>().EndTurn();
                CharacterManager.Instance.GameOver("An enemy reached the exit");
            }
        }
    }
}