using UnityEngine;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The hero's pooled rockets (not Entity shots): a warm amber body tint and a dark outline so they never read as
    /// enemy orbs. Presentation only; hitboxes unchanged.
    /// </summary>
    public sealed class HeroShotLook : MonoBehaviour
    {
        static readonly Color Warm = new Color(1f, 0.72f, 0.28f, 1f);
        static readonly Color Ink = new Color(0.12f, 0.04f, 0.02f, 0.95f);

        SpriteRenderer body;
        SpriteRenderer rim;
        bool scaled;

        public static void Attach(GameObject shot)
        {
            if (shot == null)
                return;
            if (!shot.TryGetComponent<HeroShotLook>(out var look))
                look = shot.AddComponent<HeroShotLook>();
            look.Ensure();
        }

        void Ensure()
        {
            if (body == null)
                body = GetComponentInChildren<SpriteRenderer>();
            if (body == null)
                return;
            var art = EffectAtlas.Rocket;
            if (art != null && body.sprite != art)
            {
                // Gen's amber rocket: its own colours, scaled to the old body's length so the hitbox look matches.
                if (!scaled)
                {
                    scaled = true;
                    float old = body.sprite != null ? body.sprite.bounds.size.x : 0f;
                    if (old > 0.01f)
                        body.transform.localScale *= old / art.bounds.size.x;
                }
                body.sprite = art;
            }
            body.color = art != null ? new Color(1f, 1f, 1f, body.color.a) : new Color(Warm.r, Warm.g, Warm.b, body.color.a);
            if (rim == null)
            {
                var go = new GameObject("HeroShotRim");
                go.transform.SetParent(transform, false);
                rim = go.AddComponent<SpriteRenderer>();
            }
            rim.sprite = body.sprite;
            rim.flipX = body.flipX;
            rim.flipY = body.flipY;
            rim.sortingLayerID = body.sortingLayerID;
            rim.sortingOrder = body.sortingOrder - 1;
            rim.color = Ink;
            rim.transform.localPosition = body.transform == transform ? Vector3.zero : body.transform.localPosition;
            rim.transform.localRotation = body.transform == transform ? Quaternion.identity : body.transform.localRotation;
            rim.transform.localScale = (body.transform == transform ? Vector3.one : body.transform.localScale) * 1.4f;
            rim.enabled = true;
        }

        void LateUpdate()
        {
            if (!isActiveAndEnabled)
                return;
            Ensure();
        }
    }
}
