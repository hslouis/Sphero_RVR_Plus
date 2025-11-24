using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Sphero_RVR_Plus_CS.Sensors;

namespace Sphero_RVR_Plus_CS.Core
{
	/// <summary>
	/// High-level RVR+ controller with motor & sensors
	/// </summary>
	public class RvrController
	{
		private readonly string _deviceName;
		private RvrBleConnection? _connection;
		private byte _sequenceNumber = 0;
		private TaskCompletionSource<byte[]?>? _pendingResponse;
		private byte? _expectedCid;
		private readonly List<byte> _recvBuffer = new();

		// Last values (optional caches)
		private double? _lastAmbient;
		private SensorData _sensorData;
		private double _totalDistance = 0.0;
		private double _leftWheelDistance = 0.0;
		private double _rightWheelDistance = 0.0;

#pragma warning disable CS0414, CS0169 // Unused fields - kept for future extensibility
		private (int left, int right)? _lastEnc;
		private (double ax, double ay, double az, double gx, double gy, double gz)? _lastImu;
		private (byte r, byte g, byte b, byte index, byte confidence)? _lastColor;
		private bool _colorActivated;
#pragma warning restore CS0414, CS0169

		private bool _colorStreaming;
		// Diagnostic: try writing via notify characteristic instead of command characteristic
		private bool _preferNotifyWrite = false;

		// DID/CIDs
		private const byte DID_SENSORS = 0x18;
		private const byte DID_DRIVE = 0x16;
		private const byte DID_SYSTEM = 0x11;
		private const byte CID_CONFIGURE_COLOR = 0x27; // legacy
		private const byte CID_COLOR_STREAM = 0x0F;    // color data stream
		private const byte CID_ENABLE_COLOR_NODE = 0x26; // RVR+
		private const byte CID_COLOR_LED = 0x2B;         // RVR+
		private const byte CID_COLOR_DET_NOTIFY = 0x2C;  // RVR+ enable color detection notifications
		private const byte CID_DRIVE_TANK = 0x01;        // tank drive command
		private const byte CID_RESET_ENCODERS = 0x21;    // reset encoder values
		private const byte CID_READ_ENCODERS = 0x22;     // read encoder values
		private const byte CID_SET_RGB_LED = 0x1A;       // set RGB LED
														 // Candidate CIDs for streaming-service control (based on SDK patterns)
		private const byte CID_CFG_STREAM_SERVICE = 0x39;   // configure services in a slot
		private const byte CID_START_STREAM_SERVICE = 0x3A; // start streaming services
		private const byte CID_STOP_STREAM_SERVICE = 0x3B;  // stop streaming services
		private const byte CID_CLEAR_STREAM_SERVICE = 0x3C; // clear services

		public event Action<byte[]>? RawDataReceived;
		public event Action<(byte r, byte g, byte b, byte index, byte confidence)>? ColorDataReceived;

		// Public properties to expose characteristics for diagnostic tests
		public Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic? CmdCharacteristic => _connection?.CmdCharacteristic;
		public Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic? NotifyCharacteristic => _connection?.NotifyCharacteristic;

		/// <summary>
		/// Indique si le robot est connecté
		/// </summary>
		public bool IsConnected => _connection != null;

		/// <summary>
		/// Données actuelles de tous les capteurs
		/// </summary>
		public SensorData CurrentSensorData => _sensorData;

		/// <summary>
		/// Dernière couleur détectée
		/// </summary>
		public ColorSensor LastDetectedColor => _sensorData.Color;

		/// <summary>
		/// Distance totale parcourue
		/// </summary>
		public double TotalDistance => _totalDistance;

		public RvrController(string deviceName)
		{
			_deviceName = deviceName;
			_sensorData = new SensorData();
		}
		/// <summary>
		/// Connexion async au Sphero RVR+ par BlueTooth Low Energy
		/// </summary>
		/// <returns></returns>
		public async Task<bool> ConnectAsync()
		{
			// Prefer indications can help some stacks deliver reliable notifications
			_connection = new RvrBleConnection(_deviceName, preferIndications: true);
			if (await _connection.ConnectAsync())
			{
				_connection.DataReceived += OnDataReceived;
				await SendWakeCommandAsync();
				return true;
			}
			throw new Exception("BLE device not found, 💡 Ensure RVR+ is on, in BLE pairing mode (LEDs flashing), and in range.");
			return false;
		}

		/// <summary>
		/// Déconnexion async au Sphero RVR+ 
		/// </summary>
		/// <returns></returns>
		public async Task DisconnectAsync()
		{
			if (_connection != null)
			{
				_connection.DataReceived -= OnDataReceived;
				await _connection.DisconnectAsync();
			}
		}

		/// <summary>
		/// Envoie une commande brute (pour ColorSensorManager)
		/// </summary>
		public async Task<bool> SendRawCommandAsync(byte[] command)
		{
			if (_connection == null)
				return false;

			try
			{
				var result = await _connection.SendCommandAsync(command);
				if (result)
				{
					Trace.WriteLine($"🔥 WRITE: {BitConverter.ToString(command).Replace("-", "")}");
					Trace.WriteLine("✅ WriteWithoutResponse => Success");
				}
				else
				{
					Trace.WriteLine("❌ WriteWithoutResponse => Failed");
				}
				return result;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ SendRawCommandAsync error: {ex.Message}");
				return false;
			}
		}

		private async Task SendWakeCommandAsync()
		{
			if (_connection == null) return;
			var seq = (byte)Math.Max(_sequenceNumber, (byte)0x01);
			Trace.WriteLine("🌅 Sending wake command...");
			var pkt0 = BuildJavaRawPacket(0x00, 0x13, 0x0D, seq, Array.Empty<byte>());
			var ok = await _connection.SendCommandAsync(pkt0);
			_sequenceNumber = (byte)(seq + 1);
			if (!ok)
			{
				// Retry with alt flag and brief delay
				await Task.Delay(150);
				var pkt1 = BuildJavaRawPacket(0x02, 0x13, 0x0D, _sequenceNumber, Array.Empty<byte>());
				Trace.WriteLine($"↪️ Wake retry with flag 0x02 (seq={_sequenceNumber})");
				await _connection.SendCommandAsync(pkt1);
				_sequenceNumber++;
			}
			await Task.Delay(1200); // Increased delay for RVR+ initialization
		}

