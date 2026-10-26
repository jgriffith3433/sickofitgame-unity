using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core.Input;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>content/weapons/{id}.json</summary>
    [Serializable]
    [Doc("A weapon the hero can hold (content/weapons/{id}.json). He holds several and switches with Q / the mouse wheel / the gamepad's top button, " +
         "or the weapon button on the HUD (touch). 'rocketLauncher' is his gun from the start and is always in hand; others come with the giveWeapon " +
         "action and leave with takeWeapon. A weapon is either a projectile gun (fires the hero's rocket, or a shot entity of its own) or a melee swing " +
         "(a hit box in front of him, with a combo). The aim guide follows the weapon. A weapon with a 'pickup' is found on a floor, and finding it " +
         "(the unlockWeapon action) unlocks it for good: he has it again after dying and in every later run.")]
    public class WeaponDef
    {
        [NonSerialized] public string id;

        [Required, Doc("Shown on the HUD (the HUD writes it in capitals).")] public string displayName = "";
        [Doc("One line for the cookbook and the editor.")] public string description = "";
        [Doc("HUD button and swing color.")] public Color color = new Color(1f, 0.55f, 0.2f, 1f);
        [Doc("Its icon on the HUD's weapon chip (its pickup's art).")] public Sprite icon;
        [Doc("Fires a projectile, or swings a melee hit box.")] public WeaponKind kind = WeaponKind.Projectile;
        [Doc("Seconds between shots (a projectile weapon; attack speed upgrades shorten it). A melee weapon uses its combo steps' times.")]
        public float cooldown = 0.3f;
        [Doc("Seconds before the weapon can be used after switching to it.")] public float switchDelay = 0.15f;
        [Doc("The aim guide's line: shown or not, and how long compared with the launcher's.")] public WeaponGuideDef aimGuide = new WeaponGuideDef();
        [Doc("Ammo: capacity 0 = none (never runs dry).")] public WeaponAmmoDef ammo = new WeaponAmmoDef();
        [Doc("Used when kind is Projectile. The shot is the hero's rocket.")] public WeaponProjectileDef projectile = new WeaponProjectileDef();
        [Doc("Used when kind is Melee.")] public WeaponMeleeDef melee = new WeaponMeleeDef();
        public WeaponSoundsDef sounds = new WeaponSoundsDef();
        [EntityRef, Doc("The pickup entity that carries this weapon (a 'collect' ability whose 'do' is an unlockWeapon action). Empty = never found on a floor.")]
        public string pickup = "";
        [Doc("Banner shown the first time he finds it (capitals; empty = 'NEW WEAPON: <name>').")] public string banner = "";
        [Doc("The hero's subtitle for that first find (empty = none).")] public string heroText = "";
        [SoundRef, Doc("The hero's voice line for that first find (audio.json).")] public string heroLine = "";
        [Doc("How he holds it: the player.json states he plays with it in his hands (its grip's own animation set, art/hero.json). Every weapon has real sheets of its own; nothing is drawn over them.")]
        public WeaponHandsDef hands = new WeaponHandsDef();
    }

    [Serializable]
    [Doc("A weapon's grip: which player.json state the hero plays for each move while he holds it. A name left empty is 'prefix' + the move " +
         "(ShotgunIdle, ShotgunRun, ShotgunJump, ShotgunFall, ShotgunLand, ShotgunAttack (a melee combo: ShotgunAttack1, 2, 3 ...), ShotgunRunAttack, " +
         "ShotgunAirAttack) and 'equip' is 'Equip' + prefix. 'animator': the hero's Animator plays idle, run, jump, fall and the standing attack itself " +
         "(the rocket launcher, his own gun): only land, the running and air attacks and the equip clip are played over it.")]
    public class WeaponHandsDef
    {
        [Doc("The grip's state prefix (Shotgun, Whip, ...). Empty with every name empty = the weapon has no grip of its own.")] public string prefix = "";
        public string idle = "";
        [Doc("Walking or running on the ground.")] public string run = "";
        [Doc("Rising in the air.")] public string jump = "";
        [Doc("Falling.")] public string fall = "";
        [Doc("A short clip as he lands standing.")] public string land = "";
        [Doc("Standing attack: one state for a gun (looped while he fires), one per combo step for a melee weapon.")] public List<string> attack = new List<string>();
        [Doc("Attacking while running on the ground.")] public string runAttack = "";
        [Doc("Attacking in the air.")] public string airAttack = "";
        [Doc("Played when he takes it out (switching to it).")] public string equip = "";
        [Doc("The hero's Animator draws idle, run, jump, fall and the standing attack (the launcher).")] public bool animator;

        [NonSerialized] HeroHands built;

        public HeroHands Built(WeaponDef weapon)
        {
            if (built != null)
                return built;
            if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(idle))
                return null;
            string Name(string own, string move) => !string.IsNullOrEmpty(own) ? own : prefix + move;
            var steps = new List<string>(attack);
            int combo = weapon != null && weapon.kind == WeaponKind.Melee ? Mathf.Max(1, weapon.melee.steps.Count) : 1;
            if (steps.Count == 0)
            {
                if (combo == 1)
                    steps.Add(prefix + "Attack");
                else
                    for (int i = 1; i <= combo; i++)
                        steps.Add(prefix + "Attack" + i);
            }
            built = new HeroHands
            {
                idle = Name(idle, "Idle"), run = Name(run, "Run"), jump = Name(jump, "Jump"), fall = Name(fall, "Fall"), land = Name(land, "Land"),
                attack = steps.ToArray(), runAttack = Name(runAttack, "RunAttack"), airAttack = Name(airAttack, "AirAttack"),
                equip = !string.IsNullOrEmpty(equip) ? equip : "Equip" + prefix, animator = animator,
            };
            return built;
        }
    }

    public enum WeaponKind
    {
        [Doc("Fires the hero's rocket along the aim.")] Projectile,
        [Doc("A swing: a hit box in front of the hero, a combo of steps, no ammo needed.")] Melee,
    }

    [Serializable]
    [Doc("How a weapon uses the aim guide.")]
    public class WeaponGuideDef
    {
        public bool show = true;
        [Doc("Length as a multiple of player.json aimGuide.length (a melee weapon's line is its reach, so make it short).")]
        public float length = 1f;
    }

    [Serializable]
    [Doc("A weapon's ammo. Rewinding gives spent ammo back.")]
    public class WeaponAmmoDef
    {
        [Doc("Most ammo he can hold. 0 = the weapon uses none.")] public int capacity;
        [Doc("Ammo he has when he is given the weapon (0 = full).")] public int start;
        [Doc("Ammo spent by each shot.")] public int perShot = 1;
        [Doc("Ammo the weapon makes for itself each second (0 = none).")] public float regenPerSecond;
    }

    [Serializable]
    [Doc("A projectile weapon's shot (the hero's rocket prefab; only these numbers differ). The launcher's are player.json's.")]
    public class WeaponProjectileDef
    {
        public float speed = 10f;
        public float lifetime = 5f;
        [Doc("Turn the rocket sprite to its flight direction.")] public bool faceAim = true;
        [Doc("Degrees the rocket sprite points away from 'right'.")] public float spriteAngle = 225f;
        [Doc("Multiplies each shot's damage.")] public float damageScale = 1f;
        [Doc("Extra shots from the spread Grudge apply to this weapon.")] public bool multiShot = true;
        [EntityRef, Doc("Fire this entity (a player-team shot, e.g. one with a 'projectile' ability that bounces or a 'returnToHero' ability) instead of the hero's rocket. Empty = the rocket.")]
        public string entity = "";
        [Doc("Shots per pull of the trigger, fanned evenly 'pelletSpread' degrees apart (a shotgun's blast). The spread Grudge adds to them.")]
        public int pellets = 1;
        [Doc("Degrees between neighbouring pellets.")] public float pelletSpread = 8f;
    }

    [Serializable]
    [Doc("A melee weapon: each swing is the next step of the combo.")]
    public class WeaponMeleeDef
    {
        [Doc("Seconds after a swing ends in which the next swing continues the combo. Later (or after the last step) it starts over.")]
        public float comboWindow = 0.45f;
        [Doc("The combo, in order. At least one step.")] public List<MeleeStepDef> steps = new List<MeleeStepDef>();
        [Doc("Height of the hit box (world units); its length is the step's reach.")] public float height = 1.1f;
        [Doc("The swing's swoosh (a white crescent facing right, 1 unit from its centre to its outer edge; tinted with the weapon's color). Empty = the drawn crescent.")]
        public UnityEngine.Sprite swingArt;
    }

    [Serializable]
    [Doc("One swing of a combo.")]
    public class MeleeStepDef
    {
        public string name = "jab";
        public int damage = 1;
        [Doc("How far the hit box reaches from the hero (world units).")] public float reach = 1.1f;
        [Doc("Seconds from the button to the hit landing; the swing is visible meanwhile.")] public float windup = 0.07f;
        [Doc("Seconds from the hit until the next swing can start.")] public float recovery = 0.12f;
        [Doc("Push away from the hero.")] public float knockback = 3f;
        [Doc("The swing's arc in degrees (the visible sweep).")] public float arc = 90f;
        [Doc("The visible swing's size (a bigger final blow).")] public float size = 1f;
    }

    [Serializable]
    [Doc("audio.json sound ids a weapon plays.")]
    public class WeaponSoundsDef
    {
        [SoundRef, Doc("A shot or the start of a swing (empty = the playerShoot event for a gun).")] public string use = "";
        [SoundRef, Doc("A melee hit landing.")] public string hit = "";
        [SoundRef, Doc("Pulling the trigger on an empty weapon.")] public string empty = "";
        [SoundRef, Doc("Switching to this weapon.")] public string equip = "";
    }

    /// <summary>The string names a weapon in content/weapons.</summary>
    [AttributeUsage(AttributeTargets.Field)] public sealed class WeaponRefAttribute : Attribute { }

    // =====================================================================
    // ACTIONS
    // =====================================================================

    [Serializable, JsonType("giveWeapon")]
    [Doc("Puts a weapon in the hero's hands (he keeps holding the others) and, with 'equip', takes it out. Ammo weapons come with 'ammo' rounds, or " +
         "full. Giving what he holds only adds ammo; giving one a scene took away (the launcher on Floor 998) brings it back. A rewind that goes back " +
         "before it takes the weapon away again; the weapon stays until a death ends the run (a pickup's unlockWeapon keeps it for good).")]
    public sealed class GiveWeaponAction : GameAction
    {
        [Required, WeaponRef] public string weapon = "";
        [Doc("Take it out now.")] public bool equip = true;
        [Doc("Rounds to add for a weapon that uses ammo (0 = fill it).")] public int ammo;
        [Doc("Say its name over the hero's head.")] public bool announce = true;

        public override void Run(ActionContext context) => context.World.Weapons?.Give(weapon, equip, ammo, announce);
    }

    [Serializable, JsonType("takeWeapon")]
    [Doc("Takes a weapon out of the hero's hands, the rocket launcher too (he is never left empty-handed: a scene gives him something first). " +
         "A weapon he has unlocked for good is only taken for this run: it comes back with the next run, a giveWeapon or a pickup. If it was in his hands the next one he holds is.")]
    public sealed class TakeWeaponAction : GameAction
    {
        [Required, WeaponRef] public string weapon = "";
        [Doc("Say so over the hero's head.")] public bool announce = true;

        public override void Run(ActionContext context) => context.World.Weapons?.Take(weapon, announce);
    }

    [Serializable, JsonType("takeAllWeapons")]
    [Doc("Takes every weapon out of the hero's hands except 'except' (Floor 998: Hell takes it all and leaves him his fists). Unlocked weapons are only taken for this run.")]
    public sealed class TakeAllWeaponsAction : GameAction
    {
        [WeaponRef, Doc("The weapon he keeps; empty = none (he keeps the last one he holds anyway).")] public string except = "";
        [Doc("Say so over the hero's head.")] public bool announce = false;

        public override void Run(ActionContext context) => context.World.Weapons?.TakeAll(except, announce);
    }

    [Serializable, JsonType("unlockWeapon")]
    [Doc("A pickup's action: puts the weapon in his hands and unlocks it for good (saved; he has it again after dying and in every later run). " +
         "The first time shows the weapon's banner and the hero's line (weapon 'banner', 'heroText', 'heroLine'); later finds just refill its ammo. " +
         "Use it in a 'collect' pickup's 'do', which pays out once even if a rewind brings the pickup back.")]
    public sealed class UnlockWeaponAction : GameAction
    {
        [Required, WeaponRef] public string weapon = "";
        [Doc("Take it out now.")] public bool equip = true;

        public override void Run(ActionContext context) => context.World.Weapons?.Unlock(weapon, equip);
    }

    [Serializable, JsonType("returnToHero")]
    [Doc("A boomerang: flies out along its first velocity, slowing to a stop in 'turnAfter' seconds, then comes back to the hero faster and faster and is caught " +
         "(dies without a hit) when it reaches him. Put it on a player-team shot with a 'projectile' ability that has 'pierce', so it hurts on the way out and the way back.")]
    public sealed class ReturnToHeroAbility : Ability
    {
        [Doc("Seconds out before it turns.")] public float turnAfter = 0.55f;
        [Doc("Speed coming back as a multiple of the speed it was thrown at.")] public float returnSpeed = 1.3f;
        [Doc("Caught inside this distance of the hero.")] public float catchRadius = 0.45f;

        Vector2 thrown;
        bool started;

        public override void OnSpawn() { thrown = Vector2.zero; started = false; }

        public override void Tick(float now, float dt)
        {
            if (!started)
            {
                started = true;
                thrown = Owner.Body != null ? Owner.Body.linearVelocity : Vector2.zero;
            }
            float t = now - Owner.SpawnTime;
            if (t < turnAfter)
            {
                Owner.SetVelocity(thrown * Mathf.Clamp01(1f - t / Mathf.Max(0.05f, turnAfter)));
                return;
            }
            var player = Player;
            if (player == null || !player.IsAlive)
            {
                Owner.Die(DeathCause.Despawn);
                return;
            }
            Vector2 to = player.Position + Vector2.up * 0.5f - Owner.Position;
            if (to.magnitude <= catchRadius + player.Radius)
            {
                Owner.Die(DeathCause.Despawn);
                return;
            }
            float back = Mathf.Min(1f, (t - turnAfter) / 0.35f);
            Owner.SetVelocity(to.normalized * thrown.magnitude * returnSpeed * Mathf.Max(0.2f, back));
        }

        public override void SaveState(StateWriter writer)
        {
            writer.Bool(started);
            writer.Float(thrown.x);
            writer.Float(thrown.y);
        }

        public override void LoadState(StateReader reader)
        {
            started = reader.Bool();
            thrown = new Vector2(reader.Float(), reader.Float());
        }
    }
}
