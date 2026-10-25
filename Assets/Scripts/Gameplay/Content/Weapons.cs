using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    /// <summary>content/weapons/{id}.json</summary>
    [Serializable]
    [Doc("A weapon the hero can hold (content/weapons/{id}.json). He holds several and switches with Q / the mouse wheel / the gamepad's top button, " +
         "or the weapon button on the HUD (touch). 'rocketLauncher' is his gun from the start and is always in hand; others come with the giveWeapon " +
         "action and leave with takeWeapon. A weapon is either a projectile gun (fires the hero's rocket) or a melee swing (a hit box in front of him, " +
         "with a combo). The aim guide follows the weapon.")]
    public class WeaponDef
    {
        [NonSerialized] public string id;

        [Required, Doc("Shown on the HUD (the HUD writes it in capitals).")] public string displayName = "";
        [Doc("One line for the cookbook and the editor.")] public string description = "";
        [Doc("HUD button and swing color.")] public Color color = new Color(1f, 0.55f, 0.2f, 1f);
        [Doc("Fires a projectile, or swings a melee hit box.")] public WeaponKind kind = WeaponKind.Projectile;
        [Doc("Seconds between shots (a projectile weapon; attack speed upgrades shorten it). A melee weapon uses its combo steps' times.")]
        public float cooldown = 0.3f;
        [Doc("Seconds before the weapon can be used after switching to it.")] public float switchDelay = 0.15f;
        [Doc("The aim guide's line: shown or not, and how long compared with the launcher's.")] public WeaponGuideDef aimGuide = new WeaponGuideDef();
        [Doc("Ammo: capacity 0 = none (never runs dry).")] public WeaponAmmoDef ammo = new WeaponAmmoDef();
        [Doc("Used when kind is Projectile. The shot is the hero's rocket.")] public WeaponProjectileDef projectile = new WeaponProjectileDef();
        [Doc("Used when kind is Melee.")] public WeaponMeleeDef melee = new WeaponMeleeDef();
        public WeaponSoundsDef sounds = new WeaponSoundsDef();
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
    }

    [Serializable]
    [Doc("A melee weapon: each swing is the next step of the combo.")]
    public class WeaponMeleeDef
    {
        [Doc("Seconds after a swing ends in which the next swing continues the combo. Later (or after the last step) it starts over.")]
        public float comboWindow = 0.45f;
        [Doc("The combo, in order. At least one step.")] public List<MeleeStepDef> steps = new List<MeleeStepDef>();
        [Doc("Height of the hit box (world units); its length is the step's reach.")] public float height = 1.1f;
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
         "full. Giving what he holds only adds ammo. A rewind that goes back before it takes the weapon away again; the weapon stays until a death ends the run.")]
    public sealed class GiveWeaponAction : GameAction
    {
        [Required, WeaponRef] public string weapon = "";
        [Doc("Take it out now.")] public bool equip = true;
        [Doc("Rounds to add for a weapon that uses ammo (0 = fill it).")] public int ammo;
        [Doc("Say its name over the hero's head.")] public bool announce = true;

        public override void Run(ActionContext context) => context.World.Weapons?.Give(weapon, equip, ammo, announce);
    }

    [Serializable, JsonType("takeWeapon")]
    [Doc("Takes a weapon out of the hero's hands (the rocket launcher can't be taken). If it was in his hands he is left with the launcher.")]
    public sealed class TakeWeaponAction : GameAction
    {
        [Required, WeaponRef] public string weapon = "";
        [Doc("Say so over the hero's head.")] public bool announce = true;

        public override void Run(ActionContext context) => context.World.Weapons?.Take(weapon, announce);
    }
}
