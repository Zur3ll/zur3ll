using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaApps.Core
{
    public class MessageDiscord
    {
        public string Auteur;
        public string Contenu;
        public DateTime Date;
    }

    /// <summary>
    /// Appels à l'API Discord :
    ///  - avec le token du bot (le même que le bot Garage / Concession) pour lire les salons et poster des tickets ;
    ///  - avec des webhooks pour poster sous le nom RP du joueur.
    /// </summary>
    public class Discord
    {
        private const string Api = "https://discord.com/api/v10";

        // Le même custom_id que dans src/index.js : le bot Node gère le clic "Je me charge du ticket".
        public const string BoutonPriseTicket = "prendre_ticket";

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly string tokenBot;

        static Discord()
        {
            // Certains Mono n'activent pas TLS 1.2 par défaut, requis par Discord.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public Discord(string tokenBot)
        {
            this.tokenBot = tokenBot;
        }

        public bool BotConfigure => !string.IsNullOrWhiteSpace(tokenBot);

        public static bool WebhookValide(string url)
        {
            return !string.IsNullOrWhiteSpace(url) && url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Derniers messages d'un salon, du plus ancien au plus récent.</summary>
        public async Task<List<MessageDiscord>> LireSalon(string salonId, int nombre)
        {
            HttpRequestMessage requete = RequeteBot(HttpMethod.Get, $"/channels/{salonId}/messages?limit={nombre}");
            HttpResponseMessage reponse = await http.SendAsync(requete);
            string corps = await reponse.Content.ReadAsStringAsync();

            if (!reponse.IsSuccessStatusCode)
                throw new Exception($"Discord a répondu {(int)reponse.StatusCode} : {corps}");

            return JArray.Parse(corps)
                .Select(m => new MessageDiscord
                {
                    Auteur = (string)m["author"]?["global_name"] ?? (string)m["author"]?["username"] ?? "?",
                    Contenu = ContenuLisible(m),
                    Date = (DateTime?)m["timestamp"] ?? DateTime.Now,
                })
                .Reverse()
                .ToList();
        }

        /// <summary>Poste un message avec le bot. Renvoie false en cas d'échec.</summary>
        public async Task<bool> PosterAvecBot(string salonId, JObject message)
        {
            if (!BotConfigure || string.IsNullOrWhiteSpace(salonId)) return false;

            try
            {
                HttpRequestMessage requete = RequeteBot(HttpMethod.Post, $"/channels/{salonId}/messages");
                requete.Content = new StringContent(message.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage reponse = await http.SendAsync(requete);

                if (!reponse.IsSuccessStatusCode)
                    Debug.LogWarning($"[NovaApps] Discord : {(int)reponse.StatusCode} {await reponse.Content.ReadAsStringAsync()}");

                return reponse.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NovaApps] Discord injoignable : {e.Message}");
                return false;
            }
        }

        /// <summary>Poste via un webhook, avec un nom d'auteur personnalisé (le nom RP du joueur).</summary>
        public async Task<bool> PosterAvecWebhook(string url, string nom, string contenu, JObject embed = null)
        {
            if (!WebhookValide(url)) return false;

            JObject message = new JObject
            {
                ["username"] = Ecran.Couper(nom, 80),
                ["content"] = Ecran.Couper(contenu, 1900),
                // Empêche un joueur de faire un @everyone depuis le jeu.
                ["allowed_mentions"] = new JObject { ["parse"] = new JArray() },
            };
            if (embed != null) message["embeds"] = new JArray(embed);

            try
            {
                HttpResponseMessage reponse = await http.PostAsync(url, new StringContent(message.ToString(), Encoding.UTF8, "application/json"));
                return reponse.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NovaApps] Webhook Discord injoignable : {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Embed identique à ceux du bot Node (titre, couleur, champs) + bouton "Je me charge du ticket".
        /// </summary>
        public static JObject Ticket(string titre, int couleur, string demandePar, IEnumerable<KeyValuePair<string, string>> champs)
        {
            JArray fields = new JArray { Field("Demandé par", demandePar) };
            foreach (KeyValuePair<string, string> champ in champs)
                fields.Add(Field(champ.Key, champ.Value));

            return new JObject
            {
                ["embeds"] = new JArray(new JObject
                {
                    ["title"] = titre,
                    ["color"] = couleur,
                    ["fields"] = fields,
                    ["footer"] = new JObject { ["text"] = "Envoyé depuis le téléphone (en jeu)" },
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                }),
                ["components"] = new JArray(new JObject
                {
                    ["type"] = 1,
                    ["components"] = new JArray(new JObject
                    {
                        ["type"] = 2,
                        ["style"] = 3,
                        ["label"] = "Je me charge du ticket",
                        ["emoji"] = new JObject { ["name"] = "🎫" },
                        ["custom_id"] = BoutonPriseTicket,
                    }),
                }),
                ["allowed_mentions"] = new JObject { ["parse"] = new JArray() },
            };
        }

        public static JObject Field(string nom, string valeur)
        {
            // Discord refuse les champs vides et limite leur valeur à 1024 caractères.
            return new JObject
            {
                ["name"] = Ecran.Couper(nom, 256),
                ["value"] = string.IsNullOrWhiteSpace(valeur) ? "-" : Ecran.Couper(valeur, 1024),
            };
        }

        private HttpRequestMessage RequeteBot(HttpMethod methode, string chemin)
        {
            HttpRequestMessage requete = new HttpRequestMessage(methode, Api + chemin);
            requete.Headers.TryAddWithoutValidation("Authorization", "Bot " + tokenBot);
            requete.Headers.TryAddWithoutValidation("User-Agent", "DiscordBot (NovaApps, 1.0)");
            return requete;
        }

        private static string ContenuLisible(JToken message)
        {
            List<string> morceaux = new List<string>();

            string contenu = (string)message["content"];
            if (!string.IsNullOrWhiteSpace(contenu)) morceaux.Add(contenu);

            foreach (JToken embed in message["embeds"] ?? new JArray())
            {
                string titre = (string)embed["title"] ?? (string)embed["description"];
                if (!string.IsNullOrWhiteSpace(titre)) morceaux.Add($"[{titre}]");
            }

            int fichiers = (message["attachments"] as JArray)?.Count ?? 0;
            if (fichiers > 0) morceaux.Add($"[{fichiers} pièce(s) jointe(s)]");

            return morceaux.Count > 0 ? string.Join(" ", morceaux) : "(message vide)";
        }
    }
}
