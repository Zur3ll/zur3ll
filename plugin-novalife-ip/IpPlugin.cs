using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Life;
using Life.Network;
using UnityEngine;

namespace IpPlugin
{
    /// <summary>
    /// Commande /ip : affiche le nom, l'IP, l'ID et la distance des joueurs
    /// présents dans un rayon de 50 mètres autour de celui qui tape la commande.
    /// Réservée aux administrateurs (les IP sont des données personnelles).
    /// </summary>
    public class IpPlugin : Plugin
    {
        private const float Rayon = 50f;

        public IpPlugin(IGameAPI api) : base(api)
        {
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            new SChatCommand(
                "/ip",
                "Affiche les joueurs proches (nom, IP, ID, distance)",
                "/ip",
                (player, args) => CommandeIp(player)
            ).Register();

            Debug.Log("[IpPlugin] Plugin chargé : commande /ip disponible.");
        }

        private static void CommandeIp(Player player)
        {
            if (!player.IsAdmin)
            {
                player.SendText("<color=#e74c3c>Tu n'as pas la permission d'utiliser cette commande.</color>");
                return;
            }

            Vector3 maPosition = player.setup.transform.position;

            var proches = Nova.server.Players
                .Where(p => p != null && p != player && p.setup != null)
                .Select(p => new
                {
                    Joueur = p,
                    Distance = Vector3.Distance(maPosition, p.setup.transform.position)
                })
                .Where(x => x.Distance <= Rayon)
                .OrderBy(x => x.Distance)
                .ToList();

            if (proches.Count == 0)
            {
                player.SendText($"<color=#f1c40f>[IP]</color> Aucun joueur dans un rayon de {Rayon:0} m.");
                return;
            }

            var message = new StringBuilder();
            message.AppendLine($"<color=#f1c40f>[IP]</color> {proches.Count} joueur(s) dans un rayon de {Rayon:0} m :");

            foreach (var x in proches)
            {
                Player p = x.Joueur;
                message.AppendLine(
                    $"<color=#3498db>{NomDuJoueur(p)}</color>" +
                    $" | IP : <color=#2ecc71>{IpDuJoueur(p)}</color>" +
                    $" | ID : <color=#9b59b6>({IdDuJoueur(p)})</color>" +
                    $" | Distance : <color=#e67e22>{x.Distance:0.0} m</color>");
            }

            player.SendText(message.ToString().TrimEnd());
        }

        private static string NomDuJoueur(Player p)
        {
            try
            {
                return p.FullName;
            }
            catch (Exception)
            {
                return p.account?.username ?? "Inconnu";
            }
        }

        // Même format que l'ID affiché au-dessus de la tête : (compte:personnage), ex. (1:3)
        private static string IdDuJoueur(Player p)
        {
            try
            {
                return $"{p.account.id}:{p.character.Id}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static string IpDuJoueur(Player p)
        {
            try
            {
                // Selon la version de Mirror, "address" n'existe que sur le type réel
                // de la connexion (NetworkConnectionToClient) : on le lit par réflexion.
                object conn = p.conn;
                if (conn == null)
                    return "inconnue";

                Type type = conn.GetType();
                object valeur =
                    type.GetProperty("address", BindingFlags.Public | BindingFlags.Instance)?.GetValue(conn) ??
                    type.GetField("address", BindingFlags.Public | BindingFlags.Instance)?.GetValue(conn);

                string ip = valeur as string;
                return string.IsNullOrEmpty(ip) ? "inconnue" : ip;
            }
            catch (Exception)
            {
                return "inconnue";
            }
        }
    }
}
