using System;
using System.Collections.Generic;
using System.Linq;
using Life;
using Life.Network;
using Life.UI;

namespace NovaApps.Core
{
    /// <summary>Une ligne cliquable d'une liste.</summary>
    public class Ligne
    {
        public string Texte;
        public Action Action;

        public Ligne(string texte, Action action)
        {
            Texte = texte;
            Action = action;
        }
    }

    /// <summary>Un bouton en bas d'une fenêtre.</summary>
    public class Bouton
    {
        public string Texte;
        public Action Action;

        public Bouton(string texte, Action action)
        {
            Texte = texte;
            Action = action;
        }
    }

    /// <summary>Une question d'un formulaire en plusieurs étapes.</summary>
    public class Champ
    {
        public string Cle;
        public string Question;
        public string Exemple;
        public int Max = 200;
        public bool Optionnel;
        /// <summary>Valeur utilisée si le joueur laisse le champ vide (le champ devient alors optionnel).</summary>
        public string ParDefaut;

        public Champ(string cle, string question, string exemple, int max = 200)
        {
            Cle = cle;
            Question = question;
            Exemple = exemple;
            Max = max;
        }
    }

    /// <summary>
    /// Raccourcis autour de UIPanel : toutes les applis (téléphone et ordinateur) sont construites avec.
    /// Chaque bouton ferme la fenêtre courante avant d'ouvrir la suivante.
    /// </summary>
    public static class Ecran
    {
        public static readonly System.Globalization.CultureInfo Francais = CultureFrancaise();

        private static System.Globalization.CultureInfo CultureFrancaise()
        {
            // Certains serveurs n'embarquent pas les données de langue : on retombe alors sur l'anglais.
            try { return new System.Globalization.CultureInfo("fr-FR"); }
            catch { return System.Globalization.CultureInfo.InvariantCulture; }
        }

        /// <summary>Liste de lignes cliquables (menu d'appli, fil d'actu, liste d'offres...).</summary>
        public static void Liste(Player joueur, string titre, IEnumerable<Ligne> lignes, Action retour, string vide = "Rien pour le moment.")
        {
            UIPanel panel = new UIPanel(titre, UIPanel.PanelType.Tab);

            List<Ligne> liste = lignes.ToList();
            if (liste.Count == 0)
                liste.Add(new Ligne(vide, () => Liste(joueur, titre, new List<Ligne>(), retour, vide)));

            foreach (Ligne ligne in liste)
            {
                Action action = ligne.Action;
                panel.AddTabLine(ligne.Texte, ui =>
                {
                    Fermer(joueur, ui);
                    action();
                });
            }

            AjouterRetour(joueur, panel, retour);
            panel.AddButton("Ouvrir", ui => ui.SelectTab());
            Afficher(joueur, panel);
        }

        /// <summary>Fenêtre de texte avec des boutons (lecture d'un post, d'une offre...).</summary>
        public static void Texte(Player joueur, string titre, string texte, Action retour, params Bouton[] boutons)
        {
            UIPanel panel = new UIPanel(titre, UIPanel.PanelType.Text).SetText(texte);

            AjouterRetour(joueur, panel, retour);
            foreach (Bouton bouton in boutons.Where(b => b != null))
            {
                Action action = bouton.Action;
                panel.AddButton(bouton.Texte, ui =>
                {
                    Fermer(joueur, ui);
                    action();
                });
            }

            Afficher(joueur, panel);
        }

        /// <summary>Demande un texte au joueur.</summary>
        public static void Saisie(Player joueur, string titre, string question, string exemple, Action<string> valider, Action retour, int max = 200, bool optionnel = false)
        {
            UIPanel panel = new UIPanel(titre, UIPanel.PanelType.Input)
                .SetText(question)
                .SetInputPlaceholder(exemple);

            AjouterRetour(joueur, panel, retour);
            panel.AddButton("Valider", ui =>
            {
                string valeur = Nettoyer(ui.inputText);

                if (valeur.Length == 0 && !optionnel)
                {
                    joueur.Notify(titre, "Ce champ ne peut pas être vide.", NotificationManager.Type.Error);
                    return;
                }

                if (valeur.Length > max)
                {
                    joueur.Notify(titre, $"{max} caractères maximum ({valeur.Length} actuellement).", NotificationManager.Type.Error);
                    return;
                }

                Fermer(joueur, ui);
                valider(valeur);
            });

            Afficher(joueur, panel);
        }

        /// <summary>Enchaîne plusieurs Saisie puis appelle fin avec toutes les réponses.</summary>
        public static void Formulaire(Player joueur, string titre, IList<Champ> champs, Action<Dictionary<string, string>> fin, Action retour)
        {
            Etape(joueur, titre, champs, 0, new Dictionary<string, string>(), fin, retour);
        }

