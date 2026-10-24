using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Mirror;
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

            new SChatCommand("/ordi_ajouter", "Ajouter un ordinateur à ta position (admin). Ajoute des ID d'entreprise pour le réserver.", "/ordi_ajouter [idEntreprise...]", (player, args) =>
            {
                if (!player.IsAdmin) return;

                Vector3 pos = player.setup.transform.position;
                List<int> entreprises = args.Select(a => int.TryParse(a, out int id) ? id : -1).Where(id => id >= 0).ToList();
                Config.Ordinateurs.Add(new Position { X = pos.x, Y = pos.y, Z = pos.z, Entreprises = entreprises });
                SauvegarderConfig();
                player.Notify("Ordinateurs", $"Ordinateur ajouté{(entreprises.Count > 0 ? $", réservé aux entreprises {string.Join(", ", entreprises)}" : "")} ({Config.Ordinateurs.Count} au total).", NotificationManager.Type.Success);
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

            new SChatCommand("/tel_objets", "Afficher les ID des objets de ton inventaire (admin)", "/tel_objets", (player, args) =>
            {
                if (!player.IsAdmin) return;

                Dictionary<int, int> objets = Inventaire.Lire(player);
                if (objets == null)
                {
                    string structure = Inventaire.Structure(player);
                    Debug.Log($"[NovaApps] Structure de l'inventaire : {structure}");
                    player.SendText("<color=#e74c3c>[NovaApps]</color> Inventaire illisible. La structure a été écrite dans la console du serveur : envoie-la au développeur.");
                    return;
                }

                player.SendText("<color=#3498db>[NovaApps]</color> Objets sur toi (ID : nom x quantité) :");
                foreach (KeyValuePair<int, int> objet in objets.OrderBy(o => o.Key))
                    player.SendText($"  {objet.Key} : {Inventaire.NomObjet(objet.Key)} x{objet.Value}");
                player.SendText("Mets l'ID du téléphone dans ObjetsTelephone (config.json).");
            }).Register();
        }

        // ------------------------------------------------------------------
        // Restrictions RP
        // ------------------------------------------------------------------

        /// <summary>Vérifie que le joueur peut se servir d'un téléphone ou d'un ordinateur, et explique sinon.</summary>
        private bool PeutUtiliser(Player joueur, bool telephone)
        {
            string refus = null;

            if (Config.BloquerSiAterre && joueur.character.Health <= 0)
                refus = "Tu es à terre, impossible d'utiliser ça.";
            else if (telephone && Config.TelephoneConfisqueEnPrison && joueur.character.PrisonTime > 0)
                refus = "Ton téléphone a été confisqué à ton arrivée en prison.";
            else if (telephone && Config.ObjetsTelephone.Count > 0 && Inventaire.Possede(joueur, Config.ObjetsTelephone) == false)
                refus = "Tu n'as pas de téléphone sur toi.";

            if (refus == null) return true;

            joueur.Notify(telephone ? "Téléphone" : "Ordinateur", refus, NotificationManager.Type.Error);
            return false;
        }

        // ------------------------------------------------------------------
        // Téléphone
        // ------------------------------------------------------------------

        /// <summary>Heure à laquelle chaque personnage a sorti son téléphone pour la première fois depuis sa connexion.</summary>
        private readonly Dictionary<int, DateTime> debutBatterie = new Dictionary<int, DateTime>();

        /// <summary>Batterie décorative : se vide doucement au fil de la session, rechargée à chaque connexion.</summary>
        private int Batterie(Player joueur)
        {
            int id = Joueurs.Id(joueur);
            if (!debutBatterie.TryGetValue(id, out DateTime debut))
                debutBatterie[id] = debut = DateTime.Now;
            return Mathf.Clamp(100 - (int)((DateTime.Now - debut).TotalMinutes / 6), 5, 100);
        }

        public override void OnPlayerDisconnect(NetworkConnection conn)
        {
            base.OnPlayerDisconnect(conn);
            Player joueur = Nova.server.Players.FirstOrDefault(p => p?.conn == conn);
            if (joueur?.character != null) debutBatterie.Remove(joueur.character.Id);
        }

        private string BarreDeStatut(Player joueur) => $"{DateTime.Now:HH:mm}      4G      {Batterie(joueur)} %";

        private static string Pastille(int nombre) => nombre > 0 ? $"   ({nombre})" : "";

        /// <summary>Notifications en attente, appli par appli (écran de verrouillage).</summary>
        private List<string> Notifications(Player joueur)
        {
            List<string> lignes = new List<string>();
            void Ajouter(string appli, int nombre, string quoi)
            {
                if (nombre > 0) lignes.Add($"<b>{appli}</b>  —  {nombre} {quoi}");
            }

            Ajouter("N", n.Badge(joueur), "mention(s) ou réponse(s)");
            Ajouter("Neonous", neonous.Badge(joueur), "nouveau(x) match(s) ou message(s)");
            Ajouter("LeBonNeo", leBonNeo.Badge(joueur), "conversation(s) non lue(s)");
            Ajouter("Pôle Travail", poleTravail.Badge(joueur), "candidature(s) à traiter");
            Ajouter(Config.CapitalMotors.Nom, capitalMotors.Badge(joueur), "demande(s) à traiter");
            Ajouter(Config.Journal.Nom, journal.Badge(joueur), "nouvel(s) article(s)");
            Ajouter(Config.Police.Nom, police.Badge(joueur), "élément(s) en attente");
            Ajouter("Agenda", agenda.Badge(joueur), "événement(s) aujourd'hui");
            return lignes;
        }

        /// <summary>Écran de verrouillage : heure, date et notifications, puis déverrouillage.</summary>
        private void OuvrirTelephone(Player joueur)
        {
            if (joueur?.character == null || !PeutUtiliser(joueur, true)) return;

            List<string> notifications = Notifications(joueur);
            Ecran.Texte(joueur, BarreDeStatut(joueur),
                $"<align=center><size=250%><b>{DateTime.Now:HH:mm}</b></size>\n{DateTime.Now.ToString("dddd d MMMM", Ecran.Francais)}\n{meteo.Resume()}</align>\n\n" +
                (notifications.Count > 0 ? string.Join("\n", notifications) : "<align=center><i>Aucune notification</i></align>"),
                null, new Bouton("Déverrouiller", () => Accueil(joueur)));
        }

        private void Accueil(Player joueur)
        {
            if (joueur?.character == null || !PeutUtiliser(joueur, true)) return;

            Action accueil = () => Accueil(joueur);
            Ecran.Liste(joueur, BarreDeStatut(joueur), new[]
            {
                new Ligne("N  —  le réseau social" + Pastille(n.Badge(joueur)), () => n.Ouvrir(joueur, accueil)),
                new Ligne("Neonous  —  rencontres" + Pastille(neonous.Badge(joueur)), () => neonous.Ouvrir(joueur, accueil)),
                new Ligne("LeBonNeo  —  petites annonces" + Pastille(leBonNeo.Badge(joueur)), () => leBonNeo.Ouvrir(joueur, accueil)),
                new Ligne("Pôle Travail  —  emploi" + Pastille(poleTravail.Badge(joueur)), () => poleTravail.Ouvrir(joueur, accueil, false)),
                new Ligne($"{Config.CapitalMotors.Nom}  —  concession" + Pastille(capitalMotors.Badge(joueur)), () => capitalMotors.Ouvrir(joueur, accueil)),
                new Ligne($"{Config.Journal.Nom}  —  journal" + Pastille(journal.Badge(joueur)), () => journal.Ouvrir(joueur, accueil, false)),
                new Ligne($"{Config.Police.Nom}  —  17, plaintes, amendes" + Pastille(police.Badge(joueur)), () => police.Ouvrir(joueur, accueil, false)),
                new Ligne($"Météo  —  {meteo.Resume()}", () => meteo.Ouvrir(joueur, accueil)),
                new Ligne("Agenda  —  événements" + Pastille(agenda.Badge(joueur)), () => agenda.Ouvrir(joueur, accueil)),
                new Ligne("Verrouiller", () => OuvrirTelephone(joueur)),
            }, null);
        }

        // ------------------------------------------------------------------
        // Ordinateur
        // ------------------------------------------------------------------

        private void OuvrirOrdinateur(Player joueur)
        {
            if (joueur?.character == null || !PeutUtiliser(joueur, false)) return;

            if (Config.Ordinateurs.Count > 0)
            {
                Position pc = OrdinateurLePlusProche(joueur, out float distance);
                if (distance > Config.DistanceOrdinateur)
                {
                    joueur.Notify("Ordinateur", "Il n'y a pas d'ordinateur à proximité.", NotificationManager.Type.Error);
                    return;
                }

                if (pc.Entreprises != null && pc.Entreprises.Count > 0 && !Joueurs.Membre(joueur, pc.Entreprises))
                {
                    joueur.Notify("Ordinateur", "Session verrouillée : mot de passe requis. Ce poste est réservé au personnel.", NotificationManager.Type.Error);
                    return;
                }
            }

            Action bureau = () => OuvrirOrdinateur(joueur);
            Ecran.Liste(joueur, $"Bureau · session de {joueur.character.Firstname}      {DateTime.Now:HH:mm}", new[]
            {
                new Ligne("Discord", () => discordApp.Ouvrir(joueur, bureau)),
                new Ligne("Blue Lightfox  —  navigateur web", () => blueLightfox.Ouvrir(joueur, bureau)),
                new Ligne("Pôle Travail" + Pastille(poleTravail.Badge(joueur)), () => poleTravail.Ouvrir(joueur, bureau, true)),
                new Ligne("LeBonNeo" + Pastille(leBonNeo.Badge(joueur)), () => leBonNeo.Ouvrir(joueur, bureau)),
                new Ligne((journal.EstJournaliste(joueur) ? $"{Config.Journal.Nom}  —  rédaction" : Config.Journal.Nom) + Pastille(journal.Badge(joueur)), () => journal.Ouvrir(joueur, bureau, true)),
                new Ligne((police.EstAgent(joueur) ? $"{Config.Police.Nom}  —  intranet" : Config.Police.Nom) + Pastille(police.Badge(joueur)), () => police.Ouvrir(joueur, bureau, true)),
                new Ligne("Fermer la session", () => { }),
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
