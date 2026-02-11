# iOS WebGL Debugging Guide (Without Mac)

## Quick Summary
Your game gets stuck on "Loading" when clicking "New Game" on mobile. I've added comprehensive logging to help debug this. Here's how to view the logs on iOS without a Mac.

---

## ✅ Solution 1: Use the Built-in Mobile Debug Console (EASIEST)

I've created **MobileDebugConsole.cs** that displays all Unity logs directly in your game!

### How to Use:
1. Build your WebGL game with the new scripts
2. Open the game on your iOS device
3. **Triple-tap anywhere on the screen** to show/hide the console
4. All Unity logs will appear at the bottom half of the screen
5. You can scroll through the logs with your finger

### What to Look For:
Look for logs tagged with `[MOBILE DEBUG]` that show:
- When "New Game" button is clicked
- Scene loading progress
- Any errors or timeouts
- Network connectivity status

---

## ✅ Solution 2: Safari Web Inspector on iOS (NO MAC NEEDED)

iOS Safari has a built-in developer console!

### Steps:
1. **On your iOS device:**
   - Open **Settings** > **Safari** > **Advanced**
   - Enable **"Web Inspector"**

2. **Connect to your game:**
   - Open your WebGL game in Safari on iOS
   - While the game is running, tap the URL bar
   - Scroll down and tap the **"<>"** (Developer Tools) icon
   - Tap **"Console"** to see all debug logs

### Limitations:
- Only works in Safari browser (not Chrome/Firefox on iOS)
- Console may be small on iPhone screens
- Must keep Safari tab active

---

## ✅ Solution 3: Remote JavaScript Console (eruda)

Add a remote console to your WebGL build.

### Steps:
1. Create this file in your Unity project: `Assets/WebGLTemplates/Default/TemplateData/eruda.html`

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/eruda"></script>
    <script>eruda.init();</script>
</head>
<body>
    <!-- Your WebGL game loads here -->
    <script>
        // This adds a floating console button on mobile
        console.log('Eruda console activated!');
    </script>
