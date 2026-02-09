# Game Setup Guide

This guide will walk you through setting up your complete game with main menu, levels, video system, and progression tracking.

> **WebGL Optimization:** For fast loading WebGL builds, see [WEBGL_OPTIMIZATION.md](WEBGL_OPTIMIZATION.md) for Addressables setup. This reduces your initial download from 50-200 MB to just 1-10 MB!

## Overview

Your game now has these core scripts:
- **GameProgressManager**: Handles all PlayerPrefs, level completion, and video unlocks
- **SceneTransitionManager**: Manages all scene loading
- **MainMenuManager**: Controls the main menu
- **LevelManager**: Add to each level scene to handle win conditions
- **LevelSelectionManager**: Creates dynamic level selection UI
- **VideoPlayerManager**: Handles video playback
- **VideoSelectionManager**: Creates video gallery UI
- **LevelButton & VideoButton**: UI components for selections
- **PauseMenu**: Optional pause functionality for levels

---

## Setup Instructions

### STEP 1: Create Your Scenes

Create these scenes in Unity (File → New Scene):

1. **MainMenu** - Your main menu
2. **LevelSelection** - Level selection screen
3. **VideoSelection** - Video gallery screen
4. **Level1** - Your first level (you already have this!)
5. **Level2, Level3, etc.** - Additional levels as needed
6. **Video0** - Intro video scene
7. **Video1, Video2, etc.** - One video scene per level

**Important**: Save all scenes with these EXACT names (case-sensitive).

### STEP 2: Add Scenes to Build Settings

1. Open **File → Build Settings**
2. Click **Add Open Scenes** or drag all scenes into the list
3. Make sure **MainMenu** is at index 0 (drag to reorder if needed)
4. Order suggestion:
   - MainMenu (index 0)
   - LevelSelection
   - VideoSelection
   - Level1, Level2, Level3...
   - Video0, Video1, Video2...

---

## STEP 3: Setup Main Menu Scene

