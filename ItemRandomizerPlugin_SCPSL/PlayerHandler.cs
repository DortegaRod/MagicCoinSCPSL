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

        private int coinFlipsThisRound;
        private int coinDeathsThisRound;

        private static Config Config => ItemRandomizer.Instance?.Config;

        private static Translations Translation => ItemRandomizer.Instance?.Translation;

        // ------------------------------------------------------------------
        // Coin distribution
        // ------------------------------------------------------------------
        public void OnSpawned(SpawnedEventArgs ev) {
            Player player = ev.Player;
            if (Config == null || player == null || !player.IsAlive || player.IsScp || player.IsInventoryFull)
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
                player.ShowHint(Translation.CoinAlreadyUsed, 3f);
                return;
            }

            coinFlipsThisRound++;

            if (ev.IsTails) {
                if (!Config.TailsRouletteEnabled)
                    return;

                if (Config.ConsumeCoinOnTails && serial != 0)
                    usedCoins.Add(serial);

                // Delayed like the teleport so the punishment lands when the coin does, not
                // before the player has even seen it spin.
                player.ShowHint(Translation.TeleportPending, Config.TeleportDelay);
                Track(Timing.CallDelayed(Config.TeleportDelay, () => {
                    if (player == null || !player.IsConnected || !player.IsAlive)
                        return;

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
            player.ShowHint(Translation.TeleportPending, Config.TeleportDelay);

            PendingFlip flip = new PendingFlip(player, from, Time.time);
            flip.Handle = Timing.CallDelayed(Config.TeleportDelay, () => {
                pendingFlips.Remove(flip);

                if (player == null || !player.IsConnected || !player.IsAlive)
                    return;

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

            Vector3 mine = player.Position;
            player.Teleport(partner.Player.Position);
            partner.Player.Teleport(mine);

            player.ShowHint(string.Format(Translation.CoinDance, partner.Player.Nickname), 5f);
            partner.Player.ShowHint(string.Format(Translation.CoinDance, player.Nickname), 5f);

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

            player.ShowHint(Translation.PocketPrompt, 3f);

            Track(ev.IsTails
                ? Timing.RunCoroutine(PocketDeath(player))
                : Timing.CallDelayed(Config.TeleportDelay, () => PocketEscape(player)));
        }

        private void PocketEscape(Player player) {
            if (player == null || !player.IsConnected || !player.IsAlive)
                return;

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
            if (Config == null || player == null || !lastCoinTeleport.TryGetValue(player.Id, out float teleportedAt))
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

            coroutines.Clear();
            pendingFlips.Clear();
            usedCoins.Clear();
            lastCoinTeleport.Clear();
            coinFlipsThisRound = 0;
            coinDeathsThisRound = 0;
        }
    }
}
