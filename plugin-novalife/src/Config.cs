using System.Collections.Generic;

namespace NovaApps
{
    /// <summary>
    /// Contenu de Plugins/NovaApps/config.json, créé au premier lancement.
    /// Les champs ajoutés dans une nouvelle version sont ajoutés au fichier automatiquement.
    /// </summary>
    public class Config
    {
        // Touches pour ouvrir le téléphone / l'ordinateur (noms Unity : F7, F8, P, ...). Vide = désactivé.
        // Liste : https://docs.unity3d.com/ScriptReference/KeyCode.html
        // Mets les mêmes touches que le téléphone (F9) et l'ordinateur (F2) du jeu pour que les applis s'ouvrent avec eux.
        public string ToucheTelephone = "F9";
        public string ToucheOrdinateur = "F2";

        /// <summary>
        /// Écran de verrouillage (heure, notifications) avant l'accueil des applis.
        /// À désactiver si les applis s'ouvrent avec le téléphone du jeu, qui affiche déjà l'heure.
        /// </summary>
        public bool EcranDeVerrouillage = false;

        /// <summary>
        /// ID des objets "téléphone" du jeu : il faut en porter un pour utiliser les applis.
        /// Vide = pas besoin d'objet. Pour trouver l'ID, garde ton téléphone sur toi et tape /tel_objets (admin).
        /// </summary>
        public List<int> ObjetsTelephone = new List<int>();

        /// <summary>En prison, le téléphone est confisqué.</summary>
        public bool TelephoneConfisqueEnPrison = true;

        /// <summary>Un joueur à terre (0 % de vie) ne peut utiliser ni téléphone ni ordinateur.</summary>
        public bool BloquerSiAterre = true;

        /// <summary>
        /// Emplacements des ordinateurs. Si la liste est vide, l'ordinateur s'ouvre partout.
        /// Ajoute-les en jeu avec /ordi_ajouter (admin) en te plaçant devant un PC.
        /// /ordi_ajouter 5 réserve le PC aux membres de l'entreprise 5 (ex. le commissariat).
        /// </summary>
        public List<Position> Ordinateurs = new List<Position>();
        public float DistanceOrdinateur = 2.5f;

        public DiscordConfig Discord = new DiscordConfig();
        public NConfig N = new NConfig();
        public PoleTravailConfig PoleTravail = new PoleTravailConfig();
        public CapitalMotorsConfig CapitalMotors = new CapitalMotorsConfig();
        public BlueLightfoxConfig BlueLightfox = new BlueLightfoxConfig();
        public LeBonNeoConfig LeBonNeo = new LeBonNeoConfig();
        public PoliceConfig Police = new PoliceConfig();
        public JournalConfig Journal = new JournalConfig();
        public MeteoConfig Meteo = new MeteoConfig();
        public AgendaConfig Agenda = new AgendaConfig();
    }