		// --- Motors ---
		public async Task SetMotorsAsync(int leftSpeed, int rightSpeed)
		{
			if (_connection == null) return;
			int cl = Math.Clamp(leftSpeed, -255, 255);
			int cr = Math.Clamp(rightSpeed, -255, 255);
			byte leftMode = (byte)(cl > 0 ? 0x01 : (cl < 0 ? 0x02 : 0x00));
			byte rightMode = (byte)(cr > 0 ? 0x01 : (cr < 0 ? 0x02 : 0x00));
			byte leftMag = (byte)Math.Abs(cl);
			byte rightMag = (byte)Math.Abs(cr);
			var payload = new byte[] { leftMode, leftMag, rightMode, rightMag };
			var packet = BuildJavaRawPacket(0x02, 0x16, 0x01, _sequenceNumber, payload);
			Trace.WriteLine($"🚗 Motor command: L{leftSpeed} R{rightSpeed} (seq: {_sequenceNumber})");
			Trace.WriteLine($"   Packet: {BitConverter.ToString(packet).Replace("-", "")}");
			await _connection.SendCommandAsync(packet);
			_sequenceNumber++;
		}

		/// <summary>
		/// Fait tourner le robot Sphero RVR+ vers la droite avec précision améliorée
		/// </summary>
		/// <param name="degrees">Nombre de degrés de rotation (0-255)</param>
		/// <param name="turnSpeed">Vitesse de rotation (par défaut: 100)</param>
		public async Task TurnRightAsync(byte degrees, int turnSpeed = 100)
		{
			if (_connection == null) return;

			// Validation des paramètres
			if (degrees == 0) return;

			int speed = Math.Clamp(turnSpeed, 30, 200); // Limites raisonnables de vitesse

			Trace.WriteLine($"🔄 Turning RIGHT {degrees}° at speed {speed}");

			// CALIBRAGE AMÉLIORÉ - Calcul plus précis du temps de rotation
			// Tests empiriques montrent qu'environ 12ms par degré donne une meilleure précision
			// Formule ajustée avec correction non-linéaire pour différentes vitesses
			double baseTiming = 12.0; // ms par degré à vitesse 100

			// Correction non-linéaire pour les vitesses différentes
			double speedFactor;
			if (speed >= 100)
			{
				// Vitesses élevées : relation quasi-linéaire
				speedFactor = 100.0 / speed;
			}
			else
			{
				// Vitesses faibles : correction non-linéaire (plus de friction)
				speedFactor = (100.0 / speed) * (1.0 + (100.0 - speed) / 200.0);
			}

			// Correction supplémentaire pour les petits angles (moins d'inertie)
			double angleFactor = 1.0;
			if (degrees < 45)
			{
				angleFactor = 0.85; // Les petits angles sont plus rapides
			}
			else if (degrees > 180)
			{
				angleFactor = 1.05; // Les grands angles ont plus d'inertie
			}

			// Calcul final du temps de rotation
			double timePerDegreeMs = baseTiming * speedFactor * angleFactor;
			int rotationTimeMs = (int)(degrees * timePerDegreeMs);

			Trace.WriteLine($"   📊 Timing calculé: {timePerDegreeMs:F2}ms/degré → {rotationTimeMs}ms total");

			// Pour tourner à droite : roue gauche avance, roue droite recule
			await SetMotorsAsync(speed, -speed);

			// Attendre le temps de rotation calculé
			await Task.Delay(rotationTimeMs);

			// Arrêter les moteurs
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Right turn of {degrees}° completed (precision enhanced)");
		}

		/// <summary>
		/// Fait tourner le robot Sphero RVR+ vers la gauche avec précision améliorée
		/// </summary>
		/// <param name="degrees">Nombre de degrés de rotation (0-255)</param>
		/// <param name="turnSpeed">Vitesse de rotation (par défaut: 100)</param>
		public async Task TurnLeftAsync(byte degrees, int turnSpeed = 100)
		{
			if (_connection == null) return;

			// Validation des paramètres
			if (degrees == 0) return;

			int speed = Math.Clamp(turnSpeed, 30, 200); // Limites raisonnables de vitesse

			Trace.WriteLine($"🔄 Turning LEFT {degrees}° at speed {speed}");

			// CALIBRAGE AMÉLIORÉ - Calcul plus précis du temps de rotation
			// Tests empiriques montrent qu'environ 12ms par degré donne une meilleure précision
			// Formule ajustée avec correction non-linéaire pour différentes vitesses
			double baseTiming = 12.0; // ms par degré à vitesse 100

			// Correction non-linéaire pour les vitesses différentes
			double speedFactor;
			if (speed >= 100)
			{
				// Vitesses élevées : relation quasi-linéaire
				speedFactor = 100.0 / speed;
			}
			else
			{
				// Vitesses faibles : correction non-linéaire (plus de friction)
				speedFactor = (100.0 / speed) * (1.0 + (100.0 - speed) / 200.0);
			}

			// Correction supplémentaire pour les petits angles (moins d'inertie)
			double angleFactor = 1.0;
			if (degrees < 45)
			{
				angleFactor = 0.85; // Les petits angles sont plus rapides
			}
			else if (degrees > 180)
			{
				angleFactor = 1.05; // Les grands angles ont plus d'inertie
			}

			// Calcul final du temps de rotation
			double timePerDegreeMs = baseTiming * speedFactor * angleFactor;
			int rotationTimeMs = (int)(degrees * timePerDegreeMs);

			Trace.WriteLine($"   📊 Timing calculé: {timePerDegreeMs:F2}ms/degré → {rotationTimeMs}ms total");

			// Pour tourner à gauche : roue droite avance, roue gauche recule
			await SetMotorsAsync(-speed, speed);

			// Attendre le temps de rotation calculé
			await Task.Delay(rotationTimeMs);

			// Arrêter les moteurs
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Left turn of {degrees}° completed (precision enhanced)");
		}

		/// <summary>
		/// Fait avancer le robot avec des vitesses indépendantes pour chaque moteur
		/// </summary>
		/// <param name="leftSpeed">Vitesse du moteur gauche (-255 à +255)</param>
		/// <param name="rightSpeed">Vitesse du moteur droit (-255 à +255)</param>
		public async Task DriveAsync(int leftSpeed, int rightSpeed)
		{
			await SetMotorsAsync(leftSpeed, rightSpeed);
			Trace.WriteLine($"🚗 Driving: Left={leftSpeed}, Right={rightSpeed}");
		}

