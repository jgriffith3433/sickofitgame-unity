/**
 * Unity WebGL JavaScript Plugin for Window Utilities
 * Provides functions for managing the browser window/tab.
 */

mergeInto(LibraryManager.library, {

    /**
     * Attempts to close the current browser tab/window.
     * Note: Modern browsers restrict window.close() to windows opened by scripts.
     * This may not work on all browsers or contexts, but will attempt to:
     * 1. Close the window if possible
     * 2. Navigate to about:blank as a fallback
     * 3. Show a message asking the user to close the tab manually
     */
    CloseWindow: function() {
        console.log('[Unity WebGL] Attempting to close window...');
        
        // Try to close the window
        window.close();
        
        // If window.close() didn't work (which is common in modern browsers),
        // try alternative approaches
        setTimeout(function() {
            // Check if window is still open
            if (!window.closed) {
                console.log('[Unity WebGL] window.close() did not work, trying alternatives...');
                
                // Show a message to the user
                document.body.innerHTML = '<div style="display: flex; justify-content: center; align-items: center; height: 100vh; font-family: Arial, sans-serif; font-size: 24px; text-align: center; background: #1a1a1a; color: #fff;"><div><h1>Thanks for playing!</h1><p>You can now close this tab.</p></div></div>';
                
                // Try to navigate away (some browsers allow this)
                // Uncomment if you prefer this approach:
                // window.location.href = 'about:blank';
            } else {
                console.log('[Unity WebGL] Window closed successfully');
            }
        }, 100);
    }

});
