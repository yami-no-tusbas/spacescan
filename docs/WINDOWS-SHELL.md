# Menu Shell Windows

Le clic droit et la touche Menu/Maj+F10 des listes dossiers, gros fichiers, recherche et doublons demandent les commandes à IShellFolder.GetUIObjectOf(IContextMenu). Windows construit le HMENU via QueryContextMenu. TrackPopupMenuEx affiche le menu natif, InvokeCommand reçoit l'identifiant relatif et CMINVOKECOMMANDINFOEX Unicode. Les messages WM_INITMENUPOPUP, WM_DRAWITEM, WM_MEASUREITEM et WM_MENUCHAR sont transmis à IContextMenu3/2 pour les sous-menus dynamiques et le dessin des extensions.

Les opérations originales restent dans le sous-menu SpaceScan et par leurs raccourcis. Après une mutation externe (renommer, supprimer, déplacer), F5 réanalyse le dossier sélectionné : SpaceScan ne suppose pas qu'une commande tierce est synchrone ou qu'elle a réussi.

## Limites connues

- Contrat classique IContextMenu, équivalent au menu classique / « Afficher plus d'options », pas au panneau moderne Windows 11. Une extension exclusivement IExplorerCommand/MSIX moderne n'est pas garantie dans ce contrat ; utiliser « Show in Explorer » puis le menu de l'Explorateur. Pas d'émulation de commandes tierces.
- Everything : l'entrée classique « Search Everything… » doit être activée dans les options d'Everything pour les dossiers. Elle est découverte par le Shell, sans recherche du programme ni commande Everything codée en dur. L'extension moderne seule ne garantit pas l'entrée classique.
- Un processus x64 charge les extensions COM x64. Une extension uniquement x86 ne se charge pas dans ce processus ; utiliser Explorer / la version x64 du fournisseur.
- Les objets d'une sélection doivent avoir le même parent pour GetUIObjectOf (PIDL enfants). Une sélection de recherche/doublons couvrant plusieurs dossiers affiche un repli explicitement nommé vers Explorer, avec les opérations SpaceScan conservées. Le repli ouvre un Explorer par parent, sélectionnant le premier élément de chaque groupe ; il ne prétend pas fournir un menu Shell pour toute la sélection.
- Le menu s'exécute avec le jeton de SpaceScan. Sous élévation, les extensions et applications qui refusent ce contexte peuvent être absentes ou échouer. Repli : SpaceScan non élevé / Explorer habituel. Ne jamais désactiver l'UAC ou enregistrer une extension pour contourner cela.
- Une extension COM tierce chargée dans le processus peut bloquer ou planter l'application : les exceptions gérées sont rapportées, les violations mémoire natives ne sont pas isolées. Les menus ne sont créés qu'à la demande, jamais pendant un scan ou par ligne.
- Les menus et dialogues Shell suivent Windows et les règles de l'extension, pas une imitation du thème de l'application.

## Références techniques

- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellfolder-getuiobjectof
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu3
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ns-shobjidl_core-cminvokecommandinfoex
- https://www.voidtools.com/support/everything/installing_everything/
- https://github.com/voidtools/everything_shell_extension
