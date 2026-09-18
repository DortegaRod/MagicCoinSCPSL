using Exiled.API.Enums;
using Exiled.API.Interfaces;
using ItemRandomizerPlugin_SCPSL.RoomPoints;
using PlayerRoles;
using System.Collections.Generic;
using System.ComponentModel;

namespace ItemRandomizerPlugin {
    public class Config : IConfig {
        public bool IsEnabled { get; set; } = true;

        [Description("Prints verbose diagnostics (distances, rolled items, outcome ids) to the server console.")]
        public bool Debug { get; set; } = false;

        // ------------------------------------------------------------------
        // Coin distribution
        // ------------------------------------------------------------------
        [Description("Roles that do NOT receive a free coin when they spawn. Everyone else does. SCPs and non-playing roles are always skipped regardless of this list.")]
        public List<RoleTypeId> CoinDeniedRoles { get; set; } = new List<RoleTypeId> {
            RoleTypeId.NtfPrivate,
            RoleTypeId.NtfSpecialist,
            RoleTypeId.NtfSergeant,
            RoleTypeId.NtfCaptain,
            RoleTypeId.ChaosConscript,
            RoleTypeId.ChaosMarauder,
            RoleTypeId.ChaosRepressor,
            RoleTypeId.ChaosRifleman,
        };

        // ------------------------------------------------------------------
        // Coin teleport (heads)
        // ------------------------------------------------------------------
        [Description("Seconds between the flip and the teleport actually happening.")]
        public float TeleportDelay { get; set; } = 3f;

        [Description("Duration of the SinkHole effect applied on arrival.")]
        public float SinkHoleDuration { get; set; } = 5f;

        [Description("Also burn the coin when the flip lands on tails. Leave true or players will farm the tails roulette. Never applies inside the pocket dimension, where the flip is a free 50/50.")]
        public bool ConsumeCoinOnTails { get; set; } = true;

        [Description("Rooms a coin can send you to while you are inside Light Containment.")]
        public List<RoomType> LczRooms { get; set; } = new List<RoomType> {
            RoomType.LczCurve,
            RoomType.LczStraight,
            RoomType.LczCrossing,
            RoomType.LczTCross,
            RoomType.LczCafe,
            RoomType.LczPlants,
            RoomType.LczToilets,
            RoomType.LczAirlock,
            RoomType.LczClassDSpawn,
            RoomType.LczCheckpointB,
            RoomType.LczGlassBox,
            RoomType.LczCheckpointA,
        };

        [Description("Rooms a coin can send you to once you are outside Light Containment.")]
        public List<RoomType> NonLczRooms { get; set; } = new List<RoomType> {
            RoomType.Hcz079,
            RoomType.HczEzCheckpointA,
            RoomType.HczEzCheckpointB,
            RoomType.HczArmory,
            RoomType.Hcz939,
            RoomType.HczHid,
            RoomType.Hcz049,
            RoomType.HczCrossing,
            RoomType.Hcz106,
            RoomType.HczNuke,
            RoomType.HczTestRoom,
            RoomType.HczElevatorA,
            RoomType.HczElevatorB,
            RoomType.HczTesla,
            RoomType.HczServerRoom,
            RoomType.HczCrossRoomWater,
            RoomType.HczCurve,
            RoomType.Hcz096,
            RoomType.EzCafeteria,
            RoomType.EzCheckpointHallwayA,
            RoomType.EzCheckpointHallwayB,
            RoomType.EzCollapsedTunnel,
            RoomType.EzConference,
            RoomType.EzCrossing,
            RoomType.EzCurve,
            RoomType.EzDownstairsPcs,
            RoomType.EzGateA,
            RoomType.EzGateB,
            RoomType.EzIntercom,
            RoomType.EzTCross,
            RoomType.EzUpstairsPcs,
            RoomType.EzVent,
        };

        // ------------------------------------------------------------------
        // Item randomizer (drop a coin on the machine spot)
        // ------------------------------------------------------------------
        public bool RandomizerEnabled { get; set; } = true;

        [Description("Room the randomizer spot lives in.")]
        public RoomType RandomizerRoom { get; set; } = RoomType.Lcz173;

        [Description("Drop point in coordinates LOCAL to RandomizerRoom. IMPORTANT: re-capture this with the 'roompoint' RA command after updating - the previous value was recorded with a broken transform and is not a valid local coordinate.")]
        public SerializedVector3 RandomizerDropPoint { get; set; } = new SerializedVector3(257.3519f, 13.14142f, 127.7134f);

        [Description("How close to RandomizerDropPoint the coin has to be dropped, in metres.")]
        public float RandomizerRadius { get; set; } = 3f;

