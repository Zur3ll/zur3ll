# 🌐 Plugin Nova-Life – Commande `/ip`

Quand un **admin** tape `/ip` dans le tchat, il voit tous les joueurs dans un rayon de **50 mètres** autour de lui, triés du plus proche au plus loin :

```
[IP] 2 joueur(s) dans un rayon de 50 m :
Jean Dupont | IP : 192.168.1.20 | ID : (1:3) | Distance : 3.4 m
Marie Durand | IP : 85.12.34.56 | ID : (4:7) | Distance : 27.9 m
```

> 🔒 La commande est réservée aux administrateurs : les adresses IP sont des données personnelles, un joueur normal qui tape `/ip` reçoit un message de refus.

## Compiler le plugin

Prérequis : [.NET SDK](https://dotnet.microsoft.com/download) (6 ou plus) et les fichiers du serveur Nova-Life.

1. Sur ton serveur Nova-Life, cherche le fichier `Assembly-CSharp.dll` (il est dans un dossier `..._Data\Managed`). Si ton serveur est chez un hébergeur, télécharge ce dossier via le FTP / gestionnaire de fichiers.
2. Crée un dossier `libs` à côté de `IpPlugin.csproj` et copies-y ces 4 fichiers :
   - `Assembly-CSharp.dll`
   - `Mirror.dll`
   - `UnityEngine.dll`
   - `UnityEngine.CoreModule.dll`
3. Dans un terminal, dans ce dossier :
   ```bash
   dotnet build -c Release
   ```
4. Le fichier `bin/Release/net48/IpPlugin.dll` est créé.

## Installer

1. Copie `IpPlugin.dll` dans le dossier `Plugins` de ton serveur.
2. Redémarre le serveur. Le message `[IpPlugin] Plugin chargé` apparaît dans la console.
3. En jeu, en tant qu'admin : `/ip`.

## Personnaliser

- **Rayon** : change `private const float Rayon = 50f;` dans `IpPlugin.cs`.
- **ID affiché** : même format que l'ID au-dessus de la tête des joueurs, `(ID du compte:ID du personnage)`, par ex. `(1:3)`. Modifiable dans la fonction `IdDuJoueur`.
- **Ouvrir à tout le monde** : supprime le bloc `if (!player.IsAdmin) { ... }` (déconseillé).
