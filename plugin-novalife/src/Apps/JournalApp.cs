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
    public class JournalDonnees
    {
        public int ProchainId = 1;
        public List<Article> Articles = new List<Article>();
    }

    public class Article
    {
        public int Id;
        public string Titre;
        public string Rubrique;
        public List<string> Paragraphes = new List<string>();
        public int AuteurId;
        public string AuteurNom;
        public DateTime Date;
        public int Lectures;
        public List<EntreeFiche> Commentaires = new List<EntreeFiche>();
    }

    /// <summary>
    /// Journal RP : les journalistes (membres de l'entreprise du journal) écrivent des articles,
    /// tout le monde les lit et peut les commenter. Un article s'écrit paragraphe par paragraphe,
    /// car les champs de saisie du jeu sont courts.
    /// </summary>
    public class JournalApp
    {
        private const string Or = "#c8a24a";

        private readonly Stockage<JournalDonnees> stockage = new Stockage<JournalDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "journal.json"));
        /// <summary>Articles en cours d'écriture, par ID de personnage (non sauvegardés).</summary>
        private readonly Dictionary<int, Article> brouillons = new Dictionary<int, Article>();
        private JournalDonnees D => stockage.Donnees;
        private JournalConfig Config => NovaAppsPlugin.Config.Journal;

        public bool EstJournaliste(Player joueur) => Joueurs.Membre(joueur, Config.EntreprisesJournal);

        public void Ouvrir(Player joueur, Action retour, bool surOrdinateur)
        {
            Action menu = () => Ouvrir(joueur, retour, surOrdinateur);
            Article une = D.Articles.OrderByDescending(a => a.Date).FirstOrDefault();

            List<Ligne> lignes = new List<Ligne>();
            if (une != null)
                lignes.Add(new Ligne($"À LA UNE : {Ecran.Couper(une.Titre, 55)}", () => Lire(joueur, une, menu)));
            lignes.Add(new Ligne($"Derniers articles ({D.Articles.Count})", () => Articles(joueur, "Derniers articles", D.Articles, menu)));
            lignes.Add(new Ligne("Par rubrique", () => Ecran.Choix(joueur, $"{Config.Nom} · Rubriques", Config.Rubriques,
                rubrique => Articles(joueur, rubrique, D.Articles.Where(a => a.Rubrique == rubrique), menu), menu)));
            lignes.Add(new Ligne("Rechercher", () => Ecran.Saisie(joueur, Config.Nom, "Mot-clé :", "Ex : braquage", mot =>
                Articles(joueur, $"« {mot} »", D.Articles.Where(a => $"{a.Titre} {string.Join(" ", a.Paragraphes)}".IndexOf(mot, StringComparison.OrdinalIgnoreCase) >= 0), menu), menu, 50)));

            if (EstJournaliste(joueur))
            {
                if (surOrdinateur || !Config.RedactionSurOrdinateurUniquement)
                    lignes.Add(new Ligne(brouillons.ContainsKey(Joueurs.Id(joueur)) ? "[Rédaction] Reprendre mon brouillon" : "[Rédaction] Écrire un article", () => Rediger(joueur, menu)));
                else
                    lignes.Add(new Ligne("[Rédaction] Écrire un article  —  sur ordinateur", () => Ecran.Texte(joueur, Config.Nom, "La rédaction des articles se fait depuis un ordinateur.", menu)));
            }

            Ecran.Liste(joueur, Config.Nom, lignes, retour);
        }

        private void Articles(Player joueur, string titre, IEnumerable<Article> articles, Action retour)
        {
            Action recharger = () => Articles(joueur, titre, articles, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · {titre}",
                articles.OrderByDescending(a => a.Date).Take(40).Select(a =>
                    new Ligne($"{a.Date:dd/MM}  [{a.Rubrique}]  {Ecran.Couper(a.Titre, 55)}", () => Lire(joueur, a, recharger))),
                retour, "Aucun article.");
        }

        private string TexteArticle(Article a)
        {
            return $"{Ecran.Couleur($"<b>{a.Titre}</b>", Or)}\n<size=80%>{a.Rubrique} · par {a.AuteurNom} · {a.Date:dd/MM/yyyy HH:mm}</size>\n\n{string.Join("\n\n", a.Paragraphes)}";
        }

        private void Lire(Player joueur, Article a, Action retour, bool compter = true)
        {
            Action relire = () => Lire(joueur, a, retour, false);
            if (compter)
            {
                a.Lectures++;
                stockage.Sauvegarder();
            }

            bool auteur = a.AuteurId == Joueurs.Id(joueur) || joueur.IsAdmin;
            Ecran.Texte(joueur, Config.Nom, TexteArticle(a) + $"\n\n<size=80%>{a.Lectures} lecture(s) · {a.Commentaires.Count} commentaire(s)</size>", retour,
                new Bouton($"Commentaires ({a.Commentaires.Count})", () => Commentaires(joueur, a, relire)),
                auteur ? new Bouton("Supprimer", () => Ecran.Confirmer(joueur, Config.Nom, "Supprimer cet article ?", () =>
                {
                    D.Articles.Remove(a);
                    stockage.Sauvegarder();
                    retour();
                }, relire)) : null);
        }

        private void Commentaires(Player joueur, Article a, Action retour)
        {
            Action relire = () => Commentaires(joueur, a, retour);
            string texte = a.Commentaires.Count == 0 ? "<i>Aucun commentaire. Sois le premier !</i>" :
                string.Join("\n", a.Commentaires.Skip(Math.Max(0, a.Commentaires.Count - 15)).Select(c => $"<b>{c.Agent}</b> <size=80%>{Ecran.Date(c.Date)}</size> : {c.Texte}"));

            Ecran.Texte(joueur, $"{Config.Nom} · Commentaires", texte, retour,
                new Bouton("Commenter", () => Ecran.Saisie(joueur, Config.Nom, "Ton commentaire :", "Réagis à l'article...", t =>
                {
                    a.Commentaires.Add(new EntreeFiche { Date = DateTime.Now, Agent = Joueurs.Nom(joueur), Texte = t });
                    stockage.Sauvegarder();
                    if (a.AuteurId != Joueurs.Id(joueur))
                        Joueurs.Notifier(a.AuteurId, Config.Nom, $"{Joueurs.Nom(joueur)} a commenté « {Ecran.Couper(a.Titre, 30)} ».");
                    relire();
                }, relire, 250)));
        }

        // ------------------------------------------------------------------
        // Rédaction
        // ------------------------------------------------------------------

        private void Rediger(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            if (!brouillons.TryGetValue(moi, out Article brouillon))
            {
                Ecran.Saisie(joueur, $"{Config.Nom} · Nouvel article", "Titre de l'article :", "Ex : Braquage spectaculaire à la banque centrale", titre =>
                    Ecran.Choix(joueur, $"{Config.Nom} · Rubrique", Config.Rubriques, rubrique =>
                    {
                        brouillons[moi] = new Article { Titre = titre, Rubrique = rubrique, AuteurId = moi, AuteurNom = Joueurs.Nom(joueur), Date = DateTime.Now };
                        AjouterParagraphe(joueur, retour);
                    }, retour), retour, 100);
                return;
            }

            Action relire = () => Rediger(joueur, retour);
            Ecran.Texte(joueur, $"{Config.Nom} · Brouillon",
                TexteArticle(brouillon) + (brouillon.Paragraphes.Count == 0 ? "<i>(aucun paragraphe)</i>" : "") + $"\n\n<size=80%>{brouillon.Paragraphes.Count} paragraphe(s)</size>",
                retour,
                new Bouton("Ajouter un paragraphe", () => AjouterParagraphe(joueur, retour)),
                brouillon.Paragraphes.Count > 0 ? new Bouton("Retirer le dernier", () =>
                {
                    brouillon.Paragraphes.RemoveAt(brouillon.Paragraphes.Count - 1);
                    relire();
                }) : null,
                brouillon.Paragraphes.Count > 0 ? new Bouton("Publier", () => Publier(joueur, brouillon, false, retour)) : null,
                brouillon.Paragraphes.Count > 0 ? new Bouton("Publier en flash info", () => Publier(joueur, brouillon, true, retour)) : null,
                new Bouton("Jeter le brouillon", () => Ecran.Confirmer(joueur, Config.Nom, "Jeter ce brouillon ?", () =>
                {
                    brouillons.Remove(moi);
                    retour();
                }, relire)));
        }

        private void AjouterParagraphe(Player joueur, Action retour)
        {
            Article brouillon = brouillons[Joueurs.Id(joueur)];
            Ecran.Saisie(joueur, $"{Config.Nom} · Paragraphe {brouillon.Paragraphes.Count + 1}", "Écris le paragraphe :", "Texte du paragraphe...", texte =>
            {
                brouillon.Paragraphes.Add(texte);
                Rediger(joueur, retour);
            }, () => Rediger(joueur, retour), 1000);
        }

        private void Publier(Player joueur, Article brouillon, bool flash, Action retour)
        {
            brouillon.Id = D.ProchainId++;
            brouillon.Date = DateTime.Now;
            D.Articles.Add(brouillon);
            brouillons.Remove(Joueurs.Id(joueur));
            stockage.Sauvegarder();

            if (flash)
                foreach (Player lecteur in Joueurs.TousEnLigne())
                    lecteur.Notify($"{Config.Nom} · FLASH INFO", Ecran.Couper(brouillon.Titre, 80), NotificationManager.Type.Warning);

            _ = NovaAppsPlugin.Discord.PosterAvecWebhook(Config.WebhookDiscord, Config.Nom, flash ? "🚨 **FLASH INFO**" : "📰 Nouvel article", new JObject
            {
                ["title"] = Ecran.Couper(brouillon.Titre, 250),
                ["description"] = Ecran.Couper(string.Join("\n\n", brouillon.Paragraphes), 4000),
                ["color"] = 0xc8a24a,
                ["footer"] = new JObject { ["text"] = $"{brouillon.Rubrique} · par {brouillon.AuteurNom}" },
            });

            joueur.Notify(Config.Nom, "Article publié !", NotificationManager.Type.Success);
            Lire(joueur, brouillon, retour, false);
        }
    }
}
