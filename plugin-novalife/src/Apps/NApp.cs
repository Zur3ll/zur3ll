using System;
using System.Collections.Generic;
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
    public class NDonnees
    {
        public int ProchainId = 1;
        /// <summary>Pseudo (@...) de chaque personnage, par ID de personnage.</summary>
        public Dictionary<int, string> Pseudos = new Dictionary<int, string>();
        public List<NPost> Posts = new List<NPost>();
    }

    public class NPost
    {
        public int Id;
        public int AuteurId;
        public string Pseudo;
        public string Texte;
        public DateTime Date;
        public HashSet<int> Likes = new HashSet<int>();
        /// <summary>ID du post auquel celui-ci répond, 0 sinon.</summary>
        public int ReponseA;
    }

    /// <summary>N : le réseau social façon X / Twitter.</summary>
    public class NApp
    {
        private const string Bleu = "#1d9bf0";
        private static readonly Regex Mention = new Regex(@"@(\w{3,15})");
        private static readonly Regex Hashtag = new Regex(@"#(\w{2,30})");

        private readonly Stockage<NDonnees> stockage = new Stockage<NDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "n.json"));
        private NDonnees D => stockage.Donnees;
        private NConfig Config => NovaAppsPlugin.Config.N;

        public void Ouvrir(Player joueur, Action retour)
        {
            if (!D.Pseudos.ContainsKey(Joueurs.Id(joueur)))
            {
                ChoisirPseudo(joueur, () => Ouvrir(joueur, retour), retour);
                return;
            }

            Action menu = () => Ouvrir(joueur, retour);
            Ecran.Liste(joueur, $"N · @{D.Pseudos[Joueurs.Id(joueur)]}", new[]
            {
                new Ligne("Fil d'actualité", () => Fil(joueur, menu)),
                new Ligne("Publier", () => Publier(joueur, 0, menu)),
                new Ligne("Tendances", () => Tendances(joueur, menu)),
                new Ligne("Mes mentions", () => ListePosts(joueur, "Mentions", MesMentions(joueur), menu)),
                new Ligne("Mon profil", () => Profil(joueur, Joueurs.Id(joueur), menu)),
                new Ligne("Rechercher un compte", () => RechercherCompte(joueur, menu)),
            }, retour);
        }

        private void ChoisirPseudo(Player joueur, Action suite, Action retour)
        {
            Ecran.Saisie(joueur, "N · Bienvenue", "Choisis ton pseudo (3 à 15 lettres, chiffres ou _). Il sera affiché avec un @.", "Ex : john_doe", valeur =>
            {
                string pseudo = valeur.TrimStart('@');

                if (!Regex.IsMatch(pseudo, @"^\w{3,15}$"))
                {
                    joueur.Notify("N", "Pseudo invalide : 3 à 15 caractères, lettres, chiffres ou _.", NotificationManager.Type.Error);
                    ChoisirPseudo(joueur, suite, retour);
                    return;
                }

                if (D.Pseudos.Any(p => p.Key != Joueurs.Id(joueur) && string.Equals(p.Value, pseudo, StringComparison.OrdinalIgnoreCase)))
                {
                    joueur.Notify("N", $"@{pseudo} est déjà pris.", NotificationManager.Type.Error);
                    ChoisirPseudo(joueur, suite, retour);
                    return;
                }

                D.Pseudos[Joueurs.Id(joueur)] = pseudo;
                // Les anciens posts suivent le changement de pseudo.
                foreach (NPost post in D.Posts.Where(p => p.AuteurId == Joueurs.Id(joueur)))
                    post.Pseudo = pseudo;
                stockage.Sauvegarder();
                suite();
            }, retour, 15);
        }

        private void Fil(Player joueur, Action retour)
        {
            ListePosts(joueur, "Fil d'actualité", D.Posts.Where(p => p.ReponseA == 0), retour);
        }

        private void ListePosts(Player joueur, string titre, IEnumerable<NPost> posts, Action retour)
        {
            Action recharger = () => ListePosts(joueur, titre, posts, retour);
            Ecran.Liste(joueur, $"N · {titre}",
                posts.OrderByDescending(p => p.Date).Take(Config.PostsAffiches)
                    .Select(p => new Ligne(ResumePost(p), () => Lire(joueur, p.Id, recharger))),
                retour, "Aucun post.");
        }

        private string ResumePost(NPost post)
        {
            int reponses = D.Posts.Count(p => p.ReponseA == post.Id);
            return $"@{post.Pseudo} : {Ecran.Couper(post.Texte, 55)}  ({post.Likes.Count} j'aime · {reponses} rép.)";
        }

        private void Lire(Player joueur, int postId, Action retour)
        {
            NPost post = D.Posts.FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                joueur.Notify("N", "Ce post a été supprimé.", NotificationManager.Type.Error);
                retour();
                return;
            }

            int moi = Joueurs.Id(joueur);
            bool aime = post.Likes.Contains(moi);
            List<NPost> reponses = D.Posts.Where(p => p.ReponseA == post.Id).OrderBy(p => p.Date).ToList();
            NPost parent = D.Posts.FirstOrDefault(p => p.Id == post.ReponseA);

            string texte = "";
            if (parent != null)
                texte += $"<i>En réponse à @{parent.Pseudo} : {Ecran.Couper(parent.Texte, 60)}</i>\n\n";
            texte += $"{Ecran.Couleur("<b>@" + post.Pseudo + "</b>", Bleu)}  ·  {Ecran.Date(post.Date)}\n\n{post.Texte}\n\n" +
                     $"{post.Likes.Count} j'aime  ·  {reponses.Count} réponse(s)";

            Action relire = () => Lire(joueur, postId, retour);
            Ecran.Texte(joueur, "N · Post", texte, retour,
                new Bouton(aime ? "Je n'aime plus" : "J'aime", () =>
                {
                    if (!post.Likes.Remove(moi))
                    {
                        post.Likes.Add(moi);
                        if (post.AuteurId != moi)
                            Joueurs.Notifier(post.AuteurId, "N", $"@{D.Pseudos[moi]} aime ton post.");
                    }
                    stockage.Sauvegarder();
                    relire();
                }),
                new Bouton("Répondre", () => Publier(joueur, post.Id, relire)),
                reponses.Count > 0 ? new Bouton($"Réponses ({reponses.Count})", () => ListePosts(joueur, "Réponses", reponses, relire)) : null,
                new Bouton("Profil", () => Profil(joueur, post.AuteurId, relire)),
                post.AuteurId == moi || joueur.IsAdmin ? new Bouton("Supprimer", () => Ecran.Confirmer(joueur, "N", "Supprimer ce post ?", () =>
                {
                    D.Posts.RemoveAll(p => p.Id == post.Id || p.ReponseA == post.Id);
                    stockage.Sauvegarder();
                    joueur.Notify("N", "Post supprimé.", NotificationManager.Type.Success);
                    retour();
                }, relire)) : null);
        }

        private void Publier(Player joueur, int reponseA, Action retour)
        {
            NPost parent = D.Posts.FirstOrDefault(p => p.Id == reponseA);
            string question = parent == null
                ? "Quoi de neuf ? Utilise @pseudo pour mentionner quelqu'un et #sujet pour un hashtag."
                : $"Réponse à @{parent.Pseudo} :";

            Ecran.Saisie(joueur, "N · Publier", question, "Écris ton post...", texte =>
            {
                int moi = Joueurs.Id(joueur);
                NPost post = new NPost
                {
                    Id = D.ProchainId++,
                    AuteurId = moi,
                    Pseudo = D.Pseudos[moi],
                    Texte = texte,
                    Date = DateTime.Now,
                    ReponseA = parent?.Id ?? 0,
                };
                D.Posts.Add(post);
                stockage.Sauvegarder();

                NotifierMentions(post, parent);
                _ = NovaAppsPlugin.Discord.PosterAvecWebhook(Config.WebhookDiscord, $"@{post.Pseudo} (N)",
                    parent == null ? texte : $"↪️ en réponse à @{parent.Pseudo} : {texte}");

                joueur.Notify("N", "Post publié !", NotificationManager.Type.Success);
                Lire(joueur, post.Id, retour);
            }, retour, Config.LongueurMax);
        }

        private void NotifierMentions(NPost post, NPost parent)
        {
            HashSet<int> notifies = new HashSet<int> { post.AuteurId };

            if (parent != null && notifies.Add(parent.AuteurId))
                Joueurs.Notifier(parent.AuteurId, "N", $"@{post.Pseudo} t'a répondu : {Ecran.Couper(post.Texte, 60)}");

            foreach (Match m in Mention.Matches(post.Texte))
            {
                KeyValuePair<int, string> cible = D.Pseudos.FirstOrDefault(p => string.Equals(p.Value, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                if (cible.Value != null && notifies.Add(cible.Key))
                    Joueurs.Notifier(cible.Key, "N", $"@{post.Pseudo} t'a mentionné : {Ecran.Couper(post.Texte, 60)}");
            }
        }

        private IEnumerable<NPost> MesMentions(Player joueur)
        {
            string pseudo = D.Pseudos[Joueurs.Id(joueur)];
            HashSet<int> mesPosts = new HashSet<int>(D.Posts.Where(p => p.AuteurId == Joueurs.Id(joueur)).Select(p => p.Id));

            return D.Posts.Where(p => p.AuteurId != Joueurs.Id(joueur) &&
                (mesPosts.Contains(p.ReponseA) || Mention.Matches(p.Texte).Cast<Match>()
                    .Any(m => string.Equals(m.Groups[1].Value, pseudo, StringComparison.OrdinalIgnoreCase))));
        }

        private void Tendances(Player joueur, Action retour)
        {
            Action recharger = () => Tendances(joueur, retour);
            DateTime depuis = DateTime.Now.AddDays(-7);

            var tendances = D.Posts.Where(p => p.Date > depuis)
                .SelectMany(p => Hashtag.Matches(p.Texte).Cast<Match>().Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct())
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10);

            Ecran.Liste(joueur, "N · Tendances (7 jours)",
                tendances.Select(g => new Ligne($"#{g.Key}  —  {g.Count()} post(s)", () =>
                    ListePosts(joueur, "#" + g.Key, D.Posts.Where(p => Hashtag.Matches(p.Texte).Cast<Match>()
                        .Any(m => string.Equals(m.Groups[1].Value, g.Key, StringComparison.OrdinalIgnoreCase))), recharger))),
                retour, "Aucun hashtag cette semaine.");
        }

        private void Profil(Player joueur, int personnageId, Action retour)
        {
            if (!D.Pseudos.TryGetValue(personnageId, out string pseudo))
            {
                retour();
                return;
            }

            List<NPost> posts = D.Posts.Where(p => p.AuteurId == personnageId).ToList();
            int likes = posts.Sum(p => p.Likes.Count);
            Action relire = () => Profil(joueur, personnageId, retour);

            Ecran.Texte(joueur, $"N · @{pseudo}",
                $"{Ecran.Couleur("<b>@" + pseudo + "</b>", Bleu)}\n\n{posts.Count} post(s)  ·  {likes} j'aime reçus",
                retour,
                new Bouton("Voir ses posts", () => ListePosts(joueur, "@" + pseudo, posts, relire)),
                personnageId == Joueurs.Id(joueur) ? new Bouton("Changer de pseudo", () => ChoisirPseudo(joueur, relire, relire)) : null);
        }

        private void RechercherCompte(Player joueur, Action retour)
        {
            Ecran.Saisie(joueur, "N · Rechercher", "Pseudo (ou début du pseudo) :", "Ex : john", recherche =>
            {
                string r = recherche.TrimStart('@');
                Ecran.Liste(joueur, $"N · « {r} »",
                    D.Pseudos.Where(p => p.Value.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0).Take(20)
                        .Select(p => new Ligne("@" + p.Value, () => Profil(joueur, p.Key, () => RechercherCompte(joueur, retour)))),
                    retour, "Aucun compte trouvé.");
            }, retour, 15);
        }
    }
}
