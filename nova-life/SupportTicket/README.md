# SupportTicket – plugin Nova-Life : Amboise

Ce plugin ajoute un menu de tickets à catégories avec icônes (touche **F2** par défaut), dans le style « Support BlueSide ».

## Côté joueur (touche F2 ou `/ticket`)

1. Le menu **Support &lt;nom du serveur&gt; / Ouvrir un ticket** s'ouvre. Chaque catégorie a une icône, un titre en gras et une description. Les boutons sont **CHOISIR** (vert) et **FERMER** (rouge).
2. Après le choix de la catégorie, le joueur décrit son problème puis clique sur **ENVOYER**.
3. S'il a déjà un ticket ouvert, la touche affiche ce ticket et son statut (en attente ou pris en charge par un membre du staff), avec un bouton **CLÔTURER**.

## Côté staff (`/tickets`, ou une touche à définir dans la config)

- Chaque nouveau ticket envoie un message et un son au staff en service.
- La liste affiche pour chaque ticket : l'icône de la catégorie, le nom RP du joueur, le début du message, l'ancienneté et le membre du staff qui l'a pris en charge.
- Boutons disponibles :
  - **Y ALLER** : se téléporter vers le joueur.
  - **RAMENER** : téléporter le joueur vers soi.
  - **CLÔTURER** : fermer le ticket.
  - **FERMER** : quitter le menu.

  Les deux premiers marquent le ticket « pris en charge » et préviennent le joueur.
- `/ticketicones [page]` liste toutes les icônes du jeu avec leur numéro. Cela sert à remplir `IconeId` dans la config.

## Installation

1. Ouvre `SupportTicket.csproj` dans Visual Studio 2022 ou Rider.
2. Si le jeu n'est pas installé à l'emplacement Steam par défaut, adapte `NovaLifeRefs`. Ce dossier doit contenir les DLL du jeu, dont `Assembly-CSharp.dll` et `Mirror.dll`.
3. Compile en Release, puis copie `bin/Release/net472/SupportTicket.dll` dans le dossier `Plugins` du serveur.
4. Redémarre le serveur. Le fichier `Plugins/SupportTicket/config.json` est créé automatiquement.

## ⚠️ Pourquoi pas la touche N ?

La touche **N** ouvre déjà le panel « Nouveau ticket » intégré au jeu. Un plugin ne peut pas le désactiver : avec `ToucheJoueur` sur `N`, les deux menus s'ouvrent l'un sur l'autre. Choisis donc une touche libre.

Si le serveur a déjà un `config.json` créé avec l'ancienne version, il garde la touche qui y est écrite. Modifie `ToucheJoueur` à la main dans ce fichier.

## Configuration (`config.json`)

| Clé | Rôle |
|---|---|
| `NomServeur` | Texte affiché après « Support » |
| `SousTitre` | Ligne sous le titre (« Ouvrir un ticket ») |
| `CouleurTitre` | Couleur du titre (hexadécimal) |
| `ToucheJoueur` | Touche d'ouverture pour les joueurs (`F2` par défaut) |
| `ToucheStaff` | Touche de la liste staff (`None` = commande `/tickets` uniquement) |
| `ServiceAdminRequis` | Le staff doit être en service admin pour recevoir et traiter les tickets |
| `Categories` | Liste des catégories : `Titre`, `Description`, `IconeId` |

Les noms de touches sont ceux de Unity : <https://docs.unity3d.com/ScriptReference/KeyCode.html>
