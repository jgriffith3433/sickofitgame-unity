# Addressables Server Setup Guide

Complete guide for hosting Addressables on your own domain.

---

## Step 1: Build Addressables with Remote Path

### Method A: Configure in Unity (Recommended)

1. Open **Window → Asset Management → Addressables → Groups**
2. Select your group (e.g., "Default Local Group")
3. In Inspector, configure paths:
   ```
   Build Path: RemoteBuildPath
   Load Path: https://sickofitgame.com/webgl-assets/[BuildTarget]
   ```
   - `[BuildTarget]` auto-expands to: WebGL, StandaloneWindows64, Android, etc.

4. Click **Build → New Build → Default Build Script**
5. Wait for build to complete

### Method B: Use Runtime Override Script

Add `AddressablesRemotePathOverride.cs` to your scene and set the URL in Inspector.

---

## Step 2: Upload Files to Your Server

### Files to Upload

After building Addressables, find:
```
YourProject/ServerData/WebGL/     ← All these files go to your server
```

Upload to your web server at: `https://sickofitgame.com/webgl-assets/WebGL/`

### Directory Structure on Server

```
sickofitgame.com/
├── index.html                    ← Your WebGL game
├── Build/                        ← WebGL build files
│   ├── game.loader.js
│   ├── game.framework.js.br
│   ├── game.data.br
│   └── game.wasm.br
└── webgl-assets/                 ← Addressables HERE
    └── WebGL/
        ├── catalog.json
        ├── catalog.hash
        ├── defaultlocalgroup_assets_all.bundle
        ├── defaultlocalgroup_assets_all.bundle.manifest
        ├── defaultlocalgroup_scenes_all.bundle
        └── ... (more bundles)
```

---

## Step 3: Configure Web Server

### Apache (.htaccess)

Create/edit `.htaccess` in `/webgl-assets/` folder:

```apache
# Enable CORS (required for WebGL to load from same domain)
<IfModule mod_headers.c>
    Header set Access-Control-Allow-Origin "*"
    Header set Access-Control-Allow-Methods "GET, OPTIONS"
    Header set Access-Control-Allow-Headers "Content-Type"
</IfModule>

# MIME types for Addressables
AddType application/octet-stream .bundle
AddType application/json .json
AddType text/plain .hash
AddType application/json .manifest

# Enable compression
<IfModule mod_deflate.c>
    AddOutputFilterByType DEFLATE application/json
    AddOutputFilterByType DEFLATE text/plain
</IfModule>

# Cache control (adjust as needed)
<IfModule mod_expires.c>
    ExpiresActive On
    ExpiresByType application/octet-stream "access plus 1 year"
    ExpiresByType application/json "access plus 1 day"
    ExpiresByType text/plain "access plus 1 day"
</IfModule>
```

### Nginx

Add to your nginx config:

```nginx
location /webgl-assets/ {
    # CORS headers
    add_header Access-Control-Allow-Origin *;
    add_header Access-Control-Allow-Methods "GET, OPTIONS";
    add_header Access-Control-Allow-Headers "Content-Type";
    
    # MIME types
    types {
        application/octet-stream bundle;
        application/json json;
        text/plain hash;
        application/json manifest;
    }
    
    # Compression
    gzip on;
    gzip_types application/json text/plain;
    
    # Cache control
    expires 1y;
    add_header Cache-Control "public, immutable";
}
```

### Node.js / Express

```javascript
const express = require('express');
const app = express();

// CORS
app.use('/webgl-assets', (req, res, next) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Methods', 'GET, OPTIONS');
    next();
});

// Serve Addressables
app.use('/webgl-assets', express.static('webgl-assets', {
    setHeaders: (res, path) => {
        if (path.endsWith('.bundle')) {
            res.setHeader('Content-Type', 'application/octet-stream');
        }
    }
}));
```

---

## Step 4: Test Your Setup

### Test URLs Manually

Try accessing in browser:
```
https://sickofitgame.com/webgl-assets/WebGL/catalog.json
https://sickofitgame.com/webgl-assets/WebGL/catalog.hash
```

You should see JSON content (catalog.json) and a hash string (catalog.hash).

