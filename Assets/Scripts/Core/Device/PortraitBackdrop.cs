using UnityEngine;

namespace SickOfIt.Core.Device
{
    /// <summary>
    /// On a portrait phone a 16:9 film leaves bands above and below it. This fills them with the film itself: the frame
    /// on screen, enlarged to cover the whole view and dimmed, behind the real one, so the phone shows the scene's own
    /// colours instead of black bars. It mirrors its source renderer every frame (the sprite and the sheet texture the
    /// frame player swaps in through its property block), so rewinds, skips and loops need nothing extra. It draws
    /// only while the camera shows more height than the film has, so landscape is untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitBackdrop : MonoBehaviour
    {
        /// <summary>How bright the enlarged copy is next to the film (0 black, 1 as bright).</summary>
        public const float Brightness = 0.35f;

        private SpriteRenderer source;
        private SpriteRenderer mirror;
        private Camera view;
        private MaterialPropertyBlock block;

        /// <summary>Puts a backdrop behind <paramref name="source"/>, sized to <paramref name="camera"/>. Safe to call twice.</summary>
        public static PortraitBackdrop Attach(SpriteRenderer source, Camera camera)
        {
            if (source == null || camera == null)
                return null;
            var parent = source.transform.parent;
            string name = source.name + "_PortraitBackdrop";
            var existing = parent != null ? parent.Find(name) : null;
            var go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null && parent != null)
                go.transform.SetParent(parent, false);
            var backdrop = go.GetComponent<PortraitBackdrop>();
            if (backdrop == null)
                backdrop = go.AddComponent<PortraitBackdrop>();
            backdrop.source = source;
            backdrop.view = camera;
            backdrop.mirror = go.GetComponent<SpriteRenderer>();
            if (backdrop.mirror == null)
                backdrop.mirror = go.AddComponent<SpriteRenderer>();
            backdrop.mirror.enabled = false;
            return backdrop;
        }

        /// <summary>True while it fills the bands (the playtest reads it).</summary>
        public bool Showing => mirror != null && mirror.enabled;

        private void LateUpdate()
        {
            if (mirror == null)
                return;
            var sprite = source != null && source.enabled && source.gameObject.activeInHierarchy ? source.sprite : null;
            if (sprite == null || view == null || !view.orthographic)
            {
                mirror.enabled = false;
                return;
            }

            float viewHeight = view.orthographicSize * 2f;
            float viewWidth = viewHeight * view.aspect;
            var bounds = source.bounds;
            if (viewHeight <= bounds.size.y + 0.01f)
            {
                mirror.enabled = false;
                return;
            }

            mirror.sprite = sprite;
            mirror.sharedMaterial = source.sharedMaterial;
            mirror.flipX = source.flipX;
            mirror.flipY = source.flipY;
            mirror.sortingLayerID = source.sortingLayerID;
            mirror.sortingOrder = source.sortingOrder - 1;
            var tint = source.color;
            mirror.color = new Color(tint.r * Brightness, tint.g * Brightness, tint.b * Brightness, tint.a);
            block ??= new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            mirror.SetPropertyBlock(block);

            var size = sprite.bounds.size;
            float scale = Mathf.Max(viewHeight / Mathf.Max(size.y, 0.001f), viewWidth / Mathf.Max(size.x, 0.001f)) * 1.02f;
            var t = transform;
            if (t.parent != null)
            {
                var parentScale = t.parent.lossyScale;
                t.localScale = new Vector3(scale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f), scale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f), 1f);
            }
            else
            {
                t.localScale = new Vector3(scale, scale, 1f);
            }
            var camPos = view.transform.position;
            t.position = new Vector3(camPos.x, camPos.y, source.transform.position.z + 0.01f);
            t.rotation = Quaternion.identity;
            mirror.enabled = true;
        }
    }
}