		/// <summary>
		/// Fait avancer le robot en ligne droite
		/// </summary>
		/// <param name="speed">Vitesse (-255 à +255, négatif pour reculer)</param>
		public async Task DriveForwardAsync(int speed)
		{
			await SetMotorsAsync(speed, speed);
			Trace.WriteLine($"⬆️ Driving forward at speed {speed}");
		}

		/// <summary>
		/// Fait reculer le robot
		/// </summary>
		/// <param name="speed">Vitesse (positive, 0 à 255)</param>
		public async Task DriveBackwardAsync(int speed)
		{
			int backwardSpeed = -Math.Abs(speed);
			await SetMotorsAsync(backwardSpeed, backwardSpeed);
			Trace.WriteLine($"⬇️ Driving backward at speed {Math.Abs(backwardSpeed)}");
		}

		/// <summary>
		/// Arrête complètement le robot
		/// </summary>
		public async Task StopAsync()
		{
			await SetMotorsAsync(0, 0);
			Trace.WriteLine("🛑 Robot stopped");
		}

		// --- NOUVELLES SURCHARGES AVEC DURÉE ---

		/// <summary>
		/// Fait avancer le robot en ligne droite pendant une durée spécifiée
		/// </summary>
		/// <param name="speed">Vitesse (-255 à +255, négatif pour reculer)</param>
		/// <param name="durationMs">Durée en millisecondes</param>
		public async Task DriveForwardAsync(int speed, int durationMs)
		{
			if (_connection == null) return;

			// Validation des paramètres
			speed = Math.Clamp(speed, -255, 255);
			durationMs = Math.Max(0, durationMs);

			Trace.WriteLine($"⬆️ Driving forward at speed {speed} for {durationMs}ms");

			// Démarrer le mouvement
			await SetMotorsAsync(speed, speed);

			// Attendre la durée spécifiée
			await Task.Delay(durationMs);

			// Arrêter automatiquement
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Forward drive completed ({durationMs}ms)");
		}

		/// <summary>
		/// Fait reculer le robot pendant une durée spécifiée
		/// </summary>
		/// <param name="speed">Vitesse (positive, 0 à 255)</param>
		/// <param name="durationMs">Durée en millisecondes</param>
		public async Task DriveBackwardAsync(int speed, int durationMs)
		{
			if (_connection == null) return;

			// Validation des paramètres
			int backwardSpeed = -Math.Abs(speed);
			backwardSpeed = Math.Clamp(backwardSpeed, -255, 0);
			durationMs = Math.Max(0, durationMs);

			Trace.WriteLine($"⬇️ Driving backward at speed {Math.Abs(backwardSpeed)} for {durationMs}ms");

			// Démarrer le mouvement
			await SetMotorsAsync(backwardSpeed, backwardSpeed);

			// Attendre la durée spécifiée
			await Task.Delay(durationMs);

			// Arrêter automatiquement
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Backward drive completed ({durationMs}ms)");
		}

		/// <summary>
		/// Fait avancer le robot avec des vitesses indépendantes pendant une durée spécifiée
		/// </summary>
		/// <param name="leftSpeed">Vitesse du moteur gauche (-255 à +255)</param>
		/// <param name="rightSpeed">Vitesse du moteur droit (-255 à +255)</param>
		/// <param name="durationMs">Durée en millisecondes</param>
		public async Task DriveAsync(int leftSpeed, int rightSpeed, int durationMs)
		{
			if (_connection == null) return;

			// Validation des paramètres
			leftSpeed = Math.Clamp(leftSpeed, -255, 255);
			rightSpeed = Math.Clamp(rightSpeed, -255, 255);
			durationMs = Math.Max(0, durationMs);

			Trace.WriteLine($"🚗 Driving: Left={leftSpeed}, Right={rightSpeed} for {durationMs}ms");

			// Démarrer le mouvement
			await SetMotorsAsync(leftSpeed, rightSpeed);

			// Attendre la durée spécifiée
			await Task.Delay(durationMs);

			// Arrêter automatiquement
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Drive completed ({durationMs}ms)");
		}

		/// <summary>
		/// Fait avancer le robot en arc de cercle (virage progressif) pendant une durée spécifiée
		/// </summary>
		/// <param name="speed">Vitesse de base (0 à 255)</param>
		/// <param name="turnRatio">Ratio de virage (-1.0 à +1.0: -1=gauche max, 0=droit, +1=droite max)</param>
		/// <param name="durationMs">Durée en millisecondes</param>
		public async Task DriveWithTurnAsync(int speed, double turnRatio, int durationMs)
		{
			if (_connection == null) return;

			// Validation des paramètres
			speed = Math.Clamp(speed, 0, 255);
			turnRatio = Math.Clamp(turnRatio, -1.0, 1.0);
			durationMs = Math.Max(0, durationMs);

			// Calcul des vitesses pour chaque roue
			int leftSpeed, rightSpeed;

			if (turnRatio < 0)
			{
				// Virage à gauche : ralentir la roue gauche
				leftSpeed = (int)(speed * (1.0 + turnRatio));
				rightSpeed = speed;
			}
			else if (turnRatio > 0)
			{
				// Virage à droite : ralentir la roue droite
				leftSpeed = speed;
				rightSpeed = (int)(speed * (1.0 - turnRatio));
			}
			else
			{
				// Tout droit
				leftSpeed = rightSpeed = speed;
			}

			// Assurer que les vitesses restent dans les limites
			leftSpeed = Math.Clamp(leftSpeed, -255, 255);
			rightSpeed = Math.Clamp(rightSpeed, -255, 255);

			Trace.WriteLine($"🔄 Driving with turn: Speed={speed}, Ratio={turnRatio:F2}, Left={leftSpeed}, Right={rightSpeed} for {durationMs}ms");

			// Démarrer le mouvement
			await SetMotorsAsync(leftSpeed, rightSpeed);

			// Attendre la durée spécifiée
			await Task.Delay(durationMs);

			// Arrêter automatiquement
			await SetMotorsAsync(0, 0);

			Trace.WriteLine($"✅ Turn drive completed ({durationMs}ms)");
		}

		// --- CONTRÔLE DES LEDs PRINCIPALES DU ROBOT ---