1. Open your **MainMenu** scene
2. Create a Canvas (if you don't have one)
3. Create an empty GameObject, name it "MainMenuManager"
4. Add the **MainMenuManager** component to it
5. Create UI Buttons:
   - Continue Button
   - New Game Button
   - Level Select Button
   - Videos Button
   - Quit Button (optional)
6. Assign these buttons to the MainMenuManager component in the Inspector
7. (Optional) Add a TextMeshProUGUI to the Continue button and assign it to "Continue Button Text" field

Your Main Menu is now complete!

---

## STEP 4: Setup Level Selection Scene

1. Open your **LevelSelection** scene
2. Create a Canvas (if you don't have one)
3. Create an empty GameObject, name it "LevelSelectionManager"
4. Add the **LevelSelectionManager** component to it
5. **Set Total Levels** in the Inspector (e.g., 10)

### Create Level Button Prefab:
6. Create a new GameObject in the Hierarchy
7. Add a **Button** component
8. Add a **LevelButton** component
9. Create child UI elements:
   - **TextMeshProUGUI** for level number → assign to "Level Text"
   - **GameObject/Image** for completed indicator (star, checkmark) → assign to "Completed Indicator"
   - **GameObject/Image** for locked indicator (lock icon) → assign to "Locked Indicator"
10. Make this a prefab by dragging it to your Prefabs folder
11. Delete the instance from the scene

### Finish Level Selection Setup:
12. Create a **Scroll View** or **Grid Layout** in your Canvas to hold level buttons
13. Assign the content area to the LevelSelectionManager's "Level Button Container"
14. Assign your Level Button prefab to "Level Button Prefab"
15. Create a Back button → assign to "Back Button"

Done! The level buttons will be generated automatically.

---

## STEP 5: Setup Video Selection Scene

1. Open your **VideoSelection** scene
2. Create a Canvas (if you don't have one)
3. Create an empty GameObject, name it "VideoSelectionManager"
4. Add the **VideoSelectionManager** component to it
5. **Set Total Videos** in the Inspector (usually matches your level count)

### Create Video Button Prefab:
6. Create a new GameObject in the Hierarchy
7. Add a **Button** component
8. Add a **VideoButton** component
9. Create child UI elements:
   - **TextMeshProUGUI** for video name → assign to "Video Name Text"
   - **GameObject/Image** for locked indicator → assign to "Locked Indicator"
   - (Optional) **Image** for thumbnail → assign to "Thumbnail Image"
10. Make this a prefab
11. Delete the instance from the scene

### Finish Video Selection Setup:
12. Create a **Scroll View** or **Grid Layout** for video buttons
13. Assign the content area to "Video Button Container"
14. Assign your Video Button prefab to "Video Button Prefab"
15. Create a Back button → assign to "Back Button"

Done!

---

## STEP 6: Setup Your Level Scenes

For each level scene (Level1, Level2, etc.):

1. Open the level scene
2. Create an empty GameObject, name it "LevelManager"
3. Add the **LevelManager** component
4. **Set Level Number** in the Inspector (1 for Level1, 2 for Level2, etc.)
5. Configure video settings:
   - **Play Video On First Visit**: Check this if you want videos before levels
   - **Video To Play**: Usually matches level number (0 for intro, 1 for level 1, etc.)

### Call the Win Function:
In your existing gameplay code, when the player completes the level, call:
```csharp
FindObjectOfType<LevelManager>().CompleteLevel();
```

That's it! The LevelManager handles everything else.

### Optional: Add Pause Menu:
1. Create a UI Panel for the pause menu (set inactive by default)
2. Add buttons: Resume, Restart, Main Menu
3. Create an empty GameObject, add **PauseMenu** component
4. Assign the panel and buttons in the Inspector

---

## STEP 7: Setup Video Scenes

For each video scene (Video0, Video1, etc.):

1. Open the video scene
2. Create a **Video Player** GameObject
3. Add the **VideoPlayerManager** component
4. **Set Video Number** in the Inspector (0 for intro, 1-X for level videos)
5. Assign your video clip to the Video Player component
6. Configure:
   - **Return To Level After Video**: Check this for level intro videos
   - **Render Mode**: Set to Camera or RenderTexture as needed
7. (Optional) Create a Skip button:
   - Create a Button UI
   - Assign to "Skip Button" in VideoPlayerManager
   - Create a GameObject to show/hide skip → assign to "Skip Button Object"

The video will auto-play and return to the appropriate scene when finished.

---

## STEP 8: Update Scene Names (if different)

If your scene names don't match the defaults, update them in **SceneTransitionManager.cs**:

```csharp
public const string MAIN_MENU_SCENE = "YourMainMenuSceneName";
public const string LEVEL_SELECTION_SCENE = "YourLevelSelectionSceneName";
public const string VIDEO_SELECTION_SCENE = "YourVideoSelectionSceneName";
```

And update the methods if you use different naming:
- `LoadLevel()` expects: "Level1", "Level2", etc.
- `LoadVideo()` expects: "Video0", "Video1", etc.

---

## How It All Works

### First Time Playing:
1. Player starts at Main Menu
2. "New Game" starts Level 1
3. Video 0 (intro) auto-unlocked and can be watched from gallery
4. When Level 1 is completed:
   - Level 1 marked as complete
   - Video 1 unlocked
   - Level 2 unlocked
   - Continue button now points to Level 2

### Progression System:
- **PlayerPrefs** stores:
  - Which levels are completed
  - Which videos are unlocked
  - Highest level reached
  - First-time visits to levels
- Everything persists between sessions
- Continue button always knows the current level

### Video System:
- Video 0 (intro) is always unlocked
- Videos 1-X unlock when completing the corresponding level
- Videos can be rewatched from the Video Gallery
- First-time level visits can auto-play the video
- ESC or Space skips videos

---

## Testing Your Game

### Test Fresh Player Experience:
To reset all progress (test as new player):
```csharp
GameProgressManager.Instance.ResetAllProgress();
```
You can call this from a debug button or Unity console.

### Test Specific States:
```csharp
// Unlock specific level
GameProgressManager.Instance.SetLevelCompleted(1);

// Unlock specific video
GameProgressManager.Instance.UnlockVideo(2);

// Set highest level
GameProgressManager.Instance.SetHighestLevelReached(5);
```

---

## Customization Tips

### Styling:
- Customize LevelButton and VideoButton colors in their Inspectors
- Add animations to buttons
- Add particle effects to level completion
- Customize UI layouts as desired

### Features to Add:
- Save current level progress (checkpoints within levels)
- Add level star ratings (3-star system)
- Add sound effects for UI interactions
- Add transitions/fade effects between scenes
- Add level timers or scores

### Extending the System:
- Add difficulty settings (save to PlayerPrefs)
- Add achievements
- Add collectibles that persist across levels
- Add character unlocks based on progression

---

## Troubleshooting

**Buttons don't work:**
- Make sure you have an EventSystem in your scene
- Check that buttons are assigned in the Inspector

**Videos don't play:**
- Verify video files are imported correctly
- Check VideoPlayer source is set
- Make sure scene names match exactly

**Progress not saving:**
- PlayerPrefs are saved automatically
- Check that level numbers are set correctly in LevelManager
- Use debug logs to verify completion calls

**Scenes not loading:**
- Verify all scenes are in Build Settings
- Check scene names match exactly (case-sensitive)
- Look for errors in the Console

---

## Quick Reference

### Key Methods to Call From Your Code:

**When player wins a level:**
```csharp
FindObjectOfType<LevelManager>().CompleteLevel();
```

**Manual scene transitions (if needed):**
```csharp
SceneTransitionManager.Instance.LoadMainMenu();
SceneTransitionManager.Instance.LoadLevel(levelNumber);
SceneTransitionManager.Instance.LoadVideo(videoNumber);
```

**Check progress:**
```csharp
bool completed = GameProgressManager.Instance.IsLevelCompleted(levelNum);
bool unlocked = GameProgressManager.Instance.IsVideoUnlocked(videoNum);
int currentLevel = GameProgressManager.Instance.GetCurrentLevel();
```

---

## You're Done! 🎮

Your game now has:
✅ Main Menu with Continue functionality
✅ Level Selection with locked/unlocked states
✅ Video Gallery with progression-based unlocks
✅ Automatic level progression and saving
✅ Win condition system
✅ Video playback before levels
✅ Pause menu (optional)

Start by setting up the Main Menu scene, then work through each scene type. The system is designed to be flexible and easy to extend!
