# Validation du fork — 19 novembre 2026

Poste : Windows 11 Famille x64, build 26300. Référence amont : a33675a3b6182327912002f6166a2359376defd6 (1.4.4). Compilation avec le compilateur .NET Framework x64 installé dans Windows. Données de tests séparées, aucune suppression de fichiers utilisateur ni opération Shell destructive déclenchée.

## Résultats

| Vérification | Résultat et portée |
|---|---|
| Compilation amont puis fork x64 | Réussie. Le portable vérifie également le champ machine PE = 0x8664. |
| check.ps1, tests synthétiques historiques | Réussis avant et après modifications : scan, chemins longs, statistiques, recherche, doublons, exclusions, comparaison/instantanés, intégrations, gestion d'erreurs de mise à jour, migration et notes de version. |
| check.ps1, MFT sur C:\Windows | **Échec hérité reproduit dans l'amont intact et le fork**, détaillé ci-dessous. Le test n'est pas désactivé ni transformé en succès. |
| check-fork.ps1 | Réussi : clair/sombre/auto, palette dynamique, fenêtres déjà ouvertes, confirmation thémée fermée sans accepter une opération, réglages persistés et corrompus, colonnes, 100 000 lignes, défilement. |
| Shell sans élévation | Réussi : vrai menu affiché, Everything 1.5a présent pour les dossiers, sous-menu 7-Zip affiché, commande native Propriétés exécutée et dialogue vérifié visuellement. |
| Shell avec élévation | Réussi pour création, initialisation des sous-menus et annulation, dans un processus dont le jeton administrateur est vérifié. Everything 1.5a présent. Le pilote de clics externe ne peut pas agir dans cette fenêtre élevée ; le test s'exécute dans le processus élevé. |
| Sélections Shell | Menus obtenus pour un dossier, un fichier texte, deux fichiers frères et deux dossiers frères. Les sous-menus 7-Zip, Ouvrir avec et Envoyer vers ont reçu WM_INITMENUPOPUP et fourni leurs commandes. Aucun de ces verbes n'a été exécuté par les tests automatiques. |
| Sélection de parents différents | Routage vers le repli explicitement nommé ; invariant testé automatiquement. Parcours complet du repli à vérifier manuellement. |
| Clair/sombre et colonnes | Vérification visuelle sur une vraie fenêtre avec scan de dossiers synthétiques. Dialogue Columns sombre vérifié, masquage d'une colonne et alignement des en-têtes observés. |
| MFT contrôlé en administrateur | Réussi : noms Unicode, égalité scan/MFT sans liens physiques, exclusion d'un fichier exact, reproduction du modèle de comptage des liens physiques. |
| Portable extrait et exports | Réussi : lancement du binaire extrait, code de sortie 0, exports HTML et CSV et instantané sur le jeu synthétique ; licence originale identique. |
| Moteur et licence | SpaceScan.cs et LICENSE identiques octet pour octet à l'amont. Aucun chemin d'analyse, structure par fichier ni stratégie MFT modifiés. |

Les journaux et captures de travail restent dans `.validation/` (ignoré par Git), notamment baseline.log, engine-user.log, engine-admin.log, engine-upstream-admin.log, fork-final.log, shell-user.log, shell-admin.log, mft-controlled.log et perf-*.log. Ils ne sont pas embarqués car ils peuvent contenir des chemins et noms d'extensions propres au poste. Le présent rapport consigne les résultats utiles.

## Écart du test MFT historique

Le test amont vérifie une égalité à 2 % entre le sous-arbre Windows de la MFT et un parcours classique. Sur ce poste :

| Version | MFT Windows : fichiers / octets | Classique Windows : entrées / octets |
|---|---:|---:|
| Fork | 133 399 / 36 713 638 928 | 186 825 / 48 310 075 842 |
| Amont intact | 133 398 / 36 713 542 718 | 186 824 / 48 310 012 400 |

Les lectures sont prises à des moments différents sur un système actif : la petite différence amont/fork est attendue. L'écart MFT/classique important est présent dans les deux versions. La partie suivante du script historique (exclusion de explorer.exe) n'est pas atteinte après cette assertion.

Le contrôle initial des droits utilisait le nom anglais « Administrators », qui ne reconnaît pas correctement le groupe sur ce Windows français. Le script utilise désormais WindowsBuiltInRole.Administrator : c'est ce qui a permis d'exécuter réellement ce contrôle et de révéler l'échec.

