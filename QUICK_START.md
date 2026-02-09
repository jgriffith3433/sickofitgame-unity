# Quick Start - Main Menu Setup

Follow these steps to get your Main Menu up and running quickly.

## 1. Create Main Menu Scene
- File → New Scene → Save as "MainMenu"

## 2. Create Canvas
- Right-click in Hierarchy → UI → Canvas

## 3. Create Menu Manager
- Create Empty GameObject → Name: "MainMenuManager"
- Add Component → MainMenuManager

## 4. Create Buttons
Create these UI buttons (Right-click Canvas → UI → Button - TextMeshPro):
- Continue Button
- New Game Button  
- Level Select Button
- Videos Button
- Quit Button

## 5. Connect Buttons
- Select MainMenuManager in Hierarchy
- Drag each button to its corresponding slot in the Inspector

## 6. Optional: Add Continue Text
- Add TextMeshProUGUI as child of Continue Button
- Drag to "Continue Button Text" slot in MainMenuManager

## 7. Add to Build Settings
- File → Build Settings
- Add Open Scenes (or drag MainMenu scene here)
- Make sure MainMenu is at index 0

## 8. Test!
- Press Play
- Buttons should work (even though other scenes don't exist yet)

---

## Next Steps

After Main Menu is working:

1. **Level Selection Scene** - Follow GAME_SETUP_GUIDE.md Step 4
2. **Video Selection Scene** - Follow GAME_SETUP_GUIDE.md Step 5  
3. **Update Your Level 1** - Add LevelManager component and call CompleteLevel() when player wins
4. **Create Video Scenes** - Follow GAME_SETUP_GUIDE.md Step 7

---

## Key Code for Your Level

In your existing Level 1 gameplay code, when player wins:

```csharp
FindObjectOfType<LevelManager>().CompleteLevel();
```

That's it! Everything else is automatic.
