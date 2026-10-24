using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Newtonsoft.Json.Linq;
using NovaApps.Core;

namespace NovaApps.Apps
{
    public class PoleTravailDonnees
    {
        public int ProchainId = 1;
        public List<Offre> Offres = new List<Offre>();
        public List<Candidature> Candidatures = new List<Candidature>();
        /// <summary>Recherches d'emploi (CV) déposées par les demandeurs, par ID de personnage.</summary>
        public Dictionary<int, RechercheEmploi> Recherches = new Dictionary<int, RechercheEmploi>();
    }

    public class Offre
    {
        public int Id;
        public int RecruteurId;
        public string RecruteurNom;
        public string Entreprise;
        public string Intitule;
        public string Salaire;
        public string Description;
        public string Contact;
        public DateTime Date;
        public bool Ouverte = true;
    }

    public class Candidature
    {
        public int Id;
        public int OffreId;
        public int CandidatId;
        public string CandidatNom;
        public string Message;
        public DateTime Date;
        public string Statut = StatutCandidature.EnAttente;
    }

    public static class StatutCandidature
    {
        public const string EnAttente = "En attente";
        public const string Acceptee = "Acceptée";
        public const string Refusee = "Refusée";
    }

    public class RechercheEmploi
    {
        public int PersonnageId;
        public string Nom;
        public string Metier;
        public string Experience;
        public string Disponibilites;
        public string Contact;
        public DateTime Date;
    }

    /// <summary>
    /// Pôle Travail : même appli et mêmes données sur le téléphone et l'ordinateur.
    /// Le téléphone permet de consulter et postuler ; l'ordinateur permet en plus de publier des offres
    /// et de déposer une recherche d'emploi (sauf si PublicationDepuisTelephone = true).
    /// </summary>
    public class PoleTravailApp
    {
        private const string Vert = "#2ecc71";

