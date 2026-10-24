using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using NovaApps.Core;

namespace NovaApps.Apps
{
    public class NeonousDonnees
    {
        public Dictionary<int, NeonousProfil> Profils = new Dictionary<int, NeonousProfil>();
        public List<NeonousMatch> Matchs = new List<NeonousMatch>();
    }

    public class NeonousProfil
    {
        public int PersonnageId;
        public string Prenom;
        public int Age;
        public string Genre;      // Homme / Femme / Autre
        public string Recherche;  // Homme / Femme / Tout le monde
        public string Bio;
        public bool Visible = true;
        public HashSet<int> Likes = new HashSet<int>();
        public HashSet<int> Passes = new HashSet<int>();
    }

    public class NeonousMatch
    {
        public int A;
        public int B;
        public DateTime Date;
        public List<NeonousMessage> Messages = new List<NeonousMessage>();

        public bool Concerne(int id) => A == id || B == id;
        public int Autre(int id) => A == id ? B : A;
    }

    public class NeonousMessage
    {
        public int AuteurId;
        public string Texte;
        public DateTime Date;
    }

    /// <summary>Neonous : appli de rencontre façon Tinder.</summary>
    public class NeonousApp
    {
        private const string Rose = "#ff4f7b";
        private const string ToutLeMonde = "Tout le monde";
        private static readonly string[] Genres = { "Homme", "Femme", "Autre" };