		/// <summary>
		/// Définit la couleur RGB des LEDs principales du robot
		/// </summary>
		/// <param name="red">Valeur rouge (0-255)</param>
		/// <param name="green">Valeur verte (0-255)</param>
		/// <param name="blue">Valeur bleue (0-255)</param>
		public async Task<bool> SetMainLedsAsync(byte red, byte green, byte blue)
		{
			if (_connection == null) return false;

			Trace.WriteLine($"🌈 Définition des LEDs principales: R={red} G={green} B={blue}");

			try
			{
				// Commande LED RGB - FORMAT OFFICIEL EXACT de Sphero Edu !
				// Structure découverte: 8D 3A 11 01 1A 2F [SEQ] [R] [G] [B] [CHECKSUM] D8
				var packet = BuildOfficialPacket(0x1A, 0x2F, _sequenceNumber, new byte[] { red, green, blue });

				bool success = await _connection.SendCommandAsync(packet);
				_sequenceNumber++;

				if (success)
				{
					Trace.WriteLine($"✅ LEDs principales définies avec succès");
				}
				else
				{
					Trace.WriteLine($"❌ Échec de la définition des LEDs principales");
				}

				return success;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ Erreur lors de la définition des LEDs: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Définit une couleur prédéfinie pour les LEDs principales
		/// </summary>
		/// <param name="color">Couleur prédéfinie à utiliser</param>
		public async Task<bool> SetMainLedsAsync(LedColor color)
		{
			var (r, g, b) = GetLedColorValues(color);
			Trace.WriteLine($"🎨 Couleur prédéfinie: {color}");
			return await SetMainLedsAsync(r, g, b);
		}

		/// <summary>
		/// Éteint toutes les LEDs principales du robot
		/// </summary>
		public async Task<bool> TurnOffMainLedsAsync()
		{
			Trace.WriteLine("🔌 Extinction des LEDs principales");
			return await SetMainLedsAsync(0, 0, 0);
		}

		/// <summary>
		/// Fait clignoter les LEDs principales
		/// </summary>
		/// <param name="red">Valeur rouge (0-255)</param>
		/// <param name="green">Valeur verte (0-255)</param>
		/// <param name="blue">Valeur bleue (0-255)</param>
		/// <param name="cycles">Nombre de cycles de clignotement</param>
		/// <param name="onDuration">Durée allumée en ms</param>
		/// <param name="offDuration">Durée éteinte en ms</param>
		public async Task<bool> BlinkMainLedsAsync(byte red, byte green, byte blue,
												   int cycles = 3, int onDuration = 500, int offDuration = 500)
		{
			Trace.WriteLine($"✨ Clignotement LEDs principales: {cycles} cycles");

			try
			{
				for (int i = 0; i < cycles; i++)
				{
					// Allumer
					await SetMainLedsAsync(red, green, blue);
					await Task.Delay(onDuration);

					// Éteindre
					await TurnOffMainLedsAsync();

					// Pause sauf au dernier cycle
					if (i < cycles - 1)
					{
						await Task.Delay(offDuration);
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ Erreur lors du clignotement: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Animation arc-en-ciel sur les LEDs principales
		/// </summary>
		/// <param name="duration">Durée totale de l'animation en ms</param>
		public async Task<bool> RainbowMainLedsAsync(int duration = 5000)
		{
			Trace.WriteLine("🌈 Animation arc-en-ciel sur LEDs principales");

			try
			{
				var rainbowColors = new[]
				{
					(255, 0, 0),    // Rouge
                    (255, 165, 0),  // Orange
                    (255, 255, 0),  // Jaune
                    (0, 255, 0),    // Vert
                    (0, 0, 255),    // Bleu
                    (75, 0, 130),   // Indigo
                    (238, 130, 238) // Violet
                };

				int stepDuration = duration / rainbowColors.Length;

				foreach (var (r, g, b) in rainbowColors)
				{
					await SetMainLedsAsync((byte)r, (byte)g, (byte)b);
					await Task.Delay(stepDuration);
				}

				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ Erreur animation arc-en-ciel: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Transition douce entre deux couleurs
		/// </summary>
		/// <param name="fromColor">Couleur de départ</param>
		/// <param name="toColor">Couleur d'arrivée</param>
		/// <param name="duration">Durée de la transition en ms</param>
		/// <param name="steps">Nombre d'étapes pour la transition</param>
		public async Task<bool> FadeMainLedsAsync(LedColor fromColor, LedColor toColor,
												  int duration = 2000, int steps = 20)
		{
			var (fromR, fromG, fromB) = GetLedColorValues(fromColor);
			var (toR, toG, toB) = GetLedColorValues(toColor);

			Trace.WriteLine($"🔄 Transition LED de {fromColor} vers {toColor}");

			try
			{
				int stepDuration = duration / steps;

				for (int i = 0; i <= steps; i++)
				{
					float progress = (float)i / steps;

					byte currentR = (byte)(fromR + (toR - fromR) * progress);
					byte currentG = (byte)(fromG + (toG - fromG) * progress);
					byte currentB = (byte)(fromB + (toB - fromB) * progress);

					await SetMainLedsAsync(currentR, currentG, currentB);
					await Task.Delay(stepDuration);
				}

				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ Erreur transition: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Obtient les valeurs RGB pour une couleur prédéfinie
		/// </summary>
		private (byte r, byte g, byte b) GetLedColorValues(LedColor color)
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
				_ => (255, 255, 255) // Par défaut: blanc
			};
		}


		public async Task<bool> EnableColorSensorRobustAsync(int settleDelayMs = 300)
		{
			if (_connection == null) return false;
			var payloads = new byte[][] { new byte[] { 0x01, 0x01 }, new byte[] { 0x01 } };
			foreach (var flag in new byte[] { (byte)0x02, (byte)0x00 })
			{
				foreach (var p in payloads)
				{
					var pkt = BuildJavaRawPacket(flag, DID_SENSORS, CID_CONFIGURE_COLOR, _sequenceNumber, p);
					var ok = await _connection.SendCommandAsync(pkt);
					_sequenceNumber++;
					if (ok) { await Task.Delay(settleDelayMs); return true; }
				}
			}
			return false;
		}

		public async Task<bool> EnableColorNodeAsync()
		{
			if (_connection == null) return false;
			var pkt = BuildJavaRawPacket(0x02, DID_SENSORS, CID_ENABLE_COLOR_NODE, _sequenceNumber, new byte[] { 0x01 });
			var ok = await _connection.SendCommandAsync(pkt);
			_sequenceNumber++;
			return ok;
		}

		public async Task<bool> SetColorSensorLedAsync(bool on)
		{
			if (_connection == null) return false;
			var payload = new byte[] { (byte)(on ? 0x01 : 0x00) };
			var pkt = BuildJavaRawPacket(0x02, DID_SENSORS, CID_COLOR_LED, _sequenceNumber, payload);
			var ok = await _connection.SendCommandAsync(pkt);
			_sequenceNumber++;
			return ok;
		}

		public async Task<bool> SetColorSensorLedRobustAsync(bool on)
		{
			if (_connection == null) return false;
			Trace.WriteLine($"💡 Attempting to turn color LED {(on ? "ON" : "OFF")}...");

			var onPayloads = new byte[][]
			{
				new byte[]{ 0x01 },
				new byte[]{ 0x01, 0x01 },
				new byte[]{ 0x01, 0x64 },
				new byte[]{ 0x02, 0x01 },
				new byte[]{ 0xFF },
				new byte[]{ 0x01, 0xFF },
				new byte[]{ 0xFF, 0xFF },
				new byte[]{ 0x03, 0x01 },
			};
			var offPayloads = new byte[][]
			{
				new byte[]{ 0x00 },
				new byte[]{ 0x00, 0x00 },
				new byte[]{ 0x01, 0x00 },
				new byte[]{ 0x02, 0x00 },
			};
			var payloads = on ? onPayloads : offPayloads;

			foreach (var flag in new byte[] { (byte)0x02, (byte)0x00 })
			{
				foreach (var p in payloads)
				{
					var pkt = BuildJavaRawPacket(flag, DID_SENSORS, CID_COLOR_LED, _sequenceNumber, p);
					Trace.WriteLine($"📤 LED CMD: {BitConverter.ToString(pkt).Replace("-", "")}");
					var ok = await _connection.SendCommandAsync(pkt);
					_sequenceNumber++;
					if (ok)
					{
						await Task.Delay(200); // Give LED time to respond
						Trace.WriteLine($"✅ LED command sent successfully with payload: {BitConverter.ToString(p)}");
						// Don't return immediately - try a few more variants to ensure it works
						if (Array.IndexOf(payloads, p) >= 2) return true; // After trying a few variants
					}
				}
			}
			Trace.WriteLine("⚠️ All LED command variants attempted");
			return false;
		}


		private void OnDataReceived(byte[] data)
		{
			if (data == null || data.Length == 0) return;
			_recvBuffer.AddRange(data);
			int startIdx;
			while ((startIdx = _recvBuffer.IndexOf(0x8D)) != -1)
			{
				int endIdx = _recvBuffer.IndexOf(0xD8, startIdx + 1);
				if (endIdx == -1)
				{
					if (startIdx > 0) _recvBuffer.RemoveRange(0, startIdx);
					break;
				}
				int len = endIdx - startIdx + 1;
				var frame = _recvBuffer.GetRange(startIdx, len).ToArray();
				_recvBuffer.RemoveRange(0, endIdx + 1);
				RawDataReceived?.Invoke(frame);

			
				// Inspect marker and surface protocol-level errors for diagnostics
				try
				{
					if (frame.Length >= 7)
					{
						byte marker = frame[1];
						if (marker == 0x28)
						{
							// Format we observed: 8D 28 flag DID CID SEQ [err?] D8
							byte did = frame.Length > 3 ? frame[3] : (byte)0x00;
							byte cid = frame.Length > 4 ? frame[4] : (byte)0x00;
							byte seq = frame.Length > 5 ? frame[5] : (byte)0x00;
							if (cid == 0x3F) // Likely error/async code
							{
								byte err = frame.Length > 6 ? frame[6] : (byte)0x00;
								Trace.WriteLine($"⚠️ Device error: DID=0x{did:X2} CID=0x{cid:X2} SEQ=0x{seq:X2} ERR=0x{err:X2}");
							}
						}
					}
				}
				catch { }

				// BREAKTHROUGH: Parse 0x1A2F LED command responses containing actual RGB sensor data!
				// Observed varying markers; don't require 0x3A in responses, focus on DID/CID pattern
				if (frame.Length >= 9 && frame[4] == 0x1A && frame[5] == 0x2F)
				{
					// Try most common offsets first, then fallback
					// Common capture: 8D 3A 11 01 1A 2F [SEQ] RR GG BB ... D8  => RGB at 7,8,9
					if (frame.Length >= 10)
					{
						byte r = frame[7], g = frame[8], b = frame[9];
						if (r + g + b > 0)
						{
							_lastColor = (r, g, b, 0, 0xFF);
							ColorDataReceived?.Invoke((r, g, b, 0, 0xFF));
							Trace.WriteLine($"🎨 BREAKTHROUGH! 0x1A2F Color Response: R={r} G={g} B={b} LIGNE 1626");
						}
						else if (frame.Length >= 9)
						{
							// Alternate: RGB at 6,7,8
							r = frame[6]; g = frame[7]; b = frame[8];
							if (r + g + b > 0)
							{
								_lastColor = (r, g, b, 0, 0xFF);
								ColorDataReceived?.Invoke((r, g, b, 0, 0xFF));
								Trace.WriteLine($"🎨 BREAKTHROUGH! 0x1A2F Color Response (alt): R={r} G={g} B={b}  LIGNE 1636");
							}
						}
					}
				}

				// Official Sphero format: 8D 38 flags DID CID payload (discovered from web interface)
				if (frame.Length >= 12 && frame[1] == 0x38 && frame[4] == 0x18 && frame[5] == 0x3D)
				{
					if (frame.Length == 15 && frame[1] == 0x38 && frame[4] == 0x18 && frame[5] == 0x3D)
					{
						// Color sensor: 8D 38 11 01 18 3D FF 01 RR GG BB FF 00 CHK D8
						byte r = frame[8], g = frame[9], b = frame[10];
						_lastColor = (r, g, b, 0, 0xFF); // Use confidence=0xFF for official format
						ColorDataReceived?.Invoke((r, g, b, 0, 0xFF));
						Trace.WriteLine($"🎨 BREAKTHROUGH! 0x1A2F Color Response (alt): R={r} G={g} B={b}  LIGNE 1649");
					}
					//Trace.WriteLine($"✅ Official Sphero color format: R={r} G={g} B={b}");
				}

				// Streaming parse (always run to avoid missing frames while waiting for a response)
				if (frame.Length > 6)
				{
					byte cidPrimary = frame.Length > 5 ? frame[4] : (frame.Length > 2 ? frame[2] : (byte)0x00);
					byte cidAlt = (frame.Length > 6 && cidPrimary == 0x18) ? frame[5] : cidPrimary;
					try
					{
						// Color detection notifications (enable via CID 0x2C)
						if (cidPrimary == CID_COLOR_DET_NOTIFY || cidAlt == CID_COLOR_DET_NOTIFY || cidPrimary == 0x2D || cidAlt == 0x2D)
						{
							// Expect 5 bytes: R,G,B,Index,Confidence
							// Try with both common offsets
							if (frame.Length >= 12 && frame[1] == 0x18)
							{
								byte r = frame[6], g = frame[7], b = frame[8], idx = frame[9], conf = frame[10];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1670");
							}
							else if (frame.Length >= 11)
							{
								byte r = frame[6], g = frame[7], b = frame[8], idx = frame[9], conf = frame[10];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1677");
							}
							else if (frame.Length >= 10)
							{
								byte r = frame[5], g = frame[6], b = frame[7], idx = frame[8], conf = frame[9];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1682");
							}
						}
						// Streaming service data (token-based): CID 0x3D (heuristic)
						if (cidPrimary == 0x3D || cidAlt == 0x3D)
						{
							// Expected: 8D 18 flag DID 3D SEQ [token] [sensor_data...] CHK D8
							if (frame.Length >= 10)
							{
								int tokenIdx = 6;
								byte token = frame[tokenIdx];
								byte status = (byte)(token & 0xF0);
								byte tokenId = (byte)(token & 0x0F);
								int dataStart = tokenIdx + 1;
								int dataLen = frame.Length - dataStart - 2; // exclude CHK/EOP
								if (dataLen >= 5 && tokenId == 0x01)
								{
									byte r = frame[dataStart + 0];
									byte g = frame[dataStart + 1];
									byte b = frame[dataStart + 2];
									byte idx = frame[dataStart + 3];
									byte conf = frame[dataStart + 4];
									_lastColor = (r, g, b, idx, conf);
									ColorDataReceived?.Invoke((r, g, b, idx, conf));
									Console.WriteLine("Ligne 1705");
								}
							}
						}
						// Color (5 bytes R,G,B,Idx,Conf)
						if (cidPrimary == 0x0F || cidAlt == 0x0F)
						{
							if (frame.Length >= 12 && cidPrimary == 0x18)
							{
								byte r = frame[6], g = frame[7], b = frame[8], idx = frame[9], conf = frame[10];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1720");
							}
							else if (frame.Length >= 11)
							{
								byte r = frame[6], g = frame[7], b = frame[8], idx = frame[9], conf = frame[10];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1727");
							}
							else if (frame.Length >= 10)
							{
								byte r = frame[5], g = frame[6], b = frame[7], idx = frame[8], conf = frame[9];
								_lastColor = (r, g, b, idx, conf);
								ColorDataReceived?.Invoke((r, g, b, idx, conf));
								Console.WriteLine("Ligne 1734");
							}
						}
						// Ambient
						if ((cidPrimary == 0x30 || cidAlt == 0x30) && frame.Length >= 10)
						{
							int val = (frame[6] << 24) | (frame[7] << 16) | (frame[8] << 8) | frame[9];
							_lastAmbient = val / 1000.0;
						}
						else if ((cidPrimary == 0x30 || cidAlt == 0x30) && frame.Length >= 9)
						{
							int val = (frame[5] << 24) | (frame[6] << 16) | (frame[7] << 8) | frame[8];
							_lastAmbient = val / 1000.0;
						}
						// Encoders
						else if ((cidPrimary == 0x50 || cidAlt == 0x50) && frame.Length >= 14)
						{
							int left = (frame[6] << 24) | (frame[7] << 16) | (frame[8] << 8) | frame[9];
							int right = (frame[10] << 24) | (frame[11] << 16) | (frame[12] << 8) | frame[13];
							_lastEnc = (left, right);
						}
						else if ((cidPrimary == 0x50 || cidAlt == 0x50) && frame.Length >= 13)
						{
							int left = (frame[5] << 24) | (frame[6] << 16) | (frame[7] << 8) | frame[8];
							int right = (frame[9] << 24) | (frame[10] << 16) | (frame[11] << 8) | frame[12];
							_lastEnc = (left, right);
						}
						// IMU
						else if (cidPrimary == 0x51 || cidAlt == 0x51)
						{
							if (frame.Length >= 30)
							{
								int i = 6;
								int axi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int ayi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int azi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int gxi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int gyi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int gzi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3];
								_lastImu = (axi / 1000.0, ayi / 1000.0, azi / 1000.0, gxi / 1000.0, gyi / 1000.0, gzi / 1000.0);
							}
							else if (frame.Length >= 24)
							{
								int i = 8;
								int axi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int ayi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int azi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								int gxi = (frame[i] << 24) | (frame[i + 1] << 16) | (frame[i + 2] << 8) | frame[i + 3]; i += 4;
								double ax = axi / 1000.0, ay = ayi / 1000.0, az = azi / 1000.0, gx = gxi / 1000.0;
								_lastImu = (ax, ay, az, gx, 0, 0);
							}
						}
					}
					catch { }
				}

				// Complete pending command if this matches the expected CID
				bool matchesExpected = false;
				if (_expectedCid.HasValue)
				{
					byte cid = 0xFF;
					if (frame.Length > 5) cid = frame[4];
					else if (frame.Length > 3) cid = frame[2];
					matchesExpected = cid == _expectedCid.Value;
				}
				if (matchesExpected && _pendingResponse != null && !_pendingResponse.Task.IsCompleted)
				{
					_pendingResponse.TrySetResult(frame);
					_expectedCid = null;
				}
			}
		}

		// --- Fonctions principales de la librairie RVR+ ---

		/// <summary>
		/// Lit la couleur actuellement détectée sous le robot
		/// </summary>
		/// <returns>Structure ColorSensor avec les valeurs RGB et métadonnées</returns>
		public async Task<ColorSensor> ReadColorAsync()
		{
			if (_lastColor.HasValue)
			{
				var color = _lastColor.Value;
				_sensorData.Color = new ColorSensor(color.r, color.g, color.b, color.index, color.confidence);
				_sensorData.LastUpdate = DateTime.Now;
				Trace.WriteLine($"🎨 Color read: {_sensorData.Color}");
				return _sensorData.Color;
			}

			// Retourner la dernière couleur connue ou une valeur par défaut
			Trace.WriteLine("⚠️ No recent color data available");
			return new ColorSensor(0, 0, 0, 0, 0);
		}

		/// <summary>
		/// Lit la distance totale parcourue par le robot
		/// </summary>
		/// <returns>Distance en unités du robot</returns>
		//public async Task<double> ReadDistanceAsync()
		//{
		//	// Simuler la lecture des encodeurs (implémentation simplifiée)
		//	if (_lastEnc.HasValue)
		//	{
		//		var encoders = _lastEnc.Value;
		//		_leftWheelDistance = encoders.left * 0.1; // Conversion approximative
		//		_rightWheelDistance = encoders.right * 0.1;
		//		_totalDistance = (_leftWheelDistance + _rightWheelDistance) / 2.0;

		//		_sensorData.Distance = new DistanceSensor(_leftWheelDistance, _rightWheelDistance);
		//		_sensorData.LastUpdate = DateTime.Now;
		//	}

		//	Trace.WriteLine($"📏 Distance read: {_totalDistance:F2} units");
		//	return _totalDistance;
		//}

		/// <summary>
		/// Réinitialise la distance parcourue à zéro
		/// </summary>
		//public async Task<bool> ResetDistanceAsync()
		//{
		//	if (_connection == null) return false;

		//	try
		//	{
		//		// Commande pour réinitialiser les encodeurs
		//		var pkt = BuildJavaRawPacket(0x02, DID_DRIVE, CID_RESET_ENCODERS, _sequenceNumber, Array.Empty<byte>());
		//		var ok = await _connection.SendCommandAsync(pkt);
		//		_sequenceNumber++;

		//		if (ok)
		//		{
		//			_totalDistance = 0.0;
		//			_leftWheelDistance = 0.0;
		//			_rightWheelDistance = 0.0;
		//			_sensorData.Distance.Reset();
		//			Trace.WriteLine("🔄 Distance reset to zero");
		//		}

		//		return ok;
		//	}
		//	catch (Exception ex)
		//	{
		//		Trace.WriteLine($"❌ Error resetting distance: {ex.Message}");
		//		return false;
		//	}
		//}

		/// <summary>
		/// Lit toutes les données des capteurs
		/// </summary>
		/// <returns>Structure SensorData complète</returns>
		//public async Task<SensorData> ReadAllSensorsAsync()
		//{
		//	// Mettre à jour toutes les données des capteurs
		//	_sensorData.Color = await ReadColorAsync();
		//	double totalDistance = await ReadDistanceAsync();
		//	_sensorData.Distance = new DistanceSensor(_leftWheelDistance, _rightWheelDistance);

		//	// Mettre à jour les données IMU si disponibles
		//	if (_lastImu.HasValue)
		//	{
		//		var imu = _lastImu.Value;
		//		_sensorData.Imu = new ImuSensor(imu.ax, imu.ay, imu.az, imu.gx, imu.gy, imu.gz);
		//	}

		//	// Mettre à jour la luminosité ambiante si disponible
		//	if (_lastAmbient.HasValue)
		//	{
		//		_sensorData.AmbientLight = _lastAmbient.Value;
		//	}

		//	_sensorData.LastUpdate = DateTime.Now;

		//	Trace.WriteLine($"📊 All sensors read: {_sensorData}");
		//	return _sensorData;
		//}

		/// <summary>
		/// Change la couleur de la LED principale du robot
		/// </summary>
		/// <param name="color">Couleur prédéfinie</param>
		//public async Task<bool> SetLedColorAsync(RvrLedColor color)
		//{
		//	var customColor = CustomLedColor.FromLedColor(color);
		//	return await SetLedColorAsync(customColor.Red, customColor.Green, customColor.Blue);
		//}

		/// <summary>
		/// Change la couleur de la LED principale du robot avec des valeurs RGB personnalisées
		/// </summary>
		/// <param name="red">Composante rouge (0-255)</param>
		/// <param name="green">Composante verte (0-255)</param>
		/// <param name="blue">Composante bleue (0-255)</param>
		//public async Task<bool> SetLedColorAsync(byte red, byte green, byte blue)
		//{
		//	if (_connection == null) return false;

		//	try
		//	{
		//		var payload = new byte[] { red, green, blue };
		//		var pkt = BuildJavaRawPacket(0x02, DID_SYSTEM, CID_SET_RGB_LED, _sequenceNumber, payload);
		//		var ok = await _connection.SendCommandAsync(pkt);
		//		_sequenceNumber++;

		//		if (ok)
		//		{
		//			Trace.WriteLine($"💡 LED color set to RGB({red},{green},{blue})");
		//		}

		//		return ok;
		//	}
		//	catch (Exception ex)
		//	{
		//		Trace.WriteLine($"❌ Error setting LED color: {ex.Message}");
		//		return false;
		//	}
		//}

		/// <summary>
		/// Éteint la LED principale du robot
		/// </summary>
		//public async Task<bool> TurnOffLedAsync()
		//{
		//	return await SetLedColorAsync(0, 0, 0);
		//}

		// --- Packet builders ---
		private static byte[] BuildJavaRawPacket(byte flag, byte deviceId, byte commandId, byte seq, byte[] payload)
		{
			var len = 1 + 1 + 1 + 1 + 1 + 1 + payload.Length + 1 + 1; // SOP,0x18,flag,DID,CID,SEQ,payload,CHK,EOP
			var packet = new byte[len];
			int i = 0;
			packet[i++] = 0x8D; // SOP
			packet[i++] = 0x18; // marker
			packet[i++] = flag; // flag
			packet[i++] = deviceId; // DID
			packet[i++] = commandId; // CID
			packet[i++] = seq; // SEQ
			Array.Copy(payload, 0, packet, i, payload.Length);
			i += payload.Length;
			packet[i++] = 0x00; // CHK placeholder
			packet[i++] = 0xD8; // EOP
			int sum = 0; for (int idx = 1; idx < packet.Length - 2; idx++) sum += packet[idx];
			packet[packet.Length - 2] = (byte)((sum & 0xFF) ^ 0xFF);
			return packet;
		}

		// Construit un packet au format OFFICIEL Sphero Edu (découvert via BLE sniffer)
		// Structure: 8D 3A 11 01 [DID] [CID] [SEQ] [payload] [CHECKSUM] D8
		private static byte[] BuildOfficialPacket(byte deviceId, byte commandId, byte seq, byte[] payload)
		{
			var len = 1 + 1 + 1 + 1 + 1 + 1 + 1 + payload.Length + 1 + 1; // SOP,0x3A,11,01,DID,CID,SEQ,payload,CHK,EOP
			var packet = new byte[len];
			int i = 0;
			packet[i++] = 0x8D; // SOP (Start of Packet)
			packet[i++] = 0x3A; // marker officiel (pas 0x18)
			packet[i++] = 0x11; // flag fixe officiel
			packet[i++] = 0x01; // length byte officiel
			packet[i++] = deviceId; // DID
			packet[i++] = commandId; // CID
			packet[i++] = seq; // SEQ
			Array.Copy(payload, 0, packet, i, payload.Length);
			i += payload.Length;
			packet[i++] = 0x00; // CHK placeholder
			packet[i++] = 0xD8; // EOP (End of Packet)

			// Calcul du checksum comme dans l'original
			int sum = 0;
			for (int idx = 1; idx < packet.Length - 2; idx++)
				sum += packet[idx];
			packet[packet.Length - 2] = (byte)((sum & 0xFF) ^ 0xFF);
			return packet;
		}

		/// <summary>
		/// Write official Sphero command using format 8D 3A [flags] 01 [payload] [checksum] D8
		/// Captured from edu.sphero.com official interface
		/// </summary>
		//private async Task<bool> WriteOfficialCommandAsync(byte flags, byte[] payload)
		//{
		//	if (_connection == null) return false;

		//	// Build official format: 8D 3A [flags] 01 [payload] [checksum] D8
		//	var packet = new List<byte> { 0x8D, 0x3A, flags, 0x01 };
		//	packet.AddRange(payload);

		//	// Calculate checksum (XOR of all bytes after 8D, then invert)
		//	byte checksum = 0;
		//	for (int i = 1; i < packet.Count; i++)
		//	{
		//		checksum ^= packet[i];
		//	}
		//	checksum = (byte)(~checksum); // Invert

		//	packet.Add(checksum);
		//	packet.Add(0xD8);

		//	var pktArray = packet.ToArray();
		//	Trace.WriteLine($"📤 OFFICIAL: {BitConverter.ToString(pktArray).Replace("-", "")}");

		//	bool ok = false;
		//	if (_preferNotifyWrite)
		//	{
		//		try { ok = await _connection.SendCommandViaNotifyAsync(pktArray); } catch { ok = false; }
		//		if (!ok)
		//		{
		//			ok = await _connection.SendCommandAsync(pktArray);
		//		}
		//	}
		//	else
		//	{
		//		ok = await _connection.SendCommandAsync(pktArray);
		//	}
		//	return ok;
		//}

		//private async Task<bool> WriteSpecialCommandAsync(byte command, byte[] payload)
		//{
		//	if (_connection == null) return false;

		//	// Build special format: 8D 0A [command] [payload] [checksum] D8
		//	var packet = new List<byte> { 0x8D, 0x0A, command };
		//	packet.AddRange(payload);

		//	// Calculate checksum (XOR of all bytes after 8D, then invert)
		//	byte checksum = 0;
		//	for (int i = 1; i < packet.Count; i++)
		//	{
		//		checksum ^= packet[i];
		//	}
		//	checksum = (byte)(~checksum); // Invert

		//	packet.Add(checksum);
		//	packet.Add(0xD8);

		//	var pktArray = packet.ToArray();
		//	Trace.WriteLine($"📤 SPECIAL: {BitConverter.ToString(pktArray).Replace("-", "")}");

		//	bool ok = false;
		//	if (_preferNotifyWrite)
		//	{
		//		try { ok = await _connection.SendCommandViaNotifyAsync(pktArray); } catch { ok = false; }
		//		if (!ok)
		//		{
		//			ok = await _connection.SendCommandAsync(pktArray);
		//		}
		//	}
		//	else
		//	{
		//		ok = await _connection.SendCommandAsync(pktArray);
		//	}
		//	return ok;
		//}

	
		#region Calibration Functions

		/// <summary>
		/// Calibre le capteur de couleur avec des références connues
		/// </summary>
		/// <param name="expectedColor">La couleur attendue pour calibrage</param>
		/// <returns>Données de couleur avec information de calibrage</returns>
		public async Task<ColorSensor> CalibrateColorSensorAsync(string expectedColor)
		{
			try
			{
				Trace.WriteLine($"🎯 Calibrage en cours pour '{expectedColor}'...");
				Trace.WriteLine("Assurez-vous que l'objet de référence est bien positionné sous le capteur.");

				// Prendre plusieurs lectures pour plus de précision
				var readings = new List<ColorSensor>();

				for (int i = 0; i < 5; i++)
				{
					var reading = await ReadColorAsync();
					readings.Add(reading);
					Trace.WriteLine($"Lecture {i + 1}/5: R={reading.Red} G={reading.Green} B={reading.Blue} -> {reading.ColorName}");
					await Task.Delay(500); // Délai entre les lectures
				}

				// Calculer la moyenne
				var avgR = (byte)readings.Average(r => r.Red);
				var avgG = (byte)readings.Average(r => r.Green);
				var avgB = (byte)readings.Average(r => r.Blue);

				var calibratedReading = new ColorSensor
				{
					Red = avgR,
					Green = avgG,
					Blue = avgB
				};

				Trace.WriteLine($"✅ Calibrage terminé pour '{expectedColor}':");
				Trace.WriteLine($"   Valeurs moyennes: R={avgR} G={avgG} B={avgB}");
				Trace.WriteLine($"   Détection actuelle: {calibratedReading.ColorName}");

				// Suggestions d'amélioration
				if (expectedColor.ToLower().Contains("vert") && !calibratedReading.ColorName.ToLower().Contains("vert"))
				{
					Trace.WriteLine("⚠️  ATTENTION: Couleur verte attendue mais non détectée correctement!");
					Trace.WriteLine("   Suggestions:");
					Trace.WriteLine("   - Vérifiez l'éclairage ambiant");
					Trace.WriteLine("   - Assurez-vous que la LED du capteur fonctionne");
					Trace.WriteLine("   - La surface peut avoir des reflets ou être brillante");
				}

				return calibratedReading;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ Erreur lors du calibrage: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// Test complet du capteur de couleur avec différents objets
		/// </summary>
		public async Task TestColorSensorAccuracyAsync()
		{
			Trace.WriteLine("🔬 Test complet de précision du capteur de couleur");
			Trace.WriteLine("================================================");

			string[] testColors = { "Rouge", "Vert", "Bleu", "Jaune", "Blanc", "Noir" };

			foreach (string color in testColors)
			{
				Trace.WriteLine($"\n📋 Test pour: {color}");
				Trace.WriteLine("Placez un objet de cette couleur sous le capteur et appuyez sur Entrée...");
				Console.ReadLine();

				await CalibrateColorSensorAsync(color);
				Trace.WriteLine("---");
			}

			Trace.WriteLine("\n✅ Test de précision terminé!");
		}

		#endregion

	}
}
