using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Life;
using Life.DB;
using Life.Network;
using Life.PermissionSystem;
using Life.UI;
using NovaApps.Core;
using UnityEngine;

namespace NovaApps.Apps
{
    public class ConcessionDonnees
    {
        public int ProchainId = 1;
        public List<Commande> Commandes = new List<Commande>();
        public List<RendezVous> RendezVous = new List<RendezVous>();
    }

    public class Commande
    {
        public int Id;
        public int ClientId;
        public string ClientNom;
        public int ModelId;
        public string Vehicule;
        public int Prix;
        public string CouleurOptions;
        public string Disponibilites;
        public DateTime Date;
        public string Statut = StatutCommande.EnAttente;
        public string Vendeur;
        public string Note;
    }

    public static class StatutCommande
    {
        public const string EnAttente = "En attente";
        public const string Acceptee = "Acceptée";
        public const string Prete = "Prête à livrer";
        public const string Livree = "Livrée";
        public const string Refusee = "Refusée";
        public const string Annulee = "Annulée";

        public static bool Terminee(string statut) => statut == Livree || statut == Refusee || statut == Annulee;
    }

    public class RendezVous
    {
        public int Id;
        public int ClientId;
        public string ClientNom;
        public string Vehicule;
        public string Probleme;
        public string Disponibilites;
        public DateTime Date;
        public bool Traite;
    }

    /// <summary>
    /// Capital Motors : catalogue, commande de véhicule et rendez-vous atelier depuis le téléphone.
    /// Les commandes arrivent chez les membres de la concession (en jeu) et dans le salon Discord
    /// du bot Garage / Concession, avec le même bouton "Je me charge du ticket".
    /// </summary>
    public class CapitalMotorsApp
    {
        private const string Rouge = "#e74c3c";

