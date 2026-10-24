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

1. Ouvre `IpPlugin.csproj` et remplace le chemin `NovaLifeManaged` par le dossier `..._Data\Managed` de **ton** serveur (celui qui contient `Assembly-CSharp.dll`).
2. Dans un terminal, dans ce dossier :
   ```bash
   dotnet build -c Release
   ```
3. Le fichier `bin/Release/net48/IpPlugin.dll` est créé.

## Installer

1. Copie `IpPlugin.dll` dans le dossier `Plugins` de ton serveur.
2. Redémarre le serveur. Le message `[IpPlugin] Plugin chargé` apparaît dans la console.
3. En jeu, en tant qu'admin : `/ip`.

## Personnaliser

- **Rayon** : change `private const float Rayon = 50f;` dans `IpPlugin.cs`.
- **ID affiché** : même format que l'ID au-dessus de la tête des joueurs, `(ID du compte:ID du personnage)`, par ex. `(1:3)`. Modifiable dans la fonction `IdDuJoueur`.
- **Ouvrir à tout le monde** : supprime le bloc `if (!player.IsAdmin) { ... }` (déconseillé).
