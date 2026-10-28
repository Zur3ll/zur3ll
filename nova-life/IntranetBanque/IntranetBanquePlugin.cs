using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Life;
using Life.BizSystem;
using Life.DB;
using Life.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IntranetBanque
{
    // Relie la tablette (intranet Neoran) à la vraie banque du jeu.
    // Le site garde une file de virements ; ce plugin la récupère, fait les virements en jeu,
    // puis renvoie le résultat et les vrais soldes des entreprises et des personnages reliés.
    public class IntranetBanquePlugin : Plugin
    {
        private const string Vert = "#5be29b";
        private const string Rouge = "#ff8a8d";

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private BanqueConfig config;
        private bool actif;

        public IntranetBanquePlugin(IGameAPI api) : base(api)
        {
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            string dossier = $"{pluginsPath}/IntranetBanque";
            if (!Directory.Exists(dossier))
                Directory.CreateDirectory(dossier);
            config = BanqueConfig.Load($"{dossier}/config.json");

            new SChatCommand("/intranet", "Relier ton personnage à ton compte de la tablette", "/intranet CODE", (player, args) => Lier(player, args)).Register();

            if (string.IsNullOrWhiteSpace(config.CleSecrete) || config.UrlIntranet.Contains("ton-intranet"))
            {
                Debug.LogError("[IntranetBanque] Renseigne UrlIntranet et CleSecrete dans Plugins/IntranetBanque/config.json, puis redémarre.");
                return;
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            actif = true;
            Boucle();
            Debug.Log("[IntranetBanque] Plugin initialisé");
        }

        // Lancée depuis le thread principal : les suites des await y reviennent, on peut donc toucher au jeu sans risque
        private async void Boucle()
        {
            while (actif)
            {
                try
                {
                    await Synchroniser();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IntranetBanque] Synchronisation impossible : {ex.Message}");
                }
                await Task.Delay(Math.Max(5, config.IntervalleSecondes) * 1000);
            }
        }

        private async Task<JObject> Appeler(HttpMethod methode, string route, object corps = null)
        {
            var requete = new HttpRequestMessage(methode, $"{config.UrlIntranet.TrimEnd('/')}/api/{route}");
            requete.Headers.Add("X-Plugin-Cle", config.CleSecrete);
            if (corps != null)
                requete.Content = new StringContent(JsonConvert.SerializeObject(corps), Encoding.UTF8, "application/json");

            HttpResponseMessage reponse = await http.SendAsync(requete);
            string texte = await reponse.Content.ReadAsStringAsync();
            JObject json = string.IsNullOrWhiteSpace(texte) ? new JObject() : JObject.Parse(texte);
            if (!reponse.IsSuccessStatusCode)
                throw new Exception((string)json["erreur"] ?? $"Erreur HTTP {(int)reponse.StatusCode}");
            return json;
        }

        private async Task Synchroniser()
        {
            JObject travail = await Appeler(HttpMethod.Get, "jeu/travail");

            var resultats = new List<object>();
            foreach (JObject op in travail["operations"] ?? new JArray())
            {
                string erreur = await Executer(op);
                resultats.Add(new { id = (string)op["id"], ok = erreur == null, erreur = erreur ?? "" });
            }

            var entreprises = new Dictionary<string, object>();
            foreach (int bizId in travail["entreprises"]?.Select(t => (int)t) ?? Enumerable.Empty<int>())
            {
                Bizs biz = Nova.biz.FetchBiz(bizId);
                if (biz != null)
                    entreprises[bizId.ToString()] = new { nom = biz.BizName, banque = biz.Bank };
            }

            var personnages = new Dictionary<string, object>();
            foreach (int persoId in travail["personnages"]?.Select(t => (int)t) ?? Enumerable.Empty<int>())
            {
                Characters perso = await Personnage(persoId);
                if (perso != null)
                    personnages[persoId.ToString()] = new { nom = $"{perso.Firstname} {perso.Lastname}", banque = perso.Bank };
            }

            var toutes = Nova.biz.bizs.Select(b => new { id = b.Id, nom = b.BizName }).ToList();
            await Appeler(HttpMethod.Post, "jeu/resultat", new { operations = resultats, entreprises, personnages, toutes });
        }

        // Fait un virement en jeu. Renvoie null si tout s'est bien passé, sinon la raison de l'échec.
        private async Task<string> Executer(JObject op)
        {
            try
            {
                string type = (string)op["type"];
                double montant = (double)op["montant"];
                string motif = (string)op["motif"] ?? "Virement";
                int versId = (int)op["versPersonnage"];
                if (montant <= 0)
                    return "Montant invalide.";

                Characters dest = await Personnage(versId);
                if (dest == null)
                    return "Personnage destinataire introuvable.";

                if (type == "entreprise")
                {
                    Bizs biz = Nova.biz.FetchBiz((int)op["bizId"]);
                    if (biz == null)
                        return "Entreprise introuvable en jeu.";
                    if (biz.Bank < montant)
                        return $"Solde insuffisant en jeu ({biz.Bank} €).";
                    biz.AddBankMoney(-montant, $"Intranet : {motif}");
                    await Crediter(versId, montant, $"{biz.BizName} : {motif}");
                    return null;
                }

                if (type == "joueur")
                {
                    int deId = (int)op["dePersonnage"];
                    Characters source = await Personnage(deId);
                    if (source == null)
                        return "Personnage introuvable.";
                    if (source.Bank < montant)
                        return $"Solde insuffisant en jeu ({source.Bank} €).";
                    await Crediter(deId, -montant, $"Virement vers {dest.Firstname} {dest.Lastname} : {motif}");
                    await Crediter(versId, montant, $"Virement de {source.Firstname} {source.Lastname} : {motif}");
                    return null;
                }

                return "Type d'opération inconnu.";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IntranetBanque] Opération {op["id"]} : {ex}");
                return "Erreur sur le serveur de jeu.";
            }
        }

        private static Player JoueurConnecte(int persoId) =>
            Nova.server.Players.FirstOrDefault(p => p?.character != null && p.character.Id == persoId);

        // Personnage connecté : on prend l'objet du jeu (sinon il écraserait notre modification en sauvegardant)
        private static async Task<Characters> Personnage(int persoId)
        {
            Player joueur = JoueurConnecte(persoId);
            return joueur != null ? joueur.character : await LifeDB.FetchCharacter(persoId);
        }

        private static async Task Crediter(int persoId, double montant, string raison)
        {
            Player joueur = JoueurConnecte(persoId);
            if (joueur != null)
            {
                joueur.AddBankMoney(montant, raison);
                joueur.SendText($"<color={(montant >= 0 ? Vert : Rouge)}>[Banque] {(montant >= 0 ? "+" : "")}{montant} € · {raison}</color>");
                return;
            }
            Characters perso = await LifeDB.FetchCharacter(persoId);
            perso.Bank += montant;
            await LifeDB.SaveCharacter(perso);
        }

        private async void Lier(Player player, string[] args)
        {
            if (player?.character == null)
                return;
            if (!actif)
            {
                player.SendText($"<color={Rouge}>La liaison avec la tablette n'est pas configurée sur ce serveur.</color>");
                return;
            }
            if (args.Length < 1)
            {
                player.SendText($"<color={Rouge}>Utilisation : /intranet CODE (le code s'affiche dans l'app Banque de la tablette).</color>");
                return;
            }
            try
            {
                JObject r = await Appeler(HttpMethod.Post, "jeu/lier", new { code = args[0], personnageId = player.character.Id, nom = $"{player.character.Firstname} {player.character.Lastname}" });
                player.SendText($"<color={Vert}>Personnage relié au compte « {(string)r["identifiant"]} » de la tablette.</color>");
            }
            catch (Exception ex)
            {
                player.SendText($"<color={Rouge}>{ex.Message}</color>");
            }
        }
    }
}
