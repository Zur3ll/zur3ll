using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Newtonsoft.Json.Linq;
using NovaApps.Core;
using UnityEngine;

namespace NovaApps.Apps
{
    public class ResultatWeb
    {
        public string Titre;
        public string Lien;
        public string Extrait;
        /// <summary>Titre de l'article Wikipédia, pour charger son résumé complet.</summary>
        public string ArticleWikipedia;
        /// <summary>Site interne au serveur (Pôle Travail, Capital Motors, sites configurés...).</summary>
        public Action<Player, Action> Ouvrir;
    }

    /// <summary>
    /// Blue Lightfox : navigateur de l'ordinateur. Les recherches sont de vraies recherches sur Internet :
    /// Google (Custom Search JSON API) si une clé est configurée, sinon Wikipédia.
    /// Le jeu ne peut afficher que du texte : on montre les titres, extraits et liens des résultats.
    /// </summary>
    public class BlueLightfoxApp
    {
        private const string Orange = "#ff7139";
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private readonly PoleTravailApp poleTravail;
        private readonly CapitalMotorsApp capitalMotors;
        private readonly LeBonNeoApp leBonNeo;
        private readonly JournalApp journal;
        private readonly PoliceApp police;
        private readonly List<string> historique = new List<string>();

        private BlueLightfoxConfig Config => NovaAppsPlugin.Config.BlueLightfox;
        private bool Google => !string.IsNullOrWhiteSpace(Config.GoogleCleApi) && !string.IsNullOrWhiteSpace(Config.GoogleMoteurId);

        static BlueLightfoxApp()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            // Wikipédia refuse les requêtes sans User-Agent.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NovaApps-BlueLightfox/1.0 (serveur Nova-Life)");
        }

        public BlueLightfoxApp(PoleTravailApp poleTravail, CapitalMotorsApp capitalMotors, LeBonNeoApp leBonNeo, JournalApp journal, PoliceApp police)
        {
            this.poleTravail = poleTravail;
            this.capitalMotors = capitalMotors;
            this.leBonNeo = leBonNeo;
            this.journal = journal;
            this.police = police;
        }

        public void Ouvrir(Player joueur, Action retour)
        {
            Action accueil = () => Ouvrir(joueur, retour);
            Ecran.Liste(joueur, "Blue Lightfox · Accueil", new[]
            {
                new Ligne(Google ? "Rechercher sur Google" : "Rechercher sur le web", () => DemanderRecherche(joueur, accueil)),
                new Ligne("pole-travail.nl  —  offres d'emploi", () => poleTravail.Ouvrir(joueur, accueil, true)),
                new Ligne("capital-motors.nl  —  catalogue de véhicules", () => capitalMotors.Catalogue(joueur, accueil)),
                new Ligne("lebonneo.nl  —  petites annonces", () => leBonNeo.Ouvrir(joueur, accueil)),
                new Ligne($"{SiteJournal}  —  {NovaAppsPlugin.Config.Journal.Nom}", () => journal.Ouvrir(joueur, accueil, true)),
                new Ligne("police.nl  —  plaintes, amendes, avis de recherche", () => police.Ouvrir(joueur, accueil, true)),
            }.Concat(Config.Sites.Select(s => new Ligne($"{s.Adresse}  —  {s.Nom}", () => OuvrirSite(joueur, s, accueil)))), retour);
        }

        private void DemanderRecherche(Player joueur, Action retour)
        {
            string derniere = historique.LastOrDefault();
            Ecran.Saisie(joueur, "Blue Lightfox", Google ? "Rechercher sur Google :" : "Rechercher sur le web (Wikipédia) :",
                derniere != null ? $"Dernière recherche : {derniere}" : "Ex : Amboise", recherche =>
                {
                    historique.Add(recherche);
                    if (historique.Count > 20) historique.RemoveAt(0);
                    Rechercher(joueur, recherche, retour);
                }, retour, 100);
        }

        private async void Rechercher(Player joueur, string recherche, Action retour)
        {
            Action relancer = () => Rechercher(joueur, recherche, retour);
            List<ResultatWeb> resultats = SitesInternes(recherche);
            string erreur = null;

            try
            {
                resultats.AddRange(Google ? await RechercheGoogle(recherche) : await RechercheWikipedia(recherche));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NovaApps] Recherche « {recherche} » impossible : {e.Message}");
                erreur = "Connexion impossible";
            }

            if (joueur?.character == null) return;

            List<Ligne> lignes = resultats.Select(r => new Ligne(Ecran.Couper(r.Titre, 70) + (r.Lien != null ? $"  —  {Domaine(r.Lien)}" : ""), () =>
            {
                if (r.Ouvrir != null) r.Ouvrir(joueur, relancer);
                else Page(joueur, r, relancer);
            })).ToList();
            lignes.Add(new Ligne("Nouvelle recherche", () => DemanderRecherche(joueur, retour)));

