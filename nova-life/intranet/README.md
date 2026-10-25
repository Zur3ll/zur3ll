# Intranet Nova

Site de la tablette en jeu (navigateur Blue Lightfox), dans le style d'un iPad. C'est un seul fichier, `index.html`, sans serveur ni base de données : il suffit de l'héberger tel quel.

## Modifier le contenu

Tout le contenu se trouve dans le bloc `INTRANET` au début du `<script>` de `index.html` :

| Clé | Contenu |
|---|---|
| `serveur`, `ville` | Nom du serveur et ville affichée |
| `actualites` | Articles de l'app Actualités et du widget d'accueil |
| `evenements` | Calendrier et widget « Prochain événement » (dates au format `AAAA-MM-JJ`) |
| `entreprises` | App Entreprises (`statut` : `Recrute`, `Ouvert` ou `Fermé`) |
| `emplois` | App Emplois |
| `annuaire` | App Annuaire |
| `reglement` | App Règlement |
| `liens` | App Liens (Discord, etc.) |

Les éléments marqués `exemple: true` sont des exemples et affichent une étiquette « Exemple ». Remplace-les par les vraies infos du serveur.

## Ce que fait la page

- **Écran d'accueil** : widgets (date et heure, prochain événement, dernières actualités) et icônes d'apps sur deux pages. On change de page en faisant glisser, avec les points ou avec les flèches du clavier.
- **Dock** en bas avec Actualités, Annuaire, Notes et Réglages.
- **Apps** : elles s'ouvrent en plein écran avec une animation de zoom depuis l'icône. On les ferme avec la barre du bas (clic ou glissement vers le haut), le bouton ✕ ou Échap.
- **Recherche** : cherche dans les apps, les contacts et les actualités.
- **Notes et fond d'écran** : enregistrés dans le navigateur de chaque joueur.

## Mettre en ligne

1. Héberge `index.html` sur n'importe quel hébergement de site statique (GitHub Pages, Netlify, OVH…).
2. En jeu : panel admin → **CONFIG** → **Configurer liens favoris**, puis ajoute l'adresse du site.
3. Optionnel : mets la même adresse dans `"tabletUrl"` du `config.json` du serveur pour que le navigateur s'ouvre directement dessus.
