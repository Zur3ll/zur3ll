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
    public class PoliceDonnees
    {
        public int ProchainId = 1;
        /// <summary>Fichier des personnes, par ID de personnage.</summary>
        public Dictionary<int, FichePolice> Fiches = new Dictionary<int, FichePolice>();
        public List<Amende> Amendes = new List<Amende>();
        public List<AvisDeRecherche> Avis = new List<AvisDeRecherche>();
        public List<Plainte> Plaintes = new List<Plainte>();
        public List<Appel> Appels = new List<Appel>();
    }

    public class FichePolice
    {
        public int PersonnageId;
        public string Nom;
        public List<EntreeFiche> Infractions = new List<EntreeFiche>();
        public List<EntreeFiche> Notes = new List<EntreeFiche>();
    }

    public class EntreeFiche
    {
        public DateTime Date;
        public string Agent;
        public string Texte;
    }

    public class Amende
    {
        public int Id;
        public int PersonnageId;
        public string Nom;
        public string Motif;
        public int Montant;
        public int AgentId;
        public string Agent;
        public DateTime Date;
        public bool Payee;
    }

    public class AvisDeRecherche
    {
        public int Id;
        public int PersonnageId;
        public string Nom;
        public string Motif;
        public string Signalement;
        public string Dangerosite;
        public int Prime;
        public string Agent;
        public DateTime Date;
        public bool Actif = true;
    }

    public class Plainte
    {
        public int Id;
        public int PlaignantId;
        public string PlaignantNom;
        public string Contre;
        public string Faits;
        public DateTime Date;
        public string Statut = StatutPlainte.Ouverte;
        public string Agent;
        public string Reponse;
    }

    public static class StatutPlainte
    {
        public const string Ouverte = "Ouverte";
        public const string EnCours = "En cours";
        public const string Classee = "Classée";
    }

    public class Appel
    {
        public int Id;
        public int AppelantId;
        public string Nom;
        public string Lieu;
        public string Message;
        public DateTime Date;
        public string PrisPar;
    }

    /// <summary>
    /// Police : appel au 17, plaintes, amendes et avis de recherche pour les citoyens ;
    /// fichier des personnes (casier, notes), amendes, avis et traitement des plaintes / appels pour les agents.
    /// </summary>
    public class PoliceApp
    {
        private const string Bleu = "#3c6ff0";
        private const string Rouge = "#e74c3c";
        private static readonly string[] Dangerosites = { "Faible", "Moyenne", "Élevée", "Armé et dangereux" };

        private readonly Stockage<PoliceDonnees> stockage = new Stockage<PoliceDonnees>(Path.Combine(NovaAppsPlugin.Dossier, "police.json"));
        private PoliceDonnees D => stockage.Donnees;
        private PoliceConfig Config => NovaAppsPlugin.Config.Police;

        public bool EstAgent(Player joueur) => Joueurs.Membre(joueur, Config.EntreprisesPolice);

        public IEnumerable<AvisDeRecherche> AvisActifs => D.Avis.Where(a => a.Actif);

        /// <summary>Amendes à payer, plus les appels 17 en attente pour les agents.</summary>
        public int Badge(Player joueur)
        {
            int moi = Joueurs.Id(joueur);
            return D.Amendes.Count(a => a.PersonnageId == moi && !a.Payee) + (EstAgent(joueur) ? D.Appels.Count(a => a.PrisPar == null) : 0);
        }

        public void Ouvrir(Player joueur, Action retour, bool surOrdinateur)
        {
            int moi = Joueurs.Id(joueur);
            Action menu = () => Ouvrir(joueur, retour, surOrdinateur);
            int aPayer = D.Amendes.Count(a => a.PersonnageId == moi && !a.Payee);

            List<Ligne> lignes = new List<Ligne>
            {
                new Ligne("Appeler le 17", () => Appeler(joueur, menu)),
                new Ligne("Porter plainte", () => PorterPlainte(joueur, menu)),
                new Ligne("Mes plaintes", () => MesPlaintes(joueur, menu)),
                new Ligne("Mes amendes" + (aPayer > 0 ? $"  ·  {aPayer} à payer" : ""), () => MesAmendes(joueur, menu)),
                new Ligne($"Avis de recherche ({AvisActifs.Count()})", () => ListeAvis(joueur, menu, false)),
            };

            if (EstAgent(joueur))
            {
                int appels = D.Appels.Count(a => a.PrisPar == null);
                int plaintes = D.Plaintes.Count(p => p.Statut == StatutPlainte.Ouverte);
                lignes.Add(new Ligne($"[Agent] Appels 17" + (appels > 0 ? $"  ·  {appels} en attente" : ""), () => Appels(joueur, menu)));
                lignes.Add(new Ligne($"[Agent] Plaintes" + (plaintes > 0 ? $"  ·  {plaintes} ouverte(s)" : ""), () => Plaintes(joueur, menu)));
                lignes.Add(new Ligne("[Agent] Dresser une amende", () => Ecran.ChoisirJoueur(joueur, "Police · Amende à", cible => DresserAmende(joueur, cible.character.Id, Joueurs.Nom(cible), menu), menu)));
                lignes.Add(new Ligne("[Agent] Gérer les avis de recherche", () => ListeAvis(joueur, menu, true)));

                if (surOrdinateur || !Config.FichierSurOrdinateurUniquement)
                    lignes.Add(new Ligne("[Agent] Fichier des personnes", () => Fichier(joueur, menu)));
                else
                    lignes.Add(new Ligne("[Agent] Fichier des personnes  —  sur ordinateur", () => Ecran.Texte(joueur, Config.Nom, "Le fichier des personnes se consulte depuis un ordinateur du commissariat.", menu)));
            }

            Ecran.Liste(joueur, Config.Nom, lignes, retour);
        }

        private void PrevenirAgents(string message, NotificationManager.Type type = NotificationManager.Type.Warning)
        {
            foreach (Player agent in Joueurs.TousEnLigne().Where(p => !p.IsAdmin && EstAgent(p)))
                agent.Notify(Config.Nom, message, type);
        }

        // ------------------------------------------------------------------
        // Citoyens
        // ------------------------------------------------------------------

        private void Appeler(Player joueur, Action retour)
        {
            Ecran.Formulaire(joueur, "Police · 17", new List<Champ>
            {
                new Champ("lieu", "Où te trouves-tu ?", "Ex : devant la banque, rue principale", 100),
                new Champ("message", "Que se passe-t-il ?", "Ex : braquage en cours, 2 individus armés", 300),
            }, reponses =>
            {
                Appel appel = new Appel
                {
                    Id = D.ProchainId++,
                    AppelantId = Joueurs.Id(joueur),
                    Nom = Joueurs.Nom(joueur),
                    Lieu = reponses["lieu"],
                    Message = reponses["message"],
                    Date = DateTime.Now,
                };
                D.Appels.Add(appel);
                stockage.Sauvegarder();

                PrevenirAgents($"APPEL 17 de {appel.Nom} — {appel.Lieu} : {Ecran.Couper(appel.Message, 60)}");
                int agents = Joueurs.TousEnLigne().Count(p => !p.IsAdmin && EstAgent(p));
                joueur.Notify("Police · 17", agents > 0 ? $"Appel transmis à {agents} agent(s) en ville." : "Appel enregistré, mais aucun agent n'est en ville pour le moment.",
                    agents > 0 ? NotificationManager.Type.Success : NotificationManager.Type.Warning);
                retour();
            }, retour);
        }

        private void PorterPlainte(Player joueur, Action retour)
        {
            Ecran.Formulaire(joueur, "Police · Plainte", new List<Champ>
            {
                new Champ("contre", "Contre qui ? (nom, description ou « X » si inconnu)", "Ex : John Doe", 80),
                new Champ("faits", "Décris les faits (date, lieu, ce qui s'est passé) :", "Ex : hier vers 22h, on m'a volé ma voiture devant chez moi", 800),
            }, reponses =>
            {
                Plainte plainte = new Plainte
                {
                    Id = D.ProchainId++,
                    PlaignantId = Joueurs.Id(joueur),
                    PlaignantNom = Joueurs.Nom(joueur),
                    Contre = reponses["contre"],
                    Faits = reponses["faits"],
                    Date = DateTime.Now,
                };
                D.Plaintes.Add(plainte);
                stockage.Sauvegarder();

                PrevenirAgents($"Nouvelle plainte n°{plainte.Id} de {plainte.PlaignantNom}.", NotificationManager.Type.Info);
                joueur.Notify(Config.Nom, $"Plainte n°{plainte.Id} enregistrée.", NotificationManager.Type.Success);
                retour();
            }, retour);
        }

        private void MesPlaintes(Player joueur, Action retour)
        {
            Action recharger = () => MesPlaintes(joueur, retour);
            Ecran.Liste(joueur, "Police · Mes plaintes",
                D.Plaintes.Where(p => p.PlaignantId == Joueurs.Id(joueur)).OrderByDescending(p => p.Date)
                    .Select(p => new Ligne($"n°{p.Id}  contre {Ecran.Couper(p.Contre, 30)}  —  {p.Statut}", () => Ecran.Texte(joueur, $"Police · Plainte n°{p.Id}", TextePlainte(p), recharger))),
                retour, "Tu n'as déposé aucune plainte.");
        }

        private static string TextePlainte(Plainte p)
        {
            string texte = $"<b>Plainte n°{p.Id}</b>  ·  {p.Date:dd/MM HH:mm}  ·  {p.Statut}\n<b>Plaignant :</b> {p.PlaignantNom}\n<b>Contre :</b> {p.Contre}\n\n{p.Faits}";
            if (p.Agent != null) texte += $"\n\n<b>Agent :</b> {p.Agent}";
            if (!string.IsNullOrEmpty(p.Reponse)) texte += $"\n<b>Suite donnée :</b> {p.Reponse}";
            return texte;
        }

        private void MesAmendes(Player joueur, Action retour)
        {
            Action recharger = () => MesAmendes(joueur, retour);
            Ecran.Liste(joueur, "Police · Mes amendes",
                D.Amendes.Where(a => a.PersonnageId == Joueurs.Id(joueur)).OrderBy(a => a.Payee).ThenByDescending(a => a.Date)
                    .Select(a => new Ligne($"{a.Montant} €  —  {Ecran.Couper(a.Motif, 40)}  ·  {(a.Payee ? "payée" : "À PAYER")}", () =>
                        Ecran.Texte(joueur, $"Police · Amende n°{a.Id}",
                            $"<b>Amende n°{a.Id}</b>  ·  {a.Date:dd/MM HH:mm}\n<b>Montant :</b> {a.Montant} €\n<b>Motif :</b> {a.Motif}\n<b>Agent :</b> {a.Agent}\n<b>État :</b> {(a.Payee ? "payée" : "à payer")}",
                            recharger,
                            a.Payee ? null : new Bouton($"Payer {a.Montant} € (banque)", () => Payer(joueur, a, recharger))))),
                retour, "Aucune amende. Continue comme ça !");
        }

        private void Payer(Player joueur, Amende a, Action retour)
        {
            if (a.Payee)
            {
                retour();
                return;
            }

            // L'argent en poche est géré par des porte-monnaie (objets) : l'amende se paie par la banque.
            if (joueur.Bank < a.Montant)
            {
                joueur.Notify(Config.Nom, $"Il te faut {a.Montant} € sur ton compte en banque pour payer cette amende.", NotificationManager.Type.Error);
                retour();
                return;
            }

            joueur.AddBankMoney(-a.Montant, "AMENDE_POLICE");
            a.Payee = true;
            stockage.Sauvegarder();

            Joueurs.Notifier(a.AgentId, Config.Nom, $"{a.Nom} a payé l'amende n°{a.Id} ({a.Montant} €).", NotificationManager.Type.Success);
            joueur.Notify(Config.Nom, "Amende payée.", NotificationManager.Type.Success);
            retour();
        }

        private void ListeAvis(Player joueur, Action retour, bool gestion)
        {
            Action recharger = () => ListeAvis(joueur, retour, gestion);
            List<Ligne> lignes = (gestion ? D.Avis.AsEnumerable() : AvisActifs).OrderByDescending(a => a.Actif).ThenByDescending(a => a.Date).Take(50)
                .Select(a => new Ligne($"{a.Nom}  —  {Ecran.Couper(a.Motif, 35)}{(a.Prime > 0 ? $"  ·  prime {a.Prime} €" : "")}{(a.Actif ? "" : "  ·  levé")}",
                    () => VoirAvis(joueur, a, recharger, gestion)))
                .ToList();

            if (gestion)
                lignes.Insert(0, new Ligne("+ Émettre un avis de recherche", () => EmettreAvis(joueur, recharger)));

            Ecran.Liste(joueur, "Police · Avis de recherche", lignes, retour, "Aucun avis de recherche en cours.");
        }

        private void VoirAvis(Player joueur, AvisDeRecherche a, Action retour, bool gestion)
        {
            Ecran.Texte(joueur, "Police · Avis de recherche",
                $"{Ecran.Couleur("<b>AVIS DE RECHERCHE</b>", Rouge)}\n\n<b>{a.Nom}</b>\n<b>Motif :</b> {a.Motif}\n<b>Signalement :</b> {a.Signalement}\n<b>Dangerosité :</b> {a.Dangerosite}" +
                (a.Prime > 0 ? $"\n<b>Prime :</b> {a.Prime} €" : "") + $"\n\n<size=80%>Émis le {a.Date:dd/MM} par {a.Agent}{(a.Actif ? "" : " · levé")}</size>\n\nSi vous le voyez, ne prenez aucun risque : appelez le 17.",
                retour,
                !gestion && a.Actif ? new Bouton("Signaler (17)", () => Appeler(joueur, retour)) : null,
                gestion && a.Actif ? new Bouton("Lever l'avis", () =>
                {
                    a.Actif = false;
                    stockage.Sauvegarder();
                    joueur.Notify(Config.Nom, $"Avis de recherche contre {a.Nom} levé.", NotificationManager.Type.Success);
                    retour();
                }) : null);
        }

        private void EmettreAvis(Player joueur, Action retour, int personnageId = 0, string nom = null)
        {
            List<Champ> champs = new List<Champ>();
            if (nom == null)
                champs.Add(new Champ("nom", "Nom de la personne recherchée :", "Ex : John Doe (ou « Inconnu »)", 60));
            champs.Add(new Champ("motif", "Motif :", "Ex : braquage de la supérette", 150));
            champs.Add(new Champ("signalement", "Signalement :", "Ex : homme, 1m85, veste noire, Sultan rouge", 250));
            champs.Add(new Champ("prime", "Prime en euros (0 = aucune) :", "Ex : 5000", 9) { ParDefaut = "0" });

            Ecran.Formulaire(joueur, "Police · Avis de recherche", champs, reponses =>
                Ecran.Choix(joueur, "Police · Dangerosité", Dangerosites, dangerosite =>
                {
                    int.TryParse(reponses["prime"].Replace(" ", ""), out int prime);
                    AvisDeRecherche avis = new AvisDeRecherche
                    {
                        Id = D.ProchainId++,
                        PersonnageId = personnageId,
                        Nom = nom ?? reponses["nom"],
                        Motif = reponses["motif"],
                        Signalement = reponses["signalement"],
                        Dangerosite = dangerosite,
                        Prime = Math.Max(0, prime),
                        Agent = Joueurs.Nom(joueur),
                        Date = DateTime.Now,
                    };
                    D.Avis.Add(avis);
                    stockage.Sauvegarder();

                    foreach (Player citoyen in Joueurs.TousEnLigne())
                        citoyen.Notify(Config.Nom, $"AVIS DE RECHERCHE : {avis.Nom} — {Ecran.Couper(avis.Motif, 50)}", NotificationManager.Type.Warning);
                    retour();
                }, retour), retour);
        }

        // ------------------------------------------------------------------
        // Agents
        // ------------------------------------------------------------------

        private void Appels(Player joueur, Action retour)
        {
            Action recharger = () => Appels(joueur, retour);
            Ecran.Liste(joueur, "Police · Appels 17",
                D.Appels.OrderBy(a => a.PrisPar != null).ThenByDescending(a => a.Date).Take(30).Select(a =>
                    new Ligne($"{Ecran.Date(a.Date)}  {a.Lieu}  —  {(a.PrisPar ?? "EN ATTENTE")}", () => Ecran.Texte(joueur, $"Police · Appel n°{a.Id}",
                        $"<b>Appel de {a.Nom}</b>  ·  {a.Date:dd/MM HH:mm}\n<b>Lieu :</b> {a.Lieu}\n\n{a.Message}" + (a.PrisPar != null ? $"\n\n<b>Pris par :</b> {a.PrisPar}" : ""),
                        recharger,
                        a.PrisPar == null ? new Bouton("Je prends l'appel", () =>
                        {
                            a.PrisPar = Joueurs.Nom(joueur);
                            stockage.Sauvegarder();
                            Joueurs.Notifier(a.AppelantId, "Police · 17", $"L'agent {a.PrisPar} arrive.", NotificationManager.Type.Success);
                            PrevenirAgents($"{a.PrisPar} prend l'appel de {a.Nom} ({a.Lieu}).", NotificationManager.Type.Info);
                            recharger();
                        }) : null))),
                retour, "Aucun appel.");
        }

        private void Plaintes(Player joueur, Action retour)
        {
            Action recharger = () => Plaintes(joueur, retour);
            Ecran.Liste(joueur, "Police · Plaintes",
                D.Plaintes.OrderBy(p => p.Statut == StatutPlainte.Classee).ThenByDescending(p => p.Date).Take(50).Select(p =>
                    new Ligne($"n°{p.Id}  {p.PlaignantNom} contre {Ecran.Couper(p.Contre, 25)}  —  {p.Statut}", () => TraiterPlainte(joueur, p, recharger))),
                retour, "Aucune plainte.");
        }

        private void TraiterPlainte(Player joueur, Plainte p, Action retour)
        {
            Action relire = () => TraiterPlainte(joueur, p, retour);
            Ecran.Texte(joueur, $"Police · Plainte n°{p.Id}", TextePlainte(p), retour,
                p.Statut == StatutPlainte.Ouverte ? new Bouton("Prendre en charge", () =>
                {
                    p.Statut = StatutPlainte.EnCours;
                    p.Agent = Joueurs.Nom(joueur);
                    stockage.Sauvegarder();
                    Joueurs.Notifier(p.PlaignantId, Config.Nom, $"Ta plainte n°{p.Id} est prise en charge par {p.Agent}.", NotificationManager.Type.Success);
                    relire();
                }) : null,
                p.Statut != StatutPlainte.Classee ? new Bouton("Classer", () => Ecran.Saisie(joueur, Config.Nom, "Suite donnée (envoyée au plaignant) :", "Ex : auteur interpellé, véhicule retrouvé", reponse =>
                {
                    p.Statut = StatutPlainte.Classee;
                    p.Agent = p.Agent ?? Joueurs.Nom(joueur);
                    p.Reponse = reponse;
                    stockage.Sauvegarder();
                    Joueurs.Notifier(p.PlaignantId, Config.Nom, $"Ta plainte n°{p.Id} est classée : {reponse}");
                    relire();
                }, relire, 300)) : null);
        }

        private void DresserAmende(Player agent, int personnageId, string nom, Action retour)
        {
            Ecran.Formulaire(agent, $"Police · Amende à {nom}", new List<Champ>
            {
                new Champ("motif", "Motif :", "Ex : excès de vitesse en ville", 150),
                new Champ("montant", $"Montant en euros (max {Config.AmendeMax}) :", "Ex : 250", 9),
            }, reponses =>
            {
                if (!int.TryParse(reponses["montant"].Replace(" ", "").Replace("€", ""), out int montant) || montant <= 0 || montant > Config.AmendeMax)
                {
                    agent.Notify(Config.Nom, $"Montant invalide (1 à {Config.AmendeMax} €).", NotificationManager.Type.Error);
                    DresserAmende(agent, personnageId, nom, retour);
                    return;
                }

                Amende amende = new Amende
                {
                    Id = D.ProchainId++,
                    PersonnageId = personnageId,
                    Nom = nom,
                    Motif = reponses["motif"],
                    Montant = montant,
                    AgentId = Joueurs.Id(agent),
                    Agent = Joueurs.Nom(agent),
                    Date = DateTime.Now,
                };
                D.Amendes.Add(amende);
                Fiche(personnageId, nom).Infractions.Add(new EntreeFiche { Date = DateTime.Now, Agent = amende.Agent, Texte = $"{amende.Motif} — amende de {montant} €" });
                stockage.Sauvegarder();

                Joueurs.Notifier(personnageId, Config.Nom, $"Tu as reçu une amende de {montant} € : {amende.Motif}. Paie-la dans l'appli Police.", NotificationManager.Type.Warning);
                agent.Notify(Config.Nom, $"Amende de {montant} € dressée à {nom}.", NotificationManager.Type.Success);
                retour();
            }, retour);
        }

        private FichePolice Fiche(int personnageId, string nom)
        {
            if (!D.Fiches.TryGetValue(personnageId, out FichePolice fiche))
            {
                fiche = new FichePolice { PersonnageId = personnageId, Nom = nom };
                D.Fiches[personnageId] = fiche;
            }
            fiche.Nom = nom ?? fiche.Nom;
            return fiche;
        }

        private void Fichier(Player joueur, Action retour)
        {
            Action menu = () => Fichier(joueur, retour);
            Ecran.Liste(joueur, "Police · Fichier des personnes", new[]
            {
                new Ligne("Rechercher par nom", () => Ecran.Saisie(joueur, "Police · Fichier", "Nom ou prénom :", "Ex : Doe", nom =>
                    Ecran.Liste(joueur, $"Police · « {nom} »",
                        D.Fiches.Values.Where(f => f.Nom.IndexOf(nom, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(f => f.Nom).Take(30)
                            .Select(f => new Ligne($"{f.Nom}  —  {f.Infractions.Count} infraction(s)", () => VoirFiche(joueur, f.PersonnageId, f.Nom, menu))),
                        menu, "Aucune fiche à ce nom."), menu, 40)),
                new Ligne("Fiche d'une personne en ville", () => Ecran.ChoisirJoueur(joueur, "Police · Personnes en ville", cible => VoirFiche(joueur, cible.character.Id, Joueurs.Nom(cible), menu), menu)),
                new Ligne($"Fiches avec antécédents ({D.Fiches.Values.Count(f => f.Infractions.Count > 0)})", () => Ecran.Liste(joueur, "Police · Antécédents",
                    D.Fiches.Values.Where(f => f.Infractions.Count > 0).OrderByDescending(f => f.Infractions.Max(i => i.Date)).Take(50)
                        .Select(f => new Ligne($"{f.Nom}  —  {f.Infractions.Count} infraction(s)", () => VoirFiche(joueur, f.PersonnageId, f.Nom, menu))),
                    menu)),
            }, retour);
        }

        private void VoirFiche(Player joueur, int personnageId, string nom, Action retour)
        {
            FichePolice fiche = Fiche(personnageId, nom);
            Action relire = () => VoirFiche(joueur, personnageId, nom, retour);

            List<Amende> impayees = D.Amendes.Where(a => a.PersonnageId == personnageId && !a.Payee).ToList();
            List<AvisDeRecherche> avis = AvisActifs.Where(a => a.PersonnageId == personnageId).ToList();

            string texte = $"{Ecran.Couleur($"<b>{fiche.Nom}</b>", Bleu)}  ·  dossier n°{personnageId}";
            if (avis.Count > 0) texte += "\n" + Ecran.Couleur($"<b>RECHERCHÉ(E)</b> : {string.Join(", ", avis.Select(a => a.Motif))}", Rouge);
            texte += $"\n<b>Amendes impayées :</b> {impayees.Count} ({impayees.Sum(a => a.Montant)} €)";
            texte += "\n\n<b>Casier :</b>\n" + (fiche.Infractions.Count == 0 ? "<i>vierge</i>" :
                string.Join("\n", fiche.Infractions.OrderByDescending(i => i.Date).Take(10).Select(i => $"• {i.Date:dd/MM} — {i.Texte} <size=80%>({i.Agent})</size>")));
            texte += "\n\n<b>Notes :</b>\n" + (fiche.Notes.Count == 0 ? "<i>aucune</i>" :
                string.Join("\n", fiche.Notes.OrderByDescending(n => n.Date).Take(5).Select(n => $"• {n.Date:dd/MM} — {n.Texte} <size=80%>({n.Agent})</size>")));

            Ecran.Texte(joueur, "Police · Fiche", texte, retour,
                new Bouton("Ajouter une infraction", () => Ecran.Saisie(joueur, "Police · Infraction", "Infraction et sanction :", "Ex : conduite sans permis — véhicule saisi", t =>
                {
                    fiche.Infractions.Add(new EntreeFiche { Date = DateTime.Now, Agent = Joueurs.Nom(joueur), Texte = t });
                    stockage.Sauvegarder();
                    relire();
                }, relire, 200)),
                new Bouton("Ajouter une note", () => Ecran.Saisie(joueur, "Police · Note", "Note interne (non visible par la personne) :", "Ex : fréquente le gang du port", t =>
                {
                    fiche.Notes.Add(new EntreeFiche { Date = DateTime.Now, Agent = Joueurs.Nom(joueur), Texte = t });
                    stockage.Sauvegarder();
                    relire();
                }, relire, 200)),
                new Bouton("Amende", () => DresserAmende(joueur, personnageId, fiche.Nom, relire)),
                new Bouton("Avis de recherche", () => EmettreAvis(joueur, relire, personnageId, fiche.Nom)));
        }
    }
}
