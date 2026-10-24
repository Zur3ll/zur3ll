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
        public string ToucheTelephone = "F7";
        public string ToucheOrdinateur = "F8";

        /// <summary>
        /// Emplacements des ordinateurs. Si la liste est vide, l'ordinateur s'ouvre partout.
        /// Ajoute-les en jeu avec /ordi_ajouter (admin) en te plaçant devant un PC.
        /// </summary>
        public List<Position> Ordinateurs = new List<Position>();
        public float DistanceOrdinateur = 2.5f;

        public DiscordConfig Discord = new DiscordConfig();
        public NConfig N = new NConfig();
        public PoleTravailConfig PoleTravail = new PoleTravailConfig();
        public CapitalMotorsConfig CapitalMotors = new CapitalMotorsConfig();
        public BlueLightfoxConfig BlueLightfox = new BlueLightfoxConfig();
    }

    public class Position
    {
        public float X;
        public float Y;
        public float Z;
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
