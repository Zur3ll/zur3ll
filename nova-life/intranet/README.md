# Intranet Nova

Site de la tablette en jeu (navigateur Blue Lightfox), dans le style d'un iPad.
Version PHP : elle tourne sur un hébergement Web classique (Apache + PHP), sans processus à lancer.

## Fichiers

- `index.html` : la page de la tablette.
- `api.php` : l'API (contenu, comptes staff, connexion). Remplace l'ancien `server.js`.
- `.htaccess` : redirige `/api/...` vers `api.php` et bloque l'accès aux fichiers cachés.
- `.env` : configuration secrète (mot de passe admin). À créer à partir de `.env.example`. Ne pas partager.
- `data/` : créé automatiquement, contient le contenu et les comptes staff. Protégé par `data/.htaccess`. À sauvegarder, ne pas partager.
- `DEPLOIEMENT-INFOMANIAK.md` : étapes de mise en ligne.

## Configuration (.env)

1. Copie `.env.example` en `.env`.
2. Remplace `ADMIN_MDP` par ton mot de passe (8 caractères minimum).

Le compte `admin` (ou `ADMIN_IDENTIFIANT`) est créé au premier appel avec ce mot de passe.
Si tu changes `ADMIN_MDP` dans `.env`, le nouveau mot de passe remplace l'ancien dès la première connexion avec lui.
Si tu changes le mot de passe depuis l'app Staff, il reste en place tant que personne ne se connecte avec la valeur du `.env`.

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

- **Compte admin :** identifiant `admin` (ou `ADMIN_IDENTIFIANT`), mot de passe = `ADMIN_MDP` du fichier `.env`.
- **Rôle Staff :** modifie tout le contenu de la tablette (général, actualités, calendrier, entreprises, emplois, annuaire, règlement, liens) et son propre mot de passe.
- **Rôle Admin :** pareil, plus la création, la modification et la suppression des comptes staff. Il reste toujours au moins un admin.
- Après 5 mauvais mots de passe, la connexion est bloquée 5 minutes pour cette adresse IP.
- Le journal d'erreurs du site (Apache / PHP) note qui modifie quoi.

## Mettre en ligne

Voir `DEPLOIEMENT-INFOMANIAK.md`.

En jeu : panel admin → **CONFIG** → **Configurer liens favoris**, puis ajoute l'adresse du site.
Optionnel : mets la même adresse dans `"tabletUrl"` du `config.json` du serveur de jeu.