        private readonly Stockage<NeonousDonnees> stockage = new Stockage<NeonousDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "neonous.json"));
        private NeonousDonnees D => stockage.Donnees;

        public void Ouvrir(Player joueur, Action retour)
        {
            if (!D.Profils.TryGetValue(Joueurs.Id(joueur), out NeonousProfil profil))
            {
                Ecran.Texte(joueur, "Neonous",
                    $"{Ecran.Couleur("<b>Neonous</b>", Rose)}\n\nRencontre des gens près de chez toi.\nLike les profils qui te plaisent : si c'est réciproque, c'est un match et vous pouvez discuter !",
                    retour, new Bouton("Créer mon profil", () => CreerProfil(joueur, () => Ouvrir(joueur, retour), retour)));
                return;
            }

            Action menu = () => Ouvrir(joueur, retour);
            int nonLus = D.Matchs.Count(m => m.Concerne(profil.PersonnageId) && m.Messages.Count > 0 && m.Messages.Last().AuteurId != profil.PersonnageId);

            Ecran.Liste(joueur, "Neonous", new[]
            {
                new Ligne("Découvrir des profils", () => Decouvrir(joueur, menu)),
                new Ligne($"Mes matchs ({D.Matchs.Count(m => m.Concerne(profil.PersonnageId))})" + (nonLus > 0 ? $"  ·  {nonLus} conversation(s) en attente" : ""), () => Matchs(joueur, menu)),
                new Ligne("Mon profil", () => MonProfil(joueur, menu)),
            }, retour);
        }

        private void CreerProfil(Player joueur, Action suite, Action retour)
        {
            Ecran.Formulaire(joueur, "Neonous · Mon profil", new List<Champ>
            {
                new Champ("prenom", "Ton prénom affiché :", "Ex : Lina", 20) { ParDefaut = joueur.character.Firstname },
                new Champ("age", "Ton âge :", "Ex : 24", 3),
                new Champ("bio", "Ta bio : qui es-tu, qu'est-ce que tu cherches ?", "Ex : Fan de voitures et de couchers de soleil...", 300),
            }, reponses =>
            {
                if (!int.TryParse(reponses["age"], out int age) || age < 18 || age > 99)
                {
                    joueur.Notify("Neonous", "Âge invalide (18 à 99 ans).", NotificationManager.Type.Error);
                    CreerProfil(joueur, suite, retour);
                    return;
                }

                Ecran.Choix(joueur, "Neonous · Tu es :", Genres, genre =>
                    Ecran.Choix(joueur, "Neonous · Tu cherches :", new[] { "Homme", "Femme", ToutLeMonde }, recherche =>
                    {
                        int id = Joueurs.Id(joueur);
                        D.Profils.TryGetValue(id, out NeonousProfil ancien);

                        D.Profils[id] = new NeonousProfil
                        {
                            PersonnageId = id,
                            Prenom = reponses["prenom"],
                            Age = age,
                            Genre = genre,
                            Recherche = recherche,
                            Bio = reponses["bio"],
                            Visible = ancien?.Visible ?? true,
                            Likes = ancien?.Likes ?? new HashSet<int>(),
                            Passes = ancien?.Passes ?? new HashSet<int>(),
                        };
                        stockage.Sauvegarder();
                        joueur.Notify("Neonous", "Profil enregistré !", NotificationManager.Type.Success);
                        suite();
                    }, retour), retour);
            }, retour);
        }

        private static bool Compatible(NeonousProfil moi, NeonousProfil autre)
        {
            return (moi.Recherche == ToutLeMonde || moi.Recherche == autre.Genre)
                && (autre.Recherche == ToutLeMonde || autre.Recherche == moi.Genre);
        }

        private void Decouvrir(Player joueur, Action retour)
        {
            NeonousProfil moi = D.Profils[Joueurs.Id(joueur)];

            // Les profils récemment actifs (connectés) en premier, puis au hasard.
            System.Random hasard = new System.Random();
            NeonousProfil suivant = D.Profils.Values
                .Where(p => p.PersonnageId != moi.PersonnageId && p.Visible && !moi.Likes.Contains(p.PersonnageId) && !moi.Passes.Contains(p.PersonnageId) && Compatible(moi, p))
                .OrderByDescending(p => Joueurs.EnLigne(p.PersonnageId) != null)
                .ThenBy(p => hasard.Next())
                .FirstOrDefault();

            if (suivant == null)
            {
                Ecran.Texte(joueur, "Neonous", "Tu as vu tous les profils pour le moment.\nReviens plus tard !", retour,
                    moi.Passes.Count > 0 ? new Bouton("Revoir les profils passés", () =>
                    {
                        moi.Passes.Clear();
                        stockage.Sauvegarder();
                        Decouvrir(joueur, retour);
                    }) : null);
                return;
            }

            string enLigne = Joueurs.EnLigne(suivant.PersonnageId) != null ? Ecran.Couleur("  ● en ville", "#2ecc71") : "";
            Ecran.Texte(joueur, "Neonous · Découvrir",
                $"{Ecran.Couleur($"<b>{suivant.Prenom}, {suivant.Age} ans</b>", Rose)}{enLigne}\n<i>{suivant.Genre} · cherche : {suivant.Recherche}</i>\n\n{suivant.Bio}",
                retour,
                new Bouton("Passer", () =>
                {
                    moi.Passes.Add(suivant.PersonnageId);
                    stockage.Sauvegarder();
                    Decouvrir(joueur, retour);
                }),
                new Bouton("Liker", () =>
                {
                    moi.Likes.Add(suivant.PersonnageId);
                    if (suivant.Likes.Contains(moi.PersonnageId))
                        NouveauMatch(joueur, moi, suivant, retour);
                    else
                    {
                        stockage.Sauvegarder();
                        Decouvrir(joueur, retour);
                    }
                }));
        }

        private void NouveauMatch(Player joueur, NeonousProfil moi, NeonousProfil autre, Action retour)
        {
            NeonousMatch match = new NeonousMatch { A = moi.PersonnageId, B = autre.PersonnageId, Date = DateTime.Now };
            D.Matchs.Add(match);
            stockage.Sauvegarder();

            Joueurs.Notifier(autre.PersonnageId, "Neonous", $"C'est un match avec {moi.Prenom} ! Ouvre Neonous pour lui écrire.", NotificationManager.Type.Success);

            Ecran.Texte(joueur, "Neonous · C'est un match !",
                $"{Ecran.Couleur("<b>C'est un match !</b>", Rose)}\n\n{autre.Prenom} t'a liké aussi.\nEnvoie-lui un premier message !",
                () => Decouvrir(joueur, retour),
                new Bouton("Écrire", () => Ecrire(joueur, match, () => Conversation(joueur, match, retour))));
        }

        private void Matchs(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action recharger = () => Matchs(joueur, retour);

            Ecran.Liste(joueur, "Neonous · Mes matchs",
                D.Matchs.Where(m => m.Concerne(moi))
                    .OrderByDescending(m => m.Messages.Count > 0 ? m.Messages.Last().Date : m.Date)
                    .Select(m =>
                    {
                        string nom = D.Profils.TryGetValue(m.Autre(moi), out NeonousProfil p) ? p.Prenom : "Profil supprimé";
                        NeonousMessage dernier = m.Messages.LastOrDefault();
                        string apercu = dernier == null ? "Nouveau match !" : (dernier.AuteurId == moi ? "Toi : " : "") + Ecran.Couper(dernier.Texte, 40);
                        return new Ligne($"{nom}  —  {apercu}", () => Conversation(joueur, m, recharger));
                    }),
                retour, "Pas encore de match. Continue de découvrir des profils !");
        }

        private void Conversation(Player joueur, NeonousMatch match, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            string nom = D.Profils.TryGetValue(match.Autre(moi), out NeonousProfil autre) ? autre.Prenom : "Profil supprimé";
            Action relire = () => Conversation(joueur, match, retour);

            string fil = match.Messages.Count == 0
                ? "<i>Aucun message. Lance la conversation !</i>"
                : string.Join("\n", match.Messages.Skip(Math.Max(0, match.Messages.Count - 15)).Select(m =>
                    $"<b>{(m.AuteurId == moi ? "Toi" : nom)}</b> <size=80%>{Ecran.Date(m.Date)}</size> : {m.Texte}"));

            Ecran.Texte(joueur, $"Neonous · {nom}", fil, retour,
                autre != null ? new Bouton("Écrire", () => Ecrire(joueur, match, relire)) : null,
                autre != null ? new Bouton("Voir son profil", () => Ecran.Texte(joueur, "Neonous · " + autre.Prenom,
                    $"{Ecran.Couleur($"<b>{autre.Prenom}, {autre.Age} ans</b>", Rose)}\n<i>{autre.Genre}</i>\n\n{autre.Bio}", relire)) : null,
                new Bouton("Actualiser", relire),
                new Bouton("Ne plus matcher", () => Ecran.Confirmer(joueur, "Neonous", $"Supprimer le match avec {nom} ? La conversation sera effacée.", () =>
                {
                    D.Matchs.Remove(match);
                    if (D.Profils.TryGetValue(moi, out NeonousProfil profil)) profil.Likes.Remove(match.Autre(moi));
                    stockage.Sauvegarder();
                    retour();
                }, relire)));
        }

        private void Ecrire(Player joueur, NeonousMatch match, Action retour)
        {
            Ecran.Saisie(joueur, "Neonous · Message", "Ton message :", "Salut !", texte =>
            {
                int moi = Joueurs.Id(joueur);
                match.Messages.Add(new NeonousMessage { AuteurId = moi, Texte = texte, Date = DateTime.Now });
                stockage.Sauvegarder();

                string prenom = D.Profils.TryGetValue(moi, out NeonousProfil p) ? p.Prenom : Joueurs.Nom(joueur);
                Joueurs.Notifier(match.Autre(moi), "Neonous", $"{prenom} : {Ecran.Couper(texte, 60)}");
                retour();
            }, retour, 300);
        }

        private void MonProfil(Player joueur, Action retour)
        {
            NeonousProfil p = D.Profils[Joueurs.Id(joueur)];
            Action relire = () => MonProfil(joueur, retour);

            Ecran.Texte(joueur, "Neonous · Mon profil",
                $"{Ecran.Couleur($"<b>{p.Prenom}, {p.Age} ans</b>", Rose)}\n<i>{p.Genre} · cherche : {p.Recherche}</i>\n\n{p.Bio}\n\n" +
                $"Profil {(p.Visible ? "visible" : "masqué")} · {p.Likes.Count} like(s) envoyé(s)",
                retour,
                new Bouton("Modifier", () => CreerProfil(joueur, relire, relire)),
                new Bouton(p.Visible ? "Masquer mon profil" : "Rendre visible", () =>
                {
                    p.Visible = !p.Visible;
                    stockage.Sauvegarder();
                    relire();
                }),
                new Bouton("Supprimer mon compte", () => Ecran.Confirmer(joueur, "Neonous", "Supprimer ton profil, tes likes et tous tes matchs ?", () =>
                {
                    D.Profils.Remove(p.PersonnageId);
                    D.Matchs.RemoveAll(m => m.Concerne(p.PersonnageId));
                    foreach (NeonousProfil autre in D.Profils.Values)
                    {
                        autre.Likes.Remove(p.PersonnageId);
                        autre.Passes.Remove(p.PersonnageId);
                    }
                    stockage.Sauvegarder();
                    joueur.Notify("Neonous", "Compte supprimé.", NotificationManager.Type.Success);
                    retour();
                }, relire)));
        }
    }
}
