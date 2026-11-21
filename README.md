# 🍺 T'DA Bot

**Bot Discord français tout-en-un, multi-serveurs.** Chaque serveur a ses propres réglages, et tout se configure avec une seule commande : `/config`.

[![Ajouter T'DA Bot](https://img.shields.io/badge/Ajouter%20T'DA%20Bot-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com/oauth2/authorize?client_id=1561021606036504667&permissions=8&scope=bot+applications.commands)
[![Serveur support](https://img.shields.io/badge/Serveur%20support-2B2D31?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/DMF2hAPzBW)
[![Conditions et confidentialité](https://img.shields.io/badge/CGU%20%26%20confidentialit%C3%A9-2B2D31?style=for-the-badge)](https://claude.ai/artifact/Kuu5bTcFg4PjidR97959di)

![Version](https://img.shields.io/badge/version-1.15.2-blue) ![Commandes](https://img.shields.io/badge/commandes-27-green) ![Langue](https://img.shields.io/badge/langue-fran%C3%A7ais-lightgrey)

## ✨ Fonctionnalités

| Module | Ce qu'il fait |
|---|---|
| 🛡️ **Protection** | Anti-raid, verrouillage du serveur, bilan de sécurité hebdomadaire noté sur 100 |
| 📜 **Logs** | 15 catégories : messages, membres, rôles, invitations, AutoMod, vocal… |
| 🎫 **Tickets** | Tickets par sujet avec formulaires, prise en charge, priorité, ajout de membres |
| 👋 **Arrivées & départs** | Messages de bienvenue et d'au revoir personnalisables |
| 📖 **Règlement** | Règlement en sections, validé par un bouton qui donne un rôle |
| 🎭 **Rôles-réactions** | Rôles avec réactions ou boutons |
| 🎉 **Giveaways** | Lancer, terminer, relancer : ils continuent même après un redémarrage |
| 🎵 **Musique** | SoundCloud, Spotify, YouTube, Apple Music, file d'attente, boucle, volume |
| 🔊 **Salons vocaux temporaires** | Chacun crée son salon vocal, avec un panneau de gestion |
| 🌍 **Traduction** | `/traduire` ou clic droit sur un message |
| 🟣 **Lives Twitch** | Annonce automatique quand un streamer passe en live |
| 🎮 **Jeux gratuits** | Jeux offerts sur Epic Games, Steam et GOG |
| 💰 **Économie** | Monnaie « T'Coins », travail, classement, boutique |
| 🎂 **Anniversaires** | Annonce le jour J et rôle d'anniversaire |
| 🎄 **Calendrier de l'Avent** | Une case par jour du 1er au 24 décembre |
| 🤖 **Assistant IA** | Mentionne le bot pour lui poser une question |
| 🔒 **RGPD** | `/mes-donnees` : voir, télécharger ou faire supprimer ses données |

Chaque module s'active ou se désactive avec un bouton ON/OFF dans `/config`.

## 🚀 Démarrer

1. Clique sur **Ajouter T'DA Bot** ci-dessus et choisis ton serveur.
2. Place le rôle du bot **tout en haut** de la liste des rôles.
3. Tape `/config` et règle les modules que tu veux.
4. `/aide` affiche toutes les commandes.

Une question ou un bug ? Utilise `/bug` ou `/suggestion`, ou viens sur le [serveur support](https://discord.gg/DMF2hAPzBW).

---

<sub>© T'DA. Tous droits réservés. Le code source de T'DA Bot n'est pas public.</sub>

<br>

<details>
<summary><b>🔧 Autre projet : Bot Discord Garage & Concession</b> (cliquer pour ouvrir)</summary>

<br>

Ce bot affiche deux boutons dans deux salons différents :

| Salon | Bouton | Pop-up |
|---|---|---|
| Rendez-vous garage | 🔧 Prendre rendez-vous | Nom RP, Véhicule, Problème / service souhaité, Disponibilités |
| Commande de véhicule | 🛒 Commander un véhicule | Nom RP, Véhicule souhaité, Couleur / Options, Budget, Disponibilités pour la livraison |

Chaque formulaire rempli arrive dans un **salon de réception** (par exemple un salon réservé au staff), avec la mention de la personne qui l'a envoyé.
Sous chaque demande, un bouton **« 🎫 Je me charge du ticket »** permet à un membre du staff de prendre la demande : le bouton devient gris et affiche son nom. Une demande ne peut être prise que par une seule personne.

> ℹ️ Discord limite un pop-up à **5 champs**. Le formulaire de commande en a 6 : « Couleur » et « Options » sont donc regroupés dans un seul champ.

### 1. Créer le bot sur Discord

1. Va sur <https://discord.com/developers/applications> → **New Application**.
2. Onglet **Bot** → **Reset Token** → copie le token (garde-le secret !).
3. Onglet **OAuth2 → URL Generator** :
   - Scopes : `bot`
   - Bot Permissions : `View Channels`, `Send Messages`, `Embed Links`, `Read Message History`
4. Ouvre l'URL générée et invite le bot sur ton serveur.

### 2. Récupérer les ID des salons

Dans Discord : **Paramètres utilisateur → Avancés → Mode développeur** (activer).
Ensuite clic droit sur un salon → **Copier l'identifiant du salon**.

Il te faut 3 ID : le salon rendez-vous, le salon commande, et le salon de réception.

### 3. Installer et lancer dans Visual Studio Code

Prérequis : [Node.js](https://nodejs.org) version 18 ou plus.

1. Ouvre le dossier du projet dans VS Code (**Fichier → Ouvrir le dossier**).
2. Ouvre le terminal (**Terminal → Nouveau terminal**) et tape :
   ```bash
   npm install
   ```
3. Copie le fichier `.env.example`, renomme la copie en `.env` et remplis-la :
   ```env
   DISCORD_TOKEN=ton_token_ici
   SALON_RDV_ID=id_du_salon_ici
   SALON_COMMANDE_ID=id_du_salon_ici
   SALON_RECEPTION_ID=id_du_salon_ici
   ```
4. Lance le bot :
   ```bash
   npm start
   ```

Au démarrage, le bot publie automatiquement le message avec le bouton dans chacun des deux salons. S'il est déjà présent, il ne le republie pas.
Le bot doit rester allumé pour que les boutons fonctionnent.

### Personnaliser

Les textes, les questions et les couleurs se modifient dans `src/index.js`, dans l'objet `FORMULAIRES` en haut du fichier.
Si tu modifies le texte du panneau, supprime l'ancien message du bot dans le salon puis relance le bot pour qu'il republie la nouvelle version.

</details>
