namespace ItemRandomizerPlugin {

    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Map;
    using System;
    using Player = Exiled.Events.Handlers.Player;
    using Server = Exiled.Events.Handlers.Server;
    using Map = Exiled.Events.Handlers.Map;

    public class ItemRandomizer : Plugin<Config, Translations> {
        public static ItemRandomizer Instance { get; private set; }

        public PlayerHandler _playerHandler;

        public override string Name => "ItemRandomizerPlugin";
        public override string Prefix => "IRndPl";
        public override string Author => "Megador";
        public override Version Version => new Version(1, 1, 0);
        public override Version RequiredExiledVersion => new Version(8, 9, 11);

        public override void OnEnabled() {
            Instance = this;
            _playerHandler = new PlayerHandler();

            Player.DroppingItem += _playerHandler.OnItemDropped;
            Player.FlippingCoin += _playerHandler.OnFlip;
            Player.Spawned += _playerHandler.OnSpawned;
            Player.Died += _playerHandler.OnDied;
            Server.RoundStarted += _playerHandler.OnRoundStarted;
            Server.RoundEnded += _playerHandler.OnRoundEnded;
            Map.Decontaminating += OnDecontaminating;

            Log.Info("ItemRandomizerPlugin loaded successfully");
            base.OnEnabled();
        }

        public override void OnDisabled() {
            Player.DroppingItem -= _playerHandler.OnItemDropped;
            Player.FlippingCoin -= _playerHandler.OnFlip;
            Player.Spawned -= _playerHandler.OnSpawned;
            Player.Died -= _playerHandler.OnDied;
            Server.RoundStarted -= _playerHandler.OnRoundStarted;
            Server.RoundEnded -= _playerHandler.OnRoundEnded;
            Map.Decontaminating -= OnDecontaminating;

            _playerHandler.Reset();
            _playerHandler = null;
            Instance = null;
            base.OnDisabled();
        }

        public void OnDecontaminating(DecontaminatingEventArgs ev) {
            Exiled.API.Features.Map.Broadcast(Config.DecontaminationBroadcastDuration, Translation.DecontaminationBroadcast);
        }
    }
}
