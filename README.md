# 🤖 Bot Discord – Garage & Concession

Ce bot affiche deux boutons dans deux salons différents :

| Salon | Bouton | Pop-up |
|---|---|---|
| Rendez-vous garage | 🔧 Prendre rendez-vous | Nom RP, Véhicule, Problème / service souhaité, Disponibilités |
| Commande de véhicule | 🛒 Commander un véhicule | Nom RP, Véhicule souhaité, Couleur / Options, Budget, Disponibilités pour la livraison |

Chaque formulaire rempli arrive dans un **salon de réception** (par exemple un salon réservé au staff), avec la mention de la personne qui l'a envoyé.
Sous chaque demande, un bouton **« 🎫 Je me charge du ticket »** permet à un membre du staff de prendre la demande : le bouton devient gris, affiche son nom, et la personne est mentionnée dans le salon. Une demande ne peut être prise que par une seule personne.

> ℹ️ Discord limite un pop-up à **5 champs**. Le formulaire de commande en a 6 : « Couleur » et « Options » sont donc regroupés dans un seul champ.

---

## 1. Créer le bot sur Discord

1. Va sur <https://discord.com/developers/applications> → **New Application**.
2. Onglet **Bot** → **Reset Token** → copie le token (garde-le secret !).
3. Onglet **OAuth2 → URL Generator** :
   - Scopes : `bot`
   - Bot Permissions : `View Channels`, `Send Messages`, `Embed Links`, `Read Message History`
4. Ouvre l'URL générée et invite le bot sur ton serveur.

## 2. Récupérer les ID des salons

Dans Discord : **Paramètres utilisateur → Avancés → Mode développeur** (activer).
Ensuite clic droit sur un salon → **Copier l'identifiant du salon**.

Il te faut 3 ID : le salon rendez-vous, le salon commande, et le salon de réception.

## 3. Installer et lancer dans Visual Studio Code

Prérequis : [Node.js](https://nodejs.org) version 18 ou plus.

1. Ouvre le dossier du projet dans VS Code (**Fichier → Ouvrir le dossier**).
2. Ouvre le terminal (**Terminal → Nouveau terminal**) et tape :
   ```bash
   npm install
   ```
3. Copie le fichier `.env.example`, renomme la copie en `.env` et remplis-la :
   ```env
   DISCORD_TOKEN=ton_token_ici
   SALON_RDV_ID=123456789012345678
   SALON_COMMANDE_ID=123456789012345678
   SALON_RECEPTION_ID=123456789012345678
   ```
4. Lance le bot :
   ```bash
   npm start
   ```

Au démarrage, le bot publie automatiquement le message avec le bouton dans chacun des deux salons. S'il est déjà présent, il ne le republie pas.
Le bot doit rester allumé pour que les boutons fonctionnent.

## Personnaliser

Les textes, les questions et les couleurs se modifient dans `src/index.js`, dans l'objet `FORMULAIRES` en haut du fichier.
Si tu modifies le texte du panneau, supprime l'ancien message du bot dans le salon puis relance le bot pour qu'il republie la nouvelle version.
