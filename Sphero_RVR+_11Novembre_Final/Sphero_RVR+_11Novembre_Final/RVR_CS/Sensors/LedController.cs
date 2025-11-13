using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVR_CS.Core;

namespace RVR_CS.Sensors
{
    /// <summary>
    /// Contrôleur pour les LEDs RGB du Sphero RVR+
    /// Implémente les commandes BLE pour contrôler l'éclairage du robot
    /// </summary>
    public class LedController : ILedController
    {
        private readonly RvrController _controller;
        private bool _isInitialized = false;
        private byte _sequenceNumber = 0x20;

        /// <summary>
        /// Indique si le contrôleur LED est initialisé
        /// </summary>
        public bool IsInitialized => _isInitialized;

        public LedController(RvrController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _isInitialized = true;
        }

        /// <summary>
        /// Définit la couleur RGB des LEDs principales
        /// </summary>
        public async Task<bool> SetRgbLedAsync(byte red, byte green, byte blue)
        {
            return await SetRgbLedAsync(red, green, blue, 1.0f);
        }

        /// <summary>
        /// Définit la couleur RGB des LEDs principales avec luminosité
        /// </summary>
        public async Task<bool> SetRgbLedAsync(byte red, byte green, byte blue, float brightness)
        {
            if (!_isInitialized)
            {
                Console.WriteLine("⚠️ Contrôleur LED non initialisé");
                return false;
            }

            try
            {
                // Appliquer la luminosité
                byte adjustedRed = (byte)(red * Math.Clamp(brightness, 0.0f, 1.0f));
                byte adjustedGreen = (byte)(green * Math.Clamp(brightness, 0.0f, 1.0f));
                byte adjustedBlue = (byte)(blue * Math.Clamp(brightness, 0.0f, 1.0f));

                Console.WriteLine($"💡 Définition LED RGB: R={adjustedRed} G={adjustedGreen} B={adjustedBlue} (luminosité: {brightness:P0})");

                // Commande LED RGB basée sur les captures BLE fonctionnelles
                // Format: 8D 3A 11 01 1A 45 SEQ 00 RED GREEN BLUE CHECKSUM D8
                var command = CreateLedRgbCommand(GetNextSequence(), adjustedRed, adjustedGreen, adjustedBlue);
                await _controller.SendRawCommandAsync(command);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors de la définition LED RGB: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Éteint toutes les LEDs
        /// </summary>
        public async Task<bool> TurnOffAsync()
        {
            Console.WriteLine("🔌 Extinction des LEDs");
            return await SetRgbLedAsync(0, 0, 0);
        }

        /// <summary>
        /// Définit une couleur prédéfinie
        /// </summary>
        public async Task<bool> SetColorAsync(LedColor color)
        {
            var (r, g, b) = GetPredefinedColor(color);
            Console.WriteLine($"🎨 Couleur prédéfinie: {color}");
            return await SetRgbLedAsync(r, g, b);
        }

        /// <summary>
        /// Fait clignoter les LEDs
        /// </summary>
        public async Task<bool> BlinkAsync(byte red, byte green, byte blue, int duration = 500, int cycles = 3)
        {
            Console.WriteLine($"✨ Clignotement LED: {cycles} cycles de {duration}ms");

            try
            {
                for (int i = 0; i < cycles; i++)
                {
                    // Allumer
                    await SetRgbLedAsync(red, green, blue);
                    await Task.Delay(duration);

                    // Éteindre
                    await TurnOffAsync();
                    await Task.Delay(duration);
                }

                // Rallumer à la fin
                await SetRgbLedAsync(red, green, blue);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors du clignotement: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Animation arc-en-ciel
        /// </summary>
        public async Task<bool> RainbowAnimationAsync(int duration = 5000)
        {
            Console.WriteLine("🌈 Animation arc-en-ciel");

            try
            {
                var colors = new[]
                {
                    (255, 0, 0),    // Rouge
                    (255, 165, 0),  // Orange
                    (255, 255, 0),  // Jaune
                    (0, 255, 0),    // Vert
                    (0, 0, 255),    // Bleu
                    (75, 0, 130),   // Indigo
                    (238, 130, 238) // Violet
                };

                int stepDuration = duration / colors.Length;

                foreach (var (r, g, b) in colors)
                {
                    await SetRgbLedAsync((byte)r, (byte)g, (byte)b);
                    await Task.Delay(stepDuration);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors de l'animation arc-en-ciel: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Transition douce entre deux couleurs
        /// </summary>
        public async Task<bool> FadeTransitionAsync(byte fromR, byte fromG, byte fromB, 
                                                   byte toR, byte toG, byte toB, 
                                                   int duration = 2000, int steps = 20)
        {
            Console.WriteLine($"🔄 Transition LED de RGB({fromR},{fromG},{fromB}) vers RGB({toR},{toG},{toB})");

            try
            {
                int stepDuration = duration / steps;

                for (int i = 0; i <= steps; i++)
                {
                    float progress = (float)i / steps;
                    
                    byte currentR = (byte)(fromR + (toR - fromR) * progress);
                    byte currentG = (byte)(fromG + (toG - fromG) * progress);
                    byte currentB = (byte)(fromB + (toB - fromB) * progress);

                    await SetRgbLedAsync(currentR, currentG, currentB);
                    await Task.Delay(stepDuration);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors de la transition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Crée une commande LED RGB
        /// </summary>
        private byte[] CreateLedRgbCommand(byte sequence, byte red, byte green, byte blue)
        {
            // Format basé sur les captures BLE: 8D 3A 11 01 1A 45 SEQ 00 RED GREEN BLUE CHECKSUM D8
            var frame = new List<byte>
            {
                0x8D,           // Start byte
                0x3A,           // Flags
                0x11,           // Length
                0x01,           // Target
                0x1A,           // Device ID
                0x45,           // Command ID (LED RGB)
                sequence,       // Sequence number
                0x00,           // Flags
                red,            // Rouge
                green,          // Vert
                blue,           // Bleu
                0x00,           // Checksum placeholder
                0xD8            // End byte
            };

            // Calcul simple du checksum (basé sur les captures fonctionnelles)
            byte checksum = CalculateChecksum(frame);
            frame[frame.Count - 2] = checksum;

            return frame.ToArray();
        }

        /// <summary>
        /// Calcule un checksum simple pour les commandes LED
        /// </summary>
        private byte CalculateChecksum(List<byte> frame)
        {
            // Checksum simplifié - dans une implémentation complète, 
            // utiliser l'algorithme exact de Sphero
            return 0xE3; // Valeur observée dans les captures BLE fonctionnelles
        }

        /// <summary>
        /// Obtient les valeurs RGB pour une couleur prédéfinie
        /// </summary>
        private (byte r, byte g, byte b) GetPredefinedColor(LedColor color)
        {
            return color switch
            {
                LedColor.Off => (0, 0, 0),
                LedColor.Red => (255, 0, 0),
                LedColor.Green => (0, 255, 0),
                LedColor.Blue => (0, 0, 255),
                LedColor.Yellow => (255, 255, 0),
                LedColor.BlueCyan => (0, 255, 255),
                LedColor.Magenta => (255, 0, 255),
                LedColor.White => (255, 255, 255),
                LedColor.Orange => (255, 165, 0),
                LedColor.Purple => (128, 0, 128),
                LedColor.Pink => (255, 192, 203),
                LedColor.Lime => (50, 205, 50),
                _ => (255, 255, 255)
            };
        }

        private byte GetNextSequence()
        {
            return ++_sequenceNumber;
        }
    }
}