        [Description("Items the randomizer can hand out.")]
        public List<ItemType> RandomizerItems { get; set; } = new List<ItemType> {
            ItemType.KeycardJanitor,
            ItemType.KeycardScientist,
            ItemType.KeycardResearchCoordinator,
            ItemType.KeycardZoneManager,
            ItemType.KeycardGuard,
            ItemType.KeycardMTFPrivate,
            ItemType.KeycardContainmentEngineer,
            ItemType.KeycardMTFOperative,
            ItemType.KeycardMTFCaptain,
            ItemType.KeycardFacilityManager,
            ItemType.KeycardChaosInsurgency,
            ItemType.KeycardO5,
            ItemType.Radio,
            ItemType.GunCOM15,
            ItemType.GunCOM18,
            ItemType.GunCom45,
            ItemType.GunRevolver,
            ItemType.GunFSP9,
            ItemType.GunCrossvec,
            ItemType.GunE11SR,
            ItemType.GunAK,
            ItemType.GunShotgun,
            ItemType.GunLogicer,
            ItemType.GunFRMG0,
            ItemType.GunA7,
            ItemType.MicroHID,
            ItemType.ParticleDisruptor,
            ItemType.Jailbird,
            ItemType.Ammo9x19,
            ItemType.Ammo12gauge,
            ItemType.Ammo44cal,
            ItemType.Ammo556x45,
            ItemType.Ammo762x39,
            ItemType.Medkit,
            ItemType.Painkillers,
            ItemType.Adrenaline,
            ItemType.Flashlight,
            ItemType.Lantern,
            ItemType.ArmorLight,
            ItemType.ArmorCombat,
            ItemType.ArmorHeavy,
            ItemType.GrenadeHE,
            ItemType.GrenadeFlash,
            ItemType.SCP018,
            ItemType.SCP207,
            ItemType.AntiSCP207,
            ItemType.SCP244a,
            ItemType.SCP244b,
            ItemType.SCP268,
            ItemType.SCP330,
            ItemType.SCP500,
            ItemType.SCP1576,
            ItemType.SCP1853,
            ItemType.SCP2176,
        };

        [Description("Rolling any of these fires a server-wide C.A.S.S.I.E. announcement, so everyone knows who to hunt.")]
        public List<ItemType> RandomizerJackpotItems { get; set; } = new List<ItemType> {
            ItemType.MicroHID,
            ItemType.ParticleDisruptor,
            ItemType.KeycardO5,
            ItemType.GunLogicer,
            ItemType.GunFRMG0,
            ItemType.Jailbird,
        };

        public bool AnnounceJackpot { get; set; } = true;

        [Description("C.A.S.S.I.E. line played on a jackpot. Keep it to words C.A.S.S.I.E. can actually pronounce.")]
        public string JackpotCassieMessage { get; set; } = "attention . a subject has won the lottery";

        // ------------------------------------------------------------------
        // Tails roulette (outside the pocket dimension)
        // ------------------------------------------------------------------
        public bool TailsRouletteEnabled { get; set; } = true;

        [Description("Relative weight of each tails outcome. Set one to 0 to disable it. Valid ids: swap_inventories, swap_positions, severed_hands, shrink, giant, sugar_rush, candy, flashbang, tantrum, fake_cassie.")]
        public Dictionary<string, int> TailsOutcomeWeights { get; set; } = new Dictionary<string, int> {
            { "flashbang", 18 },
            { "sugar_rush", 16 },
            { "shrink", 14 },
            { "severed_hands", 12 },
            { "tantrum", 12 },
            { "giant", 10 },
            { "candy", 8 },
            { "swap_positions", 6 },
            { "swap_inventories", 3 },
            { "fake_cassie", 1 },
        };

        [Description("How long the shrink/giant outcomes last.")]
        public float ScaleOutcomeDuration { get; set; } = 15f;

        [Description("Scale multiplier for the shrink outcome.")]
        public float ShrinkScale { get; set; } = 0.4f;

        [Description("Scale multiplier for the giant outcome.")]
        public float GiantScale { get; set; } = 1.6f;

        [Description("How long SeveredHands lasts.")]
        public float SeveredHandsDuration { get; set; } = 20f;

        [Description("C.A.S.S.I.E. line for the fake_cassie outcome. It is a lie - nothing actually breaches.")]
        public string FakeCassieMessage { get; set; } = "pitch_0.9 SCP 1 7 3 has breached containment";

        // ------------------------------------------------------------------
        // Coin dance
        // ------------------------------------------------------------------
        [Description("If two players flip heads in the same room within CoinDanceWindow seconds, they swap places instead of teleporting away.")]
        public bool CoinDanceEnabled { get; set; } = true;

        public float CoinDanceWindow { get; set; } = 2f;

        // ------------------------------------------------------------------
        // Pocket dimension
        // ------------------------------------------------------------------
        [Description("Inside the pocket dimension the coin is a free 50/50: heads gets you out, tails kills you. The coin is never consumed there.")]
        public bool PocketGambleEnabled { get; set; } = true;

        [Description("Heads inside the pocket dimension drops you next to a random living player (SCPs included) with a flashbang between you. Falls back to a random room if nobody else is alive.")]
        public bool PocketEscapeNextToPlayer { get; set; } = true;

        public float PocketFlashFuse { get; set; } = 0.15f;

        [Description("Guaranteed Flashed duration applied to both players, on top of the grenade itself. Set to 0 to rely on the grenade alone.")]
        public float PocketFlashBlindDuration { get; set; } = 4f;

        [Description("Seconds of countdown hints before the tails grenade goes off.")]
        public int PocketCountdownSeconds { get; set; } = 3;

        public float PocketGrenadeFuse { get; set; } = 0.5f;

        [Description("Drop the victim's inventory on the floor of the pocket dimension instead of deleting it, so the next visitor finds the pile.")]
        public bool PocketDropLoot { get; set; } = true;

        // ------------------------------------------------------------------
        // Server-wide comedy
        // ------------------------------------------------------------------
        [Description("Broadcast a line when somebody dies shortly after a coin teleport.")]
        public bool DeathCalloutEnabled { get; set; } = true;

        [Description("How long after a coin teleport a death still counts as the coin's fault.")]
        public float DeathCalloutWindow { get; set; } = 15f;

        [Description("Broadcast the coin's body count when the round ends.")]
        public bool RoundEndStatsEnabled { get; set; } = true;

        public ushort BroadcastDuration { get; set; } = 6;

        // ------------------------------------------------------------------
        // Decontamination
        // ------------------------------------------------------------------
        public ushort DecontaminationBroadcastDuration { get; set; } = 10;
    }
}
