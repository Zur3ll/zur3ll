# Déploiement sur Infomaniak (hébergement Web, PHP)

Pas de Node.js, pas de commande de lancement : le site fonctionne dès qu'il est uploadé sur l'hébergement.

## 1. Préparer le fichier .env (sur ton ordinateur)

Copie `.env.example` en `.env` et mets ton mot de passe dans `ADMIN_MDP`.
Ne mets pas ce fichier dans le ZIP ni sur Git : il contient le mot de passe.

## 2. Envoyer les fichiers

Dans le Manager Infomaniak, ouvre ton hébergement Web et le dossier du site (par exemple celui du domaine). Envoie-y :

- `index.html`
- `api.php`
- `.htaccess`
- le dossier `data/` (avec son `.htaccess`)
- `.env` (à la racine, à côté de `api.php`)

Le dossier `data/` est créé automatiquement si tu ne l'envoies pas, mais son `.htaccess` doit exister pour bloquer l'accès direct.
Envoi possible par le gestionnaire de fichiers du Manager, ou par SFTP/SSH.

Vérifie que les fichiers cachés (`.htaccess`, `.env`) sont bien envoyés : ils sont souvent masqués par défaut dans les clients FTP/SFTP.

## 3. Activer HTTPS

Active le certificat SSL du domaine dans le Manager, puis utilise l'adresse en `https://`.

## 4. Tester

1. Ouvre le site dans le navigateur. La tablette doit s'afficher.
2. Ouvre l'app Staff et connecte-toi avec `admin` et ton `ADMIN_MDP`.
3. Vérifie que `https://ton-domaine/.env` et `https://ton-domaine/data/staff.json` ne renvoient pas leur contenu (erreur 403 ou 404). Si l'un des deux s'affiche, ne mets pas le site en ligne tel quel et demande au support Infomaniak de vérifier que les `.htaccess` sont bien pris en compte.

## Dépannage

- **« Serveur de l'intranet injoignable »** : vérifie que `api.php` et `.htaccess` sont à la racine du site, et que le SSL est actif.
- **Erreur 500 à la connexion** : vérifie le fichier `.env` (présence, `ADMIN_MDP` d'au moins 8 caractères) et le journal d'erreurs du site dans le Manager.

## Sauvegarde

Le dossier `data/` contient le contenu et les comptes staff. Sauvegarde-le avant tout redéploiement.