        private readonly Stockage<ConcessionDonnees> stockage = new Stockage<ConcessionDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "capital-motors.json"));
        private readonly Action sauvegarderConfig;
        private ConcessionDonnees D => stockage.Donnees;
        private CapitalMotorsConfig Config => NovaAppsPlugin.Config.CapitalMotors;

        public CapitalMotorsApp(Action sauvegarderConfig)
        {
            this.sauvegarderConfig = sauvegarderConfig;
            RemplirCatalogueParDefaut();
        }

        public bool EstVendeur(Player joueur)
        {
            return Joueurs.Membre(joueur, Config.EntreprisesConcession);
        }

        public void Ouvrir(Player joueur, Action retour)
        {
            Action menu = () => Ouvrir(joueur, retour);
            int mesCommandes = D.Commandes.Count(c => c.ClientId == Joueurs.Id(joueur) && !StatutCommande.Terminee(c.Statut));

            List<Ligne> lignes = new List<Ligne>
            {
                new Ligne($"Catalogue ({Config.Catalogue.Count} modèles)", () => Catalogue(joueur, menu)),
                new Ligne("Mes commandes" + (mesCommandes > 0 ? $"  ·  {mesCommandes} en cours" : ""), () => MesCommandes(joueur, menu)),
                new Ligne("Prendre rendez-vous à l'atelier", () => PrendreRendezVous(joueur, menu)),
            };

            if (EstVendeur(joueur))
            {
                int aTraiter = D.Commandes.Count(c => c.Statut == StatutCommande.EnAttente) + D.RendezVous.Count(r => !r.Traite);
                lignes.Add(new Ligne("Espace concession" + (aTraiter > 0 ? $"  ·  {aTraiter} à traiter" : ""), () => EspaceConcession(joueur, menu)));
            }

            Ecran.Liste(joueur, Config.Nom, lignes, retour);
        }

        // ------------------------------------------------------------------
        // Côté client
        // ------------------------------------------------------------------

        private static string Prix(int prix) => prix > 0 ? $"{prix:N0} €".Replace(' ', ' ') : "Prix sur devis";

        public void Catalogue(Player joueur, Action retour)
        {
            Action recharger = () => Catalogue(joueur, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · Catalogue",
                Config.Catalogue.OrderBy(v => v.Nom).Select(v => new Ligne($"{v.Nom}  —  {Prix(v.Prix)}", () => FicheVehicule(joueur, v, recharger))),
                retour, "Le catalogue est vide pour le moment.");
        }

        private void FicheVehicule(Player joueur, VehiculeEnVente v, Action retour)
        {
            string permis = joueur.character.PermisB ? "" : $"\n\n{Ecran.Couleur("Attention : tu n'as pas le permis B.", Rouge)}";
            Ecran.Texte(joueur, $"{Config.Nom} · {v.Nom}",
                $"{Ecran.Couleur($"<b>{v.Nom}</b>", Rouge)}\n\n<b>Prix :</b> {Prix(v.Prix)}\n\nLe paiement se fait à la remise du véhicule, en concession.{permis}",
                retour, new Bouton("Commander", () => Commander(joueur, v, retour)));
        }

        private void Commander(Player joueur, VehiculeEnVente v, Action retour)
        {
            Ecran.Formulaire(joueur, $"{Config.Nom} · Commande", new List<Champ>
            {
                new Champ("options", $"Couleur et options souhaitées pour ta {v.Nom} :", "Ex : Noir mat, jantes chromées, full tuning", 300) { ParDefaut = "Aucune préférence" },
                new Champ("dispos", "Tes disponibilités pour la livraison :", "Ex : demain après 20h", 150),
            }, reponses =>
            {
                Ecran.Confirmer(joueur, $"{Config.Nom} · Récapitulatif",
                    $"<b>Véhicule :</b> {v.Nom}\n<b>Prix :</b> {Prix(v.Prix)}\n<b>Couleur / options :</b> {reponses["options"]}\n<b>Livraison :</b> {reponses["dispos"]}\n\nUn vendeur te contactera. Paiement à la remise du véhicule.",
                    () =>
                    {
                        Commande commande = new Commande
                        {
                            Id = D.ProchainId++,
                            ClientId = Joueurs.Id(joueur),
                            ClientNom = Joueurs.Nom(joueur),
                            ModelId = v.ModelId,
                            Vehicule = v.Nom,
                            Prix = v.Prix,
                            CouleurOptions = reponses["options"],
                            Disponibilites = reponses["dispos"],
                            Date = DateTime.Now,
                        };
                        D.Commandes.Add(commande);
                        stockage.Sauvegarder();

                        PrevenirVendeurs($"Nouvelle commande n°{commande.Id} : {commande.Vehicule} pour {commande.ClientNom}.");
                        _ = NovaAppsPlugin.Discord.PosterAvecBot(Config.SalonDiscordCommandes, Discord.Ticket(
                            "🛒 Nouvelle commande de véhicule", 0x3498db, $"{commande.ClientNom} (via l'appli {Config.Nom})",
                            new Dictionary<string, string>
                            {
                                ["Nom RP"] = commande.ClientNom,
                                ["Véhicule souhaité"] = commande.Vehicule,
                                ["Couleur / Options (jantes, tuning…)"] = commande.CouleurOptions,
                                ["Budget"] = Prix(commande.Prix),
                                ["Disponibilités pour la livraison"] = commande.Disponibilites,
                                ["N° de commande"] = commande.Id.ToString(),
                            }));

                        joueur.Notify(Config.Nom, $"Commande n°{commande.Id} envoyée ! Suis-la dans « Mes commandes ».", NotificationManager.Type.Success);
                        MesCommandes(joueur, retour);
                    }, retour);
            }, retour);
        }

        private void MesCommandes(Player joueur, Action retour)
        {
            Action recharger = () => MesCommandes(joueur, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · Mes commandes",
                D.Commandes.Where(c => c.ClientId == Joueurs.Id(joueur)).OrderByDescending(c => c.Date)
                    .Select(c => new Ligne($"n°{c.Id}  {c.Vehicule}  —  {c.Statut}", () => Ecran.Texte(joueur, $"{Config.Nom} · Commande n°{c.Id}", TexteCommande(c), recharger,
                        c.Statut == StatutCommande.EnAttente ? new Bouton("Annuler la commande", () => Ecran.Confirmer(joueur, Config.Nom, "Annuler cette commande ?", () =>
                        {
                            c.Statut = StatutCommande.Annulee;
                            stockage.Sauvegarder();
                            PrevenirVendeurs($"La commande n°{c.Id} ({c.Vehicule}) a été annulée par le client.");
                            recharger();
                        }, recharger)) : null))),
                retour, "Tu n'as passé aucune commande.");
        }

        private static string TexteCommande(Commande c)
        {
            string texte = $"<b>Commande n°{c.Id}</b>  ·  {c.Date:dd/MM HH:mm}\n<b>Statut :</b> {c.Statut}\n\n<b>Client :</b> {c.ClientNom}\n<b>Véhicule :</b> {c.Vehicule}\n<b>Prix :</b> {Prix(c.Prix)}\n" +
                           $"<b>Couleur / options :</b> {c.CouleurOptions}\n<b>Livraison :</b> {c.Disponibilites}";
            if (!string.IsNullOrEmpty(c.Vendeur)) texte += $"\n<b>Vendeur :</b> {c.Vendeur}";
            if (!string.IsNullOrEmpty(c.Note)) texte += $"\n<b>Note :</b> {c.Note}";
            return texte;
        }

        private void PrendreRendezVous(Player joueur, Action retour)
        {
            Ecran.Formulaire(joueur, $"{Config.Nom} · Atelier", new List<Champ>
            {
                new Champ("vehicule", "Véhicule concerné :", "Ex : Sultan RS", 60),
                new Champ("probleme", "Problème / service souhaité :", "Décris le problème ou le service voulu", 400),
                new Champ("dispos", "Tes disponibilités :", "Ex : ce soir à partir de 21h", 150),
            }, reponses =>
            {
                RendezVous rdv = new RendezVous
                {
                    Id = D.ProchainId++,
                    ClientId = Joueurs.Id(joueur),
                    ClientNom = Joueurs.Nom(joueur),
                    Vehicule = reponses["vehicule"],
                    Probleme = reponses["probleme"],
                    Disponibilites = reponses["dispos"],
                    Date = DateTime.Now,
                };
                D.RendezVous.Add(rdv);
                stockage.Sauvegarder();

                PrevenirVendeurs($"Nouvelle demande de rendez-vous atelier de {rdv.ClientNom} ({rdv.Vehicule}).");
                _ = NovaAppsPlugin.Discord.PosterAvecBot(Config.SalonDiscordCommandes, Discord.Ticket(
                    "🔧 Nouvelle demande de rendez-vous", 0xf1c40f, $"{rdv.ClientNom} (via l'appli {Config.Nom})",
                    new Dictionary<string, string>
                    {
                        ["Nom RP"] = rdv.ClientNom,
                        ["Véhicule"] = rdv.Vehicule,
                        ["Problème / service souhaité"] = rdv.Probleme,
                        ["Disponibilités"] = rdv.Disponibilites,
                    }));

                joueur.Notify(Config.Nom, "Demande envoyée ! Un mécanicien te répondra au plus vite.", NotificationManager.Type.Success);
                retour();
            }, retour);
        }

        // ------------------------------------------------------------------
        // Côté concession
        // ------------------------------------------------------------------

        private void PrevenirVendeurs(string message)
        {
            foreach (Player vendeur in Joueurs.TousEnLigne().Where(p => !p.IsAdmin && EstVendeur(p)))
                vendeur.Notify(Config.Nom, message, NotificationManager.Type.Info);
        }

        private void EspaceConcession(Player joueur, Action retour)
        {
            Action menu = () => EspaceConcession(joueur, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · Concession", new[]
            {
                new Ligne($"Commandes en cours ({D.Commandes.Count(c => !StatutCommande.Terminee(c.Statut))})", () => Commandes(joueur, false, menu)),
                new Ligne("Historique des commandes", () => Commandes(joueur, true, menu)),
                new Ligne($"Rendez-vous atelier ({D.RendezVous.Count(r => !r.Traite)} à traiter)", () => ListeRendezVous(joueur, menu)),
                new Ligne("Modifier le catalogue", () => ModifierCatalogue(joueur, menu)),
            }, retour);
        }

        private void Commandes(Player joueur, bool historique, Action retour)
        {
            Action recharger = () => Commandes(joueur, historique, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · {(historique ? "Historique" : "Commandes en cours")}",
                D.Commandes.Where(c => StatutCommande.Terminee(c.Statut) == historique)
                    .OrderByDescending(c => c.Date).Take(50)
                    .Select(c => new Ligne($"n°{c.Id}  {c.Vehicule}  —  {c.ClientNom}  ·  {c.Statut}", () => GererCommande(joueur, c, recharger))),
                retour, "Aucune commande.");
        }

        private void GererCommande(Player joueur, Commande c, Action retour)
        {
            Action relire = () => GererCommande(joueur, c, retour);
            bool terminee = StatutCommande.Terminee(c.Statut);

            Ecran.Texte(joueur, $"{Config.Nom} · Commande n°{c.Id}", TexteCommande(c), retour,
                c.Statut == StatutCommande.EnAttente ? new Bouton("Accepter", () => ChangerStatut(joueur, c, StatutCommande.Acceptee, null, relire)) : null,
                c.Statut == StatutCommande.Acceptee ? new Bouton("Prête à livrer", () => ChangerStatut(joueur, c, StatutCommande.Prete, null, relire)) : null,
                c.Statut == StatutCommande.Acceptee || c.Statut == StatutCommande.Prete ? new Bouton("Livrer", () => Livrer(joueur, c, relire)) : null,
                !terminee ? new Bouton("Refuser", () => Ecran.Saisie(joueur, Config.Nom, "Motif du refus (envoyé au client) :", "Ex : modèle en rupture de stock",
                    motif => ChangerStatut(joueur, c, StatutCommande.Refusee, motif, relire), relire, 200, true)) : null,
                !terminee ? new Bouton("Ajouter une note", () => Ecran.Saisie(joueur, Config.Nom, "Note visible par le client :", "Ex : livraison prévue samedi 21h",
                    note => ChangerStatut(joueur, c, c.Statut, note, relire), relire, 200)) : null);
        }

        private void ChangerStatut(Player vendeur, Commande c, string statut, string note, Action retour)
        {
            bool nouveauStatut = c.Statut != statut;
            c.Statut = statut;
            c.Vendeur = Joueurs.Nom(vendeur);
            if (!string.IsNullOrEmpty(note)) c.Note = note;
            stockage.Sauvegarder();

            string message = nouveauStatut ? $"Ta commande n°{c.Id} ({c.Vehicule}) : {statut}." : $"Message du vendeur pour ta commande n°{c.Id} : {note}";
            if (nouveauStatut && !string.IsNullOrEmpty(note)) message += $" {note}";
            Joueurs.Notifier(c.ClientId, Config.Nom, message, statut == StatutCommande.Refusee ? NotificationManager.Type.Error : NotificationManager.Type.Success);
            retour();
        }

        private void Livrer(Player vendeur, Commande c, Action retour)
        {
            string texte = $"Livrer la commande n°{c.Id} ({c.Vehicule}) à {c.ClientNom} ?\n\nVérifie d'avoir encaissé <b>{Prix(c.Prix)}</b> avant de confirmer.";
            if (Config.CreerVehiculeALaLivraison)
                texte += $"\n\nLe véhicule sera ajouté directement dans les véhicules de {c.ClientNom}.";

            Ecran.Confirmer(vendeur, Config.Nom, texte, async () =>
            {
                if (Config.CreerVehiculeALaLivraison && !await CreerVehicule(c))
                {
                    vendeur.Notify(Config.Nom, "Impossible de créer le véhicule, la commande n'a pas été modifiée.", NotificationManager.Type.Error);
                    retour();
                    return;
                }

                ChangerStatut(vendeur, c, StatutCommande.Livree, Config.CreerVehiculeALaLivraison ? "Le véhicule est dans ta liste de véhicules." : null, retour);
                vendeur.Notify(Config.Nom, $"Commande n°{c.Id} livrée.", NotificationManager.Type.Success);
            }, retour);
        }

        private static async Task<bool> CreerVehicule(Commande c)
        {
            try
            {
                Permissions permissions = new Permissions { owner = new Entity { characterId = c.ClientId } };
                await LifeDB.CreateVehicle(c.ModelId, JsonUtility.ToJson(permissions));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NovaApps] Création du véhicule de la commande {c.Id} impossible : {e.Message}");
                return false;
            }
        }

        private void ListeRendezVous(Player joueur, Action retour)
        {
            Action recharger = () => ListeRendezVous(joueur, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · Rendez-vous atelier",
                D.RendezVous.OrderBy(r => r.Traite).ThenByDescending(r => r.Date).Take(50).Select(r =>
                    new Ligne($"{r.ClientNom}  —  {r.Vehicule}  ·  {(r.Traite ? "traité" : "à traiter")}", () => Ecran.Texte(joueur, $"{Config.Nom} · Rendez-vous",
                        $"<b>{r.ClientNom}</b>  ·  {r.Date:dd/MM HH:mm}\n\n<b>Véhicule :</b> {r.Vehicule}\n<b>Disponibilités :</b> {r.Disponibilites}\n\n{r.Probleme}",
                        recharger,
                        !r.Traite ? new Bouton("Marquer comme traité", () =>
                        {
                            r.Traite = true;
                            stockage.Sauvegarder();
                            Joueurs.Notifier(r.ClientId, Config.Nom, $"Ta demande de rendez-vous ({r.Vehicule}) a été prise en charge par {Joueurs.Nom(joueur)}.", NotificationManager.Type.Success);
                            recharger();
                        }) : null))),
                retour, "Aucune demande de rendez-vous.");
        }

        private void ModifierCatalogue(Player joueur, Action retour)
        {
            Action recharger = () => ModifierCatalogue(joueur, retour);
            Ecran.Liste(joueur, $"{Config.Nom} · Catalogue (tous les modèles du jeu)",
                ModelesDuJeu().Select(m =>
                {
                    VehiculeEnVente enVente = Config.Catalogue.FirstOrDefault(v => v.ModelId == m.Key);
                    return new Ligne($"{m.Value}  —  {(enVente != null ? Prix(enVente.Prix) : "non vendu")}", () =>
                        Ecran.Saisie(joueur, Config.Nom, $"Prix de vente de la {m.Value} en euros.\n0 = prix sur devis · vide = retirer du catalogue.", "Ex : 45000", valeur =>
                        {
                            Config.Catalogue.RemoveAll(v => v.ModelId == m.Key);
                            if (valeur.Length > 0)
                            {
                                if (!int.TryParse(valeur.Replace(" ", "").Replace("€", ""), out int prix) || prix < 0)
                                {
                                    joueur.Notify(Config.Nom, "Prix invalide.", NotificationManager.Type.Error);
                                    recharger();
                                    return;
                                }
                                Config.Catalogue.Add(new VehiculeEnVente { ModelId = m.Key, Nom = m.Value, Prix = prix });
                            }
                            sauvegarderConfig();
                            recharger();
                        }, recharger, 12, true));
                }),
                retour);
        }

        private static IEnumerable<KeyValuePair<int, string>> ModelesDuJeu()
        {
            for (int i = 0; i < Nova.v.vehicleModels.Length; i++)
            {
                object modele = Nova.v.vehicleModels[i];
                if (modele != null && !(Membre(modele, "isDeprecated") is bool obsolete && obsolete))
                    yield return new KeyValuePair<int, string>(i, Membre(modele, "vehicleName") as string ?? (modele as UnityEngine.Object)?.name ?? $"Modèle {i}");
            }
        }

        /// <summary>
        /// Lit un champ ou une propriété d'un modèle de véhicule par son nom : la classe des modèles
        /// change selon les versions du jeu, on évite donc d'en dépendre à la compilation.
        /// </summary>
        private static object Membre(object objet, string nom)
        {
            const BindingFlags drapeaux = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            System.Type type = objet.GetType();
            return type.GetField(nom, drapeaux)?.GetValue(objet) ?? type.GetProperty(nom, drapeaux)?.GetValue(objet);
        }

        private void RemplirCatalogueParDefaut()
        {
            if (Config.Catalogue.Count > 0) return;

            try
            {
                Config.Catalogue.AddRange(ModelesDuJeu().Select(m => new VehiculeEnVente { ModelId = m.Key, Nom = m.Value, Prix = 0 }));
                sauvegarderConfig();
            }
            catch (Exception e)
            {
                // Modèles pas encore chargés : le catalogue sera rempli au prochain démarrage.
                Debug.LogWarning($"[NovaApps] Catalogue par défaut non créé : {e.Message}");
            }
        }
    }
}
