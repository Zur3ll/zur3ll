using System;
using System.Collections.Generic;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.Network.Systems;
using Life.UI;
using NovaApps.Core;
using UnityEngine;

namespace NovaApps.Apps
{
    public class Creneau
    {
        public DateTime Debut;
        /// <summary>Nom de la météo du jeu (ClearSky, Cloudy1, Foggy, LightRain, HeavyRain, Storm...).</summary>
        public string Temps;
        public int Temperature;
        public int Vent;
    }

    /// <summary>
    /// Météo : prévisions sur plusieurs créneaux. Si PiloterLaMeteo est activé, le plugin applique
    /// lui-même chaque créneau sur le serveur, donc les prévisions sont toujours justes.
    /// </summary>
    public class MeteoApp
    {
        private const int NombreCreneaux = 6;
        private const string Bleu = "#4aa3df";

        private static readonly Dictionary<string, string> Noms = new Dictionary<string, string>
        {
            ["ClearSky"] = "Ensoleillé",
            ["Cloudy1"] = "Nuageux",
            ["Cloudy2"] = "Très nuageux",
            ["Cloudy3"] = "Couvert",
            ["Foggy"] = "Brouillard",
            ["LightRain"] = "Pluie légère",
            ["HeavyRain"] = "Fortes pluies",
            ["Storm"] = "Orage",
            ["LightSnow"] = "Neige légère",
            ["HeavySnow"] = "Fortes chutes de neige",
        };

        private static readonly HashSet<string> Dangereux = new HashSet<string> { "Storm", "HeavyRain", "HeavySnow" };

        private readonly System.Random hasard = new System.Random();
        private readonly List<Creneau> previsions = new List<Creneau>();

        private MeteoConfig Config => NovaAppsPlugin.Config.Meteo;
        private TimeSpan Duree => TimeSpan.FromMinutes(Mathf.Clamp(Config.DureeCreneauMinutes, 5, 24 * 60));

        public MeteoApp()
        {
            DateTime debut = DateTime.Now;
            for (int i = 0; i < NombreCreneaux; i++)
            {
                previsions.Add(Generer(debut, previsions.LastOrDefault()?.Temps));
                debut += Duree;
            }
            Appliquer(previsions[0]);
        }

        public bool EstMeteorologue(Player joueur) => Joueurs.Membre(joueur, Config.EntreprisesMeteo);

        public static string Nom(string temps) => temps != null && Noms.TryGetValue(temps, out string nom) ? nom : temps;

        /// <summary>Appelé chaque minute : passe au créneau suivant quand son heure est arrivée.</summary>
        public void Tick()
        {
            if (previsions.Count < 2 || DateTime.Now < previsions[1].Debut) return;

            previsions.RemoveAt(0);
            Creneau dernier = previsions.Last();
            previsions.Add(Generer(dernier.Debut + Duree, dernier.Temps));
            Appliquer(previsions[0]);

            // Alerte quand un temps dangereux arrive au prochain créneau.
            Creneau suivant = previsions[1];
            if (Dangereux.Contains(suivant.Temps) && !Dangereux.Contains(previsions[0].Temps))
                foreach (Player joueur in Joueurs.TousEnLigne())
                    joueur.Notify($"Météo · {Config.Ville}", $"Alerte : {Nom(suivant.Temps).ToLower()} prévu à partir de {suivant.Debut:HH:mm}.", NotificationManager.Type.Warning);
        }

        private Creneau Generer(DateTime debut, string precedent)
        {
            // Une chance sur deux de garder le même temps : la météo ne change pas brutalement à chaque créneau.
            string temps = precedent != null && Config.Probabilites.TryGetValue(precedent, out int p) && p > 0 && hasard.Next(2) == 0
                ? precedent
                : Tirer();

            // Plus chaud l'après-midi, plus frais la nuit et sous la pluie.
            double cycle = Math.Sin((debut.Hour - 9) / 24.0 * 2 * Math.PI);
            int ajustement = temps == "ClearSky" ? 3 : temps.Contains("Rain") || temps == "Storm" ? -3 : temps.Contains("Snow") ? -12 : 0;

            return new Creneau
            {
                Debut = debut,
                Temps = temps,
                Temperature = (int)Math.Round(15 + 7 * cycle + ajustement + hasard.Next(-2, 3)),
                Vent = temps == "Storm" ? hasard.Next(50, 90) : temps.Contains("Rain") ? hasard.Next(15, 45) : hasard.Next(0, 25),
            };
        }

        private string Tirer()
        {
            List<KeyValuePair<string, int>> choix = Config.Probabilites.Where(p => p.Value > 0).ToList();
            if (choix.Count == 0) return "ClearSky";

            int tirage = hasard.Next(choix.Sum(p => p.Value));
            foreach (KeyValuePair<string, int> c in choix)
            {
                if (tirage < c.Value) return c.Key;
                tirage -= c.Value;
            }
            return choix[0].Key;
        }

        private void Appliquer(Creneau creneau)
        {
            if (!Config.PiloterLaMeteo) return;

            try
            {
                SWorld.Weather temps = (SWorld.Weather)Enum.Parse(typeof(SWorld.Weather), creneau.Temps, true);
                Nova.server.world.SetWeather(temps);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NovaApps] Météo « {creneau.Temps} » impossible à appliquer : {e.Message}");
            }
        }

        public string Resume()
        {
            Creneau c = previsions[0];
            return $"{Nom(c.Temps)}, {c.Temperature}°C";
        }

        public void Ouvrir(Player joueur, Action retour)
        {
            Action relire = () => Ouvrir(joueur, retour);
            Creneau maintenant = previsions[0];

            string texte = $"{Ecran.Couleur($"<b>{Config.Ville}</b>", Bleu)}  ·  {DateTime.Now.ToString("dddd dd/MM HH:mm", Ecran.Francais)}\n\n" +
                           $"<size=130%><b>{Nom(maintenant.Temps)}  {maintenant.Temperature}°C</b></size>\nVent : {maintenant.Vent} km/h\n\n<b>Prévisions</b>\n" +
                           string.Join("\n", previsions.Skip(1).Select(c => $"{c.Debut:HH:mm}   {Nom(c.Temps)}   {c.Temperature}°C   vent {c.Vent} km/h"));

            Ecran.Texte(joueur, "Météo", texte, retour,
                new Bouton("Actualiser", relire),
                EstMeteorologue(joueur) ? new Bouton("Modifier les prévisions", () => Modifier(joueur, relire)) : null);
        }

        private void Modifier(Player joueur, Action retour)
        {
            Action recharger = () => Modifier(joueur, retour);
            Ecran.Liste(joueur, "Météo · Modifier un créneau",
                previsions.Select((c, i) => new Ligne($"{(i == 0 ? "Maintenant" : c.Debut.ToString("HH:mm"))}  —  {Nom(c.Temps)}  {c.Temperature}°C", () =>
                    Ecran.Choix(joueur, $"Météo · Créneau {(i == 0 ? "actuel" : c.Debut.ToString("HH:mm"))}", Config.Probabilites.Keys.Select(Nom), choix =>
                    {
                        c.Temps = Config.Probabilites.Keys.First(k => Nom(k) == choix);
                        if (i == 0) Appliquer(c);
                        joueur.Notify("Météo", $"Prévision modifiée : {choix}.", NotificationManager.Type.Success);
                        recharger();
                    }, recharger))),
                retour);
        }
    }
}
