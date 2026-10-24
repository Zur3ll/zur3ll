# SupportTicket – plugin Nova-Life : Amboise

Ce plugin ajoute un menu de tickets à catégories avec icônes (touche **N** par défaut), dans le style « Support BlueSide ».

## Côté joueur (touche N ou `/ticket`)

1. Le menu **Support &lt;nom du serveur&gt; / Ouvrir un ticket** s'ouvre. Chaque catégorie a une icône, un titre en gras et une description. Les boutons sont **CHOISIR** (vert) et **FERMER** (rouge).
2. Après le choix de la catégorie, le joueur décrit son problème puis clique sur **ENVOYER**.
3. S'il a déjà un ticket ouvert, la touche affiche ce ticket et son statut (en attente ou pris en charge par un membre du staff), avec un bouton **CLÔTURER**.

## Côté staff (touche `,`)

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

## ⚠️ Touche N et panel du jeu

La touche **N** ouvre aussi le panel « Nouveau ticket » intégré au jeu, qu'un plugin ne peut pas désactiver. Les deux menus s'ouvrent l'un sur l'autre : le joueur ferme celui du jeu avec **ANNULER** pour utiliser celui du plugin. Pour l'éviter, mets une autre touche dans `ToucheJoueur` (pas `F2`, qui ouvre l'ordinateur/tablette).

Si le serveur a déjà un `config.json` créé avec une ancienne version, il garde les valeurs qui y sont écrites (touches, icônes). Modifie-les à la main ou supprime le fichier pour qu'il soit recréé.

## Configuration (`config.json`)

| Clé | Rôle |
|---|---|
| `NomServeur` | Texte affiché après « Support » |
| `SousTitre` | Ligne sous le titre (« Ouvrir un ticket ») |
| `CouleurTitre` | Couleur du titre (hexadécimal) |
| `ToucheJoueur` | Touche d'ouverture pour les joueurs (`N` par défaut) |
| `ToucheStaff` | Touche de la liste staff (`Comma` = touche `,` par défaut) |
| `ServiceAdminRequis` | Le staff doit être en service admin pour recevoir et traiter les tickets |
| `Categories` | Liste des catégories : `Titre`, `Description`, `IconeId` |

Les noms de touches sont ceux de Unity : <https://docs.unity3d.com/ScriptReference/KeyCode.html>
