using UnityEngine;

public class LevelWinTrigger : MonoBehaviour
{
    [SerializeField] private LevelManager _levelManager = null;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _levelManager.CompleteLevel();
        }
    }
}