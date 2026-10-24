using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SupportTicket
{
    public class TicketCategory
    {
        public string Titre;
        public string Description;
        // Numéro de l'icône du jeu (utiliser /ticketicones en jeu pour trouver le bon numéro)
        public int IconeId;
    }

    public class SupportConfig
    {
        public string NomServeur = "Nova";
        public string SousTitre = "Ouvrir un ticket";
        public string CouleurTitre = "#9db4ff";

        // Touches : noms Unity (https://docs.unity3d.com/ScriptReference/KeyCode.html)
        public string ToucheJoueur = "N";
        // Touche "," (Comma) pour ouvrir la liste des tickets côté staff
        public string ToucheStaff = "Comma";

        // Le staff doit être en service admin pour voir et traiter les tickets
        public bool ServiceAdminRequis = true;

        public List<TicketCategory> Categories = new List<TicketCategory>
        {
            new TicketCategory { Titre = "Signalement d'un joueur", Description = "Non-respect du règlement, freekill, insultes", IconeId = 381 },
            new TicketCategory { Titre = "Personnage bloqué", Description = "Coincé dans le décor, sous la carte", IconeId = 748 },
            new TicketCategory { Titre = "Bug ou problème technique", Description = "Objet, interface ou action qui ne fonctionne pas", IconeId = 309 },
            new TicketCategory { Titre = "Remboursement", Description = "Perte d'argent, d'objets ou de véhicule après un bug", IconeId = 327 },
            new TicketCategory { Titre = "Véhicule", Description = "Véhicule disparu, bloqué ou introuvable", IconeId = 398 },
        };

        public static SupportConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                SupportConfig defaults = new SupportConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented));
                return defaults;
            }

            return JsonConvert.DeserializeObject<SupportConfig>(File.ReadAllText(path));
        }
    }
}
