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
    public class LeBonNeoDonnees
    {
        public int ProchainId = 1;
        public List<Annonce> Annonces = new List<Annonce>();
        public List<ConversationAnnonce> Conversations = new List<ConversationAnnonce>();
    }

    public class Annonce
    {
        public int Id;
        public int VendeurId;
        public string VendeurNom;
        public string Categorie;
        public string Titre;
        public int Prix;
        public string Description;
        public string Contact;
        public DateTime Date;
        /// <summary>Date de mise en ligne ou de dernière remontée : sert au tri et à l'expiration.</summary>
        public DateTime Remontee;
        public bool Vendue;
        public int Vues;
    }

    public class ConversationAnnonce
    {
        public int Id;
        public int AnnonceId;
        public int AcheteurId;
        public string AcheteurNom;
        public int VendeurId;
        public bool LuParAcheteur = true;
        public bool LuParVendeur;
        public List<NeonousMessage> Messages = new List<NeonousMessage>();

        public bool Concerne(int id) => AcheteurId == id || VendeurId == id;
        public bool NonLu(int id) => id == AcheteurId ? !LuParAcheteur : !LuParVendeur;
    }

    /// <summary>LeBonNeo : petites annonces entre joueurs façon Leboncoin.</summary>
    public class LeBonNeoApp
    {
        private const string Orange = "#ff6e14";

        private readonly Stockage<LeBonNeoDonnees> stockage = new Stockage<LeBonNeoDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "lebonneo.json"));
        private LeBonNeoDonnees D => stockage.Donnees;
        private LeBonNeoConfig Config => NovaAppsPlugin.Config.LeBonNeo;

        private IEnumerable<Annonce> EnLigne => D.Annonces.Where(a => !a.Vendue && a.Remontee > DateTime.Now.AddDays(-Config.DureeJours));

        /// <summary>Conversations avec un message non lu.</summary>
        public int Badge(Player joueur)
        {
            int moi = Joueurs.Id(joueur);
            return D.Conversations.Count(c => c.Concerne(moi) && c.Messages.Count > 0 && c.NonLu(moi));
        }

        public void Ouvrir(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action menu = () => Ouvrir(joueur, retour);
            int nonLus = D.Conversations.Count(c => c.Concerne(moi) && c.NonLu(moi));

            Ecran.Liste(joueur, "LeBonNeo", new[]
            {
                new Ligne($"Toutes les annonces ({EnLigne.Count()})", () => Annonces(joueur, "Toutes les annonces", EnLigne, menu)),
                new Ligne("Par catégorie", () => Ecran.Choix(joueur, "LeBonNeo · Catégories",
                    Config.Categories.Select(c => $"{c} ({EnLigne.Count(a => a.Categorie == c)})"),
                    choix =>
                    {
                        string categorie = Config.Categories.First(c => choix.StartsWith(c + " ("));
                        Annonces(joueur, categorie, EnLigne.Where(a => a.Categorie == categorie), menu);
                    }, menu)),
                new Ligne("Rechercher", () => Ecran.Saisie(joueur, "LeBonNeo", "Que cherches-tu ?", "Ex : Sultan, appartement, réparation...", mot =>
                    Annonces(joueur, $"« {mot} »", EnLigne.Where(a => $"{a.Titre} {a.Description} {a.Categorie}".IndexOf(mot, StringComparison.OrdinalIgnoreCase) >= 0), menu), menu, 50)),
                new Ligne("Déposer une annonce", () => Deposer(joueur, menu)),
                new Ligne("Mes annonces", () => Annonces(joueur, "Mes annonces", D.Annonces.Where(a => a.VendeurId == moi), menu)),
                new Ligne("Mes messages" + (nonLus > 0 ? $"  ·  {nonLus} non lu(s)" : ""), () => Messages(joueur, menu)),
            }, retour);
        }

        private static string Prix(int prix) => prix > 0 ? $"{prix:N0} €".Replace(' ', ' ') : "Prix à débattre";

        private void Annonces(Player joueur, string titre, IEnumerable<Annonce> annonces, Action retour)
        {
            Action recharger = () => Annonces(joueur, titre, annonces, retour);
            Ecran.Liste(joueur, "LeBonNeo · " + titre,
                annonces.OrderByDescending(a => a.Remontee).Take(50).Select(a =>
                    new Ligne($"{Ecran.Couper(a.Titre, 45)}  —  {Prix(a.Prix)}  ·  {a.Categorie}{(a.Vendue ? "  ·  VENDU" : "")}", () => Voir(joueur, a, recharger))),
                retour, "Aucune annonce.");
        }

        private void Voir(Player joueur, Annonce a, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            bool mienne = a.VendeurId == moi;
            Action relire = () => Voir(joueur, a, retour);

            if (!mienne)
            {
                a.Vues++;
                stockage.Sauvegarder();
            }

            string etat = a.Vendue ? Ecran.Couleur("\n<b>VENDU</b>", Orange) :
                a.Remontee <= DateTime.Now.AddDays(-Config.DureeJours) ? "\n<i>Annonce expirée : remonte-la pour la remettre en ligne.</i>" : "";

            Ecran.Texte(joueur, "LeBonNeo · Annonce",
                $"{Ecran.Couleur($"<b>{a.Titre}</b>", Orange)}\n<b>{Prix(a.Prix)}</b>  ·  {a.Categorie}{etat}\n\n{a.Description}\n\n" +
                $"<b>Vendeur :</b> {a.VendeurNom}\n<b>Contact :</b> {a.Contact}\n<size=80%>Publiée le {a.Date:dd/MM} · {a.Vues} vue(s)</size>",
                retour,
                !mienne && !a.Vendue ? new Bouton("Envoyer un message", () => Ecrire(joueur, ConversationAvec(a, joueur), relire)) : null,
                mienne && !a.Vendue ? new Bouton("Marquer comme vendu", () =>
                {
                    a.Vendue = true;
                    stockage.Sauvegarder();
                    relire();
                }) : null,
                mienne && !a.Vendue ? new Bouton("Remonter l'annonce", () =>
                {
                    if (a.Remontee > DateTime.Now.AddHours(-24))
                    {
                        joueur.Notify("LeBonNeo", "Tu peux remonter ton annonce une fois toutes les 24 h.", NotificationManager.Type.Error);
                    }
                    else
                    {
                        a.Remontee = DateTime.Now;
                        stockage.Sauvegarder();
                        joueur.Notify("LeBonNeo", "Annonce remontée en tête de liste !", NotificationManager.Type.Success);
                    }
                    relire();
                }) : null,
                mienne || joueur.IsAdmin ? new Bouton("Supprimer", () => Ecran.Confirmer(joueur, "LeBonNeo", "Supprimer cette annonce et ses messages ?", () =>
                {
                    D.Annonces.Remove(a);
                    D.Conversations.RemoveAll(c => c.AnnonceId == a.Id);
                    stockage.Sauvegarder();
                    retour();
                }, relire)) : null);
        }

        private void Deposer(Player joueur, Action retour)
        {
            Ecran.Choix(joueur, "LeBonNeo · Catégorie de l'annonce", Config.Categories, categorie =>
            {
                string telephone = joueur.character.PhoneNumber;
                Ecran.Formulaire(joueur, "LeBonNeo · Nouvelle annonce", new List<Champ>
                {
                    new Champ("titre", "Titre de l'annonce :", "Ex : Sultan RS noire, très bon état", 60),
                    new Champ("prix", "Prix en euros (0 = à débattre) :", "Ex : 25000", 12),
                    new Champ("description", "Description :", "État, kilométrage, adresse, détails...", 600),
                    new Champ("contact", "Contact :", "Ex : 555-1234", 100) { ParDefaut = string.IsNullOrEmpty(telephone) ? null : telephone },
                }, reponses =>
                {
                    if (!int.TryParse(reponses["prix"].Replace(" ", "").Replace("€", ""), out int prix) || prix < 0)
                    {
                        joueur.Notify("LeBonNeo", "Prix invalide.", NotificationManager.Type.Error);
                        Deposer(joueur, retour);
                        return;
                    }

                    Annonce annonce = new Annonce
                    {
                        Id = D.ProchainId++,
                        VendeurId = Joueurs.Id(joueur),
                        VendeurNom = Joueurs.Nom(joueur),
                        Categorie = categorie,
                        Titre = reponses["titre"],
                        Prix = prix,
                        Description = reponses["description"],
                        Contact = reponses["contact"],
                        Date = DateTime.Now,
                        Remontee = DateTime.Now,
                    };
                    D.Annonces.Add(annonce);
                    stockage.Sauvegarder();

                    _ = NovaAppsPlugin.Discord.PosterAvecWebhook(Config.WebhookDiscord, "LeBonNeo", "Nouvelle annonce !", new JObject
                    {
                        ["title"] = $"🏷️ {annonce.Titre}",
                        ["description"] = Ecran.Couper(annonce.Description, 1500),
                        ["color"] = 0xff6e14,
                        ["fields"] = new JArray
                        {
                            Discord.Field("Prix", Prix(annonce.Prix)),
                            Discord.Field("Catégorie", annonce.Categorie),
                            Discord.Field("Vendeur", $"{annonce.VendeurNom} · {annonce.Contact}"),
                        },
                    });

                    joueur.Notify("LeBonNeo", "Annonce publiée !", NotificationManager.Type.Success);
                    Voir(joueur, annonce, retour);
                }, retour);
            }, retour);
        }

        private ConversationAnnonce ConversationAvec(Annonce a, Player acheteur)
        {
            int id = Joueurs.Id(acheteur);
            ConversationAnnonce conversation = D.Conversations.FirstOrDefault(c => c.AnnonceId == a.Id && c.AcheteurId == id);
            if (conversation != null) return conversation;

            conversation = new ConversationAnnonce { Id = D.ProchainId++, AnnonceId = a.Id, AcheteurId = id, AcheteurNom = Joueurs.Nom(acheteur), VendeurId = a.VendeurId };
            D.Conversations.Add(conversation);
            return conversation;
        }

        private void Messages(Player joueur, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Action recharger = () => Messages(joueur, retour);

            Ecran.Liste(joueur, "LeBonNeo · Mes messages",
                D.Conversations.Where(c => c.Concerne(moi) && c.Messages.Count > 0)
                    .OrderByDescending(c => c.Messages.Last().Date)
                    .Select(c =>
                    {
                        Annonce a = D.Annonces.FirstOrDefault(x => x.Id == c.AnnonceId);
                        string avec = c.VendeurId == moi ? c.AcheteurNom : a?.VendeurNom ?? "?";
                        return new Ligne($"{(c.NonLu(moi) ? "● " : "")}{Ecran.Couper(a?.Titre ?? "Annonce supprimée", 35)}  —  {avec}", () => Conversation(joueur, c, recharger));
                    }),
                retour, "Aucun message.");
        }

        private void Conversation(Player joueur, ConversationAnnonce c, Action retour)
        {
            int moi = Joueurs.Id(joueur);
            Annonce a = D.Annonces.FirstOrDefault(x => x.Id == c.AnnonceId);
            string autreNom = c.VendeurId == moi ? c.AcheteurNom : a?.VendeurNom ?? "Vendeur";
            Action relire = () => Conversation(joueur, c, retour);

            if (moi == c.AcheteurId) c.LuParAcheteur = true;
            else c.LuParVendeur = true;
            stockage.Sauvegarder();

            string fil = string.Join("\n", c.Messages.Skip(Math.Max(0, c.Messages.Count - 15)).Select(m =>
                $"<b>{(m.AuteurId == moi ? "Toi" : autreNom)}</b> <size=80%>{Ecran.Date(m.Date)}</size> : {m.Texte}"));

            Ecran.Texte(joueur, $"LeBonNeo · {Ecran.Couper(a?.Titre ?? "Annonce supprimée", 40)}", fil, retour,
                a != null ? new Bouton("Répondre", () => Ecrire(joueur, c, relire)) : null,
                a != null ? new Bouton("Voir l'annonce", () => Voir(joueur, a, relire)) : null);
        }

        private void Ecrire(Player joueur, ConversationAnnonce c, Action retour)
        {
            Annonce a = D.Annonces.FirstOrDefault(x => x.Id == c.AnnonceId);
            Ecran.Saisie(joueur, "LeBonNeo · Message", $"Ton message à propos de « {a?.Titre} » :", "Ex : Bonjour, toujours disponible ?", texte =>
            {
                int moi = Joueurs.Id(joueur);
                c.Messages.Add(new NeonousMessage { AuteurId = moi, Texte = texte, Date = DateTime.Now });
                bool parAcheteur = moi == c.AcheteurId;
                c.LuParAcheteur = parAcheteur;
                c.LuParVendeur = !parAcheteur;
                stockage.Sauvegarder();

                Joueurs.Notifier(parAcheteur ? c.VendeurId : c.AcheteurId, "LeBonNeo", $"{Joueurs.Nom(joueur)} ({Ecran.Couper(a?.Titre, 25)}) : {Ecran.Couper(texte, 50)}");
                joueur.Notify("LeBonNeo", "Message envoyé.", NotificationManager.Type.Success);
                Conversation(joueur, c, retour);
            }, retour, 300);
        }
    }
}
