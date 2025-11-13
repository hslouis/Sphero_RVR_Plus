using System;
using System.Threading.Tasks;

namespace RVR_CS.Sensors
{
    /// <summary>
    /// Interface pour le contrôle des LEDs du Sphero RVR+
    /// </summary>
    public interface ILedController
    {
        /// <summary>
        /// Définit la couleur RGB des LEDs principales
        /// </summary>
        /// <param name="red">Composante rouge (0-255)</param>
        /// <param name="green">Composante verte (0-255)</param>
        /// <param name="blue">Composante bleue (0-255)</param>
        /// <returns>True si la commande a été envoyée avec succès</returns>
        Task<bool> SetRgbLedAsync(byte red, byte green, byte blue);

        /// <summary>
        /// Définit la couleur RGB des LEDs principales avec luminosité
        /// </summary>
        /// <param name="red">Composante rouge (0-255)</param>
        /// <param name="green">Composante verte (0-255)</param>
        /// <param name="blue">Composante bleue (0-255)</param>
        /// <param name="brightness">Luminosité générale (0.0-1.0)</param>
        /// <returns>True si la commande a été envoyée avec succès</returns>
        Task<bool> SetRgbLedAsync(byte red, byte green, byte blue, float brightness);

        /// <summary>
        /// Éteint toutes les LEDs
        /// </summary>
        /// <returns>True si la commande a été envoyée avec succès</returns>
        Task<bool> TurnOffAsync();

        /// <summary>
        /// Définit une couleur prédéfinie
        /// </summary>
        /// <param name="color">Couleur prédéfinie</param>
        /// <returns>True si la commande a été envoyée avec succès</returns>
        Task<bool> SetColorAsync(LedColor color);

        /// <summary>
        /// Fait clignoter les LEDs
        /// </summary>
        /// <param name="red">Composante rouge</param>
        /// <param name="green">Composante verte</param>
        /// <param name="blue">Composante bleue</param>
        /// <param name="duration">Durée du clignotement en millisecondes</param>
        /// <param name="cycles">Nombre de cycles de clignotement</param>
        /// <returns>True si la commande a été envoyée avec succès</returns>
        Task<bool> BlinkAsync(byte red, byte green, byte blue, int duration = 500, int cycles = 3);

        /// <summary>
        /// Indique si le contrôleur LED est initialisé
        /// </summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// Couleurs prédéfinies pour les LEDs
    /// </summary>
    public enum LedColor
    {
        Off,
        Red,
        Green,
        Blue,
        Yellow,
		BlueCyan,
        Magenta,
        White,
        Orange,
        Purple,
        Pink,
        Lime
    }
}