### Test in WebGL Build

1. Build your WebGL game
2. Upload to server
3. Open in browser
4. Check browser Console (F12) for:
   - Download messages
   - No 404 errors
   - Addressables loading successfully

---

## URLs Unity Will Request

Based on your Load Path, Unity requests:

```
Base: https://sickofitgame.com/webgl-assets/WebGL/

Unity requests:
├── catalog.json                      (Asset catalog)
├── catalog.hash                      (Version check)
├── defaultlocalgroup_assets_all.bundle    (Assets bundle)
├── defaultlocalgroup_scenes_all.bundle    (Scenes bundle)
└── [group_name]_[content]_all.bundle     (Other bundles)
```

The exact bundle names depend on your Addressables Groups configuration.

---

## File Types & MIME Types Reference

| File Type | Extension | MIME Type | Description |
|-----------|-----------|-----------|-------------|
| Asset Bundle | `.bundle` | `application/octet-stream` | Binary asset data |
| Catalog | `.json` | `application/json` | Asset locations |
| Hash | `.hash` | `text/plain` | Version hash |
| Manifest | `.bundle.manifest` | `application/json` | Bundle metadata |

---

## Common Issues & Solutions

### Issue: 404 Not Found

**Cause:** Files not uploaded or wrong path  
**Fix:** 
- Verify files exist at exact URL
- Check case sensitivity (Linux servers)
- Verify `[BuildTarget]` expanded correctly (should be "WebGL")

### Issue: CORS Error

**Cause:** Missing CORS headers  
**Fix:** Add CORS headers in server config (see Step 3)

### Issue: Wrong MIME Type

**Cause:** Server doesn't recognize `.bundle` files  
**Fix:** Configure MIME types in server config

### Issue: Files Too Large / Slow Downloads

**Solutions:**
- Enable gzip/brotli compression on server
- Use a CDN (Cloudflare, AWS CloudFront)
- Split into smaller Addressables groups

---

## Advanced: Using a CDN

For better performance worldwide:

### Option 1: Cloudflare (Free)

1. Add sickofitgame.com to Cloudflare
2. Upload Addressables to `/webgl-assets/`
3. Cloudflare automatically caches and optimizes
4. No code changes needed!

### Option 2: AWS S3 + CloudFront

1. Upload `ServerData/WebGL/` to S3 bucket
2. Create CloudFront distribution
3. Point to S3 bucket
4. Update Load Path in Unity to CloudFront URL:
   ```
   https://d1234abcd.cloudfront.net/WebGL/
   ```

### Option 3: Google Cloud Storage + CDN

Similar to AWS setup but with GCS.

---

## Version Management

When you update your game:

1. **Update assets** → Rebuild Addressables
2. **New bundles generated** → `catalog.hash` changes
3. **Upload new files** → Old versions auto-invalid
4. **Players** → Auto-download updated bundles

The `.hash` file tells Unity when to re-download.

---

## Deployment Checklist

Before going live:

- [ ] Built Addressables with remote path configured
- [ ] Uploaded all files from `ServerData/WebGL/` to server
- [ ] Configured MIME types on server
- [ ] Configured CORS headers
- [ ] Tested URLs manually in browser
- [ ] Built WebGL game
- [ ] Uploaded WebGL build
- [ ] Tested in actual browser
- [ ] Checked browser console for errors
- [ ] Verified assets load correctly
- [ ] Tested on slow connection (throttle in DevTools)

---

## Quick Start Summary

```bash
# 1. Configure remote path in Unity
Build Path: RemoteBuildPath
Load Path: https://sickofitgame.com/webgl-assets/[BuildTarget]

# 2. Build Addressables
Window → Addressables → Groups → Build

# 3. Upload files
Upload: ServerData/WebGL/* → https://sickofitgame.com/webgl-assets/WebGL/

# 4. Configure server (CORS + MIME types)
See Step 3 above

# 5. Build & upload WebGL game
File → Build Settings → WebGL → Build
Upload to https://sickofitgame.com/

# 6. Test!
Open game in browser and check console
```

---

You're serving Addressables! 🚀
