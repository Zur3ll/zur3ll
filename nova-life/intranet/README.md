# Intranet Nova

Site de la tablette en jeu (navigateur Blue Lightfox), dans le style d'un iPad.

- `index.html` : la page de la tablette.
- `server.js` : un petit serveur Node.js (aucune dépendance) qui sert la page, enregistre le contenu modifié par le staff et gère les comptes staff.
- `data/` : créé au premier lancement, contient `contenu.json` et `staff.json` (mots de passe chiffrés). À sauvegarder, ne pas partager.

## Modifier le contenu

Le plus simple : l'app **Staff** de la tablette (voir plus bas). Les modifications sont enregistrées sur le serveur et visibles par tous les joueurs.

Le contenu par défaut, affiché tant que le staff n'a rien enregistré, se trouve dans le bloc `INTRANET` au début du `<script>` de `index.html` :

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

## Espace staff

L'app **Staff** (page 2 de l'accueil) demande un identifiant et un mot de passe.

- **Premier lancement :** `server.js` crée le compte `admin` et affiche son mot de passe **une seule fois** dans la console. Connecte-toi et change-le dans « Mon profil et mot de passe ».
- **Rôle Staff :** modifie tout le contenu de la tablette (général, actualités, calendrier, entreprises, emplois, annuaire, règlement, liens) et son propre mot de passe.
- **Rôle Admin :** pareil, plus la création, la modification et la suppression des comptes staff. Il reste toujours au moins un admin.
- Après 5 mauvais mots de passe, la connexion est bloquée 5 minutes pour cette adresse IP.
- La console du serveur note qui modifie quoi.

## Lancer le serveur

Il faut Node.js 18 ou plus. Dans le dossier `intranet` :

```bash
node server.js
```

Le site est alors sur `http://localhost:3000`. Pour un autre port, définis la variable d'environnement `PORT`.

Si tu ouvres `index.html` directement sans le serveur, la tablette marche avec le contenu par défaut, mais l'app Staff indique que le serveur est injoignable.

## Mettre en ligne

1. Héberge le dossier `intranet` chez un hébergeur qui fait tourner Node.js (un hébergement « bot Discord / Node.js » suffit, par exemple chez OxygenServ), avec `npm start` comme commande de démarrage. Un hébergement de site statique seul (GitHub Pages…) ne suffit plus pour l'espace staff. Utilise une adresse en `https://` pour que les mots de passe ne circulent pas en clair.
2. En jeu : panel admin → **CONFIG** → **Configurer liens favoris**, puis ajoute l'adresse du site.
3. Optionnel : mets la même adresse dans `"tabletUrl"` du `config.json` du serveur pour que le navigateur s'ouvre directement dessus.
