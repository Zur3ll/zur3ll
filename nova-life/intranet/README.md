# Neoran Intranet

Site de la tablette en jeu (navigateur Blue Lightfox), dans le style d'un iPad.
Version PHP : elle tourne sur un hébergement Web classique (Apache + PHP), sans processus à lancer.

## Fichiers

- `index.html` : la page de la tablette.
- `api.php` : l'API (contenu, comptes, connexion, entreprises, tchat).
- `.htaccess` : redirige `/api/...` vers `api.php` et bloque l'accès aux fichiers cachés.
- `.env` : configuration secrète (mot de passe admin, `PLUGIN_CLE` pour la liaison avec le jeu). À créer à partir de `.env.example`. Ne pas partager.
- `data/` : créé automatiquement, contient le contenu, les comptes (`staff.json`), les entreprises (`entreprises.json`) et les tchats (`tchat-<id>.json`). Protégé par `data/.htaccess`. À sauvegarder, ne pas partager.
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
- **Notes et fond d'écran** : enregistrés dans le compte du joueur quand il est connecté, donc retrouvés sur n'importe quelle tablette. Sans compte, ils restent dans le navigateur du PC. À la première connexion, l'app Notes propose d'importer les notes du PC.

## Comptes Neoran (app Réglages)

Comme sur un iPad, le compte se gère en haut de l'app **Réglages** :

- **Créer un compte :** chaque joueur crée un compte (nom du personnage, identifiant, mot de passe, téléphone en jeu facultatif). 5 créations maximum par heure et par connexion.
- **Profil :** nom du personnage, téléphone et mot de passe (le mot de passe actuel est demandé pour le changer).
- **Portefeuille :** solde du joueur et derniers salaires et virements reçus de ses entreprises.
- La connexion est partagée par Réglages, Gestion entreprise et Staff, et reste active 7 jours.

Trois rôles : **Joueur** (par défaut), **Staff** et **Admin**. Un admin change le rôle d'un compte depuis l'app Staff → Comptes.

## Gestion entreprise

L'app **Gestion entreprise** (première icône de l'accueil) :

- **Création :** seul le staff crée une entreprise, en indiquant l'identifiant du compte du patron.
- **Grades :**
  - **Patron :** tout gérer, nommer gérants et nouveau patron (l'ancien patron devient alors gérant), modifier le nom et le secteur.
  - **Gérant :** gérer les employés (pas les autres gérants), les règles, les virements et les salaires.
  - **Employé :** voir l'équipe, les règles, ses propres paiements et le tchat. Peut quitter l'entreprise.
- **Onglets :** Aperçu, Employés (ajout par identifiant de compte, poste, salaire, grade), Virements (virement à un employé, « Payer tous les salaires », historique), Règles internes, Tchat (nouveaux messages toutes les 4 secondes) et Paramètres.
- **Staff :** voit et gère toutes les entreprises comme un patron, crédite ou débite la trésorerie et peut supprimer une entreprise.

**Argent :** une entreprise non reliée utilise un solde propre à l'intranet (crédité ou débité par le staff). Une entreprise **reliée au jeu** utilise sa vraie banque en jeu : voir plus bas.

## Liaison avec le jeu (banque réelle)

Le plugin **IntranetBanque** (dossier `nova-life/IntranetBanque`), installé sur le serveur Nova-Life, relie la tablette à la vraie banque du jeu. Toutes les 10 secondes, il récupère les virements demandés sur la tablette, les fait en jeu, puis renvoie le résultat et les vrais soldes.

1. Dans le `.env` du site, ajoute une ligne `PLUGIN_CLE=` suivie d'une longue phrase secrète (au moins 32 caractères au hasard).
2. Installe le plugin et mets la même clé dans `Plugins/IntranetBanque/config.json` (voir son README).

Ensuite :
- **Joueurs :** dans l'app **Banque**, « Obtenir mon code », puis taper `/intranet CODE` en jeu. Le compte voit alors la vraie banque de son personnage et peut faire des virements à d'autres joueurs reliés (par identifiant Neoran).
- **Entreprises :** dans Gestion entreprise → Paramètres → **Entreprise en jeu**, le staff choisit l'entreprise du jeu (ou tape son ID). Seul le staff peut le faire : un patron pourrait sinon relier l'entreprise de quelqu'un d'autre. Le solde affiché devient celui du jeu, et les virements et salaires sont payés avec la banque de l'entreprise en jeu. Chaque employé payé doit avoir relié son personnage.
- **Statuts :** chaque virement passe par « En attente », « En cours », puis « Effectué » ou « Échoué » (avec la raison, par exemple un solde insuffisant en jeu). Si le serveur de jeu ne confirme pas dans les 10 minutes, l'opération passe en « À vérifier » et n'est jamais refaite automatiquement, pour ne pas payer deux fois.
- L'argent déjà en cours d'envoi est réservé : on ne peut pas dépenser deux fois la même somme.

## Espace staff

L'app **Staff** (page 2 de l'accueil) utilise les mêmes comptes que Réglages, avec le rôle Staff ou Admin.

- **Compte admin :** identifiant `admin` (ou `ADMIN_IDENTIFIANT`), mot de passe = `ADMIN_MDP` du fichier `.env`.
- **Rôle Staff :** modifie tout le contenu de la tablette (général, actualités, calendrier, entreprises, emplois, annuaire, règlement, liens) et gère toutes les entreprises.
- **Rôle Admin :** pareil, plus la gestion de tous les comptes (rôle, mot de passe, suppression). Il reste toujours au moins un admin.
- Après 5 mauvais mots de passe, la connexion est bloquée 5 minutes pour cette adresse IP.
- Le journal d'erreurs du site (Apache / PHP) note qui modifie quoi.

## Mettre en ligne

Voir `DEPLOIEMENT-INFOMANIAK.md`.

En jeu : panel admin → **CONFIG** → **Configurer liens favoris**, puis ajoute l'adresse du site.
Optionnel : mets la même adresse dans `"tabletUrl"` du `config.json` du serveur de jeu.
