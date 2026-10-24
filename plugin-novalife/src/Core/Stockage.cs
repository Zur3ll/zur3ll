using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace NovaApps.Core
{
    /// <summary>
    /// Fichier JSON chargé au démarrage et réécrit à chaque modification.
    /// Les données des applis (posts N, profils Neonous, offres Pôle Travail...) vivent dans ces fichiers.
    /// </summary>
    public class Stockage<T> where T : new()
    {
        private readonly string chemin;

        public T Donnees { get; private set; }

        public Stockage(string chemin)
        {
            this.chemin = chemin;
            Charger();
        }

        private void Charger()
        {
            try
            {
                if (File.Exists(chemin))
                {
                    Donnees = JsonConvert.DeserializeObject<T>(File.ReadAllText(chemin)) ?? new T();
                    return;
                }
            }
            catch (Exception e)
            {
                // Fichier corrompu : on le met de côté plutôt que de l'écraser.
                Debug.LogError($"[NovaApps] Lecture impossible de {chemin} : {e.Message}");
                try { File.Copy(chemin, chemin + ".corrompu-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
                catch (Exception copie) { Debug.LogError($"[NovaApps] Copie de secours impossible : {copie.Message}"); }
            }

            Donnees = new T();
            Sauvegarder();
        }

        public void Sauvegarder()
        {
            try
            {
                // Écriture dans un fichier temporaire puis remplacement : pas de fichier à moitié écrit en cas de crash.
                string temporaire = chemin + ".tmp";
                File.WriteAllText(temporaire, JsonConvert.SerializeObject(Donnees, Formatting.Indented));
                if (File.Exists(chemin)) File.Delete(chemin);
                File.Move(temporaire, chemin);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NovaApps] Sauvegarde impossible de {chemin} : {e.Message}");
            }
        }
    }
}
