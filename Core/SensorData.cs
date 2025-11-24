using System;

namespace Sphero_RVR_Plus_CS.Core
{
    /// <summary>
    /// Structure de données pour stocker les valeurs des capteurs du Sphero RVR+
    /// </summary>
    public struct SensorData
    {
        /// <summary>
        /// Couleur détectée sous le robot (R, G, B, Index, Confiance)
        /// </summary>
        public ColorSensor Color { get; set; }

        /// <summary>
        /// Distance parcourue par le robot (en unités du robot)
        /// </summary>
        public DistanceSensor Distance { get; set; }

        /// <summary>
        /// Données de l'IMU (accéléromètre et gyroscope)
        /// </summary>
        //public ImuSensor Imu { get; set; }

        /// <summary>
        /// Données de luminosité ambiante
        /// </summary>
        public double AmbientLight { get; set; }

        /// <summary>
        /// Timestamp de la dernière mise à jour des données
        /// </summary>
        public DateTime LastUpdate { get; set; }

        public SensorData()
        {
            Color = new ColorSensor();
            Distance = new DistanceSensor();
            //Imu = new ImuSensor();
            AmbientLight = 0.0;
            LastUpdate = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Color: {Color}, Distance: {Distance.Total:F2}, Light: {AmbientLight:F1}";
        }
    }

    /// <summary>
    /// Données du capteur de couleur
    /// </summary>
    public struct ColorSensor
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte Index { get; set; }
        public byte Confidence { get; set; }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="index"></param>
        /// <param name="confidence"></param>
        public ColorSensor(byte r, byte g, byte b, byte index = 0, byte confidence = 0)
        {
            Red = r;
            Green = g;
            Blue = b;
            Index = index;
            Confidence = confidence;
        }

        /// <summary>
        /// Convertit la couleur en nom lisible avec détection améliorée
        /// </summary>
        public string ColorName
        {
            get
            {
                return GetColorName(Red, Green, Blue);
            }
        }

        /// <summary>
        /// Analyse avancée de la couleur avec prise en compte des nuances
        /// </summary>
        private  string GetColorName(byte r, byte g, byte b)
        {
            // Calculs pour l'analyse de couleur
            int total = r + g + b;
            double brightness = total / 3.0;
            
            // Seuils dynamiques basés sur la luminosité
            int highThreshold = (int)(brightness * 1.3);
            int lowThreshold = (int)(brightness * 0.7);
            
            // Cas spéciaux d'abord
            if (total < 120) return "Noir/Très sombre";
            if (r > 230 && g > 230 && b > 230) return "Blanc";
            
            // Analyse des ratios pour une détection plus précise
            double rRatio = r / (double)total;
            double gRatio = g / (double)total;
            double bRatio = b / (double)total;
            
            // Détection de couleurs avec debug info
            Console.WriteLine($"🔍 Analyse: R={r} G={g} B={b}, Ratios: R={rRatio:F2} G={gRatio:F2} B={bRatio:F2}, Luminosité={brightness:F1}");
            
            // Couleurs saturées (ratio dominant > 0.4)
            if (rRatio > 0.4)
            {
                if (g < 100 && b < 100) return "Rouge pur";
                if (g > r * 0.6) return "Orange/Jaune-rouge";
                return "Rouge dominant";
            }
            
            if (gRatio > 0.4)
            {
                if (r < 100 && b < 100) return "Vert pur";
                if (r > g * 0.7) return "Jaune-vert";
                return "Vert dominant";
            }
            
            if (bRatio > 0.4)
            {
                if (r < 100 && g < 100) return "Bleu pur";
                if (g > b * 0.7) return "Cyan/Bleu-vert";
                return "Bleu dominant";
            }
            
            // Couleurs mixtes
            if (Math.Abs(r - g) < 30)
            {
                if (b < r - 50) return "Jaune/Doré";
                if (b > r + 50) return "Bleu-gris";
                return "Gris";
            }
            
            if (Math.Abs(r - b) < 30)
            {
                if (g < r - 50) return "Magenta/Violet";
                return "Rose-gris";
            }
            
            if (Math.Abs(g - b) < 30)
            {
                if (r < g - 50) return "BleuCyan";
                return "Turquoise";
            }
            
            // Cas particulier pour votre lecture R=131, G=89, B=57
            if (r >= 120 && r <= 140 && g >= 80 && g <= 100 && b >= 50 && b <= 70)
            {
                return "BRUN/MARRON (carte verte détectée avec reflet)";
            }
            
            // Détection basée sur les valeurs dominantes avec seuils adaptatifs
            int max = Math.Max(Math.Max(r, g), b);
            int min = Math.Min(Math.Min(r, g), b);
            int range = max - min;
            
            if (range < 20) return $"Gris ({brightness:F0})";
            
            if (r == max)
            {
                if (g > b + 20) return "Orange/Brun";
                if (b > g + 20) return "Magenta/Rose";
                return "Rouge/Brun";
            }
            else if (g == max)
            {
                if (r > b + 20) return "Lime/Jaune-vert";
                if (b > r + 20) return "Vert-bleu";
                return "Vert";
            }
            else // b == max
            {
                if (r > g + 20) return "Violet/Bleu-rouge";
                if (g > r + 20) return "Bleu-vert";
                return "Bleu";
            }
        }

        public override string ToString()
        {
            return $"RGB({Red},{Green},{Blue}) - {ColorName} [Conf:{Confidence}]";
        }
    }

    /// <summary>
    /// Données du capteur de distance
    /// </summary>
    public struct DistanceSensor
    {
        public double LeftWheel { get; set; }
        public double RightWheel { get; set; }
        public double Total { get; set; }

        public DistanceSensor(double left, double right)
        {
            LeftWheel = left;
            RightWheel = right;
            Total = (left + right) / 2.0;
        }

        public void Reset()
        {
            LeftWheel = 0;
            RightWheel = 0;
            Total = 0;
        }

        public override string ToString()
        {
            return $"Total:{Total:F2} (L:{LeftWheel:F2}, R:{RightWheel:F2})";
        }
    }

 
}