        private readonly Stockage<PoleTravailDonnees> stockage = new Stockage<PoleTravailDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "pole-travail.json"));
        private PoleTravailDonnees D => stockage.Donnees;
        private PoleTravailConfig Config => NovaAppsPlugin.Config.PoleTravail;

        public int NombreOffresOuvertes => D.Offres.Count(o => o.Ouverte);

        /// <summary>Candidatures en attente sur les offres du joueur.</summary>
        public int Badge(Player joueur)
        {
            int moi = Joueurs.Id(joueur);
            return D.Candidatures.Count(c => c.Statut == StatutCandidature.EnAttente && D.Offres.Any(o => o.Id == c.OffreId && o.RecruteurId == moi));
        }

        public void Ouvrir(Player joueur, Action retour, bool surOrdinateur)
        {
            int moi = Joueurs.Id(joueur);
            bool publier = surOrdinateur || Config.PublicationDepuisTelephone;
            Action menu = () => Ouvrir(joueur, retour, surOrdinateur);

            int aTraiter = D.Candidatures.Count(c => c.Statut == StatutCandidature.EnAttente && D.Offres.Any(o => o.Id == c.OffreId && o.RecruteurId == moi));

            List<Ligne> lignes = new List<Ligne>
            {
                new Ligne($"Offres d'emploi ({NombreOffresOuvertes})", () => Offres(joueur, null, menu)),
                new Ligne("Rechercher une offre", () => Ecran.Saisie(joueur, "Pôle Travail", "Métier, entreprise ou mot-clé :", "Ex : mécanicien", mot => Offres(joueur, mot, menu), menu, 50)),
                new Ligne("Mes candidatures", () => MesCandidatures(joueur, menu)),
                new Ligne(publier ? "Ma recherche d'emploi (CV)" : "Ma recherche d'emploi (CV)  —  modifiable sur ordinateur", () => MaRecherche(joueur, menu, publier)),
                new Ligne($"Espace recruteur" + (aTraiter > 0 ? $"  ·  {aTraiter} candidature(s) à traiter" : ""), () => EspaceRecruteur(joueur, menu, publier)),
            };

            Ecran.Liste(joueur, surOrdinateur ? "Pôle Travail · pole-travail.nl" : "Pôle Travail", lignes, retour);
        }

        // ------------------------------------------------------------------
        // Côté demandeur d'emploi
        // ------------------------------------------------------------------

        public void Offres(Player joueur, string recherche, Action retour)
        {
            Action recharger = () => Offres(joueur, recherche, retour);
            IEnumerable<Offre> offres = D.Offres.Where(o => o.Ouverte);

            if (!string.IsNullOrWhiteSpace(recherche))
                offres = offres.Where(o => $"{o.Intitule} {o.Entreprise} {o.Description}".IndexOf(recherche, StringComparison.OrdinalIgnoreCase) >= 0);

            Ecran.Liste(joueur, string.IsNullOrWhiteSpace(recherche) ? "Pôle Travail · Offres" : $"Pôle Travail · « {recherche} »",
                offres.OrderByDescending(o => o.Date)
                    .Select(o => new Ligne($"{o.Intitule}  —  {o.Entreprise}  ·  {o.Salaire}", () => VoirOffre(joueur, o, recharger))),
                retour, "Aucune offre pour le moment.");
        }

        private void VoirOffre(Player joueur, Offre offre, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Candidature dejaPostule = D.Candidatures.FirstOrDefault(c => c.OffreId == offre.Id && c.CandidatId == moi);

            string texte = $"{Ecran.Couleur($"<b>{offre.Intitule}</b>", Vert)}\n<b>{offre.Entreprise}</b>  ·  publiée le {offre.Date:dd/MM}\n\n" +
                           $"<b>Salaire :</b> {offre.Salaire}\n<b>Contact :</b> {offre.Contact}\n\n{offre.Description}";
            if (dejaPostule != null)
                texte += $"\n\n<i>Tu as postulé le {dejaPostule.Date:dd/MM} · {dejaPostule.Statut}</i>";

            Ecran.Texte(joueur, "Pôle Travail · Offre", texte, retour,
                offre.RecruteurId != moi && dejaPostule == null && offre.Ouverte ? new Bouton("Postuler", () => Postuler(joueur, offre, retour)) : null);
        }

        private void Postuler(Player joueur, Offre offre, Action retour)
        {
            Ecran.Saisie(joueur, "Pôle Travail · Postuler", $"Ta candidature pour « {offre.Intitule} » ({offre.Entreprise}).\nPrésente-toi en quelques lignes et indique comment te joindre :",
                "Ex : Motivé, dispo le soir, 555-1234", message =>
                {
                    int moi = Joueurs.Id(joueur);
                    D.Candidatures.Add(new Candidature
                    {
                        Id = D.ProchainId++,
                        OffreId = offre.Id,
                        CandidatId = moi,
                        CandidatNom = Joueurs.Nom(joueur),
                        Message = message,
                        Date = DateTime.Now,
                    });
                    stockage.Sauvegarder();

                    Joueurs.Notifier(offre.RecruteurId, "Pôle Travail", $"Nouvelle candidature de {Joueurs.Nom(joueur)} pour « {offre.Intitule} ».");
                    joueur.Notify("Pôle Travail", "Candidature envoyée ! Le recruteur sera prévenu.", NotificationManager.Type.Success);
                    retour();
                }, retour, 500);
        }

        private void MesCandidatures(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action recharger = () => MesCandidatures(joueur, retour);

            Ecran.Liste(joueur, "Pôle Travail · Mes candidatures",
                D.Candidatures.Where(c => c.CandidatId == moi).OrderByDescending(c => c.Date).Select(c =>
                {
                    Offre offre = D.Offres.FirstOrDefault(o => o.Id == c.OffreId);
                    string titre = offre != null ? $"{offre.Intitule} ({offre.Entreprise})" : "Offre supprimée";
                    return new Ligne($"{titre}  —  {c.Statut}", () =>
                    {
                        if (offre != null) VoirOffre(joueur, offre, recharger);
                        else recharger();
                    });
                }),
                retour, "Tu n'as postulé à aucune offre.");
        }

        private void MaRecherche(Player joueur, Action retour, bool modifiable)
        {
            int moi = Joueurs.Id(joueur);
            Action relire = () => MaRecherche(joueur, retour, modifiable);

            if (!D.Recherches.TryGetValue(moi, out RechercheEmploi r))
            {
                Ecran.Texte(joueur, "Pôle Travail · Ma recherche",
                    "Tu n'as pas encore déposé de recherche d'emploi.\nLes recruteurs peuvent consulter les recherches déposées et te contacter." +
                    (modifiable ? "" : "\n\n<i>Le dépôt se fait depuis un ordinateur.</i>"),
                    retour, modifiable ? new Bouton("Déposer ma recherche", () => DeposerRecherche(joueur, relire)) : null);
                return;
            }

            Ecran.Texte(joueur, "Pôle Travail · Ma recherche", TexteRecherche(r), retour,
                modifiable ? new Bouton("Modifier", () => DeposerRecherche(joueur, relire)) : null,
                new Bouton("Retirer ma recherche", () => Ecran.Confirmer(joueur, "Pôle Travail", "Retirer ta recherche d'emploi ?", () =>
                {
                    D.Recherches.Remove(moi);
                    stockage.Sauvegarder();
                    relire();
                }, relire)));
        }

        private void DeposerRecherche(Player joueur, Action retour)
        {
            string telephone = joueur.character.PhoneNumber;

            Ecran.Formulaire(joueur, "Pôle Travail · Ma recherche", new List<Champ>
            {
                new Champ("metier", "Quel(s) métier(s) recherches-tu ?", "Ex : Mécanicien, chauffeur, vendeur", 100),
                new Champ("experience", "Ton expérience / tes compétences :", "Ex : 2 ans au garage de Saint-Branchs, permis poids lourd", 500),
                new Champ("dispos", "Tes disponibilités :", "Ex : tous les soirs à partir de 21h", 150),
                new Champ("contact", "Comment te contacter ?", "Ex : 555-1234", 100) { ParDefaut = string.IsNullOrEmpty(telephone) ? null : telephone },
            }, reponses =>
            {
                int moi = Joueurs.Id(joueur);
                D.Recherches[moi] = new RechercheEmploi
                {
                    PersonnageId = moi,
                    Nom = Joueurs.Nom(joueur),
                    Metier = reponses["metier"],
                    Experience = reponses["experience"],
                    Disponibilites = reponses["dispos"],
                    Contact = reponses["contact"],
                    Date = DateTime.Now,
                };
                stockage.Sauvegarder();
                joueur.Notify("Pôle Travail", "Recherche d'emploi publiée !", NotificationManager.Type.Success);
                retour();
            }, retour);
        }

        private static string TexteRecherche(RechercheEmploi r)
        {
            return $"{Ecran.Couleur($"<b>{r.Nom}</b>", Vert)}  ·  déposée le {r.Date:dd/MM}\n\n<b>Recherche :</b> {r.Metier}\n<b>Disponibilités :</b> {r.Disponibilites}\n<b>Contact :</b> {r.Contact}\n\n{r.Experience}";
        }

        // ------------------------------------------------------------------
        // Côté recruteur
        // ------------------------------------------------------------------

        private void EspaceRecruteur(Player joueur, Action retour, bool publier)
        {
            Action menu = () => EspaceRecruteur(joueur, retour, publier);
            Ecran.Liste(joueur, "Pôle Travail · Espace recruteur", new[]
            {
                new Ligne(publier ? "Publier une offre" : "Publier une offre  —  depuis un ordinateur", () =>
                {
                    if (publier) PublierOffre(joueur, menu);
                    else Ecran.Texte(joueur, "Pôle Travail", "La publication d'offres se fait depuis un ordinateur.", menu);
                }),
                new Ligne("Mes offres", () => MesOffres(joueur, menu)),
                new Ligne($"Consulter les recherches d'emploi ({D.Recherches.Count})", () => Recherches(joueur, menu)),
            }, retour);
        }

        private void PublierOffre(Player joueur, Action retour)
        {
            string entreprise = joueur.HasBiz ? joueur.biz.BizName : null;
            string telephone = joueur.character.PhoneNumber;

            Ecran.Formulaire(joueur, "Pôle Travail · Nouvelle offre", new List<Champ>
            {
                new Champ("intitule", "Intitulé du poste :", "Ex : Mécanicien(ne) confirmé(e)", 60),
                new Champ("entreprise", "Entreprise :", "Ex : Garage Capital Motors", 60) { ParDefaut = entreprise },
                new Champ("salaire", "Salaire :", "Ex : 1 500 € / service", 40),
                new Champ("description", "Description du poste et du profil recherché :", "Missions, horaires, conditions...", 800),
                new Champ("contact", "Contact :", "Ex : 555-1234 ou passer au garage", 100) { ParDefaut = string.IsNullOrEmpty(telephone) ? null : telephone },
            }, reponses =>
            {
                Offre offre = new Offre
                {
                    Id = D.ProchainId++,
                    RecruteurId = Joueurs.Id(joueur),
                    RecruteurNom = Joueurs.Nom(joueur),
                    Intitule = reponses["intitule"],
                    Entreprise = reponses["entreprise"],
                    Salaire = reponses["salaire"],
                    Description = reponses["description"],
                    Contact = reponses["contact"],
                    Date = DateTime.Now,
                };

                Ecran.Confirmer(joueur, "Pôle Travail · Aperçu",
                    $"{Ecran.Couleur($"<b>{offre.Intitule}</b>", Vert)}\n<b>{offre.Entreprise}</b>\n\n<b>Salaire :</b> {offre.Salaire}\n<b>Contact :</b> {offre.Contact}\n\n{offre.Description}",
                    () =>
                    {
                        D.Offres.Add(offre);
                        stockage.Sauvegarder();
                        AnnoncerSurDiscord(offre);

                        // Prévient les demandeurs dont la recherche correspond au poste.
                        foreach (RechercheEmploi r in D.Recherches.Values.Where(r => r.PersonnageId != offre.RecruteurId && Correspond(r, offre)))
                            Joueurs.Notifier(r.PersonnageId, "Pôle Travail", $"Nouvelle offre qui pourrait t'intéresser : {offre.Intitule} ({offre.Entreprise}).");

                        joueur.Notify("Pôle Travail", "Offre publiée !", NotificationManager.Type.Success);
                        retour();
                    }, retour);
            }, retour);
        }

        private static bool Correspond(RechercheEmploi recherche, Offre offre)
        {
            return recherche.Metier
                .Split(new[] { ',', ' ', '/', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(mot => mot.Length >= 4)
                .Any(mot => offre.Intitule.IndexOf(mot, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void AnnoncerSurDiscord(Offre offre)
        {
            JObject embed = new JObject
            {
                ["title"] = $"💼 {offre.Intitule}",
                ["description"] = Ecran.Couper(offre.Description, 2000),
                ["color"] = 0x2ecc71,
                ["fields"] = new JArray
                {
                    Discord.Field("Entreprise", offre.Entreprise),
                    Discord.Field("Salaire", offre.Salaire),
                    Discord.Field("Contact", offre.Contact),
                },
                ["footer"] = new JObject { ["text"] = "Postule en jeu depuis l'appli Pôle Travail" },
            };
            _ = NovaAppsPlugin.Discord.PosterAvecWebhook(Config.WebhookDiscord, "Pôle Travail", "Nouvelle offre d'emploi !", embed);
        }

        private void MesOffres(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action recharger = () => MesOffres(joueur, retour);

            // Les admins voient toutes les offres pour pouvoir modérer.
            Ecran.Liste(joueur, joueur.IsAdmin ? "Pôle Travail · Toutes les offres (admin)" : "Pôle Travail · Mes offres",
                D.Offres.Where(o => o.RecruteurId == moi || joueur.IsAdmin).OrderByDescending(o => o.Date).Select(o =>
                {
                    int enAttente = D.Candidatures.Count(c => c.OffreId == o.Id && c.Statut == StatutCandidature.EnAttente);
                    return new Ligne($"{o.Intitule}  —  {(o.Ouverte ? "ouverte" : "fermée")}  ·  {enAttente} en attente", () => GererOffre(joueur, o, recharger));
                }),
                retour, "Tu n'as publié aucune offre.");
        }

        private void GererOffre(Player joueur, Offre offre, Action retour)
        {
            Action relire = () => GererOffre(joueur, offre, retour);
            List<Candidature> candidatures = D.Candidatures.Where(c => c.OffreId == offre.Id).OrderBy(c => c.Date).ToList();

            Ecran.Texte(joueur, "Pôle Travail · " + offre.Intitule,
                $"{Ecran.Couleur($"<b>{offre.Intitule}</b>", Vert)}  ·  {offre.Entreprise}\nPubliée le {offre.Date:dd/MM} par {offre.RecruteurNom}  ·  {(offre.Ouverte ? "ouverte" : "fermée")}\n\n{candidatures.Count} candidature(s)",
                retour,
                new Bouton($"Candidatures ({candidatures.Count})", () => Ecran.Liste(joueur, "Pôle Travail · Candidatures",
                    candidatures.Select(c => new Ligne($"{c.CandidatNom}  —  {c.Statut}  ·  {c.Date:dd/MM}", () => VoirCandidature(joueur, offre, c, relire))),
                    relire, "Aucune candidature pour le moment.")),
                new Bouton(offre.Ouverte ? "Fermer l'offre" : "Rouvrir l'offre", () =>
                {
                    offre.Ouverte = !offre.Ouverte;
                    stockage.Sauvegarder();
                    relire();
                }),
                new Bouton("Supprimer", () => Ecran.Confirmer(joueur, "Pôle Travail", "Supprimer définitivement cette offre et ses candidatures ?", () =>
                {
                    D.Offres.Remove(offre);
                    D.Candidatures.RemoveAll(c => c.OffreId == offre.Id);
                    stockage.Sauvegarder();
                    retour();
                }, relire)));
        }

        private void VoirCandidature(Player joueur, Offre offre, Candidature c, Action retour)
        {
            Action relire = () => VoirCandidature(joueur, offre, c, retour);
            string cv = D.Recherches.TryGetValue(c.CandidatId, out RechercheEmploi r) ? "\n\n<b>Sa recherche d'emploi :</b>\n" + TexteRecherche(r) : "";

            Ecran.Texte(joueur, "Pôle Travail · Candidature",
                $"<b>{c.CandidatNom}</b>  ·  {c.Date:dd/MM HH:mm}  ·  {c.Statut}\n\n{c.Message}{cv}",
                retour,
                c.Statut != StatutCandidature.Acceptee ? new Bouton("Accepter", () => Repondre(c, offre, StatutCandidature.Acceptee, relire)) : null,
                c.Statut != StatutCandidature.Refusee ? new Bouton("Refuser", () => Repondre(c, offre, StatutCandidature.Refusee, relire)) : null);
        }

        private void Repondre(Candidature c, Offre offre, string statut, Action retour)
        {
            c.Statut = statut;
            stockage.Sauvegarder();

            string message = statut == StatutCandidature.Acceptee
                ? $"Bonne nouvelle ! Ta candidature « {offre.Intitule} » a été acceptée. Contact : {offre.Contact}"
                : $"Ta candidature « {offre.Intitule} » n'a pas été retenue.";
            Joueurs.Notifier(c.CandidatId, "Pôle Travail", message,
                statut == StatutCandidature.Acceptee ? NotificationManager.Type.Success : NotificationManager.Type.Info);
            retour();
        }

        private void Recherches(Player joueur, Action retour)
        {
            Action recharger = () => Recherches(joueur, retour);
            Ecran.Liste(joueur, "Pôle Travail · Recherches d'emploi",
                D.Recherches.Values.OrderByDescending(r => r.Date)
                    .Select(r => new Ligne($"{r.Nom}  —  {Ecran.Couper(r.Metier, 50)}", () => Ecran.Texte(joueur, "Pôle Travail · " + r.Nom, TexteRecherche(r), recharger))),
                retour, "Aucune recherche d'emploi déposée.");
        }
    }
}
