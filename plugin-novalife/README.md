# 📱💻 NovaApps – Téléphone & Ordinateur pour Nova-Life

Plugin serveur Nova-Life : Amboise qui ajoute des applications au **téléphone** et à l'**ordinateur**.

| Où | Appli | Ce qu'elle fait |
|---|---|---|
| 📱 Téléphone | **N** | Réseau social façon X : fil d'actu, posts (280 car.), j'aime, réponses, @mentions (notifiées), #hashtags et tendances, profils, recherche de comptes. Option : recopie des posts dans un salon Discord. |
| 📱 Téléphone | **Neonous** | Rencontres façon Tinder : profil (prénom, âge, genre, ce que tu cherches, bio), découverte des profils compatibles (Passer / Liker), match quand c'est réciproque, messagerie entre matchs, masquer / supprimer son profil. |
| 📱 + 💻 | **Pôle Travail** | Même appli et **mêmes données** sur le téléphone et l'ordinateur. Offres d'emploi, recherche par mot-clé, candidatures (le recruteur est notifié), suivi des candidatures, dépôt d'une recherche d'emploi (CV). Espace recruteur : publier une offre, voir les candidatures, accepter / refuser (le candidat est notifié), consulter les recherches d'emploi. **Publication d'offres et dépôt de CV sur l'ordinateur** (option pour l'autoriser aussi sur le téléphone). |
| 📱 Téléphone | **Capital Motors** | Catalogue de la concession, **commande de véhicule**, suivi de la commande, rendez-vous atelier. Espace concession (membres de l'entreprise) : accepter / refuser, ajouter une note, marquer « prête », **livrer** → le véhicule est créé directement dans le garage du client. Les commandes et RDV arrivent aussi **dans le salon Discord du bot Garage / Concession, avec le bouton « 🎫 Je me charge du ticket »**. |
| 📱 + 💻 | **LeBonNeo** | Petites annonces façon Leboncoin : catégories, recherche, dépôt d'annonce (titre, prix, description, contact), messagerie acheteur ↔ vendeur avec notifications, « vendu », remonter son annonce (1 fois / 24 h), expiration automatique. Option : annonce des nouvelles annonces sur Discord. |
| 📱 + 💻 | **Police** | **Citoyens** : appeler le 17 (alerte immédiate aux agents en ville), porter plainte et suivre sa plainte, voir et **payer ses amendes**, consulter les avis de recherche. **Agents** (membres de l'entreprise police) : prendre les appels, traiter / classer les plaintes, dresser une amende, émettre / lever un avis de recherche (alerte à toute la ville), **fichier des personnes** avec casier, notes internes, amendes impayées (sur ordinateur par défaut). |
| 📱 + 💻 | **Journal RP** | Journal du serveur (nom configurable, « Le Nova Matin » par défaut) : article à la une, rubriques, recherche, commentaires. **Journalistes** (membres de l'entreprise du journal) : écriture d'articles paragraphe par paragraphe (sur ordinateur par défaut), publication normale ou en **flash info** (notification à toute la ville). Option : publication sur Discord. |
| 📱 Téléphone | **Météo** | Temps actuel, température, vent et prévisions des 5 prochaines heures. Par défaut le plugin **pilote la vraie météo du serveur** selon ses prévisions : elles sont donc toujours justes. Alerte quand un orage arrive. Les météorologues (entreprise configurée) peuvent modifier les prévisions. |
| 📱 Téléphone | **Agenda** | Événements de la ville : créer (nom, date, lieu, description, organisateur), « Je participe », **rappel automatique** aux participants 15 min avant, annulation avec notification. Les événements créés par un admin sont « officiels » et annoncés à tous. |
| 💻 Ordinateur | **Discord** | Lit les **vrais salons** de ton serveur Discord et permet d'y **écrire depuis le jeu** (sous le nom RP du joueur si tu configures un webhook). Affiche le lien d'invitation. |
| 💻 Ordinateur | **Blue Lightfox** | Navigateur avec de **vraies recherches Internet** : Google si tu configures une clé API, sinon Wikipédia (gratuit, sans clé). Ouvre les résumés d'articles, envoie le lien dans le chat. Propose aussi les « sites » du serveur (pole-travail.nl, capital-motors.nl, sites perso dans la config). |

### Ouvrir le téléphone / l'ordinateur

- Téléphone : touche **F9** (la même que le téléphone du jeu) ou commande **/tel**
- Ordinateur : touche **F2** (la même que l'ordinateur du jeu) ou commande **/ordi**
- Les applis **s'ouvrent en même temps que le téléphone / l'ordinateur du jeu** et **se referment avec lui** quand on rappuie sur la touche. Si les deux se décalent (par exemple après avoir fermé avec Échap), appuie une fois de plus.
- Les touches se changent dans `config.json` (`ToucheTelephone`, `ToucheOrdinateur`). ⚠️ Si ton `config.json` existait déjà, il garde les anciennes touches F7 / F8 : remplace-les par F9 et F2.
- `EcranDeVerrouillage` : `false` par défaut, puisque le téléphone du jeu affiche déjà l'heure. Mets `true` pour avoir l'écran de verrouillage du plugin.
- Par défaut l'ordinateur s'ouvre partout. Pour qu'il ne marche **que devant un vrai PC du jeu**, place-toi devant un ordinateur et tape **/ordi_ajouter** (admin). **/ordi_retirer** supprime le plus proche.
- **/ordi_ajouter 5** réserve ce PC aux membres de l'entreprise 5 : par exemple les PC du commissariat pour la police, ceux de la rédaction pour le journal. Les autres joueurs voient « Session verrouillée : mot de passe requis ».

### Réalisme

- **Écran de verrouillage** : à l'ouverture, le téléphone affiche l'heure, la date, la météo et les **notifications en attente** de chaque appli (mentions N, messages Neonous / LeBonNeo, candidatures, amendes, appels 17 pour les agents, nouveaux articles, événements du jour). On le **déverrouille** pour arriver sur l'écran d'accueil.
- **Écran d'accueil** : barre de statut (heure, réseau, batterie) et **pastilles** `(3)` à côté de chaque appli qui a du nouveau.
- **Il faut un téléphone sur soi** : mets l'ID de l'objet téléphone de ton serveur dans `ObjetsTelephone`. Pour le trouver, garde ton téléphone sur toi et tape **/tel_objets** (admin) : la liste des objets de ton inventaire s'affiche avec leurs ID. Si l'inventaire ne peut pas être lu sur ta version du jeu, la vérification est désactivée (personne n'est bloqué) et un message apparaît dans la console.
- **En prison**, le téléphone est confisqué (`TelephoneConfisqueEnPrison`).
- **À terre** (0 % de vie), impossible d'utiliser le téléphone ou l'ordinateur (`BloquerSiAterre`).

> ⚠️ **Ce qu'un plugin peut faire et ne peut pas faire.**
> Les plugins Nova-Life tournent sur le serveur. Ils ne peuvent pas modifier l'écran du téléphone d'origine du jeu, ni afficher une vraie page web.
> Ces applis utilisent donc les fenêtres de plugin du jeu (listes, textes, saisies), ouvertes par touche ou par commande.
> Pour la même raison, Blue Lightfox fait de vraies recherches (Google / Wikipédia) mais affiche des **titres, extraits et résumés en texte**, pas les sites eux-mêmes.

---

## 1. Compiler le plugin

Prérequis : Nova-Life installé via Steam, et le [SDK .NET](https://dotnet.microsoft.com/download) (8 ou plus) **ou** Visual Studio.

1. Ouvre `NovaApps.csproj` et vérifie la ligne `NovaManaged` : elle doit pointer vers le dossier
   `...\steamapps\common\Nova-Life\Nova-Life_Data\Managed` de ton PC.
2. Dans un terminal, dans le dossier `plugin-novalife` :
   ```bash
   dotnet build -c Release
   ```
   (ou `dotnet build -c Release -p:NovaManaged="D:\SteamLibrary\steamapps\common\Nova-Life\Nova-Life_Data\Managed"`)
3. Récupère `bin/Release/net48/NovaApps.dll`.

> Le plugin n'utilise que l'API de base du jeu (pas besoin de ModKit ni d'AAMenu).

## 2. Installer sur le serveur

1. Copie `NovaApps.dll` dans le dossier `Plugins` de ton serveur.
2. Démarre le serveur une fois : le dossier `Plugins/NovaApps/` est créé avec `config.json` et les fichiers de données
   (`n.json`, `neonous.json`, `lebonneo.json`, `pole-travail.json`, `capital-motors.json`, `police.json`, `journal.json`, `agenda.json`).
3. Arrête le serveur, remplis `config.json` (voir ci-dessous) et redémarre.

## 3. Configuration (`Plugins/NovaApps/config.json`)

### Capital Motors
```json
"CapitalMotors": {
  "Nom": "Capital Motors",
  "EntreprisesConcession": [12],
  "CreerVehiculeALaLivraison": true,
  "SalonDiscordCommandes": "ID_DU_SALON_RECEPTION",
  "Catalogue": [ { "ModelId": 3, "Nom": "Sultan RS", "Prix": 45000 } ]
}
```
- `EntreprisesConcession` : ID de l'entreprise (ou des entreprises) de la concession en jeu. Ses membres ont accès à l'**Espace concession**. Les admins y ont toujours accès.
- Au premier lancement, **tous les véhicules du jeu** sont ajoutés au catalogue avec « prix sur devis ». La concession peut ensuite modifier les prix et retirer des modèles **en jeu** : *Espace concession → Modifier le catalogue*.
- `SalonDiscordCommandes` : mets le même ID que `SALON_RECEPTION_ID` du bot pour recevoir les commandes là où arrivent déjà les formulaires Discord. Il faut aussi remplir `Discord.TokenBot`.

### Discord (appli de l'ordinateur + liaison avec le bot)
```json
"Discord": {
  "TokenBot": "LE_MEME_TOKEN_QUE_DISCORD_TOKEN_DU_BOT",
  "Invitation": "https://discord.gg/xxxx",
  "Salons": [
    { "Nom": "général", "Id": "123456789", "Webhook": "https://discord.com/api/webhooks/...", "LectureSeule": false },
    { "Nom": "annonces", "Id": "987654321", "Webhook": "", "LectureSeule": true }
  ],
  "MessagesAffiches": 15
}
```
- **Utilise le token du bot Garage / Concession** (`DISCORD_TOKEN` du `.env`). Le bouton « Je me charge du ticket » des commandes passées en jeu est alors géré par ce bot, comme pour les formulaires Discord.
- Le bot doit voir les salons listés (permissions *Voir le salon* + *Lire l'historique*).
- Pour **lire** le contenu des messages, active **Message Content Intent** : [Discord Developer Portal](https://discord.com/developers/applications) → ton appli → **Bot** → *Privileged Gateway Intents*.
- `Webhook` (facultatif) : paramètres du salon → Intégrations → Webhooks → *Copier l'URL*. Avec un webhook, les messages envoyés depuis le jeu apparaissent sous le **nom RP** du joueur. Sans webhook, le bot poste « **Prénom Nom** (en jeu) : message ».
- ⚠️ `config.json` contient le token : ne le partage jamais.

### Blue Lightfox (recherche web)
- Rien à faire : les recherches passent par **Wikipédia** par défaut.
- Pour de **vrais résultats Google** : crée un moteur sur <https://programmablesearchengine.google.com> (option « Rechercher sur tout le Web ») pour obtenir l'**ID du moteur (cx)**, puis une clé API *Custom Search JSON API* sur <https://console.cloud.google.com>. Remplis `GoogleCleApi` et `GoogleMoteurId`. Google offre un quota gratuit limité de requêtes par jour : vérifie les conditions actuelles sur leur site.
- `Sites` : ajoute des « sites internes » au serveur (mairie, entreprises, journal RP...) avec des mots-clés. Ils apparaissent sur la page d'accueil et en tête des résultats.

### Pôle Travail
- `PublicationDepuisTelephone` : `false` par défaut (publication d'offres et dépôt de CV seulement sur ordinateur). Mets `true` pour l'autoriser aussi sur le téléphone.
- `WebhookDiscord` (facultatif) : chaque nouvelle offre est annoncée dans ce salon Discord.

### N
- `WebhookDiscord` (facultatif) : chaque post N est recopié dans ce salon Discord (« fil N » visible hors jeu).
- `LongueurMax`, `PostsAffiches` : taille max d'un post et nombre de posts dans le fil.

### LeBonNeo
- `DureeJours` : nombre de jours avant qu'une annonce disparaisse des résultats (14 par défaut). Le vendeur peut la remonter.
- `Categories` : liste des catégories proposées.
- `WebhookDiscord` (facultatif) : salon Discord où annoncer les nouvelles annonces.

### Police
```json
"Police": { "Nom": "Police Nationale", "EntreprisesPolice": [5], "AmendeMax": 10000, "FichierSurOrdinateurUniquement": true }
```
- `EntreprisesPolice` : ID de l'entreprise de la police en jeu. Ses membres ont les menus **[Agent]**.
- Les amendes se paient **par la banque** depuis l'appli (l'argent en poche du jeu passe par des porte-monnaie) : l'argent est retiré du compte du joueur et l'agent est prévenu.
- `FichierSurOrdinateurUniquement` : le casier / les notes ne se consultent que sur un ordinateur du commissariat (pense à `/ordi_ajouter` devant les PC).

### Journal
```json
"Journal": { "Nom": "Le Nova Matin", "EntreprisesJournal": [8], "RedactionSurOrdinateurUniquement": true, "WebhookDiscord": "" }
```
- `EntreprisesJournal` : ID de l'entreprise du journal en jeu. Ses membres peuvent écrire des articles.
- `Rubriques` : liste des rubriques proposées.
- Les champs de saisie du jeu étant courts, un article s'écrit **paragraphe par paragraphe**. Le brouillon reste en mémoire tant que le serveur tourne.

### Météo
```json
"Meteo": { "Ville": "Amboise", "PiloterLaMeteo": true, "DureeCreneauMinutes": 60, "Probabilites": { "ClearSky": 40, "Cloudy1": 25, "Foggy": 8, "LightRain": 15, "HeavyRain": 8, "Storm": 4 }, "EntreprisesMeteo": [] }
```
- `PiloterLaMeteo` : `true` = le plugin change la météo du serveur à chaque créneau (prévisions exactes). Mets `false` si un autre plugin gère déjà la météo : les prévisions seront alors seulement indicatives.
- `Probabilites` : chances de chaque temps (noms de la météo du jeu). Mets 0 pour supprimer un temps (par ex. `"Storm": 0`).
- `EntreprisesMeteo` : entreprises autorisées à modifier les prévisions (par ex. le journal pour un présentateur météo).

### Agenda
- `CreationReserveeAuxEntreprises` : `true` pour que seuls les membres d'une entreprise puissent créer un événement.
- `RappelMinutes` : délai du rappel avant le début (15 par défaut).
- Dates acceptées : `25/10 21:30`, `25/10/2027 21h`, ou juste `21:30` / `21h` (aujourd'hui, ou demain si l'heure est passée).

## Modération

- Les admins peuvent supprimer n'importe quel post N, annonce LeBonNeo, article ou événement, et gérer toutes les offres Pôle Travail (*Espace recruteur → Mes offres*). Ils ont aussi accès aux menus Police, Journal, Météo et Capital Motors.
- Les textes tapés par les joueurs sont nettoyés : impossible d'y glisser des balises de couleur / taille. Personne ne peut faire un `@everyone` sur Discord depuis le jeu.
- Toutes les données sont des fichiers JSON lisibles dans `Plugins/NovaApps/`. Fais-en une copie de sauvegarde de temps en temps.
