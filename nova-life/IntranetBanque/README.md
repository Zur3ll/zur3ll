# IntranetBanque – plugin Nova-Life : Amboise

Relie la tablette (Neoran Intranet) à la vraie banque du jeu :

- l'app **Banque** de la tablette affiche le vrai compte en banque du personnage et permet des virements entre joueurs ;
- les entreprises de **Gestion entreprise** reliées à une entreprise du jeu affichent sa vraie banque, et leurs virements et salaires sont payés en jeu.

## Fonctionnement

Toutes les 10 secondes (réglable), le plugin :

1. demande au site les virements en attente (`/api/jeu/travail`) ;
2. les fait en jeu : il débite la banque de l'entreprise ou du joueur, puis crédite celle du destinataire. Le joueur connecté reçoit un message dans le chat ;
3. renvoie le résultat de chaque virement, les soldes des entreprises et des personnages reliés, et la liste des entreprises du jeu (`/api/jeu/resultat`).

Le site n'a jamais accès directement au jeu : il ne fait que proposer des virements, et c'est le plugin qui vérifie les soldes en jeu.

## Relier un personnage

Dans l'app **Banque**, le joueur clique sur « Obtenir mon code », puis tape en jeu :

```
/intranet CODE
```

Le code est valable 15 minutes. Un personnage ne peut être relié qu'à un seul compte.

## Installation

1. Dans le `.env` du site, ajoute `PLUGIN_CLE=` suivi d'une longue phrase secrète (au moins 32 caractères au hasard).
2. Ouvre `IntranetBanque.csproj` dans Visual Studio ou VS Code, vérifie le chemin `NovaLifeRefs`, puis lance `dotnet build -c Release`.
3. Copie `bin/Release/net472/IntranetBanque.dll` dans `Servers/NovaLifeServer/Plugins` sur OxygenServ, puis redémarre le serveur.
4. Ouvre `Plugins/IntranetBanque/config.json`, qui vient d'être créé, et renseigne :

| Clé | Rôle |
|---|---|
| `UrlIntranet` | Adresse du site, sans `/` à la fin (ex. `https://intranet.neoran.fr`) |
| `CleSecrete` | La même valeur que `PLUGIN_CLE` du `.env` du site |
| `IntervalleSecondes` | Fréquence des échanges avec le site (10 par défaut, 5 minimum) |

5. Redémarre le serveur. Dans la tablette, Gestion entreprise → Paramètres doit maintenant proposer la liste des entreprises du jeu.

Si la console du serveur affiche `[IntranetBanque] Synchronisation impossible`, vérifie l'adresse du site, la clé, et que le serveur de jeu peut accéder à internet.
