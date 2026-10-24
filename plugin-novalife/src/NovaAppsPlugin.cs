using System;
using System.IO;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Newtonsoft.Json;
using NovaApps.Apps;
using NovaApps.Core;
using UnityEngine;

namespace NovaApps
{
    /// <summary>
    /// Point d'entrée du plugin : téléphone (N, Neonous, LeBonNeo, Pôle Travail, Capital Motors, Journal, Police, Météo, Agenda)
    /// et ordinateur (Discord, Blue Lightfox, Pôle Travail, LeBonNeo, Journal, Police).
    /// </summary>
    public class NovaAppsPlugin : Plugin
    {
        public static Config Config;
        public static string Dossier;
        public static Discord Discord;

        private string cheminConfig;
        private KeyCode toucheTelephone = KeyCode.None;
        private KeyCode toucheOrdinateur = KeyCode.None;

        private NApp n;
        private NeonousApp neonous;
        private PoleTravailApp poleTravail;
        private CapitalMotorsApp capitalMotors;
        private DiscordApp discordApp;
        private BlueLightfoxApp blueLightfox;
        private LeBonNeoApp leBonNeo;
        private PoliceApp police;
        private JournalApp journal;
        private MeteoApp meteo;
        private AgendaApp agenda;

        public NovaAppsPlugin(IGameAPI api) : base(api)
        {
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            Dossier = Path.Combine(pluginsPath, "NovaApps");
            Directory.CreateDirectory(Dossier);
            ChargerConfig();

            Discord = new Discord(Config.Discord.TokenBot);

            n = new NApp();
            neonous = new NeonousApp();
            poleTravail = new PoleTravailApp();
            capitalMotors = new CapitalMotorsApp(SauvegarderConfig);
            discordApp = new DiscordApp();
            leBonNeo = new LeBonNeoApp();
            police = new PoliceApp();
            journal = new JournalApp();
            meteo = new MeteoApp();
            agenda = new AgendaApp();
            blueLightfox = new BlueLightfoxApp(poleTravail, capitalMotors, leBonNeo, journal, police);

            Nova.server.OnMinutePassedEvent += ChaqueMinute;
            EnregistrerCommandes();
            Debug.Log("[NovaApps] Téléphone et ordinateur prêts.");
        }

        public override void OnPlayerInput(Player player, KeyCode keyCode, bool onUI)
        {
            base.OnPlayerInput(player, keyCode, onUI);

            if (onUI || player?.character == null || keyCode == KeyCode.None) return;

            if (keyCode == toucheTelephone)
                OuvrirTelephone(player);
            else if (keyCode == toucheOrdinateur)
                OuvrirOrdinateur(player);
        }

        private void ChaqueMinute()
        {
            try
            {
                meteo.Tick();
                agenda.Tick();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NovaApps] Erreur de la tâche minute : {e}");
            }
        }

        private void EnregistrerCommandes()
        {
            new SChatCommand("/tel", new[] { "/telephone", "/phone" }, "Ouvrir le téléphone", "/tel", (player, args) => OuvrirTelephone(player)).Register();
            new SChatCommand("/ordi", new[] { "/pc", "/ordinateur" }, "Utiliser l'ordinateur", "/ordi", (player, args) => OuvrirOrdinateur(player)).Register();

            new SChatCommand("/ordi_ajouter", "Ajouter un ordinateur à ta position (admin)", "/ordi_ajouter", (player, args) =>
            {
                if (!player.IsAdmin) return;

                Vector3 pos = player.setup.transform.position;
                Config.Ordinateurs.Add(new Position { X = pos.x, Y = pos.y, Z = pos.z });
                SauvegarderConfig();
                player.Notify("Ordinateurs", $"Ordinateur ajouté ({Config.Ordinateurs.Count} au total).", NotificationManager.Type.Success);
            }).Register();

            new SChatCommand("/ordi_retirer", "Retirer l'ordinateur le plus proche (admin)", "/ordi_retirer", (player, args) =>
            {
                if (!player.IsAdmin) return;

                Position proche = OrdinateurLePlusProche(player, out float distance);
                if (proche == null || distance > 10f)
                {
                    player.Notify("Ordinateurs", "Aucun ordinateur à moins de 10 m.", NotificationManager.Type.Error);
                    return;
                }

                Config.Ordinateurs.Remove(proche);
                SauvegarderConfig();
                player.Notify("Ordinateurs", "Ordinateur retiré.", NotificationManager.Type.Success);
            }).Register();
        }

        // ------------------------------------------------------------------
        // Écrans d'accueil
        // ------------------------------------------------------------------

