using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Life;
using Life.Network;
using Life.UI;
using Mirror;
using UnityEngine;

namespace SupportTicket
{
    public class Ticket
    {
        public Player Auteur;
        public TicketCategory Categorie;
        public string Message;
        public DateTime OuvertLe;
        public string PrisEnChargePar;
    }

    public class SupportTicketPlugin : Plugin
    {
        private const string Vert = "#7bc96f";
        private const string Rouge = "#e5533d";
        private const string Gris = "#c8c8c8";
        private const int IconesParPage = 40;

        private readonly List<Ticket> tickets = new List<Ticket>();
        private SupportConfig config;
        private KeyCode toucheJoueur;
        private KeyCode toucheStaff;

        public SupportTicketPlugin(IGameAPI api) : base(api)
        {
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            string dossier = $"{pluginsPath}/SupportTicket";
            if (!Directory.Exists(dossier))
                Directory.CreateDirectory(dossier);

            config = SupportConfig.Load($"{dossier}/config.json");

            if (!Enum.TryParse(config.ToucheJoueur, true, out toucheJoueur))
                toucheJoueur = KeyCode.N;
            if (!Enum.TryParse(config.ToucheStaff, true, out toucheStaff))
                toucheStaff = KeyCode.None;

            new SChatCommand("/ticket", "Ouvrir un ticket support", "/ticket", (player, args) => OuvrirMenuJoueur(player)).Register();
            new SChatCommand("/tickets", "Liste des tickets (staff)", "/tickets", (player, args) => OuvrirListeStaff(player)).Register();
            new SChatCommand("/ticketicones", "Aperçu des icônes (staff)", "/ticketicones [page]", (player, args) =>
            {
                int page = 1;
                if (args.Length > 0) int.TryParse(args[0], out page);
                OuvrirApercuIcones(player, Math.Max(1, page));
            }).Register();

            Debug.Log("[SupportTicket] Plugin initialisé");
        }

        public override void OnPlayerInput(Player player, KeyCode keyCode, bool onUI)
        {
            base.OnPlayerInput(player, keyCode, onUI);

            if (onUI)
                return;

            if (keyCode == toucheJoueur)
                OuvrirMenuJoueur(player);
            else if (toucheStaff != KeyCode.None && keyCode == toucheStaff)
                OuvrirListeStaff(player);
        }

        public override void OnPlayerDisconnect(NetworkConnection conn)
        {
            base.OnPlayerDisconnect(conn);
            tickets.RemoveAll(t => t.Auteur == null || t.Auteur.conn == conn);
        }

        private string Titre(string sousTitre)
        {
            string titre = $"<color={config.CouleurTitre}>Support {config.NomServeur}</color>";
            return string.IsNullOrEmpty(sousTitre) ? titre : $"{titre}\n<size=60%><color=#ffffff>{sousTitre}</color></size>";
        }

        private static string Bouton(string texte, string couleur) => $"<color={couleur}><b>{texte}</b></color>";

        private Ticket TicketDe(Player player) => tickets.FirstOrDefault(t => t.Auteur == player);

        // ---------- Côté joueur ----------

        private void OuvrirMenuJoueur(Player player)
        {
            Ticket existant = TicketDe(player);
            if (existant != null)
            {
                OuvrirTicketEnCours(player, existant);
                return;
            }

            UIPanel panel = new UIPanel(Titre(config.SousTitre), UIPanel.PanelType.TabPrice)
                .AddButton(Bouton("CHOISIR", Vert), ui => ui.SelectTab())
                .AddButton(Bouton("FERMER", Rouge), ui => player.ClosePanel(ui));

            foreach (TicketCategory categorie in config.Categories)
            {
                TicketCategory choix = categorie;
                panel.AddTabLine($"<b>{choix.Titre}</b>", choix.Description, choix.IconeId, ui => OuvrirSaisie(player, choix));
            }

            player.ShowPanelUI(panel);
        }

        private void OuvrirSaisie(Player player, TicketCategory categorie)
        {
            UIPanel panel = new UIPanel(Titre(categorie.Titre), UIPanel.PanelType.Input)
                .SetInputPlaceholder("Décrivez votre problème en quelques mots...")
                .AddButton(Bouton("ENVOYER", Vert), ui =>
                {
                    string message = ui.inputText?.Trim();
                    if (string.IsNullOrEmpty(message))
                    {
                        player.SendText($"<color={Rouge}>Vous ne pouvez pas envoyer un ticket vide.</color>");
                        return;
                    }

                    player.ClosePanel(ui);
                    CreerTicket(player, categorie, message);
                })
                .AddButton(Bouton("RETOUR", Rouge), ui => OuvrirMenuJoueur(player));

            player.ShowPanelUI(panel);
        }

        private void OuvrirTicketEnCours(Player player, Ticket ticket)
        {
            string statut = ticket.PrisEnChargePar != null
                ? $"<color={Vert}>Pris en charge par {ticket.PrisEnChargePar}</color>"
                : $"<color={Gris}>En attente d'un membre du staff</color>";

            UIPanel panel = new UIPanel(Titre("Votre ticket en cours"), UIPanel.PanelType.Text)
                .SetText($"<b>{ticket.Categorie.Titre}</b>\n{ticket.Message}\n\n{statut}")
                .AddButton(Bouton("CLÔTURER", Rouge), ui =>
                {
                    tickets.Remove(ticket);
                    player.ClosePanel(ui);
                    player.SendText($"<color={Vert}>Votre ticket a été clôturé.</color>");
                })
                .AddButton(Bouton("FERMER", Gris), ui => player.ClosePanel(ui));

            player.ShowPanelUI(panel);
        }

