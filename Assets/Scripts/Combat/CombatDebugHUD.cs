using UnityEngine;
using TMPro;

/// <summary>
/// Shows combat stats during gameplay for debugging
/// Add to Canvas in your scene
/// </summary>
public class CombatDebugHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    
    [Header("Auto Create UI")]
    [SerializeField] private bool autoCreateUI = true;
    
    private HealthSystem playerHealth;
    private GameObject player;
    
    private void Start()
    {
        // Find player (including inactive)
        var allObjects = FindObjectsOfType<GameObject>(true);
        player = System.Array.Find(allObjects, obj => obj.CompareTag("Player"));
        if (player != null)
        {
            playerHealth = player.GetComponent<HealthSystem>();
        }
        
        // Create UI if needed
        if (autoCreateUI && debugText == null)
        {
            CreateDebugUI();
        }
        
        if (debugText != null)
        {
            debugText.gameObject.SetActive(showDebugInfo);
        }
    }
    
    private void Update()
    {
        // Toggle visibility
        if (Input.GetKeyDown(toggleKey))
        {
            showDebugInfo = !showDebugInfo;
            if (debugText != null)
            {
                debugText.gameObject.SetActive(showDebugInfo);
            }
        }
        
        // Update stats
        if (showDebugInfo && debugText != null)
        {
            UpdateDebugText();
        }
    }
    
    private void UpdateDebugText()
    {
        int activeSpawners = EnemySpawner.GetActiveSpawnerCount();
        
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int aliveEnemies = 0;
        foreach (var enemy in enemies)
        {
            HealthSystem health = enemy.GetComponent<HealthSystem>();
            if (health != null && health.IsAlive)
            {
                aliveEnemies++;
            }
        }
        
        string playerHealthText = "???";
        if (playerHealth != null)
        {
            playerHealthText = $"{playerHealth.CurrentHealth}/{playerHealth.MaxHealth}";
            if (playerHealth.IsInvincible)
            {
                playerHealthText += " (INVINCIBLE)";
            }
        }
        
        debugText.text = $"=== COMBAT DEBUG ===\n" +
                        $"Player HP: {playerHealthText}\n" +
                        $"Spawners: {activeSpawners}\n" +
                        $"Enemies: {aliveEnemies}\n" +
                        $"\nPress {toggleKey} to hide";
    }
    
    private void CreateDebugUI()
    {
        // Create text object
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(transform);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-10, -10);
        rect.sizeDelta = new Vector2(300, 150);
        
        debugText = textObj.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 18;
        debugText.color = Color.white;
        debugText.alignment = TextAlignmentOptions.TopRight;
        debugText.text = "Combat Debug";
        
        // Add outline for readability
        var outline = textObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);
    }
}
