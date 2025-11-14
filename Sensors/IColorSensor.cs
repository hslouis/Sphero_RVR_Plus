using System.Threading.Tasks;

namespace Sphero_RVR_Plus_CS.Sensors
{
    /// <summary>
    /// Interface pour les capteurs de couleur
    /// </summary>
    public interface IColorSensor
    {
        /// <summary>
        /// Active le capteur de couleur
        /// </summary>
        /// <returns>True si l'activation a réussi</returns>
        Task<bool> ActivateAsync();

        /// <summary>
        /// Lit les valeurs de couleur depuis le capteur avec détection automatique
        /// </summary>
        /// <returns>Structure ColorReading avec détection automatique de couleur</returns>
        Task<ColorReading?> ReadColorAsync();

        /// <summary>
        /// Indique si le capteur est activé
        /// </summary>
        bool IsActivated { get; }
    }
}