        private void CreerTicket(Player player, TicketCategory categorie, string message)
        {
            tickets.Add(new Ticket
            {
                Auteur = player,
                Categorie = categorie,
                Message = message,
                OuvertLe = DateTime.Now,
            });

            player.SendText($"<color={Vert}>Votre ticket a bien été envoyé à l'équipe de modération.</color>");

            foreach (Player staff in Nova.server.Players.Where(EstStaffDisponible).ToList())
            {
                staff.SendText($"<color={config.CouleurTitre}>[Support]</color> Nouveau ticket de <b>{NomRP(player)}</b> — {categorie.Titre} : {message}\n/tickets pour voir la liste");
                staff.setup.TargetPlayClairon(0.2f);
            }
        }

        // ---------- Côté staff ----------

        private bool EstStaffDisponible(Player player) =>
            player != null && player.IsAdmin && (!config.ServiceAdminRequis || player.IsAdminService);

        private static string NomRP(Player player) => $"{player.character.Firstname} {player.character.Lastname}";

        private static string Anciennete(DateTime date)
        {
            int minutes = (int)(DateTime.Now - date).TotalMinutes;
            return minutes < 1 ? "à l'instant" : $"il y a {minutes} min";
        }

        private void OuvrirListeStaff(Player player)
        {
            if (!player.IsAdmin)
                return;

            if (!EstStaffDisponible(player))
            {
                player.SendText($"<color={Rouge}>Vous devez être en service admin pour traiter les tickets.</color>");
                return;
            }

            tickets.RemoveAll(t => t.Auteur == null || t.Auteur.conn == null || t.Auteur.setup == null);
            List<Ticket> liste = tickets.ToList();

            if (liste.Count == 0)
            {
                UIPanel vide = new UIPanel(Titre("Tickets en attente"), UIPanel.PanelType.Text)
                    .SetText("Aucun ticket ouvert pour le moment.")
                    .AddButton(Bouton("FERMER", Rouge), ui => player.ClosePanel(ui));
                player.ShowPanelUI(vide);
                return;
            }

            UIPanel panel = new UIPanel(Titre($"{liste.Count} ticket(s) en attente"), UIPanel.PanelType.TabPrice)
                .AddButton(Bouton("Y ALLER", Vert), ui => ui.SelectTab())
                .AddButton(Bouton("RAMENER", Vert), ui => AgirSurSelection(player, ui, liste, Ramener))
                .AddButton(Bouton("CLÔTURER", Rouge), ui => AgirSurSelection(player, ui, liste, Cloturer))
                .AddButton(Bouton("FERMER", Gris), ui => player.ClosePanel(ui));

            foreach (Ticket ticket in liste)
            {
                Ticket t = ticket;
                string extrait = t.Message.Length > 45 ? t.Message.Substring(0, 45) + "..." : t.Message;
                string charge = t.PrisEnChargePar != null ? $" · <color={Vert}>{t.PrisEnChargePar}</color>" : "";

                panel.AddTabLine(
                    $"<b>{NomRP(t.Auteur)}</b> — {t.Categorie.Titre}",
                    $"{extrait}\n<color={Gris}>{Anciennete(t.OuvertLe)}</color>{charge}",
                    t.Categorie.IconeId,
                    ui =>
                    {
                        player.ClosePanel(ui);
                        PrendreEnCharge(player, t);
                        player.setup.TargetSetPosition(t.Auteur.setup.transform.position);
                    });
            }

            player.ShowPanelUI(panel);
        }

        private void AgirSurSelection(Player staff, UIPanel ui, List<Ticket> liste, Action<Player, Ticket> action)
        {
            if (ui.selectedTab < 0 || ui.selectedTab >= liste.Count)
                return;

            Ticket ticket = liste[ui.selectedTab];
            staff.ClosePanel(ui);

            if (!tickets.Contains(ticket) || ticket.Auteur?.setup == null)
            {
                staff.SendText($"<color={Rouge}>Ce ticket n'existe plus.</color>");
                return;
            }

            action(staff, ticket);
        }

        private void PrendreEnCharge(Player staff, Ticket ticket)
        {
            if (ticket.PrisEnChargePar != null)
                return;

            ticket.PrisEnChargePar = staff.account.username;
            ticket.Auteur.SendText($"<color={Vert}>{ticket.PrisEnChargePar} prend en charge votre ticket.</color>");
        }

        private void Ramener(Player staff, Ticket ticket)
        {
            PrendreEnCharge(staff, ticket);
            ticket.Auteur.setup.TargetSetPosition(staff.setup.transform.position);
        }

        private void Cloturer(Player staff, Ticket ticket)
        {
            tickets.Remove(ticket);
            ticket.Auteur.SendText($"<color={Rouge}>Votre ticket a été clôturé par {staff.account.username}.</color>");
            staff.SendText($"<color={Vert}>Ticket de {NomRP(ticket.Auteur)} clôturé.</color>");
        }

        // Permet au staff de repérer le numéro d'une icône pour le config.json
        private void OuvrirApercuIcones(Player player, int page)
        {
            if (!player.IsAdmin)
                return;

            int debut = (page - 1) * IconesParPage;
            int fin = debut + IconesParPage;

            UIPanel panel = new UIPanel($"Icônes {debut} à {fin - 1} (page {page})", UIPanel.PanelType.TabPrice)
                .AddButton(Bouton("SUIVANT", Vert), ui => OuvrirApercuIcones(player, page + 1))
                .AddButton(Bouton("PRÉCÉDENT", Gris), ui => OuvrirApercuIcones(player, Math.Max(1, page - 1)))
                .AddButton(Bouton("FERMER", Rouge), ui => player.ClosePanel(ui));

            for (int i = debut; i < fin; i++)
                panel.AddTabLine($"Icône n°{i}", "", i, ui => { });

            player.ShowPanelUI(panel);
        }
    }
}