    public class Position
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>Entreprises autorisées à utiliser ce PC. Vide = PC public.</summary>
        public List<int> Entreprises = new List<int>();
    }

    public class DiscordConfig
    {
        /// <summary>Token du bot Discord (le même que DISCORD_TOKEN du bot Garage / Concession).</summary>
        public string TokenBot = "";

        /// <summary>Lien d'invitation affiché dans l'appli Discord de l'ordinateur.</summary>
        public string Invitation = "https://discord.gg/ton-invitation";

        /// <summary>Salons visibles depuis l'ordinateur.</summary>
        public List<SalonDiscord> Salons = new List<SalonDiscord>
        {
            new SalonDiscord { Nom = "général", Id = "", Webhook = "", LectureSeule = false },
            new SalonDiscord { Nom = "annonces", Id = "", Webhook = "", LectureSeule = true },
        };

        public int MessagesAffiches = 15;
    }

    public class SalonDiscord
    {
        public string Nom;
        public string Id;
        /// <summary>Facultatif : si rempli, les messages envoyés depuis le jeu apparaissent sous le nom RP du joueur.</summary>
        public string Webhook;
        public bool LectureSeule;
    }

    public class NConfig
    {
        /// <summary>Facultatif : webhook d'un salon Discord où recopier chaque post N.</summary>
        public string WebhookDiscord = "";
        public int LongueurMax = 280;
        public int PostsAffiches = 30;
    }

    public class PoleTravailConfig
    {
        /// <summary>Si false, publier une offre ou déposer un CV n'est possible que depuis l'ordinateur.</summary>
        public bool PublicationDepuisTelephone = false;

        /// <summary>Facultatif : webhook d'un salon Discord où annoncer les nouvelles offres.</summary>
        public string WebhookDiscord = "";
    }

    public class CapitalMotorsConfig
    {
        public string Nom = "Capital Motors";

        /// <summary>ID des entreprises (en jeu) dont les membres gèrent les commandes. Les admins y ont toujours accès.</summary>
        public List<int> EntreprisesConcession = new List<int>();

        /// <summary>Quand une commande est livrée, crée directement le véhicule dans le garage du client.</summary>
        public bool CreerVehiculeALaLivraison = true;

        /// <summary>Salon Discord où arrivent les commandes (ex. SALON_RECEPTION_ID du bot). Nécessite Discord.TokenBot.</summary>
        public string SalonDiscordCommandes = "";

        /// <summary>Véhicules en vente. Rempli automatiquement au premier lancement, modifiable en jeu par la concession.</summary>
        public List<VehiculeEnVente> Catalogue = new List<VehiculeEnVente>();
    }

    public class VehiculeEnVente
    {
        public int ModelId;
        public string Nom;
        /// <summary>0 = prix sur devis.</summary>
        public int Prix;
    }

    public class BlueLightfoxConfig
    {
        /// <summary>
        /// Pour de vrais résultats Google : clé API "Custom Search JSON API" + ID du moteur (cx).
        /// Laisse vide pour utiliser Wikipédia (gratuit, sans clé).
        /// </summary>
        public string GoogleCleApi = "";
        public string GoogleMoteurId = "";

        /// <summary>Sites "internes" au serveur, proposés en premier quand la recherche contient un mot-clé.</summary>
        public List<SiteInterne> Sites = new List<SiteInterne>
        {
            new SiteInterne
            {
                Nom = "Mairie d'Amboise",
                Adresse = "mairie-amboise.nl",
                MotsCles = new List<string> { "mairie", "amboise", "papiers" },
                Contenu = "Bienvenue sur le site de la mairie.\nHoraires : tous les soirs de 20h à 23h.\nPour toute démarche, présentez-vous à l'accueil.",
            },
        };
    }

    public class SiteInterne
    {
        public string Nom;
        public string Adresse;
        public List<string> MotsCles = new List<string>();
        public string Contenu;
    }
}

    public class LeBonNeoConfig
    {
        /// <summary>Une annonce disparaît des résultats après ce nombre de jours (le vendeur peut la remonter).</summary>
        public int DureeJours = 14;

        /// <summary>Facultatif : webhook d'un salon Discord où annoncer les nouvelles annonces.</summary>
        public string WebhookDiscord = "";

        public List<string> Categories = new List<string> { "Véhicules", "Immobilier", "Objets", "Services", "Autre" };
    }

    public class PoliceConfig
    {
        public string Nom = "Police Nationale";

        /// <summary>ID des entreprises (en jeu) dont les membres sont des agents. Les admins ont toujours accès.</summary>
        public List<int> EntreprisesPolice = new List<int>();

        /// <summary>Montant maximum d'une amende.</summary>
        public int AmendeMax = 10000;

        /// <summary>Si true, le fichier des personnes (casier, notes) ne se consulte que depuis un ordinateur.</summary>
        public bool FichierSurOrdinateurUniquement = true;
    }

    public class JournalConfig
    {
        public string Nom = "Le Nova Matin";

        /// <summary>ID des entreprises (en jeu) dont les membres sont journalistes. Les admins ont toujours accès.</summary>
        public List<int> EntreprisesJournal = new List<int>();

        /// <summary>Si true, l'écriture des articles se fait uniquement depuis un ordinateur.</summary>
        public bool RedactionSurOrdinateurUniquement = true;

        /// <summary>Facultatif : webhook d'un salon Discord où publier les articles.</summary>
        public string WebhookDiscord = "";

        public List<string> Rubriques = new List<string> { "Actualité", "Faits divers", "Économie", "Politique", "Sport", "Culture", "Communiqué officiel" };
    }

    public class MeteoConfig
    {
        public string Ville = "Amboise";

        /// <summary>
        /// true : le plugin change lui-même la météo du serveur selon ses prévisions (prévisions toujours justes).
        /// false : les prévisions sont seulement affichées dans l'appli, sans toucher à la météo du jeu.
        /// </summary>
        public bool PiloterLaMeteo = true;

        /// <summary>Durée d'un créneau de prévision, en minutes réelles.</summary>
        public int DureeCreneauMinutes = 60;

        /// <summary>Chances de chaque temps (noms de la météo du jeu). Mets 0 pour ne jamais l'avoir.</summary>
        public Dictionary<string, int> Probabilites = new Dictionary<string, int>
        {
            ["ClearSky"] = 40,
            ["Cloudy1"] = 25,
            ["Foggy"] = 8,
            ["LightRain"] = 15,
            ["HeavyRain"] = 8,
            ["Storm"] = 4,
        };

        /// <summary>ID des entreprises (ex. le journal) qui peuvent modifier les prévisions. Les admins ont toujours accès.</summary>
        public List<int> EntreprisesMeteo = new List<int>();
    }

    public class AgendaConfig
    {
        /// <summary>Si true, seuls les membres d'une entreprise (et les admins) peuvent créer un événement.</summary>
        public bool CreationReserveeAuxEntreprises = false;

        /// <summary>Les participants reçoivent un rappel ce nombre de minutes avant le début.</summary>
        public int RappelMinutes = 15;
    }
