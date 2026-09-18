using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ItemRandomizerPlugin {
    public class PlayerHandler {
        /// <summary>
        /// A heads flip that has been paid for but has not teleported yet. Kept around only long
        /// enough for a second player to turn it into a coin dance.
        /// </summary>
        private sealed class PendingFlip {
            public PendingFlip(Player player, RoomType room, float time) {
                Player = player;
                Room = room;
                Time = time;
            }

            public Player Player { get; }

            public RoomType Room { get; }

            public float Time { get; }

            public CoroutineHandle Handle { get; set; }
        }

        private static readonly Random Rng = new Random();

        private readonly CoinOutcomes outcomes = new CoinOutcomes(Rng);
        private readonly HashSet<ushort> usedCoins = new HashSet<ushort>();
        private readonly List<PendingFlip> pendingFlips = new List<PendingFlip>();
        private readonly List<CoroutineHandle> coroutines = new List<CoroutineHandle>();
        private readonly Dictionary<int, float> lastCoinTeleport = new Dictionary<int, float>();

        /// <summary>
        /// Current coin-inflicted size per player, as a multiplier of their normal scale. Only
        /// players who have flipped tails without hitting heads yet appear here.
        /// </summary>
        private readonly Dictionary<int, float> growthScale = new Dictionary<int, float>();

        private int coinFlipsThisRound;
        private int coinDeathsThisRound;

        private static Config Config => ItemRandomizer.Instance?.Config;

        private static Translations Translation => ItemRandomizer.Instance?.Translation;

        // ------------------------------------------------------------------
        // Coin distribution
        // ------------------------------------------------------------------
        public void OnSpawned(SpawnedEventArgs ev) {
            Player player = ev.Player;
            if (Config == null || player == null)
                return;

            // A new life always starts at normal size, whatever the coin did to the last one.
            if (growthScale.Remove(player.Id) && player.IsConnected)
                player.Scale = Vector3.one;

            if (!player.IsAlive || player.IsScp || player.IsInventoryFull)
                return;

            if (Config.CoinDeniedRoles != null && Config.CoinDeniedRoles.Contains(player.Role.Type))
                return;

            player.AddItem(ItemType.Coin);
        }

        // ------------------------------------------------------------------
        // Item randomizer
        // ------------------------------------------------------------------
        public void OnItemDropped(DroppingItemEventArgs ev) {
            Player player = ev.Player;
            if (Config == null || !Config.RandomizerEnabled || player == null)
                return;

            if (ev.Item == null || ev.Item.Type != ItemType.Coin)
                return;

            Room room = player.CurrentRoom;
            if (room == null || room.Type != Config.RandomizerRoom || !IsNearDropPoint(player))
                return;

            if (Config.RandomizerItems == null || Config.RandomizerItems.Count == 0)
                return;

            ItemType rolled = Config.RandomizerItems[Rng.Next(Config.RandomizerItems.Count)];

            ev.IsAllowed = false;
            player.RemoveItem(ev.Item);
            player.AddItem(rolled);
            Log.Debug($"{player.Nickname} traded a coin for {rolled}.");

            if (Config.AnnounceJackpot && Config.RandomizerJackpotItems != null && Config.RandomizerJackpotItems.Contains(rolled)) {
                Exiled.API.Features.Cassie.Message(Config.JackpotCassieMessage, false, false, true);
                Map.Broadcast(Config.BroadcastDuration, string.Format(Translation.JackpotBroadcast, player.Nickname, rolled));
            }
        }

        /// <summary>
        /// The configured drop point is stored in room-local space, so it has to be pushed back
        /// into world space here - the exact inverse of what the 'roompoint' command records.
        /// </summary>
        private bool IsNearDropPoint(Player player) {
            Room room = Room.Get(Config.RandomizerRoom);
            if (room == null)
                return false;

            Vector3 worldPoint = room.Transform.TransformPoint(Config.RandomizerDropPoint);
            float distance = Vector3.Distance(player.Position, worldPoint);

            Log.Debug($"Coin dropped {distance:0.00}m from the randomizer point (radius {Config.RandomizerRadius}m).");
            return distance <= Config.RandomizerRadius;
        }

        // ------------------------------------------------------------------
        // Coin flip
        // ------------------------------------------------------------------
        public void OnFlip(FlippingCoinEventArgs ev) {
            Player player = ev.Player;
            if (Config == null || player == null || !player.IsAlive)
                return;

            Room room = player.CurrentRoom;
            if (room == null) {
                Log.Debug("Coin flipped outside of any room; ignoring.");
                return;
            }

            if (room.Type == RoomType.Pocket) {
                coinFlipsThisRound++;
                HandlePocketFlip(ev, player);
                return;
            }

            if (Warhead.IsDetonated)
                return;

            ushort serial = ev.Item?.Serial ?? 0;
            if (serial != 0 && usedCoins.Contains(serial)) {
                ev.IsAllowed = false;
                Notify(player, Translation.CoinAlreadyUsed, 3);
                return;
            }

            coinFlipsThisRound++;

            if (ev.IsTails) {
                if (Config.TailsPunishment == TailsPunishmentMode.None)
                    return;

                if (Config.ConsumeCoinOnTails && serial != 0)
                    usedCoins.Add(serial);

                // Delayed like the teleport so the punishment lands when the coin does, not
                // before the player has even seen it spin.
                Notify(player, Translation.TeleportPending, (ushort)Mathf.Max(1f, Config.TeleportDelay));
                Track(Timing.CallDelayed(Config.TeleportDelay, () => {
                    if (player == null || !player.IsConnected || !player.IsAlive)
                        return;

                    if (Config.TailsPunishment == TailsPunishmentMode.Growth) {
                        Grow(player);
                        return;
                    }

                    string outcome = outcomes.Roll(player);
                    Log.Debug($"{player.Nickname} flipped tails -> {outcome ?? "nothing"}.");
                }));

                return;
            }

            RoomType destination = PickDestination(room);
            if (destination == RoomType.Unknown) {
                // Outside Light Containment the coin stays inert until decontamination.
                Log.Debug($"{player.Nickname} flipped heads in {room.Type} but no destination is available.");
                return;
            }

            // Burn the coin now, not when the teleport lands: otherwise the flip can be repeated
            // during the delay and every one of them queues up its own teleport.
            if (serial != 0)
                usedCoins.Add(serial);

            if (TryCoinDance(player, room))
                return;

            ScheduleTeleport(player, room.Type, destination);
        }

        private RoomType PickDestination(Room room) {
            if (room.Zone == ZoneType.LightContainment)
                return PickDifferentRoom(Config.LczRooms, room.Type);

            return Map.IsLczDecontaminated ? PickDifferentRoom(Config.NonLczRooms, room.Type) : RoomType.Unknown;
        }

        /// <summary>
        /// Picks a room from <paramref name="rooms"/> that is not the one the player is standing in.
        /// Filtering the list beats re-rolling in a loop: it cannot spin forever when the list holds
        /// a single room, and it actually compares room types instead of a Room against an enum.
        /// </summary>
        private RoomType PickDifferentRoom(List<RoomType> rooms, RoomType current) {
            if (rooms == null || rooms.Count == 0)
                return RoomType.Unknown;

            List<RoomType> candidates = rooms.Where(candidate => candidate != current).ToList();
            return candidates.Count == 0 ? RoomType.Unknown : candidates[Rng.Next(candidates.Count)];
        }

        private void ScheduleTeleport(Player player, RoomType from, RoomType destination) {
            Notify(player, Translation.TeleportPending, (ushort)Mathf.Max(1f, Config.TeleportDelay));

            PendingFlip flip = new PendingFlip(player, from, Time.time);
            flip.Handle = Timing.CallDelayed(Config.TeleportDelay, () => {
                pendingFlips.Remove(flip);

                if (player == null || !player.IsConnected || !player.IsAlive)
                    return;

                ResetGrowth(player);
                player.Teleport(destination);
                player.EnableEffect(EffectType.SinkHole, Config.SinkHoleDuration);
                lastCoinTeleport[player.Id] = Time.time;
                Log.Debug($"{player.Nickname} teleported to {destination}.");
            });

            pendingFlips.Add(flip);
        }

        /// <summary>
        /// Two players flipping heads in the same room within the configured window swap places
        /// with each other instead of being scattered across the zone.
        /// </summary>
        private bool TryCoinDance(Player player, Room room) {
            if (!Config.CoinDanceEnabled)
                return false;

            float now = Time.time;

            // Entries stay in the list until their own coroutine clears them, so Reset() can always
            // kill a teleport that is still in flight. The dance window is applied here instead.
            PendingFlip partner = pendingFlips.FirstOrDefault(flip =>
                flip.Player != null
                && flip.Player != player
                && flip.Player.IsAlive
                && flip.Room == room.Type
                && now - flip.Time <= Config.CoinDanceWindow);

            if (partner == null)
                return false;

            Timing.KillCoroutines(partner.Handle);
            pendingFlips.Remove(partner);

            ResetGrowth(player);
            ResetGrowth(partner.Player);

            Vector3 mine = player.Position;
            player.Teleport(partner.Player.Position);
            partner.Player.Teleport(mine);

            Notify(player, string.Format(Translation.CoinDance, partner.Player.Nickname), 5);
            Notify(partner.Player, string.Format(Translation.CoinDance, player.Nickname), 5);

            lastCoinTeleport[player.Id] = now;
            lastCoinTeleport[partner.Player.Id] = now;

            Log.Debug($"Coin dance between {player.Nickname} and {partner.Player.Nickname} in {room.Type}.");
            return true;
        }

        // ------------------------------------------------------------------
        // Pocket dimension
        // ------------------------------------------------------------------
        /// <summary>
        /// Inside the pocket dimension the coin is a free 50/50 - heads gets you out, tails kills
        /// you - so it is deliberately never marked as spent here.
        /// </summary>
        private void HandlePocketFlip(FlippingCoinEventArgs ev, Player player) {
            if (!Config.PocketGambleEnabled)
                return;

            Notify(player, Translation.PocketPrompt, 3);

            Track(ev.IsTails
                ? Timing.RunCoroutine(PocketDeath(player))
                : Timing.CallDelayed(Config.TeleportDelay, () => PocketEscape(player)));
        }

        private void PocketEscape(Player player) {
            if (player == null || !player.IsConnected || !player.IsAlive)
                return;

            ResetGrowth(player);

            Player host = Config.PocketEscapeNextToPlayer ? RandomHost(player) : null;
            if (host == null) {
                RoomType fallback = PickDifferentRoom(Config.NonLczRooms, RoomType.Pocket);
                if (fallback == RoomType.Unknown)
                    return;

                player.Teleport(fallback);
                player.EnableEffect(EffectType.SinkHole, Config.SinkHoleDuration);
                lastCoinTeleport[player.Id] = Time.time;
                return;
            }

            // Land slightly off-centre so the two players are not stacked in the same spot.
            Vector3 offset = new Vector3((float)(Rng.NextDouble() - 0.5), 0.5f, (float)(Rng.NextDouble() - 0.5));
            player.Teleport(host.Position + offset);
            lastCoinTeleport[player.Id] = Time.time;

            FlashGrenade flash = (FlashGrenade)Item.Create(ItemType.GrenadeFlash);
            flash.FuseTime = Config.PocketFlashFuse;
            flash.SpawnActive(host.Position, player);

            if (Config.PocketFlashBlindDuration > 0f) {
                player.EnableEffect(EffectType.Flashed, Config.PocketFlashBlindDuration);
                host.EnableEffect(EffectType.Flashed, Config.PocketFlashBlindDuration);
            }

            player.ShowHint(string.Format(Translation.PocketEscape, host.Nickname), 5f);
            host.ShowHint(string.Format(Translation.PocketEscapeVictim, player.Nickname), 5f);
            Log.Debug($"{player.Nickname} escaped the pocket dimension onto {host.Nickname}.");
        }

        private IEnumerator<float> PocketDeath(Player player) {
            for (int remaining = Config.PocketCountdownSeconds; remaining > 0; remaining--) {
                if (player == null || !player.IsConnected || !player.IsAlive)
                    yield break;

                player.ShowHint(string.Format(Translation.PocketCountdown, remaining), 1.1f);
                yield return Timing.WaitForSeconds(1f);
            }

            if (player == null || !player.IsConnected || !player.IsAlive)
                yield break;

            player.ShowHint(Translation.PocketDoom, 2f);

            if (Config.PocketDropLoot) {
                // Leave the pile on the floor so the next visitor finds it.
                player.DropItems();
            }
            else {
                foreach (Item item in player.Items.ToList()) {
                    if (item != null && item.Type != ItemType.Coin)
                        player.RemoveItem(item);
                }
            }

            ExplosiveGrenade grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE);
            grenade.FuseTime = Config.PocketGrenadeFuse;
            grenade.SpawnActive(player.Position, player);
            lastCoinTeleport[player.Id] = Time.time;
        }

        private Player RandomHost(Player player) {
            List<Player> pool = Player.List.Where(candidate =>
                candidate != null
                && candidate != player
                && candidate.IsAlive
                && candidate.CurrentRoom != null
                && candidate.CurrentRoom.Type != RoomType.Pocket).ToList();

            return pool.Count == 0 ? null : pool[Rng.Next(pool.Count)];
        }

        // ------------------------------------------------------------------
        // Server-wide comedy
        // ------------------------------------------------------------------
        public void OnDied(DiedEventArgs ev) {
            Player player = ev.Player;
            if (Config == null || player == null)
                return;

            growthScale.Remove(player.Id);

            if (!lastCoinTeleport.TryGetValue(player.Id, out float teleportedAt))
                return;

            lastCoinTeleport.Remove(player.Id);

            if (Time.time - teleportedAt > Config.DeathCalloutWindow)
                return;

            coinDeathsThisRound++;
            if (Config.DeathCalloutEnabled)
                Map.Broadcast(Config.BroadcastDuration, string.Format(Translation.DeathCallout, player.Nickname));
        }

        // ------------------------------------------------------------------
        // Round lifecycle
        // ------------------------------------------------------------------
        public void OnRoundStarted() {
            Reset();
        }

        public void OnRoundEnded(RoundEndedEventArgs ev) {
            if (Config != null && Config.RoundEndStatsEnabled && coinFlipsThisRound > 0)
                Map.Broadcast(Config.BroadcastDuration, string.Format(Translation.RoundEndStats, coinDeathsThisRound, coinFlipsThisRound));

            Reset();
        }

        /// <summary>
        /// Sends a personal notice as a broadcast instead of a hint. SCP:SL shows its own
        /// heads/tails hint the instant a coin is flipped and there is only one hint slot, so
        /// anything pushed from inside the flip event is overwritten before it can be read.
        /// Broadcasts live on a separate UI channel and survive.
        /// </summary>
        private static void Notify(Player player, string message, ushort seconds) {
            if (player == null || !player.IsConnected || string.IsNullOrEmpty(message))
                return;

            player.Broadcast(seconds, message, shouldClearPrevious: true);
        }

        /// <summary>
        /// Every tails pushes the player further above their normal size, and it stacks until they
        /// finally flip heads. Growing is purely a downside - a bigger target, and past a certain
        /// point a body that will not fit through a doorway - so unlike shrinking it can never turn
        /// into an accidental reward. It is also what keeps flipping-until-heads from being free.
        /// </summary>
        private void Grow(Player player) {
            float current = growthScale.TryGetValue(player.Id, out float stored) ? stored : 1f;
            float next = Mathf.Min(current + Config.TailsGrowthStep, Config.TailsGrowthMax);

            growthScale[player.Id] = next;
            player.Scale = Vector3.one * next;

            bool capped = next >= Config.TailsGrowthMax - 0.001f;
            string message = capped ? Translation.TailsGrowthCapped : Translation.TailsGrowth;
            player.ShowHint(string.Format(message, Mathf.RoundToInt(next * 100f)), 5f);

            Log.Debug($"{player.Nickname} flipped tails -> size {next:0.00}x{(capped ? " (capped)" : string.Empty)}.");
        }

        /// <summary>
        /// Heads pays out, so the coin hands the player their normal size back. Announced through
        /// <see cref="Notify"/> because the coin dance calls this from inside the flip event, where
        /// a hint would be overwritten by the game's own heads/tails hint.
        /// </summary>
        private void ResetGrowth(Player player) {
            if (player == null || !growthScale.Remove(player.Id) || !player.IsConnected)
                return;

            player.Scale = Vector3.one;
            Notify(player, Translation.GrowthReset, 4);
        }

        /// <summary>
        /// Keeps a handle around so <see cref="Reset"/> can kill it, dropping the ones that have
        /// already finished so the list does not grow for the whole round.
        /// </summary>
        private CoroutineHandle Track(CoroutineHandle handle) {
            coroutines.RemoveAll(existing => !existing.IsRunning);
            coroutines.Add(handle);
            return handle;
        }

        public void Reset() {
            foreach (CoroutineHandle handle in coroutines)
                Timing.KillCoroutines(handle);

            foreach (PendingFlip flip in pendingFlips)
                Timing.KillCoroutines(flip.Handle);

            foreach (int playerId in growthScale.Keys.ToList()) {
                Player grown = Player.Get(playerId);
                if (grown != null && grown.IsConnected)
                    grown.Scale = Vector3.one;
            }

            coroutines.Clear();
            pendingFlips.Clear();
            usedCoins.Clear();
            lastCoinTeleport.Clear();
            growthScale.Clear();
            coinFlipsThisRound = 0;
            coinDeathsThisRound = 0;
        }
    }
}