Le moteur MFT conserve un nom et un parent par enregistrement, alors que le parcours classique compte les entrées de dossiers. Le contrôle synthétique avec un fichier de 777 octets et un second lien physique reproduit 4 entrées classiques contre 3 enregistrements MFT, avec exactement 777 octets d'écart. Cela démontre une différence de sémantique et contribue à expliquer les écarts sur Windows/WinSxS ; cela ne prouve pas que la totalité de l'écart C:\Windows se limite aux liens physiques. Une qualification exhaustive de ce volume reste nécessaire avant de certifier ces totaux.

Le test contrôlé sans lien physique donne 3 fichiers et 2 023 octets dans les deux méthodes. Une exclusion de fichier retire exactement 1 234 octets et une entrée de la MFT. Trois passages MFT du volume système ont pris respectivement environ 1 605, 1 757 et 1 436 ms sur ce poste (699 335 fichiers environ). Ces temps ne caractérisent pas un volume de dizaines de millions d'entrées.

## Rendu : comparaison indicative

Même poste, même taille de fenêtre, 100 000 objets de lignes synthétiques, vingt grands défilements par passe, une passe de chauffe puis médiane de trois passes :

| Mesure | Amont | Fork |
|---|---:|---:|
| Éléments de lignes matérialisés | 25 | 33 |
| Médiane de vingt défilements | 1 360 ms | 810 ms |
| Mémoire privée du processus de test | 239 079 424 octets | 249 589 760 octets |

Les lignes plus compactes rendent davantage de lignes visibles, ce qui explique en partie le nombre de contrôles différent. La mesure de mémoire comprend le banc de test et les objets synthétiques ; ce n'est pas la mémoire d'une analyse réelle. La machine reste utilisée par d'autres applications. Ces valeurs montrent un rendu borné et aucune dégradation de temps évidente dans ce scénario ; elles ne constituent pas une garantie de performance sur de très grands volumes. Aucun coût par entrée n'a été ajouté au moteur.

## Essais manuels encore nécessaires

- Sur un volume de plusieurs millions puis dizaines de millions d'entrées : durée MFT, pic mémoire, recherche, duplication, expansion de grands dossiers, tri et endurance ; comparer aux mêmes données et conditions amont. Ces volumes n'ont pas été qualifiés ici.
- Tester toutes les extensions effectivement utilisées : lancement d'Everything, choix d'une application, invocation d'un verbe tiers et comportement sous élévation. Leur présence et initialisation sont validées ici ; leur exécution complète dépend du fournisseur.
- Vérifier la sélection multiple de parents différents, le repli Explorer et un élément supprimé depuis le dernier scan.
- Tester les opérations destructives, copie/déplacement, annulation et réanalyse sur un volume jetable. Aucun fichier utilisateur n'a été modifié pour ces validations.
- Modifier réellement le thème Windows pendant que le mode Automatic et plusieurs fenêtres sont ouverts ; la détection du registre, la logique de palette et les fenêtres existantes sont testées, mais les réglages Windows de l'utilisateur n'ont pas été modifiés.
- Contrôler 100/150/200 % de mise à l'échelle, plusieurs moniteurs, haut contraste, clavier et lecteurs d'écran. Les menus Shell gardent la présentation imposée par Windows.
- Vérifier les fenêtres de sélection/export et les rapports depuis une autre machine Windows 11, sans dépendance de développement. Le portable a été compilé et son contenu/architecture vérifiés sur le poste de développement ; pas encore sur une machine propre.

## Limites assumées

Le menu est le menu classique natif IContextMenu, pas le panneau moderne Explorer Windows 11. Les extensions exclusivement modernes, x86, dépendantes d'Explorer ou incompatibles avec l'élévation peuvent manquer : voir WINDOWS-SHELL.md. Les extensions COM tierces restent dans le processus et peuvent le bloquer ou le faire planter.

Les colonnes sont configurables en visibilité et largeur ; leur ordre reste fixe et les largeurs doivent être adaptées à la fenêtre. Le texte de l'interface reste celui de l'amont (anglais). La refonte ne prétend pas proposer toutes les métriques de WizTree, notamment la taille allouée ou les attributs par fichier.
