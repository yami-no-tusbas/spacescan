# SpaceScan personnel — Windows x64

Fork de [SpaceScan par limburatorul / Protagonist Labs](https://github.com/limburatorul/spacescan), sous licence MIT. Le copyright et la licence originale sont conservés dans LICENSE et NOTICE.txt.

## Utilisation

Extraire toute l'archive, puis lancer `SpaceScan.exe`. Aucun installateur ni téléchargement de dépendances n'est nécessaire sur Windows 11 x64 avec le .NET Framework 4.x fourni par Windows. Le binaire n'est pas signé. Il s'agit d'une version personnelle, distincte des distributions officielles.

- **Scan / Browse / Drives** : mêmes modes d'analyse qu'en amont. Lire directement la MFT d'un volume NTFS nécessite les droits administrateur ; les dossiers sont parcourus classiquement.
- **Folders** : arborescence aplatie et virtualisée, lignes de 21 pixels, barres de taille, taille, pourcentage et nombre de fichiers.
- **Appearance** : Light, Dark ou Automatic. Le dernier suit `AppsUseLightTheme` de Windows, y compris ses changements pendant l'exécution. Les fenêtres de l'application et menus secondaires partagent la palette ; les dialogues et extensions Shell restent dessinés par Windows / leur fournisseur.
- **Columns** : masquer ou afficher les colonnes de la vue active et régler leurs largeurs en pixels. Laisser vide pour une largeur automatique ; Reset rétablit cette vue. La colonne du nom reste visible. L'ordre des colonnes reste fixe ; choisir des largeurs adaptées à la fenêtre (le défilement horizontal est désactivé comme dans l'amont).
- **Clic droit / touche Menu / Maj+F10** : véritable menu classique du Shell Windows, extensions installées et sous-menus. Les opérations historiques sont disponibles dans **SpaceScan**. Les raccourcis Entrée, F5, Suppr, Maj+Suppr et Ctrl+C sont conservés.
- **Search, Duplicates, File types, Compare, filtres et exports HTML/CSV** : fonctions amont conservées. Les rapports exportés gardent la présentation de l'amont.
- **About** : attribution, licence, source amont et lien facultatif de soutien. Aucun minuteur promotionnel ni condition d'utilisation.

Après une opération lancée depuis une extension Shell, sélectionner le dossier concerné puis F5 : les extensions ne garantissent pas toutes une exécution synchrone et SpaceScan ne devine pas leurs effets.

## Données et coexistence

Les réglages, exclusions et instantanés du fork sont dans `%APPDATA%\SpaceScanFork`, séparés de l'installation officielle. Le fork ne migre pas les données amont, ne repointe pas automatiquement ses associations et ne s'inscrit pas comme l'exécutable officiel dans les cartes partagées. Les intégrations Shelf, Backup Labs et File Labs restent utilisables lorsqu'elles sont installées.

Le bouton d'intégration dans Settings peut ajouter volontairement « Scan with SpaceScan (personal) » dans l'Explorateur ; les clés `SpaceScanFork` sont distinctes des clés officielles. L'installation officielle n'est pas modifiée lors des essais automatisés : `SPACESCAN_TEST_ROOT` isole les réglages des tests.

Le contrôle automatique des mises à jour officielles a été retiré du démarrage : il pouvait remplacer le fork par une version amont. « Upstream releases… » ouvre leur page pour consultation. Ce fork ne contient pas de service de mise à jour automatique propre.

## Compilation et tests

Depuis le dossier du dépôt, avec Windows PowerShell :

```powershell
.\build-portable.ps1
.\check.ps1 -SkipMft
.\check-fork.ps1
.\check-fork.ps1 -Shell
.\check-fork.ps1 -Shell -Elevated -Engine
```

`build.cmd` compile avec `Framework64\v4.0.30319\csc.exe`, C# 5, WPF, `/optimize /platform:x64`. Pas de SDK .NET récent ni NuGet requis. `build-portable.ps1` contrôle le type AMD64 du PE et produit le dossier et l'archive `dist\SpaceScan-personal-1.4.4-win-x64`, avec licence, notices, documentation, provenance et sommes SHA-256. Les anciens scripts d'installateur amont sont conservés mais ne constituent pas la livraison de ce fork.

`check.ps1` couvre les fonctions historiques et, en administrateur sans `-SkipMft`, le contrôle MFT historique. **Ce contrôle sur C:\Windows échoue aussi sur l'amont sur cette machine** : voir VALIDATION.md. L'échec n'est pas neutralisé dans les tests.

`check-fork.ps1` vérifie les palettes, fenêtres déjà ouvertes, persistance/corruption des réglages, colonnes et virtualisation de 100 000 lignes. `-Shell` construit les menus réels puis les annule, sans exécuter leurs opérations. `-Elevated` affiche une invite UAC. `-Engine` ajoute les contrôles amont sous le même jeton administrateur. Des logiciels tiers peuvent ajouter des commandes ou modifier le résultat selon le poste.

Le benchmark de rendu reproductible est `check-perf.ps1 -BaselineExe <chemin-vers-exe-amont>`. Le contrôle MFT synthétique avec liens physiques est dans `tests\MftChecks.cs` et son script `check-mft.ps1` ; il nécessite un volume temporaire NTFS et les droits administrateur.

Consulter [WINDOWS-SHELL.md](WINDOWS-SHELL.md) pour les limites des extensions et [VALIDATION.md](VALIDATION.md) pour les essais réalisés et restant manuels.