        private void OuvrirTelephone(Player joueur)
        {
            if (joueur?.character == null) return;

            Action accueil = () => OuvrirTelephone(joueur);
            Ecran.Liste(joueur, $"Téléphone · {DateTime.Now:HH:mm}", new[]
            {
                new Ligne("N  —  le réseau social", () => n.Ouvrir(joueur, accueil)),
                new Ligne("Neonous  —  rencontres", () => neonous.Ouvrir(joueur, accueil)),
                new Ligne("LeBonNeo  —  petites annonces", () => leBonNeo.Ouvrir(joueur, accueil)),
                new Ligne("Pôle Travail  —  emploi", () => poleTravail.Ouvrir(joueur, accueil, false)),
                new Ligne($"{Config.CapitalMotors.Nom}  —  concession", () => capitalMotors.Ouvrir(joueur, accueil)),
                new Ligne($"{Config.Journal.Nom}  —  journal", () => journal.Ouvrir(joueur, accueil, false)),
                new Ligne($"{Config.Police.Nom}  —  17, plaintes, amendes", () => police.Ouvrir(joueur, accueil, false)),
                new Ligne($"Météo  —  {meteo.Resume()}", () => meteo.Ouvrir(joueur, accueil)),
                new Ligne("Agenda" + (agenda.NombreAujourdhui > 0 ? $"  —  {agenda.NombreAujourdhui} événement(s) aujourd'hui" : "  —  événements"), () => agenda.Ouvrir(joueur, accueil)),
            }, null);
        }

        private void OuvrirOrdinateur(Player joueur)
        {
            if (joueur?.character == null) return;

            if (Config.Ordinateurs.Count > 0)
            {
                OrdinateurLePlusProche(joueur, out float distance);
                if (distance > Config.DistanceOrdinateur)
                {
                    joueur.Notify("Ordinateur", "Il n'y a pas d'ordinateur à proximité.", NotificationManager.Type.Error);
                    return;
                }
            }

            Action bureau = () => OuvrirOrdinateur(joueur);
            Ecran.Liste(joueur, "Ordinateur · Bureau", new[]
            {
                new Ligne("Discord", () => discordApp.Ouvrir(joueur, bureau)),
                new Ligne("Blue Lightfox  —  navigateur web", () => blueLightfox.Ouvrir(joueur, bureau)),
                new Ligne("Pôle Travail", () => poleTravail.Ouvrir(joueur, bureau, true)),
                new Ligne("LeBonNeo", () => leBonNeo.Ouvrir(joueur, bureau)),
                new Ligne(journal.EstJournaliste(joueur) ? $"{Config.Journal.Nom}  —  rédaction" : Config.Journal.Nom, () => journal.Ouvrir(joueur, bureau, true)),
                new Ligne(police.EstAgent(joueur) ? $"{Config.Police.Nom}  —  intranet" : Config.Police.Nom, () => police.Ouvrir(joueur, bureau, true)),
            }, null);
        }

        private Position OrdinateurLePlusProche(Player joueur, out float distance)
        {
            Vector3 pos = joueur.setup.transform.position;
            Position proche = Config.Ordinateurs
                .OrderBy(o => Vector3.Distance(pos, new Vector3(o.X, o.Y, o.Z)))
                .FirstOrDefault();

            distance = proche == null ? float.MaxValue : Vector3.Distance(pos, new Vector3(proche.X, proche.Y, proche.Z));
            return proche;
        }

        // ------------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------------

        private void ChargerConfig()
        {
            cheminConfig = Path.Combine(Dossier, "config.json");
            Config = new Config();

            if (File.Exists(cheminConfig))
            {
                try
                {
                    // Replace : sinon les listes par défaut (salons, sites...) seraient ajoutées à celles du fichier.
                    Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(cheminConfig),
                        new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }) ?? new Config();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NovaApps] config.json invalide, valeurs par défaut utilisées : {e.Message}");
                    Config = new Config();
                    toucheTelephone = LireTouche(Config.ToucheTelephone);
                    toucheOrdinateur = LireTouche(Config.ToucheOrdinateur);
                    return; // On ne réécrit pas le fichier pour ne pas perdre ce que l'admin a tapé.
                }
            }

            toucheTelephone = LireTouche(Config.ToucheTelephone);
            toucheOrdinateur = LireTouche(Config.ToucheOrdinateur);
            SauvegarderConfig();
        }

        private void SauvegarderConfig()
        {
            File.WriteAllText(cheminConfig, JsonConvert.SerializeObject(Config, Formatting.Indented));
        }

        private static KeyCode LireTouche(string nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return KeyCode.None;
            if (Enum.TryParse(nom.Trim(), true, out KeyCode touche)) return touche;

            Debug.LogWarning($"[NovaApps] Touche inconnue \"{nom}\" dans config.json : ignorée.");
            return KeyCode.None;
        }
    }
}
