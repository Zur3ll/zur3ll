using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using NovaApps.Core;

namespace NovaApps.Apps
{
    public class AgendaDonnees
    {
        public int ProchainId = 1;
        public List<Evenement> Evenements = new List<Evenement>();
    }

    public class Evenement
    {
        public int Id;
        public string Titre;
        public DateTime Date;
        public string Lieu;
        public string Description;
        public int OrganisateurId;
        public string Organisateur;
        /// <summary>Créé par un admin : annoncé à toute la ville.</summary>
        public bool Officiel;
        public HashSet<int> Participants = new HashSet<int>();
        public bool RappelEnvoye;
    }

    /// <summary>Agenda : événements de la ville (soirées, ouvertures, courses, réunions...), avec rappel avant le début.</summary>
    public class AgendaApp
    {
        private const string Violet = "#9b59b6";
        private static readonly string[] FormatsDate = { "d/M H:mm", "d/M/yyyy H:mm" };

        private readonly Stockage<AgendaDonnees> stockage = new Stockage<AgendaDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "agenda.json"));
        private AgendaDonnees D => stockage.Donnees;
        private AgendaConfig Config => NovaAppsPlugin.Config.Agenda;

        private IEnumerable<Evenement> AVenir => D.Evenements.Where(e => e.Date > DateTime.Now.AddHours(-3)).OrderBy(e => e.Date);

        public int NombreAujourdhui => AVenir.Count(e => e.Date.Date == DateTime.Today);

        /// <summary>Événements du jour auxquels le joueur participe ou qu'il organise.</summary>
        public int Badge(Player joueur)
        {
            int moi = Joueurs.Id(joueur);
            return AVenir.Count(e => e.Date.Date == DateTime.Today && (e.Participants.Contains(moi) || e.OrganisateurId == moi));
        }

        public void Ouvrir(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action menu = () => Ouvrir(joueur, retour);

            Ecran.Liste(joueur, "Agenda", new[]
            {
                new Ligne($"Aujourd'hui ({NombreAujourdhui})", () => Liste(joueur, "Aujourd'hui", AVenir.Where(e => e.Date.Date == DateTime.Today), menu)),
                new Ligne($"Tous les événements à venir ({AVenir.Count()})", () => Liste(joueur, "À venir", AVenir, menu)),
                new Ligne("Mes événements", () => Liste(joueur, "Mes événements", AVenir.Where(e => e.Participants.Contains(moi) || e.OrganisateurId == moi), menu)),
                new Ligne("Créer un événement", () => Creer(joueur, menu)),
            }, retour);
        }

        /// <summary>Appelé chaque minute : rappel aux participants et nettoyage des vieux événements.</summary>
        public void Tick()
        {
            bool modifie = false;

            foreach (Evenement e in D.Evenements.Where(e => !e.RappelEnvoye && e.Date <= DateTime.Now.AddMinutes(Config.RappelMinutes) && e.Date > DateTime.Now.AddMinutes(-5)))
            {
                e.RappelEnvoye = true;
                modifie = true;
                foreach (int id in e.Participants.Append(e.OrganisateurId).Distinct())
                    Joueurs.Notifier(id, "Agenda", $"« {e.Titre} » commence à {e.Date:HH:mm} — {e.Lieu}.", NotificationManager.Type.Warning);
            }

            modifie |= D.Evenements.RemoveAll(e => e.Date < DateTime.Now.AddDays(-7)) > 0;
            if (modifie) stockage.Sauvegarder();
        }

        private void Liste(Player joueur, string titre, IEnumerable<Evenement> evenements, Action retour)
        {
            Action recharger = () => Liste(joueur, titre, evenements, retour);
            Ecran.Liste(joueur, "Agenda · " + titre,
                evenements.Take(50).Select(e => new Ligne($"{QuandCourt(e.Date)}  {Ecran.Couper(e.Titre, 40)}  —  {Ecran.Couper(e.Lieu, 25)}{(e.Officiel ? "  ★" : "")}", () => Voir(joueur, e, recharger))),
                retour, "Aucun événement.");
        }

        private static string QuandCourt(DateTime date)
        {
            if (date.Date == DateTime.Today) return "Auj. " + date.ToString("HH:mm");
            if (date.Date == DateTime.Today.AddDays(1)) return "Dem. " + date.ToString("HH:mm");
            return date.ToString("ddd dd/MM HH:mm", Ecran.Francais);
        }

        private void Voir(Player joueur, Evenement e, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            bool participe = e.Participants.Contains(moi);
            bool organisateur = e.OrganisateurId == moi || joueur.IsAdmin;
            Action relire = () => Voir(joueur, e, retour);

            string etat = e.Date <= DateTime.Now ? Ecran.Couleur("\n<b>En cours</b>", Violet) : "";
            Ecran.Texte(joueur, "Agenda · Événement",
                $"{Ecran.Couleur($"<b>{e.Titre}</b>", Violet)}{(e.Officiel ? "  ★ officiel" : "")}{etat}\n<b>Quand :</b> {e.Date.ToString("dddd dd/MM à HH:mm", Ecran.Francais)}\n<b>Où :</b> {e.Lieu}\n" +
                $"<b>Organisé par :</b> {e.Organisateur}\n\n{e.Description}\n\n{e.Participants.Count} participant(s){(participe ? " · tu participes" : "")}",
                retour,
                e.OrganisateurId != moi ? new Bouton(participe ? "Je ne viens plus" : "Je participe", () =>
                {
                    if (participe) e.Participants.Remove(moi);
                    else
                    {
                        e.Participants.Add(moi);
                        Joueurs.Notifier(e.OrganisateurId, "Agenda", $"{Joueurs.Nom(joueur)} participe à « {Ecran.Couper(e.Titre, 30)} ».");
                    }
                    stockage.Sauvegarder();
                    relire();
                }) : null,
                organisateur ? new Bouton("Annuler l'événement", () => Ecran.Confirmer(joueur, "Agenda", "Annuler cet événement ? Les participants seront prévenus.", () =>
                {
                    D.Evenements.Remove(e);
                    stockage.Sauvegarder();
                    foreach (int id in e.Participants)
                        Joueurs.Notifier(id, "Agenda", $"« {e.Titre} » ({e.Date:dd/MM HH:mm}) est annulé.", NotificationManager.Type.Error);
                    retour();
                }, relire)) : null);
        }

        private void Creer(Player joueur, Action retour)
        {
            if (Config.CreationReserveeAuxEntreprises && !joueur.IsAdmin && !joueur.HasBiz)
            {
                Ecran.Texte(joueur, "Agenda", "Seules les entreprises peuvent créer des événements.", retour);
                return;
            }

            string organisateur = joueur.HasBiz ? joueur.biz.BizName : null;
            Ecran.Formulaire(joueur, "Agenda · Nouvel événement", new List<Champ>
            {
                new Champ("titre", "Nom de l'événement :", "Ex : Soirée d'ouverture du Neon Club", 60),
                new Champ("date", "Date et heure (jj/mm hh:mm), ou juste l'heure pour aujourd'hui :", "Ex : 25/10 21:30  ou  21:30", 20),
                new Champ("lieu", "Lieu :", "Ex : Neon Club, centre-ville", 80),
                new Champ("description", "Description :", "Programme, dress code, entrée...", 500),
                new Champ("organisateur", "Organisé par :", "Ex : Neon Club", 60) { ParDefaut = organisateur ?? Joueurs.Nom(joueur) },
            }, reponses =>
            {
                if (!LireDate(reponses["date"], out DateTime date))
                {
                    joueur.Notify("Agenda", "Date invalide. Exemples : 25/10 21:30 ou 21:30.", NotificationManager.Type.Error);
                    Creer(joueur, retour);
                    return;
                }

                Evenement e = new Evenement
                {
                    Id = D.ProchainId++,
                    Titre = reponses["titre"],
                    Date = date,
                    Lieu = reponses["lieu"],
                    Description = reponses["description"],
                    OrganisateurId = Joueurs.Id(joueur),
                    Organisateur = reponses["organisateur"],
                    Officiel = joueur.IsAdmin,
                };
                D.Evenements.Add(e);
                stockage.Sauvegarder();

                if (e.Officiel)
                    foreach (Player citoyen in Joueurs.TousEnLigne())
                        citoyen.Notify("Agenda", $"Nouvel événement : {e.Titre} — {QuandCourt(e.Date)}, {e.Lieu}.", NotificationManager.Type.Info);

                joueur.Notify("Agenda", "Événement créé !", NotificationManager.Type.Success);
                Voir(joueur, e, retour);
            }, retour);
        }

        /// <summary>Lit "25/10 21:30", "25/10/2026 21h30" ou juste "21:30" / "21h" (aujourd'hui, ou demain si l'heure est passée).</summary>
        private static bool LireDate(string texte, out DateTime date)
        {
            // "21h30" -> "21:30", "21h" -> "21:00"
            texte = Regex.Replace(texte.Trim().ToLowerInvariant(), @"(\d{1,2})h(\d{2})?$", m => $"{m.Groups[1].Value}:{(m.Groups[2].Success ? m.Groups[2].Value : "00")}");

            if (DateTime.TryParseExact(texte, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime heure))
            {
                date = DateTime.Today + heure.TimeOfDay;
                if (date < DateTime.Now) date = date.AddDays(1);
                return true;
            }

            if (DateTime.TryParseExact(texte, FormatsDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            {
                // Sans année précisée, une date passée désigne l'année prochaine.
                if (date < DateTime.Now.AddDays(-1) && texte.Count(c => c == '/') == 1) date = date.AddYears(1);
                return date > DateTime.Now.AddHours(-1);
            }

            return false;
        }
    }
}
