using Exiled.API.Interfaces;
using System.ComponentModel;

namespace ItemRandomizerPlugin {
    /// <summary>
    /// Every player-facing string the plugin can print. Exiled writes this out to its own
    /// translations file, so server owners can reword or translate the jokes without a rebuild.
    /// </summary>
    public class Translations : ITranslation {
        // ------------------------------------------------------------------
        // Coin teleport
        // ------------------------------------------------------------------
        [Description("Shown when a player flips a coin that has already been spent.")]
        public string CoinAlreadyUsed { get; set; } = "<color=#888888>Esta moneda ya está gastada.</color>";

        [Description("Shown during the delay between the flip and the teleport.")]
        public string TeleportPending { get; set; } = "<color=#ffcc00>La moneda decide tu destino...</color>";

        [Description("Shown to both players when a coin dance swaps them. {0} = the other player's nickname.")]
        public string CoinDance { get; set; } = "<color=#ffcc00>Danza de monedas con {0}.</color>";

        // ------------------------------------------------------------------
        // Pocket dimension
        // ------------------------------------------------------------------
        [Description("Shown the moment a coin is flipped inside the pocket dimension.")]
        public string PocketPrompt { get; set; } = "<color=#ffcc00>Cara: sales. Cruz: sales en pedazos.</color>";

        [Description("Countdown before the tails grenade. {0} = seconds remaining.")]
        public string PocketCountdown { get; set; } = "<color=#ff3333><b>{0}</b></color>";

        [Description("Last hint before the grenade goes off.")]
        public string PocketDoom { get; set; } = "<color=#ff3333><b>Cruz.</b></color>";

        [Description("Shown to the escapee when heads drops them next to somebody. {0} = the other player's nickname.")]
        public string PocketEscape { get; set; } = "<color=#33ff66>Cara. Sales... encima de {0}.</color>";

        [Description("Shown to the player somebody just landed on. {0} = the escapee's nickname.")]
        public string PocketEscapeVictim { get; set; } = "<color=#ffcc00>{0} acaba de salir de la dimensión de bolsillo encima de ti.</color>";

        // ------------------------------------------------------------------
        // Tails: growth mode
        // ------------------------------------------------------------------
        [Description("Shown on every tails while growth is stacking. {0} = your new size as a percentage.")]
        public string TailsGrowth { get; set; } = "<color=#ff6666>Cruz. La moneda te hace más grande. ({0}%)</color>";

        [Description("Shown on the tails that pushes you to the size cap. {0} = the cap as a percentage.")]
        public string TailsGrowthCapped { get; set; } = "<color=#ff3333>Cruz. Ya no puedes crecer más. ({0}%)</color>";

        [Description("Shown when heads finally lands and the coin gives you your size back.")]
        public string GrowthReset { get; set; } = "<color=#33ff66>Cara. Vuelves a tu tamaño.</color>";

        // ------------------------------------------------------------------
        // Tails: roulette mode
        // ------------------------------------------------------------------
        [Description("{0} = the other player's nickname.")]
        public string TailsSwapInventories { get; set; } = "<color=#ff6666>Tu inventario se ha ido con {0}.</color>";

        [Description("{0} = the other player's nickname.")]
        public string TailsSwapPositions { get; set; } = "<color=#ff6666>Has cambiado de sitio con {0}. Suerte.</color>";

        public string TailsSeveredHands { get; set; } = "<color=#ff6666>La moneda te ha cobrado las manos.</color>";

        public string TailsShrink { get; set; } = "<color=#ff6666>La moneda te ha encogido.</color>";

        public string TailsGiant { get; set; } = "<color=#ff6666>La moneda te ha hecho enorme. Y visible.</color>";

        public string TailsSugarRush { get; set; } = "<color=#ff6666>Demasiada cola. Buena suerte frenando.</color>";

        public string TailsCandy { get; set; } = "<color=#ff6666>Todo tu inventario era caramelo.</color>";

        public string TailsFlashbang { get; set; } = "<color=#ff6666>La moneda te deslumbra.</color>";

        public string TailsTantrum { get; set; } = "<color=#ff6666>Algo ha pasado por aquí.</color>";

        public string TailsFakeCassie { get; set; } = "<color=#ff6666>La moneda ha mentido a toda la instalación.</color>";

        // ------------------------------------------------------------------
        // Server-wide
        // ------------------------------------------------------------------
        [Description("Broadcast when a player dies shortly after a coin teleport. {0} = nickname.")]
        public string DeathCallout { get; set; } = "<color=#ffcc00>{0} confió en la moneda. La moneda no confió en él.</color>";

        [Description("Broadcast on a jackpot roll. {0} = nickname, {1} = item.")]
        public string JackpotBroadcast { get; set; } = "<color=#ffcc00>{0} ha ganado la lotería de la Fundación: {1}</color>";

        [Description("Broadcast at round end. {0} = coin deaths, {1} = total flips.")]
        public string RoundEndStats { get; set; } = "<color=#ffcc00>La moneda se cobró {0} vidas hoy, en {1} tiradas.</color>";

        [Description("Broadcast when Light Containment starts decontaminating.")]
        public string DecontaminationBroadcast { get; set; } = "COIN TP ENABLED IN ALL THE FACILITY";
    }
}
