using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Life;
using Life.Network;
using UnityEngine;

namespace NovaApps.Core
{
    /// <summary>
    /// Lecture de l'inventaire d'un joueur. L'API publique du jeu ne documente pas la liste des objets
    /// d'un inventaire : on la lit par réflexion (champs nommés itemId / number), pour ne pas dépendre
    /// d'une classe interne qui change selon les versions.
    /// </summary>
    public static class Inventaire
    {
        private const BindingFlags Drapeaux = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] NomsId = { "itemId", "ItemId", "itemID", "id", "Id" };
        private static readonly string[] NomsQuantite = { "number", "Number", "amount", "Amount", "count", "Count", "quantity" };

        private static bool avertissementAffiche;

        /// <summary>
        /// Indique si le joueur porte un des objets demandés.
        /// Renvoie null si l'inventaire n'a pas pu être lu : on ne bloque alors personne.
        /// </summary>
        public static bool? Possede(Player joueur, ICollection<int> objets)
        {
            Dictionary<int, int> contenu = Lire(joueur);
            if (contenu == null)
            {
                if (!avertissementAffiche)
                {
                    avertissementAffiche = true;
                    Debug.LogWarning("[NovaApps] Inventaire illisible : la vérification du téléphone est désactivée. Lance /tel_objets en jeu et envoie le résultat de la console au développeur.");
                }
                return null;
            }

            return objets.Any(id => contenu.TryGetValue(id, out int nombre) && nombre > 0);
        }

        /// <summary>Objets de l'inventaire (ID → quantité), ou null si illisible.</summary>
        public static Dictionary<int, int> Lire(Player joueur)
        {
            object inventaire = joueur?.setup?.inventory;
            if (inventaire == null) return null;

            foreach (object valeur in Membres(inventaire))
            {
                if (!(valeur is IEnumerable liste) || valeur is string) continue;

                Dictionary<int, int> contenu = new Dictionary<int, int>();
                bool reconnu = false;

                foreach (object element in liste)
                {
                    if (element == null) continue;
                    if (!(Valeur(element, NomsId) is int id)) break;

                    reconnu = true;
                    int nombre = Valeur(element, NomsQuantite) is int n ? n : 1;
                    if (id > 0 && nombre > 0)
                        contenu[id] = (contenu.TryGetValue(id, out int deja) ? deja : 0) + nombre;
                }

                if (reconnu) return contenu;
            }

            return null;
        }

        /// <summary>Nom lisible d'un objet du jeu.</summary>
        public static string NomObjet(int id)
        {
            try
            {
                object objet = Nova.man.item.GetItem(id);
                if (objet == null) return "?";
                return Valeur(objet, new[] { "itemName", "ItemName", "displayName" }) as string ?? (objet as UnityEngine.Object)?.name ?? "?";
            }
            catch
            {
                return "?";
            }
        }

        /// <summary>Description de la structure de l'inventaire, pour le diagnostic (/tel_objets).</summary>
        public static string Structure(Player joueur)
        {
            object inventaire = joueur?.setup?.inventory;
            if (inventaire == null) return "inventaire introuvable";

            Type type = inventaire.GetType();
            return type.FullName + " : " + string.Join(", ",
                type.GetFields(Drapeaux).Select(f => $"{f.FieldType.Name} {f.Name}")
                    .Concat(type.GetProperties(Drapeaux).Where(p => p.GetIndexParameters().Length == 0).Select(p => $"{p.PropertyType.Name} {p.Name}")));
        }

        private static IEnumerable<object> Membres(object objet)
        {
            Type type = objet.GetType();
            foreach (FieldInfo champ in type.GetFields(Drapeaux))
                yield return Lire(() => champ.GetValue(objet));
            foreach (PropertyInfo propriete in type.GetProperties(Drapeaux).Where(p => p.GetIndexParameters().Length == 0 && p.CanRead))
                yield return Lire(() => propriete.GetValue(objet));
        }

        private static object Lire(Func<object> lecture)
        {
            try { return lecture(); }
            catch { return null; }
        }

        private static object Valeur(object objet, IEnumerable<string> noms)
        {
            Type type = objet.GetType();
            foreach (string nom in noms)
            {
                FieldInfo champ = type.GetField(nom, Drapeaux);
                if (champ != null) return Lire(() => champ.GetValue(objet));

                PropertyInfo propriete = type.GetProperty(nom, Drapeaux);
                if (propriete != null && propriete.GetIndexParameters().Length == 0) return Lire(() => propriete.GetValue(objet));
            }
            return null;
        }
    }
}
