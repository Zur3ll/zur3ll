using System.Collections.Generic;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;

namespace NovaApps.Core
{
    public static class Joueurs
    {
        public static int Id(Player joueur) => joueur.character.Id;

        public static string Nom(Player joueur) => Ecran.Nettoyer(joueur.GetFullName());

        /// <summary>Joueur connecté qui joue ce personnage, ou null.</summary>
        public static Player EnLigne(int personnageId)
        {
            return Nova.server.Players.FirstOrDefault(p => p?.character != null && p.character.Id == personnageId);
        }

        /// <summary>Envoie une notification au personnage s'il est connecté.</summary>
        public static void Notifier(int personnageId, string titre, string message, NotificationManager.Type type = NotificationManager.Type.Info)
        {
            Player cible = EnLigne(personnageId);
            cible?.Notify(titre, message, type);
        }

        public static IEnumerable<Player> TousEnLigne()
        {
            return Nova.server.Players.Where(p => p?.character != null);
        }
    }
}
