using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using RVR_CS.Core;
using System.Diagnostics;

namespace RVR_CS.Sensors
{
    /// <summary>
    /// Gestionnaire du capteur de couleur Sphero RVR+ avec séquence d'activation officielle
    /// </summary>
    public class ColorSensorManager : IColorSensor
    {
        private readonly RvrController _controller;
        private bool _isActivated = false;
        private byte _sequenceNumber = 0x10;
        private const int SINGLE_READ_TIMEOUT_MS = 2000;
        private const int SINGLE_READ_MIN_SAMPLES = 5;
        private const int SINGLE_READ_MAX_SAMPLES = 15;
        private const int SINGLE_READ_SAMPLE_WINDOW_MS = 600;


        /// <summary>
        /// Événement déclenché quand une nouvelle couleur est détectée
        /// </summary>
        public event Action<SensorData>? ColorDetected;

        /// <summary>
        /// Indique si le capteur est activé
        /// </summary>
        public bool IsActivated => _isActivated;

        public ColorSensorManager(RvrController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>
        /// Gestionnaire interne pour rediriger les données du contrôleur vers notre événement
        /// </summary>
        private void OnControllerColorDataReceived((byte r, byte g, byte b, byte index, byte confidence) colorData)
        {
            var sensorData = new SensorData
            {
                Color = new ColorSensor(colorData.r, colorData.g, colorData.b, colorData.index, colorData.confidence),
                LastUpdate = DateTime.Now
            };
            
            ColorDetected?.Invoke(sensorData);
        }

        /// <summary>
        /// Démarre le streaming continu avec événements
        /// </summary>
        public async Task<bool> StartStreamingWithEventsAsync()
        {
            if (!_isActivated)
            {
                var activated = await ActivateAsync();
                if (!activated) return false;
            }

            try
            {
                Trace.WriteLine("?? Démarrage streaming avec événements...");
                
                // S'abonner aux données pour rediriger vers notre événement
                _controller.ColorDataReceived += OnControllerColorDataReceived;
                
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur démarrage streaming: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Arrête le streaming continu
        /// </summary>
        public Task<bool> StopStreamingWithEventsAsync()
        {
            try
            {
                Trace.WriteLine("?? Arrêt streaming...");
                
                // Se désabonner
                _controller.ColorDataReceived -= OnControllerColorDataReceived;
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur arrêt streaming: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Active le capteur de couleur avec la séquence officielle complète
        /// </summary>
        /// <returns>True si l'activation réussit</returns>
        public async Task<bool> ActivateAsync()
        {
            if (_isActivated)
            {
                Trace.WriteLine("? Capteur de couleur déjà activé");
                return true;
            }

            try
            {
                Trace.WriteLine("?? Activation du capteur de couleur...");
                
                // Séquence officielle basée sur la capture BLE fonctionnelle
                await ExecuteOfficialActivationSequence();
                
                _isActivated = true;
                Trace.WriteLine("? Capteur de couleur activé avec succès!");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur lors de l'activation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Désactive le capteur de couleur
        /// </summary>
        /// <returns>True si la désactivation réussit</returns>
        public async Task<bool> DeactivateAsync()
        {
            if (!_isActivated)
            {
                Trace.WriteLine("?? Capteur de couleur déjà désactivé");
                return true;
            }

            try
            {
                Trace.WriteLine("?? Désactivation du capteur de couleur...");
                
                // Envoyer commande de désactivation (éteindre la LED)
                byte seq = GetNextSequence();
                await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x38, seq, 0x00, 0xFF, 0xD8 });
                
                _isActivated = false;
                Trace.WriteLine("? Capteur de couleur désactivé avec succès!");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur désactivation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lit une couleur du capteur (requiert activation préalable)
        /// IMPORTANT: Après l'activation, les données arrivent automatiquement via notifications BLE
        /// Format: 8D 38 11 01 18 3D FF 01 FF [R] [G] [B] FF 00 [CHK] D8
        /// </summary>
        /// <returns>Structure ColorReading avec les valeurs RGB</returns>
        public async Task<ColorReading?> ReadColorAsync()
        {
            try
            {
                var sensorData = await WaitForSingleColorAsync();
                if (!sensorData.HasValue)
                {
                    return null;
                }

                var color = sensorData.Value.Color;
                return new ColorReading
                {
                    R = color.Red,
                    G = color.Green,
                    B = color.Blue,
                    Timestamp = sensorData.Value.LastUpdate
                };
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur lecture couleur: {ex.Message}");
                return null;
            }
        }

        ///// <summary>
        ///// Lit UNE SEULE couleur sur demande (pas de streaming)
        ///// </summary>
        ///// <returns>Les données de couleur lues ou null si échec</returns>
        //public async Task<SensorData?> ReadSingleColorAsync()
        //{
        //    try
        //    {
        //        Trace.WriteLine("?? Lecture ponctuelle de couleur...");
        //        var result = await WaitForSingleColorAsync();
        //        if (result.HasValue)
        //        {
        //            Trace.WriteLine($"? Couleur lue: {result.Value}");
        //        }
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.WriteLine($"? Erreur lecture ponctuelle: {ex.Message}");
        //        return null;
        //    }
        //}

        /// <summary>
        /// Attend une mesure ponctuelle du capteur de couleur via les notifications BLE
        /// </summary>
        private async Task<SensorData?> WaitForSingleColorAsync(int timeoutMs = SINGLE_READ_TIMEOUT_MS)
        {
            if (!_isActivated)
            {
                var activated = await ActivateAsync();
                if (!activated)
                {
                    Trace.WriteLine("?? Impossible de lire la couleur - activation requise.");
                    return null;
                }
            }

            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var samples = new List<SensorData>(SINGLE_READ_MAX_SAMPLES);
            using var sampleWindowCts = new CancellationTokenSource(SINGLE_READ_SAMPLE_WINDOW_MS);
            sampleWindowCts.Token.Register(() => completionSource.TrySetResult(true));

            void Handler((byte r, byte g, byte b, byte index, byte confidence) colorData)
            {
                if (completionSource.Task.IsCompleted || samples.Count >= SINGLE_READ_MAX_SAMPLES)
                {
                    return;
                }

                var sensorData = new SensorData
                {
                    Color = new ColorSensor(colorData.r, colorData.g, colorData.b, colorData.index, colorData.confidence),
                    LastUpdate = DateTime.Now
                };

                samples.Add(sensorData);

                if (samples.Count >= SINGLE_READ_MIN_SAMPLES)
                {
                    completionSource.TrySetResult(true);
                }
            }

            _controller.ColorDataReceived += Handler;

            try
            {
                await SendSingleColorReadCommand();

                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);

                if (!samples.Any())
                {
                    Trace.WriteLine("?? Timeout - aucune donnée de couleur reçue");
                    return null;
                }

                if (completedTask != completionSource.Task)
                {
                    Trace.WriteLine("?? Lecture partielle - utilisation des échantillons disponibles");
                }

                var avgR = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(samples.Average(s => s.Color.Red))));
                var avgG = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(samples.Average(s => s.Color.Green))));
                var avgB = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(samples.Average(s => s.Color.Blue))));
                var bestSample = samples
                    .OrderByDescending(s => s.Color.Confidence)
                    .ThenByDescending(s => s.LastUpdate)
                    .First();

                return new SensorData
                {
                    Color = new ColorSensor(avgR, avgG, avgB, bestSample.Color.Index, bestSample.Color.Confidence),
                    LastUpdate = bestSample.LastUpdate
                };
            }
            finally
            {
                _controller.ColorDataReceived -= Handler;
            }
        }