        private static void Etape(Player joueur, string titre, IList<Champ> champs, int index, Dictionary<string, string> reponses, Action<Dictionary<string, string>> fin, Action retour)
        {
            if (index >= champs.Count)
            {
                fin(reponses);
                return;
            }

            Champ champ = champs[index];
            string question = $"<b>Étape {index + 1}/{champs.Count}</b>\n{champ.Question}";
            if (champ.ParDefaut != null)
                question += $"\n<i>Laisse vide pour : {champ.ParDefaut}</i>";
            else if (champ.Optionnel)
                question += "\n<i>(facultatif)</i>";

            Saisie(joueur, titre, question, champ.Exemple, valeur =>
            {
                reponses[champ.Cle] = valeur.Length == 0 && champ.ParDefaut != null ? champ.ParDefaut : valeur;
                Etape(joueur, titre, champs, index + 1, reponses, fin, retour);
            },
            // "Retour" revient à la question précédente, ou quitte le formulaire à la première.
            index == 0 ? retour : () => Etape(joueur, titre, champs, index - 1, reponses, fin, retour),
            champ.Max, champ.Optionnel || champ.ParDefaut != null);
        }

        /// <summary>Choix parmi une liste de textes (catégorie, rubrique, genre...).</summary>
        public static void Choix(Player joueur, string titre, IEnumerable<string> options, Action<string> choisi, Action retour)
        {
            Liste(joueur, titre, options.Select(o => new Ligne(o, () => choisi(o))), retour);
        }

        /// <summary>Choix d'un joueur connecté (autre que soi), trié par nom.</summary>
        public static void ChoisirJoueur(Player joueur, string titre, Action<Player> choisi, Action retour)
        {
            Liste(joueur, titre,
                Joueurs.TousEnLigne().Where(p => p != joueur).OrderBy(p => p.FullName)
                    .Select(p => new Ligne(Joueurs.Nom(p), () =>
                    {
                        if (p.character != null) choisi(p);
                        else retour();
                    })),
                retour, "Personne d'autre n'est en ville.");
        }

        /// <summary>Demande "Oui / Non" avant une action importante.</summary>
        public static void Confirmer(Player joueur, string titre, string texte, Action oui, Action non)
        {
            Texte(joueur, titre, texte, non, new Bouton("Confirmer", oui));
        }

        /// <summary>Fenêtre de plugin actuellement ouverte, par joueur (pour la refermer avec la touche du téléphone / PC).</summary>
        private static readonly Dictionary<Player, UIPanel> ouvertes = new Dictionary<Player, UIPanel>();

        private static void Afficher(Player joueur, UIPanel panel)
        {
            ouvertes[joueur] = panel;
            joueur.ShowPanelUI(panel);
        }

        private static void Fermer(Player joueur, UIPanel panel)
        {
            if (ouvertes.TryGetValue(joueur, out UIPanel courante) && courante == panel)
                ouvertes.Remove(joueur);
            joueur.ClosePanel(panel);
        }

        /// <summary>Une fenêtre des applis est-elle ouverte pour ce joueur ?</summary>
        public static bool EstOuvert(Player joueur) => ouvertes.ContainsKey(joueur);

        /// <summary>Ferme la fenêtre des applis du joueur, s'il y en a une.</summary>
        public static void FermerTout(Player joueur)
        {
            if (ouvertes.TryGetValue(joueur, out UIPanel courante))
                Fermer(joueur, courante);
        }

        /// <summary>Oublie la fenêtre du joueur (fermée par Échap ou à la déconnexion, sans passer par nos boutons).</summary>
        public static void Oublier(Player joueur) => ouvertes.Remove(joueur);

        private static void AjouterRetour(Player joueur, UIPanel panel, Action retour)
        {
            panel.AddButton(retour != null ? "Retour" : "Fermer", ui =>
            {
                Fermer(joueur, ui);
                retour?.Invoke();
            });
        }

        /// <summary>
        /// Retire les balises de texte riche qu'un joueur pourrait glisser dans un message
        /// (&lt;color&gt;, &lt;size&gt;...) pour qu'il ne casse pas l'affichage des autres.
        /// </summary>
        public static string Nettoyer(string texte)
        {
            if (string.IsNullOrEmpty(texte)) return "";
            return texte.Replace("<", "‹").Replace(">", "›").Trim();
        }

        public static string Couper(string texte, int max)
        {
            if (string.IsNullOrEmpty(texte)) return "";
            string uneLigne = texte.Replace("\n", " ");
            return uneLigne.Length <= max ? uneLigne : uneLigne.Substring(0, max - 1) + "…";
        }

        public static string Date(DateTime date)
        {
            return date.Date == DateTime.Today ? date.ToString("HH:mm") : date.ToString("dd/MM HH:mm");
        }

        public static string Couleur(string texte, string hex)
        {
            return $"<color={hex}>{texte}</color>";
        }
    }
}
