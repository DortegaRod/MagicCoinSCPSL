using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Hazards;
using Exiled.API.Features.Items;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace ItemRandomizerPlugin {
    /// <summary>
    /// The tails roulette. Every punishment lives in this weighted table, so adding a new joke is
    /// one entry here plus one weight in <see cref="Config.TailsOutcomeWeights"/> - OnFlip never
    /// has to grow another branch.
    /// </summary>
    public class CoinOutcomes {
        private sealed class Outcome {
            public Outcome(string id, Action<Player> apply) {
                Id = id;
                Apply = apply;
            }

            public string Id { get; }

            public Action<Player> Apply { get; }
        }

        private readonly Random rng;
        private readonly List<Outcome> table;

        public CoinOutcomes(Random rng) {
            this.rng = rng;
            table = new List<Outcome> {
                new Outcome("flashbang", Flashbang),
                new Outcome("sugar_rush", SugarRush),
                new Outcome("shrink", player => Rescale(player, Config.ShrinkScale, Translation.TailsShrink)),
                new Outcome("giant", player => Rescale(player, Config.GiantScale, Translation.TailsGiant)),
                new Outcome("severed_hands", SeveredHands),
                new Outcome("tantrum", Tantrum),
                new Outcome("candy", Candy),
                new Outcome("swap_positions", SwapPositions),
                new Outcome("swap_inventories", SwapInventories),
                new Outcome("fake_cassie", FakeCassie),
            };
        }

        private static Config Config => ItemRandomizer.Instance?.Config;

        private static Translations Translation => ItemRandomizer.Instance?.Translation;

        /// <summary>
        /// Picks a punishment according to the configured weights and applies it.
        /// Returns the outcome id that fired, or null if nothing did.
        /// </summary>
        public string Roll(Player player) {
            if (player == null || !player.IsAlive || Config == null)
                return null;

            int total = table.Sum(outcome => WeightOf(outcome.Id));
            if (total <= 0)
                return null;

            int roll = rng.Next(total);
            foreach (Outcome outcome in table) {
                roll -= WeightOf(outcome.Id);
                if (roll < 0) {
                    outcome.Apply(player);
                    return outcome.Id;
                }
            }

            return null;
        }

        private static int WeightOf(string id) {
            Dictionary<string, int> weights = Config?.TailsOutcomeWeights;
            if (weights == null)
                return 0;

            return weights.TryGetValue(id, out int weight) && weight > 0 ? weight : 0;
        }

        // ------------------------------------------------------------------
        // Outcomes
        // ------------------------------------------------------------------
        private void Flashbang(Player player) {
            FlashGrenade flash = (FlashGrenade)Item.Create(ItemType.GrenadeFlash);
            flash.FuseTime = 0.15f;
            flash.SpawnActive(player.Position, player);
            player.ShowHint(Translation.TailsFlashbang, 5f);
        }

        private void SugarRush(Player player) {
            player.EnableEffect(EffectType.Scp207, 12f);
            player.ChangeEffectIntensity(EffectType.Scp207, 4, 12f);
            player.EnableEffect(EffectType.Blurred, 12f);
            player.ShowHint(Translation.TailsSugarRush, 5f);
        }

        private void Rescale(Player player, float factor, string message) {
            player.Scale = Vector3.one * factor;
            player.ShowHint(message, 5f);

            Timing.CallDelayed(Config.ScaleOutcomeDuration, () => {
                if (player != null && player.IsConnected)
                    player.Scale = Vector3.one;
            });
        }

        private void SeveredHands(Player player) {
            player.EnableEffect(EffectType.SeveredHands, Config.SeveredHandsDuration);
            player.ShowHint(Translation.TailsSeveredHands, 5f);
        }

        private void Tantrum(Player player) {
            TantrumHazard.PlaceTantrum(player.Position);
            player.ShowHint(Translation.TailsTantrum, 5f);
        }

        private void Candy(Player player) {
            foreach (Item item in player.Items.ToList()) {
                if (item != null && item.Type != ItemType.Coin)
                    player.RemoveItem(item);
            }

            player.AddItem(ItemType.SCP330);
            player.ShowHint(Translation.TailsCandy, 5f);
        }

        private void SwapPositions(Player player) {
            Player other = RandomOther(player, allowScps: true);
            if (other == null) {
                Flashbang(player);
                return;
            }

            Vector3 mine = player.Position;
            player.Teleport(other.Position);
            other.Teleport(mine);

            player.ShowHint(string.Format(Translation.TailsSwapPositions, other.Nickname), 5f);
            other.ShowHint(string.Format(Translation.TailsSwapPositions, player.Nickname), 5f);
        }

        private void SwapInventories(Player player) {
            Player other = RandomOther(player, allowScps: false);
            if (other == null) {
                Candy(player);
                return;
            }

            // Swapping by ItemType loses per-item state (loaded ammo, radio battery), but it is
            // the only way that cannot leave an item owned by two inventories at once.
            List<ItemType> mine = player.Items.Select(item => item.Type).ToList();
            List<ItemType> theirs = other.Items.Select(item => item.Type).ToList();

            player.ClearInventory();
            other.ClearInventory();

            foreach (ItemType type in theirs)
                player.AddItem(type);

            foreach (ItemType type in mine)
                other.AddItem(type);

            player.ShowHint(string.Format(Translation.TailsSwapInventories, other.Nickname), 5f);
            other.ShowHint(string.Format(Translation.TailsSwapInventories, player.Nickname), 5f);
        }

        private void FakeCassie(Player player) {
            Exiled.API.Features.Cassie.Message(Config.FakeCassieMessage, false, false, true);
            player.ShowHint(Translation.TailsFakeCassie, 5f);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        /// <summary>
        /// Picks a random living player other than <paramref name="player"/>. Players inside the
        /// pocket dimension are never eligible - swapping with them would either hand out a free
        /// escape or drop somebody in there without a coin.
        /// </summary>
        private Player RandomOther(Player player, bool allowScps) {
            List<Player> pool = Player.List.Where(candidate =>
                candidate != null
                && candidate != player
                && candidate.IsAlive
                && (allowScps || !candidate.IsScp)
                && candidate.CurrentRoom != null
                && candidate.CurrentRoom.Type != RoomType.Pocket).ToList();

            return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
        }
    }
}
