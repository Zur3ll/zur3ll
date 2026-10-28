using System.IO;
using Newtonsoft.Json;

namespace IntranetBanque
{
    public class BanqueConfig
    {
        // Adresse du site de l'intranet, sans / à la fin (ex. https://intranet.neoran.fr)
        public string UrlIntranet = "https://ton-intranet.fr";

        // Même valeur que PLUGIN_CLE dans le fichier .env du site
        public string CleSecrete = "";

        // Fréquence des échanges avec le site
        public int IntervalleSecondes = 10;

        public static BanqueConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                BanqueConfig defaults = new BanqueConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented));
                return defaults;
            }

            return JsonConvert.DeserializeObject<BanqueConfig>(File.ReadAllText(path));
        }
    }
}