</body>
</html>
```

2. Or add this to your existing WebGL template's index.html:
```html
<script src="https://cdn.jsdelivr.net/npm/eruda"></script>
<script>eruda.init();</script>
```

3. Rebuild your WebGL game
4. On iOS, you'll see a floating green button in the corner
5. Tap it to see the console

---

## ✅ Solution 4: Remote Debugging Service (inspect.dev)

For professional remote debugging without a Mac:

### Steps:
1. Go to https://inspect.dev (or similar services like RemoteJS)
2. Sign up for a free account
3. Add their JavaScript snippet to your WebGL template
4. View logs from any browser in real-time

---

## 🔍 What The Logs Will Show

With the changes I made, you should see logs like:

```
[MOBILE DEBUG] New Game button clicked!
[MOBILE DEBUG] Attempting to load Video1...
[MOBILE DEBUG] SceneLoadingManager present: True
[MOBILE DEBUG] SceneLoadingManager.LoadVideo(1) called
[MOBILE DEBUG] Activating 2 loading UI objects
[MOBILE DEBUG] Calling SceneTransitionManager.LoadVideo(1)
[MOBILE DEBUG] Scene name: Video1, UseAddressables: True
[MOBILE DEBUG] Calling AddressablesSceneLoader.LoadScene(Video1)
[MOBILE DEBUG] Starting scene load: 'Video1'...
[MOBILE DEBUG] Platform: WebGLPlayer, Internet: ReachableViaLocalAreaNetwork
[MOBILE DEBUG] Scene load progress: 10.0%
[MOBILE DEBUG] Scene load progress: 25.5%
...
```

---

## 🐛 Common Issues and What Logs Will Reveal

### Issue 1: Timeout During Loading
**Log shows:**
```
[MOBILE DEBUG] Scene load timeout after 60s! Scene: Video1
```
**Fix:** Your Addressables bundle is too large or slow to download. Check your hosting and bundle sizes.

---

### Issue 2: No Internet Connection
**Log shows:**
```
[MOBILE DEBUG] Internet: NotReachable
```
**Fix:** iOS may be blocking network access. Check Settings > [Your Browser] > Allow network access.

---

### Issue 3: Missing Scene in Addressables
**Log shows:**
```
[MOBILE DEBUG] Failed to load scene 'Video1'
[MOBILE DEBUG] Exception: InvalidKeyException
```
**Fix:** Video1 scene is not properly added to Addressables. Check your Addressables groups.

---

### Issue 4: Callback Never Called
**Log shows progress but never completes:**
```
[MOBILE DEBUG] Scene load progress: 99.0%
(then nothing)
```
**Fix:** Scene activation issue. Check if you have any scene loading barriers or initialization scripts that are blocking.

---

## 📱 Testing Checklist

Run your game on iOS and check:

1. ✅ Triple-tap works to show MobileDebugConsole
2. ✅ You see "[Mobile Debug Console Ready]" message
3. ✅ Click "New Game" button
4. ✅ Look for "[MOBILE DEBUG] New Game button clicked!"
5. ✅ Watch for progress percentages
6. ✅ Check for any red error messages
7. ✅ Take screenshots of the console to review later

---

## 🔧 Quick Fixes to Try

If you see the loading screen but nothing happens:

### Fix 1: Check Addressables Settings
- Open **Window > Asset Management > Addressables > Groups**
- Make sure **Video1** scene is in an Addressable group
- Check that **Build & Load Paths** are set to "Remote" for WebGL
- Rebuild Addressables: **Build > New Build > Default Build Script**

### Fix 2: Enable Verbose Logging
In Unity Editor:
1. Select the **AddressablesOperationManager** GameObject (or any GameObject)
2. Make sure **Show Debug Logs** is enabled
3. Rebuild your WebGL build

### Fix 3: Test Without Addressables
In Unity Editor:
1. Find **SceneTransitionManager** in your scene or create it
2. Uncheck **"Use Addressables"**
3. Add Video1 scene to **Build Settings > Scenes In Build**
4. Rebuild and test

---

## 📊 Understanding the Loading Flow

When you click "New Game", this happens:

1. **MainMenuManager.OnNewGameClicked()** - Resets progress, prepares Video1
2. **SceneLoadingManager.LoadVideo(1)** - Shows loading UI
3. **SceneTransitionManager.LoadVideo(1)** - Routes to scene loader
4. **AddressablesSceneLoader.LoadScene("Video1")** - Starts async load
5. **AddressablesOperationManager.LoadSceneAsync()** - Downloads + activates scene
6. Scene should appear

If it gets stuck, the logs will show exactly where it stopped!

---

## 🆘 Still Stuck?

If you're still seeing the loading indicator forever:

1. **Check the MobileDebugConsole** - Triple-tap to view logs
2. **Look for the last log message** - This tells you where it froze
3. **Check for timeout messages** - 60s for scenes, 90s for labels
4. **Verify internet connection** - Should show "ReachableViaLocalAreaNetwork"
5. **Test on WiFi vs Cellular** - Some networks block WebGL downloads

Share the console logs and I can help diagnose the exact issue!

---

## 💡 Pro Tips

1. **Enable MobileDebugConsole early** - Add it to your first scene so it's always available
2. **Test locally first** - Use `python -m http.server` to test WebGL builds locally
3. **Check bundle sizes** - Large Addressables bundles can timeout on slow connections
4. **Use browser cache** - After first load, subsequent loads should be faster
5. **Monitor progress** - The percentage logs show if download is progressing or stuck

---

## 📝 Summary

**Best Option for You:** Use the **MobileDebugConsole** I created. Just triple-tap the screen to see all logs directly in your game. No additional setup needed!

The logs will tell you exactly why it's getting stuck in the loading state.
