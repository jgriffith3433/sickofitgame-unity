# Unity C# Conventions

## References & Caching

### Rule: Serialized references over GetComponent
- **Prefer `[SerializeField]` fields** wired in the inspector or by generators
- **`GetComponent` is allowed ONCE** in `Awake()` as a fallback if serialized ref is null
- **NEVER** call `GetComponent` in `Update`, `FixedUpdate`, `OnCollisionEnter`, or any per-frame/per-event path
- **`TryGetComponent`** over `GetComponent` + null check when the component is optional

### Rule: Cache everything in Awake/Start
```csharp
// Good — cached once
[SerializeField] private Rigidbody2D rb;
private void Awake()
{
    if (rb == null) rb = GetComponent<Rigidbody2D>();
}

// Bad — every frame
private void Update()
{
    GetComponent<Rigidbody2D>().velocity = ...;
}
```

### Rule: No FindObjectOfType in gameplay code
- `FindAnyObjectByType` / `FindObjectsByType` are expensive scene scans
- Use them ONLY in `Start()` for one-time setup, never in Update loops
- Prefer: inject references via `SetPlayer()`, `RegisterEnemy()`, etc.
- Singleton access (`GameClock.Instance`) is fine for true singletons

## Lifecycle

### Initialization Order
1. `Awake()` — Cache components, set up internal state. No cross-object communication.
2. `OnEnable()` — Subscribe to events, re-resolve references after reactivation.
3. `Start()` — Cross-object setup (find singletons, register with managers). Safe to access other objects.
4. `OnDisable()` — Unsubscribe from events.
5. `OnDestroy()` — Final cleanup.

### Reactivation (SetActive)
- When `SetActive(true)` is called, `OnEnable()` fires but NOT `Awake()` or `Start()`
- Any state that needs resetting on reactivation goes in `OnEnable()`
- This matters for the rewind system — enemies reactivate, they need valid state

### Scene Loading
- `[RuntimeInitializeOnLoadMethod]` runs ONCE before any scene loads — use for global setup
- `SceneManager.sceneLoaded` fires per scene — use for per-scene bootstrap
- Don't assume `Camera.main` exists in `Awake()` — it might not be initialized yet

## Rewind System Rules
- **Never Destroy enemies** — use `gameObject.SetActive(false)` so rewind can bring them back
- **Rewindable state lives in components** (`RewindableTransform`, `RewindableHealth`, ...) that self-register with `RewindTimeline.Instance` in `Awake`; see ARCHITECTURE.md "Rewind System"
- **Freeze during rewind**: check `GameClock.Instance?.IsRewinding` and return early from AI/input
- **`onDeath` events**: guard with IsRewinding check — don't spawn drops during rewind restore

## Naming & Style
- PascalCase for public members, camelCase for private
- `[SerializeField]` for inspector-exposed private fields
- `[Header("Section")]` to group inspector fields
- `[Tooltip("...")]` for non-obvious fields
- `[RequireComponent]` when a MonoBehaviour depends on sibling components
- Null-conditional (`?.`) for optional singletons: `AnalyticsManager.Instance?.Track()`

## Assembly References
- `SickOfIt.Combat` → `SickOfIt.Core`, `SickOfIt.Data`
- `SickOfIt.Core` → `SickOfIt.Data`
- `SickOfIt.Tools.Editor` → everything (editor-only)
- Cross-assembly access: use fully-qualified names or add a using
- Don't add assembly references just for one call — use fully-qualified instead
