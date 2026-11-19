# État initial — 19 novembre 2026

Source : https://github.com/limburatorul/spacescan, master a33675a3b6182327912002f6166a2359376defd6 (1.4.4).

- C# 5, WPF, .NET Framework 4.x livré avec Windows ; compilation directe par csc Framework64, sans NuGet.
- SpaceScan.cs : Scanner, Mft, Finder, Dupes, Snapshot, Comparer, Report, Cli. Aucune réécriture du moteur prévue.
- App.cs : interface, collections, intégrations, opérations fichiers, mise à jour amont. MainWindow.xaml : styles et vues.
- Arborescence aplatie dans une ListBox avec virtualisation Recycling ; préserver ce modèle et les limites existantes.
- Colonnes fixes, thème sombre à couleurs codées en dur, acrylic DWM, arrondis et ombres.
- Menu fichiers WPF codé en dur : aucune extension Shell tierce actuellement chargée.
- Bouton pizza permanent avec minuteur de double pulsation toutes les dix secondes.
- check.ps1 : scan synthétique et chemins longs, statistiques, recherche, doublons, exclusions, instantanés, intégrations, mises à jour et migration ; section MFT uniquement administrateur et comparaison sur C:\Windows.
- build.cmd produit un exécutable AnyCPU ; le fork fournira un x64 portable explicite avec licence.
- Licence MIT, copyright Protagonist Labs à conserver, attribution à limburatorul.

## Découpage prévu

1. État initial et validation de référence.
2. Menu Shell natif IContextMenu/2/3 avec sous-menu SpaceScan ; repli explicite Explorer en cas d'échec ou de sélection de parents différents.
3. Interface compacte, palettes clair/sombre/automatique persistées, colonnes configurables et soutien dans À propos.
4. Vérifications, archive portable x64 et documentation des limites et essais manuels.

Les captures ont été reçues ensuite : l'organisation reprend une barre de commandes, des onglets simples et un tableau compact, sans reprendre de ressources de WizTree.

Les performances sur plusieurs dizaines de millions d'entrées et la compatibilité d'extensions tierces ne peuvent pas être déduites de la seule compilation : conserver les résultats réellement mesurés et une matrice d'essais manuels.

Référence compilée : check.ps1 passe tous ses contrôles synthétiques ; MFT ignoré car processus non administrateur.