        /// <summary>
        /// Envoie une commande pour lire UNE couleur (pas de streaming)
        /// </summary>
        private async Task SendSingleColorReadCommand()
        {
            try
            {
                // Commande de lecture ponctuelle basée sur les commandes qui fonctionnent
                // Cette commande utilise 0x3A (lecture immédiate) au lieu de 0x39 (streaming)
                byte seq = GetNextSequence();
                
                // Commande inspirée du frame 20 qui fait une lecture ponctuelle
                var command = new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3A, seq, 0x00, 0x96, 0x61, 0xD8 };
                
                Trace.WriteLine($"?? Envoi commande lecture unique: {string.Join(" ", command.Select(b => b.ToString("X2")))}");
                await _controller.SendRawCommandAsync(command);
                
                await Task.Delay(100); // Petit délai pour traitement
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"? Erreur envoi commande lecture: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exécute la séquence d'activation officielle EXACTE de l'app Sphero
        /// Basée sur la capture BLE réelle qui fonctionne
        /// </summary>
        private async Task ExecuteOfficialActivationSequence()
        {
            Trace.WriteLine("?? === SÉQUENCE OFFICIELLE SPHERO ===");
            Trace.WriteLine("Basée sur la capture BLE de l'application officielle");
            
            // FRAME 1: Wake command
            Trace.WriteLine("?? [1/29] Wake command...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x13, 0x10, 0x58, 0x38, 0xD8 });
            await Task.Delay(3000); // Délai important après wake
            
            // FRAME 2: Config 0x1A 0x2F
            Trace.WriteLine("?? [2/29] Config command 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x1A, 0x2F, 0x59, 0x00, 0x00, 0x00, 0x11, 0xD8 });
            await Task.Delay(50);
            
            // FRAME 3: LED Matrix setup 0x1A 0x1A
            Trace.WriteLine("?? [3/29] LED Matrix setup...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x1A, 0x1A, 0x5A, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE6, 0xD8 });
            await Task.Delay(20);
            
            // FRAME 4: Config 0x1A 0x2F
            Trace.WriteLine("?? [4/29] Config command 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x1A, 0x2F, 0x5B, 0x00, 0x00, 0x00, 0x0F, 0xD8 });
            await Task.Delay(7);
            
            // FRAME 5: Power Management 0x16 0x07
            Trace.WriteLine("? [5/29] Power management...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x16, 0x07, 0x5C, 0x00, 0x00, 0x00, 0x00, 0x39, 0xD8 });
            await Task.Delay(9);
            
            // FRAME 6: 0x18 0x13
            Trace.WriteLine("?? [6/29] Sensor setup 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x13, 0x5D, 0x2A, 0xD8 });
            await Task.Delay(6);
            
            // FRAME 7: 0x16 0x06
            Trace.WriteLine("?? [7/29] Power config...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x16, 0x06, 0x5E, 0x38, 0xD8 });
            await Task.Delay(24);
            
            // FRAME 8: Stream config 0x18 0x3B
            Trace.WriteLine("?? [8/29] Stream config 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3B, 0x5F, 0x01, 0xD8 });
            await Task.Delay(35);
            
            // FRAME 9-11: More stream configs
            Trace.WriteLine("?? [9/29] Stream config 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3B, 0x60, 0xFF, 0xD8 });
            await Task.Delay(9);
            
            Trace.WriteLine("?? [10/29] Stream config 3...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3B, 0x61, 0xFF, 0xD8 });
            await Task.Delay(8);
            
            Trace.WriteLine("?? [11/29] Stream config 4...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3B, 0x62, 0xFD, 0xD8 });
            await Task.Delay(11);
            
            // FRAME 12: 0x18 0x38
            Trace.WriteLine("? [12/29] Data config 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x38, 0x63, 0x01, 0xFF, 0xD8 });
            await Task.Delay(8);
            
            // FRAME 13-16: Sensor configs 0x18 0x3C
            Trace.WriteLine("?? [13/29] Sensor config 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3C, 0x64, 0xFB, 0xD8 });
            await Task.Delay(8);
            
            Trace.WriteLine("?? [14/29] Sensor config 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3C, 0x65, 0xF9, 0xD8 });
            await Task.Delay(8);
            
            Trace.WriteLine("?? [15/29] Sensor config 3...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3C, 0x66, 0xF9, 0xD8 });
            await Task.Delay(32);
            
            Trace.WriteLine("?? [16/29] Sensor config 4...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3C, 0x67, 0xF7, 0xD8 });
            await Task.Delay(13);
            
            // FRAME 17-18: Start streaming 0x18 0x39
            Trace.WriteLine("?? [17/29] Start streaming 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x39, 0x68, 0x01, 0x00, 0x03, 0x00, 0xF6, 0xD8 });
            await Task.Delay(10);
            
            Trace.WriteLine("?? [18/29] Start streaming 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x39, 0x69, 0x02, 0x00, 0x0A, 0x02, 0xEB, 0xD8 });
            await Task.Delay(7);
            
            // FRAME 19: 0x18 0x3A
            Trace.WriteLine("?? [19/29] Stream setup 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3A, 0x6A, 0x00, 0x96, 0x61, 0xD8 });
            await Task.Delay(9);
            
            // FRAME 20-22: More streaming configs
            Trace.WriteLine("? [20/29] Start streaming 3...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x39, 0x6B, 0x01, 0x00, 0x03, 0x00, 0xF3, 0xD8 });
            await Task.Delay(10);
            
            Trace.WriteLine("?? [21/29] Start streaming 4...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x39, 0x6C, 0x02, 0x00, 0x0A, 0x02, 0xE8, 0xD8 });
            await Task.Delay(7);
            
            Trace.WriteLine("?? [22/29] Stream setup 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x3A, 0x6D, 0x00, 0x96, 0x5E, 0xD8 });
            await Task.Delay(9);
            
            // FRAME 23-24: Complex streaming configs
            Trace.WriteLine("?? [23/29] Complex stream config 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x39, 0x6E, 0x01, 0x00, 0x01, 0x02, 0x00, 0x02, 0x02, 0x00, 0x04, 0x02, 0xE5, 0xD8 });
            await Task.Delay(10);
            
            Trace.WriteLine("?? [24/29] Complex stream config 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x39, 0x6F, 0x02, 0x00, 0x06, 0x02, 0x00, 0x07, 0x02, 0xDF, 0xD8 });
            await Task.Delay(8);
            
            Trace.WriteLine("?? [25/29] Stream setup 3...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3A, 0x70, 0x00, 0x96, 0x5A, 0xD8 });
            await Task.Delay(7);
            
            // FRAME 26-28: Final streaming configs
            Trace.WriteLine("? [26/29] Final stream config 1...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x39, 0x71, 0x01, 0x00, 0x01, 0x02, 0x00, 0x02, 0x02, 0x00, 0x04, 0x02, 0xE2, 0xD8 });
            await Task.Delay(10);
            
            Trace.WriteLine("?? [27/29] Final stream config 2...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x39, 0x72, 0x02, 0x00, 0x06, 0x02, 0x00, 0x07, 0x02, 0xDC, 0xD8 });
            await Task.Delay(7);
            
            Trace.WriteLine("?? [28/29] Final stream setup...");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x12, 0x01, 0x18, 0x3A, 0x73, 0x00, 0x96, 0x57, 0xD8 });
            await Task.Delay(200); // Délai important avant la LED finale
            
            // FRAME 29: THE MAGIC COMMAND - LED RGB qui allume le capteur!
            Trace.WriteLine("?? [29/29] *** COMMANDE MAGIQUE LED RGB *** ");
            Trace.WriteLine("?? Cette commande allume la LED du capteur !");
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x1A, 0x45, 0x74, 0x00, 0xFF, 0xFF, 0xFF, 0xE3, 0xD8 });
            
            // *** AJOUT CRITIQUE *** - Commande manquante identifiée dans les logs officiels
            Trace.WriteLine("?? [CRITIQUE] Configure color sensor streaming...");
            Trace.WriteLine("?? Cette commande déclenche l'envoi automatique des données couleur!");
            var seq = GetNextSequence();
            await SendRawCommand(new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x18, 0x39, seq, 0x01, 0x00, 0x03, 0x00, 0x3C, 0xD8 });
            await Task.Delay(500); // Délai pour que le streaming démarre
            
            Trace.WriteLine("? SÉQUENCE OFFICIELLE TERMINÉE !");
            Trace.WriteLine("?? La LED du capteur devrait maintenant être ALLUMÉE !");
            Trace.WriteLine("?? Les données de couleur arrivent automatiquement via notifications BLE!");
        }

        /// <summary>
        /// Envoie une commande brute sans modification
        /// </summary>
        private async Task SendRawCommand(byte[] command)
        {
            await _controller.SendRawCommandAsync(command);
        }

        ///// <summary>
        ///// Crée une commande de lecture de couleur
        ///// </summary>
        //private byte[] CreateColorReadCommand(byte sequence)
        //{
        //    // 8D 3A 11 01 1A 2F SEQ CHECKSUM D8
        //    var frame = new byte[] { 0x8D, 0x3A, 0x11, 0x01, 0x1A, 0x2F, sequence, 0x00, 0xD8 };
            
        //    // Calcul du checksum (simple pour cet exemple)
        //    byte checksum = 0x85; // Valeur basée sur la capture BLE
        //    frame[7] = checksum;
            
        //    return frame;
        //}

        ///// <summary>
        ///// Crée une trame Sphero avec calcul automatique de checksum
        ///// </summary>
        //private byte[] CreateFrame(byte flag, byte len, byte did, byte cid, byte seq, params byte[] payload)
        //{
        //    var frame = new List<byte> { 0x8D, flag, len, 0x01, did, cid, seq };
        //    if (payload != null) frame.AddRange(payload);
            
        //    // Checksum simplifié pour l'exemple
        //    frame.Add(0x00); // Placeholder checksum
        //    frame.Add(0xD8);
            
        //    return frame.ToArray();
        //}

        //private byte[] CreateFrame(byte flag, byte len, byte b1, byte did, byte cid, byte seq, params byte[] payload)
        //{
        //    var frame = new List<byte> { 0x8D, flag, len, b1, did, cid, seq };
        //    if (payload != null) frame.AddRange(payload);
            
        //    // Checksum simplifié 
        //    frame.Add(0x00); // Placeholder
        //    frame.Add(0xD8);
            
        //    return frame.ToArray();
        //}

        //private async Task SendCommand(byte[] command)
        //{
        //    await _controller.SendRawCommandAsync(command);
        //}

        private byte GetNextSequence()
        {
            return ++_sequenceNumber;
        }
    }

    /// <summary>
    /// Structure pour les données de couleur lues avec détection automatique de couleur
    /// </summary>
    public struct ColorReading
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Propriétés d'accès compatibles avec l'ancien code
        /// </summary>
        public byte Red => R;
        public byte Green => G;
        public byte Blue => B;

        /// <summary>
        /// Couleur détectée automatiquement basée sur les valeurs RGB
        /// </summary>
        public LedColor DetectedColor => GetDetectedColor();


		public LedColor GetDetectedColor()
		{
			// On copie les valeurs d'instance dans des variables locales
			int r = R;
			int g = G;
			int b = B;

			RgbToHsv(r, g, b, out double h, out double s, out double v);
			return HueToLed(h, s, v, r, g, b);
		}

		// Helpers statiques = ne capturent pas "this"
		private static void RgbToHsv(int r, int g, int b, out double h, out double s, out double v)
		{
			double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));
			double delta = max - min;

			v = max;
			s = (max <= 0.0) ? 0.0 : (delta / max);

			if (delta == 0.0)
			{
				h = 0.0;
			}
			else if (max == rd)
			{
				h = 60.0 * (((gd - bd) / delta) % 6.0);
			}
			else if (max == gd)
			{
				h = 60.0 * (((bd - rd) / delta) + 2.0);
			}
			else // max == bd
			{
				h = 60.0 * (((rd - gd) / delta) + 4.0);
			}

			if (h < 0) h += 360.0;
		}

		private static LedColor HueToLed(double h, double s, double v, int r, int g, int b)
		{
			// Seuils globaux
			const double OFF_V = 0.12;   // très sombre → Off
			const double LOW_V = 0.25;   // sombre
			const double LOW_S = 0.12;   // désaturé → blanc/gris

			// 1) Noir / éteint
			if (v < OFF_V)
				return LedColor.Off;

			// 2) Blanc / gris (peu saturé)
			if (s < LOW_S)
			{
				if (v > 0.85) return LedColor.White;   // blanc franc
				if (v < LOW_V) return LedColor.Off;    // quasi noir
				return LedColor.White;                 // gris → on mappe sur White
			}

			// 3) Dominances directes pour affiner la zone bleu-vert
			bool greenDominant = g > r + 25 && g >= b - 5;
			bool blueDominant = b > r + 25 && b >= g + 5;

			// 4) Plages de teinte
			if (h >= 345 || h < 15) return LedColor.Red;
			if (h < 45) return LedColor.Orange;
			if (h < 70) return LedColor.Yellow;
			if (h < 95) return LedColor.Lime;
			if (h < 150) return LedColor.Green;

			// Zone bleu-vert : 150–195°
			if (h < 195)
			{
				if (greenDominant) return LedColor.Green; // vert tirant sur le bleu
				if (blueDominant) return LedColor.Blue;  // bleu tirant sur le vert
				return LedColor.BlueCyan;                 // vrai cyan
			}

			if (h < 255) return LedColor.Blue;
			if (h < 285) return LedColor.Purple;
			if (h < 330) return LedColor.Magenta;

			// 330–345 → Rose si assez lumineux, sinon magenta
			return (v > 0.6) ? LedColor.Pink : LedColor.Magenta;
		}

		/// <summary>
		/// Retourne le nom de la couleur détectée en français
		/// </summary>
		public string GetColorNameFrench()
        {
            return DetectedColor switch
            {
                LedColor.Off => "Éteint",
                LedColor.Red => "Rouge",
                LedColor.Green => "Vert",
                LedColor.Blue => "Bleu",
                LedColor.Yellow => "Jaune",
                LedColor.BlueCyan => "Cyan",
                LedColor.Magenta => "Magenta",
                LedColor.White => "Blanc",
                LedColor.Orange => "Orange",
                LedColor.Purple => "Violet",
                LedColor.Pink => "Rose",
                LedColor.Lime => "Vert Lime",
                _ => "Inconnu"
            };
        }

        public override string ToString()
        {
            return $"RGB({R}, {G}, {B}) -> {GetColorNameFrench()} [{Timestamp:HH:mm:ss.fff}]";
        }
    }
}