            Ecran.Liste(joueur, $"Blue Lightfox · {Ecran.Couper(recherche, 40)}" + (erreur != null ? $" ({erreur})" : ""), lignes, retour);
        }

        private async void Page(Player joueur, ResultatWeb resultat, Action retour)
        {
            string contenu = resultat.Extrait;

            if (resultat.ArticleWikipedia != null)
            {
                try
                {
                    contenu = await ResumeWikipedia(resultat.ArticleWikipedia) ?? contenu;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NovaApps] Article Wikipédia indisponible : {e.Message}");
                }
            }

            if (joueur?.character == null) return;

            Ecran.Texte(joueur, "Blue Lightfox · " + Domaine(resultat.Lien),
                $"{Ecran.Couleur("<b>" + Ecran.Nettoyer(resultat.Titre) + "</b>", Orange)}\n<size=80%>{Ecran.Nettoyer(resultat.Lien)}</size>\n\n{Ecran.Couper(Ecran.Nettoyer(contenu), 1800)}",
                retour,
                new Bouton("Envoyer le lien dans mon chat", () =>
                {
                    joueur.SendText($"<color={Orange}>[Blue Lightfox]</color> {resultat.Titre} : {resultat.Lien}");
                    joueur.Notify("Blue Lightfox", "Lien envoyé dans ton chat.", NotificationManager.Type.Success);
                    Page(joueur, resultat, retour);
                }));
        }

        private void OuvrirSite(Player joueur, SiteInterne site, Action retour)
        {
            Ecran.Texte(joueur, "Blue Lightfox · " + site.Adresse, $"{Ecran.Couleur("<b>" + site.Nom + "</b>", Orange)}\n<size=80%>{site.Adresse}</size>\n\n{site.Contenu}", retour);
        }

        /// <summary>Sites du serveur dont un mot-clé apparaît dans la recherche : affichés avant les résultats Internet.</summary>
        private List<ResultatWeb> SitesInternes(string recherche)
        {
            List<ResultatWeb> sites = new List<ResultatWeb>();
            bool Contient(params string[] mots) => mots.Any(m => recherche.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);

            if (Contient("emploi", "travail", "job", "recrut", "pole", "pôle"))
                sites.Add(new ResultatWeb { Titre = "Pôle Travail - Offres d'emploi", Lien = "https://pole-travail.nl", Ouvrir = (j, r) => poleTravail.Ouvrir(j, r, true) });

            if (Contient("voiture", "véhicule", "vehicule", "concession", "capital", "auto"))
                sites.Add(new ResultatWeb { Titre = $"{NovaAppsPlugin.Config.CapitalMotors.Nom} - Catalogue", Lien = "https://capital-motors.nl", Ouvrir = (j, r) => capitalMotors.Catalogue(j, r) });

            if (Contient("annonce", "vendre", "achat", "acheter", "occasion", "lebonneo", "leboncoin"))
                sites.Add(new ResultatWeb { Titre = "LeBonNeo - Petites annonces", Lien = "https://lebonneo.nl", Ouvrir = (j, r) => leBonNeo.Ouvrir(j, r) });

            if (Contient("journal", "actu", "info", "news", NovaAppsPlugin.Config.Journal.Nom))
                sites.Add(new ResultatWeb { Titre = NovaAppsPlugin.Config.Journal.Nom, Lien = "https://" + SiteJournal, Ouvrir = (j, r) => journal.Ouvrir(j, r, true) });

            if (Contient("police", "plainte", "amende", "recherché", "recherche police", "gendarmerie"))
                sites.Add(new ResultatWeb { Titre = $"{NovaAppsPlugin.Config.Police.Nom} - Services en ligne", Lien = "https://police.nl", Ouvrir = (j, r) => police.Ouvrir(j, r, true) });

            foreach (SiteInterne site in Config.Sites.Where(s => s.MotsCles.Any(m => Contient(m))))
                sites.Add(new ResultatWeb { Titre = site.Nom, Lien = "https://" + site.Adresse, Ouvrir = (j, r) => OuvrirSite(j, site, r) });

            return sites;
        }

        private async Task<List<ResultatWeb>> RechercheGoogle(string recherche)
        {
            string url = "https://www.googleapis.com/customsearch/v1?num=8&hl=fr&safe=active" +
                         $"&key={Uri.EscapeDataString(Config.GoogleCleApi)}&cx={Uri.EscapeDataString(Config.GoogleMoteurId)}&q={Uri.EscapeDataString(recherche)}";

            JObject json = JObject.Parse(await http.GetStringAsync(url));
            return (json["items"] as JArray ?? new JArray()).Select(item => new ResultatWeb
            {
                Titre = (string)item["title"],
                Lien = (string)item["link"],
                Extrait = (string)item["snippet"],
                ArticleWikipedia = ArticleDepuisLien((string)item["link"]),
            }).ToList();
        }

        private async Task<List<ResultatWeb>> RechercheWikipedia(string recherche)
        {
            string url = "https://fr.wikipedia.org/w/api.php?action=query&list=search&format=json&utf8=1&srlimit=8&srsearch=" + Uri.EscapeDataString(recherche);

            JObject json = JObject.Parse(await http.GetStringAsync(url));
            return (json["query"]?["search"] as JArray ?? new JArray()).Select(item =>
            {
                string titre = (string)item["title"];
                return new ResultatWeb
                {
                    Titre = titre,
                    Lien = "https://fr.wikipedia.org/wiki/" + Uri.EscapeDataString(titre.Replace(' ', '_')),
                    Extrait = WebUtility.HtmlDecode(Regex.Replace((string)item["snippet"] ?? "", "<.*?>", "")),
                    ArticleWikipedia = titre,
                };
            }).ToList();
        }

        private static async Task<string> ResumeWikipedia(string article)
        {
            string url = "https://fr.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(article.Replace(' ', '_'));
            JObject json = JObject.Parse(await http.GetStringAsync(url));
            return (string)json["extract"];
        }

        private static string ArticleDepuisLien(string lien)
        {
            Match m = Regex.Match(lien ?? "", @"^https?://fr\.wikipedia\.org/wiki/([^#?]+)");
            return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value).Replace('_', ' ') : null;
        }

        private static string SiteJournal => Regex.Replace(NovaAppsPlugin.Config.Journal.Nom.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-') + ".nl";

        private static string Domaine(string lien)
        {
            return Uri.TryCreate(lien ?? "", UriKind.Absolute, out Uri uri) ? uri.Host.Replace("www.", "") : "";
        }
    }
}
