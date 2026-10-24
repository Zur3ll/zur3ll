using System;
using System.Collections.Generic;
using System.Linq;
using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Newtonsoft.Json.Linq;
using NovaApps.Core;
using UnityEngine;

namespace NovaApps.Apps
{
    /// <summary>
    /// Discord sur l'ordinateur : lit les vrais salons du serveur Discord et permet d'y écrire depuis le jeu.
    /// Utilise le token du bot (lecture) et, si configuré, un webhook par salon (écriture sous le nom RP).
    /// </summary>
    public class DiscordApp
    {
        private const string Violet = "#5865f2";

        private DiscordConfig Config => NovaAppsPlugin.Config.Discord;

        public void Ouvrir(Player joueur, Action retour)
        {
            Action menu = () => Ouvrir(joueur, retour);

            if (!NovaAppsPlugin.Discord.BotConfigure)
            {
                Ecran.Texte(joueur, "Discord", "Discord n'est pas encore relié au serveur.\nUn administrateur doit renseigner le token du bot dans config.json.", retour,
                    new Bouton("Rejoindre le Discord", () => Invitation(joueur, menu)));
                return;
            }

            List<Ligne> lignes = Config.Salons
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .Select(s => new Ligne("# " + s.Nom + (s.LectureSeule ? "  (lecture seule)" : ""), () => Salon(joueur, s, menu)))
                .ToList();
            lignes.Add(new Ligne("Rejoindre le Discord (lien d'invitation)", () => Invitation(joueur, menu)));

            Ecran.Liste(joueur, "Discord", lignes, retour);
        }

        private void Invitation(Player joueur, Action retour)
        {
            joueur.SendText($"<color={Violet}>[Discord]</color> Rejoins-nous : {Config.Invitation}");
            Ecran.Texte(joueur, "Discord", $"{Ecran.Couleur("<b>Rejoins notre Discord !</b>", Violet)}\n\n{Config.Invitation}\n\n<i>Le lien a aussi été envoyé dans ton chat.</i>", retour);
        }

        private async void Salon(Player joueur, SalonDiscord salon, Action retour)
        {
            Action relire = () => Salon(joueur, salon, retour);
            string texte;

            try
            {
                List<MessageDiscord> messages = await NovaAppsPlugin.Discord.LireSalon(salon.Id, Mathf.Clamp(Config.MessagesAffiches, 1, 50));
                texte = messages.Count == 0
                    ? "<i>Aucun message dans ce salon.</i>"
                    : string.Join("\n", messages.Select(m =>
                        $"{Ecran.Couleur("<b>" + Ecran.Nettoyer(m.Auteur) + "</b>", Violet)} <size=80%>{Ecran.Date(m.Date.ToLocalTime())}</size>\n{Ecran.Couper(Ecran.Nettoyer(m.Contenu), 300)}"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NovaApps] Lecture du salon {salon.Nom} impossible : {e.Message}");
                texte = "Impossible de charger ce salon.\n<i>Vérifie que le bot a accès au salon et que l'intent « Message Content » est activé.</i>";
            }

            // Le joueur a pu se déconnecter pendant le chargement.
            if (joueur?.character == null) return;

            Ecran.Texte(joueur, "Discord · #" + salon.Nom, texte, retour,
                new Bouton("Actualiser", relire),
                salon.LectureSeule ? null : new Bouton("Écrire", () => Ecrire(joueur, salon, relire)));
        }

        private void Ecrire(Player joueur, SalonDiscord salon, Action retour)
        {
            Ecran.Saisie(joueur, "Discord · #" + salon.Nom, "Ton message (envoyé sur le vrai Discord, sous ton nom RP) :", "Écris ton message...", async texte =>
            {
                string nom = Joueurs.Nom(joueur);
                bool envoye = Discord.WebhookValide(salon.Webhook)
                    ? await NovaAppsPlugin.Discord.PosterAvecWebhook(salon.Webhook, $"{nom} (en jeu)", texte)
                    : await NovaAppsPlugin.Discord.PosterAvecBot(salon.Id, new JObject
                    {
                        ["content"] = $"**{nom}** (en jeu) : {texte}",
                        ["allowed_mentions"] = new JObject { ["parse"] = new JArray() },
                    });

                if (joueur?.character == null) return;

                joueur.Notify("Discord", envoye ? "Message envoyé !" : "L'envoi a échoué.", envoye ? NotificationManager.Type.Success : NotificationManager.Type.Error);
                retour();
            }, retour, 1500);
        }
    }
}
