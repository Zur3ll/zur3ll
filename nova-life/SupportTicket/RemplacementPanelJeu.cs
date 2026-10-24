using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Life;
using Life.CharacterSystem;
using Life.Network;
using Mirror;
using UnityEngine;

namespace SupportTicket
{
    // Quand le joueur appuie sur N, son jeu envoie la commande réseau CmdInputTicketPanel au serveur,
    // qui répond en ouvrant le panel « Nouveau ticket » du jeu. On remplace la réponse du serveur
    // à ces commandes dans la table de Mirror pour ouvrir notre menu à la place.
    public static class RemplacementPanelJeu
    {
        private static readonly string[] Commandes =
        {
            "System.Void Life.CharacterSystem.CharacterSetup::CmdInputTicketPanel()",
            "System.Void Life.CharacterSystem.CharacterSetup::CmdTicketPanel()",
        };

        public static bool Installer(Action<Player> ouvrirMenu)
        {
            try
            {
                // Les commandes sont enregistrées par le constructeur statique de CharacterSetup :
                // on le force à s'exécuter maintenant pour ne pas être écrasé plus tard.
                RuntimeHelpers.RunClassConstructor(typeof(CharacterSetup).TypeHandle);

                FieldInfo champTable = typeof(RemoteProcedureCalls).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(f => typeof(IDictionary).IsAssignableFrom(f.FieldType));
                IDictionary table = champTable?.GetValue(null) as IDictionary;
                if (table == null)
                {
                    Debug.LogError("[SupportTicket] Table des commandes Mirror introuvable, panel du jeu conservé.");
                    return false;
                }

                RemoteCallDelegate remplacement = (composant, lecteur, connexion) =>
                {
                    Player player = Nova.server.Players.FirstOrDefault(p => p.conn == connexion);
                    if (player != null)
                        ouvrirMenu(player);
                };

                int remplacees = 0;
                foreach (string commande in Commandes)
                {
                    int hash = commande.GetStableHashCode();
                    foreach (DictionaryEntry entree in table)
                    {
                        long cle = Convert.ToInt64(entree.Key);
                        if (cle != hash && cle != (ushort)hash)
                            continue;

                        FieldInfo champFonction = entree.Value.GetType()
                            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .FirstOrDefault(f => f.FieldType == typeof(RemoteCallDelegate));
                        if (champFonction == null)
                            break;

                        champFonction.SetValue(entree.Value, remplacement);
                        remplacees++;
                        break;
                    }
                }

                if (remplacees == 0)
                {
                    Debug.LogError("[SupportTicket] Commandes de ticket du jeu introuvables, panel du jeu conservé.");
                    return false;
                }

                Debug.Log($"[SupportTicket] Panel de ticket du jeu remplacé ({remplacees}/{Commandes.Length} commandes).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupportTicket] Impossible de remplacer le panel du jeu : {ex}");
                return false;
            }
        }
    }
}
