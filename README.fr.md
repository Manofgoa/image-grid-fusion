[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](README.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Image Grid Fusion

Une petite application de bureau Windows, qui démarre vite, et fusionne 1 à 4 images en une seule image au format du fil d'actualité de Twitter/X — ou un carré, un portrait, une story, du 16:9 ou le format propre au contenu.

Chaque mot employé ici a un seul sens, donné dans le [glossaire](GLOSSARY.fr.md).

## Fonctionnalités

- `.exe` à démarrage rapide avec une interface graphique, sans installeur
- Fusionne 1 à 4 images en une seule ; une image seule remplit tout le canevas et peut être exportée
- Format de sortie à choisir parmi quelques formats — **Twitter** 1200:628 (≈1,91:1, le défaut), **Square** 1:1, **Portrait** 4:5, **Story** 9:16, **Landscape** 16:9, ou **Free**, le format propre au contenu ; c'est le format qui compte, pas la résolution (voir Format, Taille du canevas)
- Glisser-déposer des images sur l'icône du `.exe` ou sur la fenêtre, ou les coller avec `Ctrl+V`
- Coller ou déposer aussi un texte, depuis n'importe quelle application : il devient une image, rendue comme un fichier texte, avec son gras, son italique, son souligné, son barré et ses couleurs conservés (voir Texte collé)
- Pas seulement des images : les vidéos, les PDF, les fichiers texte et tout fichier dont Windows affiche une vignette sont transformés en image (voir Aperçus)
- Une zone de dépôt **Add images** à droite de l'aperçu : y déposer des fichiers les ajoute après les images actuelles, et un clic dessus permet de choisir des fichiers
  - Sans image, la grille vide fait la même chose : un clic permet de choisir des fichiers (un **+** et un curseur en forme de main montrent qu'elle est cliquable), on peut y déposer des fichiers, ou les coller
- Un panneau **explorateur de fichiers** à droite de l'aperçu : saisir quelques lettres pour trouver un fichier dans un dossier de base et ses sous-dossiers — depuis un index mis en cache à côté de l'exe, donc la réponse suit la frappe — ou saisir `*` pour parcourir tous les fichiers, le plus récent d'abord, ou les parcourir dossier par dossier avec 📁 ; les voir sous forme de tuiles de vignettes, chargées quelques écrans à la fois au fil du défilement, les mettre en favoris avec le cœur — ou déposer sur le panneau des fichiers depuis l'Explorateur, ou une cellule par sa poignée ✥ — et les faire glisser dans une cellule (voir Explorateur de fichiers)
- Plusieurs dispositions par nombre d'images, à choisir dans une bande de miniatures — d'autres sous son groupe **More** — plus une bascule miroir ; faire glisser le séparateur entre deux cellules pour les redimensionner (voir Dispositions)
- Pas de liste d'images : l'aperçu de la grille *est* l'interface
  - Cliquer sur une cellule pour la sélectionner, `Esc` pour la désélectionner ; les onglets d'effets agissent sur la cellule sélectionnée (voir Effets)
    - La cellule sélectionnée affiche, en vert fluo en bas à gauche, après une icône de dossier, le nom du fichier d'où vient son image — raccourci au milieu s'il est trop large, l'extension conservée ; le survoler donne le chemin complet. Jamais dans les exports
    - L'icône de dossier à gauche du nom ouvre l'Explorateur sur le dossier du fichier, le fichier sélectionné (fichier déplacé ou supprimé depuis : son dossier s'ouvre, s'il existe encore, et la barre d'état le dit)
    - Une image sans fichier affiche à la place la façon dont elle est arrivée, sans icône : *Pasted image*, *Pasted text* ou *Dropped text*
  - Survoler une cellule la met en contour et affiche un **×** pour la retirer, ou appuyer sur `Delete` pour retirer la cellule sélectionnée
  - Les vidéos, les GIF animés, les PDF de plusieurs pages et les longs textes se jouent en direct dans leur cellule (voir Contenu animé) ; l'effet **Frames** règle l'endroit où l'un démarre, ou le fige sur une image
    - Une **ligne de progression** vert fluo le long du bas de chaque cellule en lecture montre jusqu'où sa boucle a été jouée, en glissant de façon continue ; aucune sur un contenu figé. Jamais dans les exports
  - Les sons de toutes les vidéos sont mixés ; l'effet **Volume** règle chacun de 0 à 200 %, ou le coupe
  - Une **bande son** — le son d'un fichier audio ou vidéo — peut être mixée par-dessus, pour toute la grille (voir Global)
  - Un **fondu** fait entrer tout le mix depuis le silence au début de la vidéo, et le fait sortir vers le silence à sa fin (voir Fade)
  - Zoomer une cellule de 10 % à 2000 % (le maximum peut être changé, voir Fichier de paramètres) : avec le curseur de l'effet **Zoom** (il s'aimante sur 100 %), ou avec la molette de la souris au-dessus de n'importe quelle cellule, autour du point sous la souris (le passage par 100 % s'y arrête). Chaque cran fait varier le zoom de 5 %, sur les multiples de 5 : 103 % → 105 % → 110 %, ou 100 % dans l'autre sens ; avec **Ctrl** maintenu, de 1 %. Au-delà de 200 %, les pas passent à 25 % (5 % avec Ctrl) : 195 % → 200 % → 225 % → 250 %. La molette au-dessus du curseur Zoom prend les mêmes pas
  - Faire glisser une image pour la déplacer dans sa cellule, à n'importe quel zoom — y compris au-delà des bords de la cellule, pour centrer un détail situé sur la bordure de l'image ; la zone découverte prend la couleur des bandes (voir Règles d'ajustement), et au moins 10 % de la cellule reste toujours couverte pour pouvoir la rattraper
    - Arrêts magnétiques : l'image s'arrête quand l'un de ses bords s'aligne sur un bord de la cellule, et quand elle est centrée ; continuer à glisser d'environ 24 px pour dépasser un arrêt (revenir vers l'intérieur au-dessus d'un bord est libre). Tant qu'elle est tenue, un guide en pointillés vert fluo montre l'arrêt : le long du bord aligné, ou à travers le centre (les deux lignes se croisent quand elle est centrée dans les deux sens)
    - Maintenir `Shift` pendant le glissement pour ignorer les arrêts
  - Les **touches fléchées** déplacent de la même façon l'image sélectionnée, quel que soit l'onglet sélectionné, de 1 pixel d'écran par appui, 10 avec `Ctrl` (maintenir une touche la répète). Elles vont à l'image une fois l'aperçu cliqué ; un clic sur un curseur, une liste ou l'explorateur de fichiers les lui rend. Un arrêt magnétique retient l'image pendant un appui : elle s'arrête exactement dessus, le guide s'affiche, et l'appui suivant la quitte — un arrêt de bord aussi au retour vers l'intérieur ; ajouter `Shift` pour ignorer les arrêts. `Ctrl+Shift` + une flèche saute directement au prochain arrêt dans cette direction — le centre ou un bord, le premier rencontré — et y retient l'image avec son guide. Dans la vue d'édition du recadrage, elles ne déplacent pas l'image, comme un glissement
    - Zoomer garde l'image là où elle a été déplacée, à n'importe quel zoom, mais la ramène toujours dans ses arrêts
  - Pendant que le zoom change (molette ou curseur), son pourcentage s'affiche en vert fluo dans le coin supérieur droit de la cellule, juste sous le ×; il reste 1 s après le dernier changement, puis disparaît en fondu. Jamais dans les exports
  - Pendant qu'une image bouge dans sa cellule — glissée, poussée par les flèches, zoomée, tournée, recadrée, ou décalée par un séparateur, le format ou les bordures — sa **position** s'affiche en vert fluo en son centre : un point, un trait pointillé depuis le centre de la cellule, et son décalage par rapport à ce centre en pixels de l'export, `x -35px` au-dessus de `y +12px` (x vers la droite, y vers le bas), gardé dans la cellule. Elle reste tant que le bouton de la souris est enfoncé, puis 1 s après le dernier changement, puis disparaît en fondu. Pas dans la vue d'édition du recadrage ; jamais dans les exports
  - Zoomer et déplacer s'affichent en direct, lissés à la fin du geste (la molette : une fois qu'elle cesse de tourner) ; pas pendant un export
  - Faire glisser la poignée **✥** affichée au milieu d'une cellule survolée sur une autre cellule pour échanger les deux images (dans une petite cellule, elle rétrécit, ou se place sous le **×**)
  - Déposer un fichier ou un texte sur une cellule pour la remplacer
  - **Clear all** (en bas à gauche) retire d'un coup toutes les images et les effets globaux, et remet le format sur Twitter, sans confirmation, de retour à l'état initial
- **Annuler** avec `Ctrl+Z`, autant de fois que nécessaire, et **rétablir** avec `Ctrl+Y` ou `Ctrl+Shift+Z` : images ajoutées, remplacées, supprimées ou échangées, effets, disposition, séparateurs, format et effets globaux (voir Annuler et rétablir)
- Des effets par cellule, depuis les onglets d'effets en haut de la fenêtre (voir Effets), et le format et les effets globaux pour toute la grille, depuis les onglets Global en bas, au-dessus de la barre du bas (voir Global)
  - Des **Borders** (bordures) sur la grille, désactivées au démarrage : des crochets rose vif (hotpink) aux quatre coins, ou un espace entre les cellules dessiné en trait plein, tiretés, pointillé ou double, avec un cadre extérieur facultatif ; les coins de la grille arrondis comme Twitter / X affiche les images ; leur couleur se règle depuis le menu **⚙** et est mémorisée (voir Borders)
- Copier dans le presse-papiers (`Ctrl+C`) ou enregistrer (`Ctrl+S`) : un PNG, ou une vidéo MP4 quand la grille contient un contenu qui se joue ou qu'une bande son est activée ; la flèche ▾ à côté de chaque bouton force un GIF en boucle ou une vidéo MP4 ; celle de Copy propose aussi un JPEG léger pour partager dans les applications de messagerie qui limitent la taille des images (WhatsApp : 16 Mo)
- Vit dans la zone de notification : fermer la fenêtre ne fait que la masquer, l'icône de la zone de notification la ramène, et l'application peut démarrer avec Windows (voir Zone de notification et démarrage)
- La fenêtre mémorise sa taille d'un lancement à l'autre — jamais son état agrandi : elle s'ouvre non agrandie, centrée, à la taille qu'elle avait en dernier, réduite pour tenir sur un écran plus petit (voir Zone de notification et démarrage)

## Ajout d'images

- Tant qu'il reste des cellules libres, les nouvelles images les remplissent dans l'ordre.
- Une fois la grille pleine, une nouvelle image remplace la cellule sélectionnée, ou la dernière image (image 4) si aucune n'est sélectionnée.
- Ajout de plusieurs fichiers à la fois (coller, déposer, le sélecteur Add images, ou arguments de la ligne de commande) : les emplacements libres sont remplis d'abord, le premier fichier en trop applique la règle de remplacement ci-dessus, et tout excédent supplémentaire est ignoré, avec un message dans la ligne d'état.
- Un texte collé ou déposé est une seule image, placée selon les mêmes règles.

## Explorateur de fichiers

Un panneau repliable à droite de l'aperçu, ouvert au démarrage : une zone de recherche sur un **dossier de base** et ses sous-dossiers, et les fichiers trouvés sous forme de tuiles de vignettes, à faire glisser dans les cellules.

- **Tuiles** : chaque fichier est une tuile qui montre sa vignette — celle que Windows affiche dans l'Explorateur, chargée en arrière-plan et conservée pour la session, agrandie ou réduite pour remplir sa tuile — avec le cœur dans un médaillon à son coin et le nom du fichier en dessous. La **largeur du panneau** vous appartient : faire glisser son bord gauche (le séparateur entre l'aperçu et le panneau), l'aperçu lui cédant la place ; elle est mémorisée d'une session à l'autre. Les flèches se déplacent entre les tuiles, `Enter` ajoute la tuile sélectionnée.
- **Taille des tuiles** : le curseur au bas du panneau règle la taille des tuiles, de 100 à 1 000 px. Les rangées sont toujours pleines : autant de tuiles par rangée qu'il en tient à cette taille, étirées à la largeur de la rangée — élargir le panneau agrandit les tuiles, jusqu'à ce qu'une de plus tienne et qu'elles rétrécissent toutes d'un coup. Un résultat isolé reçoit la largeur qu'il aurait dans une rangée pleine, jamais toute la rangée. La molette de la souris au-dessus des tuiles fait défiler la liste ; **Ctrl + molette** déplace le curseur, de 15 % par cran, vers le haut pour des tuiles plus grandes. La taille est mémorisée d'une session à l'autre.
- **Dossier de base** : choisi depuis le menu **⚙** (**File explorer folder…**) et mémorisé d'une session à l'autre. La première recherche faite sans dossier de base affiche, à la place de la liste, une invitation avec un bouton **Choose folder…** qui fait la même chose.
- **Index** : tous les fichiers sous le dossier de base — entrées masquées et système ignorées — sont listés dans `files.index`, à côté de l'exe. Il est chargé au démarrage, donc la recherche fonctionne tout de suite ; puis le dossier est **réanalysé en arrière-plan**, à chaque lancement et avec le bouton **↻**, et la ligne d'état du panneau suit : *Counting… 1 234*, puis *Indexing… 5/346*, puis *346 files · indexed 21:03*. La date de création de chaque fichier est enregistrée avec son chemin, pour la liste `*`. La recherche ne lit jamais le disque.
- **Recherche** : à la frappe, **tous les résultats**, le meilleur d'abord. Chaque mot saisi doit apparaître dans le nom du fichier ou ses sous-dossiers, accents et casse ignorés : *ete* trouve *Été.jpg*, *vacances chat* trouve `Vacances 2025\chat.jpg`. Classement selon les mots trouvés dans le nom du fichier lui-même, puis un nom commençant par le premier mot avant un nom qui le contient, puis le nom le plus court. La légende indique combien de fichiers correspondent.
- **Tout** : saisir `*` seul pour lister **tous les fichiers de l'index**, le plus récemment créé d'abord — un fichier copié ou téléchargé hier arrive en premier, quelle que soit l'ancienneté de la photo. La légende indique *All files (12 345)*. Avec des mots (`* chat`), l'étoile est ignorée et la recherche est normale.
- **Chargement au défilement** : chaque liste — favoris, recherche, `*` — affiche ses tuiles quelques écrans à la fois : 2 par défaut, le choix **File explorer pages per load** du menu **⚙** allant de 1 à 10, mémorisé d'une session à l'autre. Le dernier emplacement d'un chargement est une tuile **Loading…** ; faire défiler jusqu'à elle (ou appuyer sur `↓`, `→`, `Page Down` ou `End` après la dernière tuile) et les écrans suivants se chargent, la première nouvelle vignette prenant sa place. Une fois la liste complète, plus aucune tuile Loading… ne reste. Charger davantage, la fin d'une réanalyse ou un cœur retiré des favoris laisse la liste où elle est ; seule une nouvelle recherche la ramène en haut.
- **Favoris** : cliquer sur le cœur au coin d'une tuile (♡ → ♥). Ils sont conservés dans `favorites.txt` à côté de l'exe, sous forme de chemins absolus. Tant que la zone de recherche est vide, la grille affiche tous les favoris, le plus récent d'abord ; pendant une recherche, ils sont seulement marqués, pas promus.
- **Dépôt de favoris** : déposer des fichiers sur le panneau — n'importe où dessus, résultats de recherche ou favoris, et sur sa bande repliée aussi — et ils deviennent des favoris, le dernier déposé étant le plus récent ; le panneau est encadré pendant le survol. N'importe quel fichier, même hors du dossier de base ; un dossier est ignoré ; un fichier déjà favori remonte tout en haut. Une recherche affichée reste, ses cœurs mis à jour. Une cellule fonctionne de la même façon : la faire glisser par sa poignée **✥** hors de la grille et la relâcher au-dessus du panneau — sans échange — et son fichier devient un favori. Les tuiles du panneau ne sont pas redéposées sur lui (leur cœur le fait), et un texte glissé depuis une autre application est refusé. Cela fonctionne aussi pendant un export.
- **Favoris collés** : une cellule sans fichier — une image collée, un texte collé ou déposé — est d'abord enregistrée dans `favorites-from-pasted\` à côté de l'exe, sous le nom `pasted-yyyyMMdd-HHmmss` (`-2`, `-3`… si le nom est pris) : l'image telle qu'elle a été collée, en PNG, sans les effets de la cellule ; un texte tel qu'il est venu, en `.rtf`, `.html` ou `.txt`, pour qu'il se relise avec ses styles et toutes ses pages. La cellule prend ensuite ce fichier pour sien : son nom s'affiche, avec l'icône de dossier qui ouvre l'Explorateur dessus, et le redéposer n'ajoute aucune copie. Retirer le cœur de l'un de ces favoris envoie son fichier dans la Corbeille — l'application l'a créé pour le favori ; retirer le cœur de tout autre favori laisse son fichier intact.
- **Dans la grille** : faire glisser une tuile sur une cellule pour remplacer son image, ou sur la zone **Add images** pour l'ajouter — exactement comme un fichier de l'Explorateur ; double-cliquer sur une tuile, ou appuyer sur `Enter` dessus, pour l'ajouter comme le sélecteur Add images ; `Enter` dans la zone de recherche prend le premier résultat. Clic droit sur une tuile pour **Open file location** ; la survoler donne son chemin complet.
- Un fichier supprimé depuis la dernière analyse quitte l'index (et les favoris) à l'instant où il est glissé, double-cliqué ou ouvert, avec un message dans la ligne d'état ; un fichier créé depuis apparaît au prochain lancement ou avec **↻**.
- **Vue dossiers** : le bouton **📁** à gauche de la zone de recherche fait passer le panneau de sa vue recherche (favoris, recherche, `*`) à la navigation dans le dossier de base **dossier par dossier**, et inversement. Le dossier ouvert est listé **depuis le disque**, comme l'Explorateur le liste : ses sous-dossiers d'abord, de A à Z — vides compris — puis ses fichiers, le plus récent d'abord ; un fichier créé depuis la dernière analyse s'affiche aussitôt. Une **tuile de dossier** montre la vignette que Windows donne au dossier (un dossier dessiné s'il n'en a pas) et son nom suivi du nombre de fichiers en dessous, sous-dossiers compris, tel que l'index les compte. Double-cliquer dessus, ou appuyer sur `Enter` dessus, pour l'ouvrir ; une tuile de dossier n'a pas de cœur et ne se glisse pas, et son clic droit propose **Open in Explorer**. La ligne de légende devient un **fil d'Ariane** : **↑** ouvre le dossier parent, chaque dossier du chemin s'ouvre au clic, et les comptes suivent à sa fin ; `Backspace` ou `Alt+↑` remontent aussi, le dossier qu'on vient de quitter étant sélectionné. Une recherche saisie dans la vue dossiers regarde **uniquement sous le dossier ouvert** : les dossiers qui correspondent d'abord, puis les fichiers qui correspondent ; `*` liste tous les dossiers en dessous, chacun suivi de ses sous-dossiers, puis tous les fichiers en dessous, le plus récent d'abord. La vue et le dossier ouvert sont mémorisés d'une session à l'autre ; un dossier disparu du disque cède la place à son plus proche parent encore présent.
- Le bouton **»** replie le panneau en une fine bande, son **«** le ramène ; l'état est mémorisé d'une session à l'autre.

## Effets

- En haut de la fenêtre, la rangée d'options, puis les onglets qui pendent en dessous : une étiquette **Effects**, un onglet par effet — **Background**, **Crop**, **Zoom**, **Animations**, **Rotate**, **Flip**, **Frames**, **Black & white**, **Blur**, **Volume** — et, tout à droite, un bouton **Reset** aussi haut que les onglets. Ils agissent sur la cellule sélectionnée ; sans cellule sélectionnée, les deux rangées sont désactivées.
- Chaque onglet contient une case à cocher, cochée tant que son effet est activé pour la cellule sélectionnée. Cliquer dessus active ou désactive l'effet, et sélectionne l'onglet.
- Cliquer sur un onglet ailleurs le sélectionne : ses options s'affichent dans la rangée au-dessus, jointe à lui. L'onglet sélectionné reste sélectionné quand une autre cellule est sélectionnée, ou aucune. La rangée d'options est toujours là, vide tant qu'aucun onglet n'est sélectionné.
- Désactiver un effet conserve ses réglages : il est dessiné comme son défaut (pas de fond, l'image entière, 100 % centrée, droite, non retournée, jouée depuis le début, en couleur, nette, entendue à 100 %) jusqu'à ce qu'il soit réactivé, tel qu'il était. Un effet désactivé affiche ses réglages conservés dans ses options.
- Modifier n'importe quelle option d'un effet l'active, à partir de ses réglages conservés.
- **Ctrl + molette** sur le curseur d'une option — ici et dans les options des effets globaux — le déplace de 5 %, sur les multiples de 5 (103 % → 105 %), au lieu d'une unité par cran : 5° pour l'angle fin, 0,5 % pour l'épaisseur des bordures, et le curseur Frames de 5 % des images ou des pages. Le curseur prend la molette comme il le fait sans Ctrl. Le curseur de zoom fait exception : sa molette prend les pas de la molette au-dessus de la cellule, Ctrl les rendant plus fins (voir Zoom).
- La rangée d'options se termine par un bouton **Reset** qui ramène l'effet de l'onglet sélectionné à son état par défaut : réglages par défaut, désactivé — sauf Background, réactivé (voir Background).
- Le **Reset** tout à droite des onglets le fait pour tous les effets de la cellule sélectionnée d'un coup, et remet aussi chaque séparateur de la grille là où sa disposition le place (voir Redimensionner les cellules).
- Un effet qui ne s'applique pas à la cellule sélectionnée (**Frames** sur une image fixe, **Volume** sur une image sans son) garde son onglet sélectionnable, mais sa case à cocher et ses options sont désactivées ; l'info-bulle de la case en donne la raison.
- Un effet appartient à la cellule et à son image : remplacer l'image (dépôt, `Ctrl+V`, sélecteur) ou retirer une image réinitialise les effets des cellules dont l'image change — sauf le Volume des images qui se décalent après un retrait, conservé pour que ce qu'on entend ne change pas ; échanger deux cellules ou changer de disposition les conserve.
- Les effets s'affichent dans l'aperçu, dans chaque export, et sur les vidéos pendant leur lecture.

### Background (fond)

- Le remplissage peint derrière l'image, sur toute sa cellule : les bandes autour d'elle et ses pixels transparents le montrent. **Activé par défaut**, pour chaque image placée dans une cellule, avec la couleur automatique à 100 %.
- **Automatic color** (cochée par défaut) : la couleur que les règles d'ajustement calculent à partir de la partie de l'image affichée (voir Règles d'ajustement).
- **Opacity** : de 0 à 100 % (100 % par défaut).
- Le **bouton de couleur** est peint avec la couleur utilisée. Cliquer dessus ouvre la boîte de dialogue de couleur standard, sur cette couleur ; choisir une couleur décoche *Automatic color*. La cocher de nouveau abandonne la couleur choisie ; la décocher garde la couleur automatique du moment comme couleur choisie.
- **Black & white** rend aussi le fond gris, quelle que soit sa couleur.
- **Désactivé**, ou sous 100 %, la cellule est transparente derrière son image : l'aperçu y montre des carreaux gris et blancs, comme les applications de dessin. Un PNG — enregistré, ou le format PNG d'une copie — conserve la transparence ; le bitmap copié, la vidéo MP4 et le GIF affichent du blanc à la place.
- Remplacer l'image, le Reset propre à Background et le Reset des onglets le remettent activé, automatique, à 100 %.

### Crop (recadrage)

- Garde une partie de l'image, qui **devient l'image** : la partie gardée remplit la cellule selon les règles d'ajustement, comme le ferait une image entière (jusqu'à 15 % rognés, des bandes au-delà) ; son fond automatique est calculé sur elle, la couleur suivant les barres en direct ; et le canevas est dimensionné pour qu'elle ne soit pas réduite (voir Taille du canevas). **10 % retirés de chaque bord** à l'activation.
- **Vue d'édition** : tant que l'onglet Crop est sélectionné et que le recadrage est activé, la cellule sélectionnée affiche l'**image entière** — pivotée et retournée, mais ni zoomée ni tournée d'un angle fin — ajustée entière, la partie retirée atténuée, sur le fond que prend l'image recadrée. Les autres cellules, les autres onglets et chaque export affichent l'image recadrée.
- Quatre barres vert fluo à travers l'image règlent chaque côté de la partie gardée indépendamment ; une barre glissée à moins de 6 px du bord de l'image s'aimante dessus. Un glissement **à l'intérieur de la partie gardée la déplace tout entière**, sa taille conservée, arrêtée aux bords de l'image. Ailleurs sur cette cellule, un glissement ne fait rien tant que la vue d'édition s'affiche ; la poignée ✥ et le × continuent de fonctionner.
- Ses **quatre coins**, marqués chacun d'un crochet en L vert, déplacent ensemble les deux barres qui s'y croisent, en suivant la souris sur les deux axes tandis que le coin opposé reste en place ; chaque barre s'aimante sur le bord de l'image comme seule. Sous un ratio (voir les options ci-dessous), la partie gardée le conserve : elle grandit ou rétrécit depuis le coin opposé, aussi loin que va la souris sur l'un ou l'autre axe, arrêtée aux bords de l'image.
- La **molette de la souris**, n'importe où sur cette cellule, agrandit la partie gardée (vers le haut) ou la réduit (vers le bas) de 5 % par cran, en gardant son ratio — celui du bouton, ou sa forme actuelle sous Free — autour de son centre, ou autour du point sous le curseur avec **Ctrl** maintenu. En grandissant contre un bord de l'image, elle glisse le long de celui-ci et continue de grandir de l'autre côté, jusqu'à la plus grande partie gardée à ce ratio ; en rétrécissant, elle s'arrête à l'écart minimal des barres.
- Le recadrage **suit l'image** : la tourner ou la retourner garde la même partie. Zoom, angle fin et Blur s'appliquent à l'image recadrée.
- Options : les boutons de ratio **Free**, **1:1**, **4:3**, **16:9** et **9:16**, chacun dessinant son format. En choisir un remodèle la partie gardée au plus grand rectangle à ce ratio qu'elle contienne, autour de son centre ; sous un ratio, une barre glissée entraîne les deux d'en face, autour du centre, pour que le ratio tienne. Le ratio est en pixels, donc 1:1 est carré quelle que soit l'image. Un quart de tour le tourne avec elle — 16:9 devient 9:16 — et libère un 4:3, qui n'a pas de bouton 3:4.
- **100 %**, après les boutons de ratio, garde l'**image entière** : chaque barre revient sur son bord, le ratio libéré (Free enfoncé). Comme n'importe quelle option, il active d'abord le recadrage — un clic sur un recadrage désactivé l'active à 100 %. Le Reset propre au recadrage ramène toujours 10 % à l'intérieur de chaque bord, désactivé.

### Zoom

- Options : le zoom, de 10 % au zoom maximum (2000 % par défaut) sur une échelle logarithmique, s'aimantant sur 100 %. La molette au-dessus déplace le zoom des pas de la molette au-dessus d'une cellule — 5 %, 1 % avec Ctrl, 25 % / 5 % au-dessus de 200 % (voir plus haut) ; ses touches fléchées gardent leurs propres petits pas.
- **Contain** et **Fill**, après le zoom, ajustent l'image à sa cellule : **Contain** montre l'image entière, des bandes d'un côté ; **Fill** couvre la cellule, le surplus rogné. L'ajustement **dure** : tant qu'un bouton est enfoncé, le zoom suit la cellule — redimensionnée par un séparateur, une disposition, le format — et l'image, recadrée ou tournée ; le curseur et le libellé montrent le zoom obtenu. L'image reste là où elle a été déplacée, et peut toujours être glissée.
  - Enfoncer l'autre bouton bascule sur lui ; relâcher le bouton enfoncé, la molette ou le curseur quittent l'ajustement pour un zoom libre, en partant du zoom affiché.
  - Un angle fin zoome toujours l'image ajustée juste assez pour couvrir ce qu'elle couvre non tournée.
- La molette de la souris et le glissement continuent de fonctionner sur chaque cellule, sélectionnée ou non (voir plus haut) — mais sur la cellule sélectionnée, tant que l'onglet Crop ou Blur affiche ses barres, la molette redimensionne leur zone à la place (voir Crop, Blur) ; l'effet est activé dès que l'image est zoomée ou déplacée. Sur une cellule dont le zoom est désactivé, ils partent de l'image telle qu'affichée, en remplaçant le zoom conservé.

### Animations

- Joue un mouvement dans la cellule, sur l'horloge de la grille. Un seul type pour l'instant, **Zoom** : l'image zoome en avant, puis revient, en continu — depuis l'endroit où les autres effets la placent (l'effet Zoom compris) jusqu'à +20 % et retour, sur une sinusoïde, ralentissant aux deux extrémités.
- Options : la liste déroulante **Type** (**Zoom**), puis la durée d'un **aller-retour**, de 1 s à 30 s (6 s par défaut) : plus on va à droite, plus c'est rapide.
- S'applique à toute image, une image figée comprise. Le zoom garde le centre de l'image là où l'effet Zoom le met.
- Le mouvement suit l'horloge de la grille, donc l'aperçu joue ce que l'export donne : activer l'effet, ou changer sa durée, reprend le mouvement là où en est l'horloge ; une image qui arrive ou qui est retirée le fait repartir de zéro avec la grille.
- Une cellule en mouvement **joue une boucle**, aussi longue que son aller-retour : elle affiche la ligne de progression sur ce cycle (une vidéo en lecture garde celle de sa propre boucle), fait produire une vidéo MP4 à **Copy** et **Save**, et compte dans la durée de la vidéo comme la boucle d'une vidéo — la boucle la plus longue l'emporte, et sur une grille d'images fixes elle fixe la durée même avec la bande son activée. La vidéo démarre à l'état de départ ; un PNG montre cet état.

### Rotate (rotation)

- Options : les quatre rotations, **0°**, **90°**, **180°** et **270°**, et un angle fin de −45° à +45° par pas de 1°, ajouté à la rotation. Un bouton de rotation n'est enfoncé que tant que l'angle tombe exactement dessus ; en cliquer un règle cet angle, l'angle fin revenant à 0°.
- À un angle fin, l'image tourne autour du centre de sa cellule, zoomée juste assez pour continuer à couvrir la partie de la cellule qu'elle couvre sans rotation : aucun coin de la cellule ne reste vide.
- Déplacer une image tournée : les arrêts magnétiques, leurs guides et la marge de 10 % s'appliquent à la boîte autour de l'image tournée. Dans ses arrêts, elle continue de couvrir sa cellule ; poussée au-delà, elle garde sa place et son zoom, et ses coins découverts prennent la couleur des bandes.

### Flip (retournement)

- Options : **Horizontal** et **Vertical**, chacun indépendamment.

### Frames (images)

- Pour une vidéo, un GIF animé, un PDF de plusieurs pages ou un long texte seulement ; le bouton est désactivé sur les autres images.
- Options : un curseur le long des images ou des pages, et **Freeze**.
- Non figé, le curseur règle l'endroit où le contenu **commence à jouer** — son début par défaut — dans l'aperçu et dans la vidéo exportée, son son compris.
- Figé, le contenu s'arrête sur l'image que choisit le curseur, dans l'aperçu et dans chaque export : une cellule figée est exportée comme cette image fixe, et une grille dont tous les contenus sont figés est copiée et enregistrée en PNG. Une vidéo figée n'a pas de son.

### Black & white (noir et blanc)

- Options : l'intensité, de 0 (les couleurs) à 100 % ; les bandes la suivent.

### Blur (flou)

- Floute les bandes autour d'un rectangle de la cellule, qui reste net ; le rectangle est centré sur la moitié de la cellule à l'activation.
- Quatre barres vert fluo à travers la cellule, deux verticales et deux horizontales, règlent chaque côté du rectangle net indépendamment : les faire glisser tant que les options du flou s'affichent et que le flou est activé. Une barre glissée à moins de 6 px de son bord de la cellule s'aimante dessus, pour qu'aucune mince bande floue n'y reste. Ses quatre coins, marqués chacun d'un crochet en L vert, déplacent ensemble les deux barres qui s'y croisent. La **molette de la souris**, n'importe où sur cette cellule, agrandit ou réduit le rectangle net comme elle le fait de la partie gardée du recadrage (voir Crop) — 5 % par cran, sa forme gardée, autour de son centre ou du point sous le curseur avec Ctrl — dans les limites de la cellule.
- Le rectangle reste en place dans la cellule quand l'image est zoomée, déplacée ou tournée.
- Options : **Gaussian** ou **Pixelate**, et l'intensité, relative à la taille de la cellule pour qu'un export ressemble à l'aperçu.

### Volume

- Pour une vidéo avec une piste son seulement, figée comprise (son volume s'applique de nouveau dès qu'elle joue) ; désactivé sur les autres images.
- Options : **Mute**, puis le volume, de 0 à 200 %. Le curseur qui atteint 0 coche Mute. Cocher Mute garde le niveau du curseur, et la décocher le ramène ; la décocher à 0 ramène le volume à 100 %. Le curseur reste toujours utilisable : le déplacer au-dessus de 0 désactive la coupure.
- Une vidéo ajoutée ou déposée (ou remplaçant une autre) est **entendue à 100 %**, l'effet désactivé, quoi que jouent les autres cellules : les sons de toutes les vidéos sont mixés en même temps. Les deux Reset l'y ramènent. Désactiver l'effet fait entendre la vidéo à 100 %.
- Au-dessus de 100 %, le son est amplifié, écrêté là où il dépasse la pleine échelle. L'aperçu et la vidéo exportée le jouent tous deux à son volume.

## Global

- Ce qui concerne toute la grille — pas une cellule — se trouve en bas de la fenêtre, le miroir des effets de cellule en haut : les onglets, puis la rangée d'options en dessous, juste au-dessus de la barre du bas. La rangée d'onglets contient une étiquette **Global**, l'onglet **Format**, un onglet par effet global — **Soundtrack**, **Fade**, **Borders** — et, tout à droite, un bouton **Reset** aussi haut que les onglets.
- L'onglet de chaque effet global contient une case à cocher, cochée tant que son effet est activé. Cliquer dessus active ou désactive l'effet, en gardant ses réglages, et sélectionne l'onglet. Cliquer sur un onglet ailleurs le sélectionne : ses options s'affichent dans la rangée en dessous, jointe à lui. Aucun onglet n'est sélectionné au démarrage ; la rangée d'options est toujours là, aussi haute que les miniatures du Format, vide tant qu'aucun onglet n'est sélectionné.
- Le **Format** est un réglage, toujours en vigueur : son onglet n'a pas de case à cocher.
- Un effet désactivé affiche ses réglages conservés dans ses options, et en modifier un l'active.
- La rangée d'options se termine par un bouton **Reset** qui ramène l'onglet sélectionné à son état initial — celui que **Clear all** rétablit : le format Twitter, un effet désactivé avec ses réglages initiaux ; le **Reset** tout à droite des onglets le fait pour tous d'un coup. Les boutons **Reset** des cellules laissent les onglets Global tranquilles, et les leurs laissent les cellules tranquilles.
- Ils fonctionnent sans cellule sélectionnée, et sont verrouillés pendant un export.

### Format

- Le format de l'image ou de la vidéo finale — le canevas de l'aperçu et chaque export — choisi dans une rangée de miniatures, chacune dessinant la disposition actuelle, ses cellules telles que redimensionnées, au format correspondant, son nom en dessous ; la miniature active est mise en évidence, un clic sur une autre la choisit.
- **Free**, le format propre au contenu, en contour tireté ; **Twitter**, 1200:628 (≈1,91:1), le format du fil d'actualité de Twitter / X et le défaut ; **Square 1:1** ; **Portrait 4:5** ; **Story 9:16** ; **Landscape 16:9**. Survoler une miniature indique à quoi elle convient.
- La grille s'étire au format, et chaque image s'ajuste à sa cellule par la règle d'ajustement habituelle (voir Règles d'ajustement).
- **Free** prend, entre 9:16 et 21:9, le ratio où les images perdent le moins — ce que la règle d'ajustement rogne plus les bandes qu'elle laisse, sur toutes les cellules, une grande cellule comptant davantage : une image seule obtient son propre ratio. Les cellules vides et les textes (qui prennent la forme de leur cellule) ne comptent pas ; sans rien qui compte, c'est le ratio de Twitter. Il suit la grille quand elle change — une image ajoutée, retirée, échangée, recadrée ou tournée, une autre disposition — mais reste figé tant qu'un séparateur ou une barre de recadrage est glissé, ou que la molette redimensionne la partie gardée, recalculé au relâchement ou quand la molette s'arrête, pour que le canevas ne change jamais de forme sous la souris.
- La **bande des dispositions** dessine ses miniatures au format.
- Non mémorisé : l'application démarre en Twitter, et **Clear all** et les boutons **Reset** l'y ramènent.

### Soundtrack (bande son)

- Le son d'un **fichier audio** (mp3, wav, m4a, aac, wma, flac…) ou d'une **vidéo** est mixé **par-dessus** les sons des vidéos, qui continuent de jouer à leur propre Volume — dans l'aperçu et dans la vidéo MP4 exportée ; un GIF n'a pas de son.
- Cocher l'onglet **Soundtrack** : sans fichier encore, cela ouvre un sélecteur, et la bande son reste désactivée s'il est annulé ; avec un fichier, cela active ou désactive la bande son, en gardant son fichier et son volume. Un fichier **déposé sur les onglets ou les options Global** devient la bande son et l'active ; de même pour un fichier choisi avec **Browse…**. Un fichier dont Windows ne lit aucune piste son est refusé, avec un message dans la ligne d'état.
- Options : **Browse…**, le nom du fichier (son chemin complet dans une info-bulle ; *No file* tant qu'aucun n'est choisi), et le volume, de 0 à 200 % — au-dessus de 100 %, amplifié et écrêté comme celui d'une vidéo. Réglé avant tout fichier, le volume attend le premier.
- Son **Reset** oublie le fichier : la bande son désactivée, le volume revenu à 100 %.
- Elle suit la durée de la grille, la boucle la plus longue : une bande son plus courte **boucle**, une plus longue est **coupée**. Une grille d'images fixes n'a pas de durée propre : avec la bande son activée, elle dure aussi longtemps que la bande son — **Copy** et **Save** produisent alors une vidéo MP4 des images fixes et du son au lieu d'un PNG.
- L'aperçu la joue tant que la grille contient une image, depuis son début à l'activation — et de nouveau, avec les images, chaque fois qu'une image arrive ou est retirée — en boucle sur la durée de la grille.

### Fade (fondu)

- Tout le mix sonore — chaque vidéo entendue et la bande son — **monte depuis le silence** pendant la durée du fondu au début de la vidéo, et **retombe vers le silence** pendant autant avant sa fin. Dans la vidéo MP4 exportée, et dans l'aperçu à chaque boucle de la grille : un son plus court que la grille continue de boucler à l'intérieur sans fondu, seuls le début et la fin de la grille font un fondu. Un GIF n'a pas de son.
- **Désactivé au démarrage**, et revenu à cet état avec **Clear all** et les Reset. Cocher l'onglet **Fade** pour l'activer ou le désactiver, ses réglages conservés.
- Options : la **durée**, de 0,1 à 5 s par pas de 0,1 s, 1 s au départ, utilisée pour les deux extrémités ; puis la **courbe**, deux boutons dessinant la forme du fondu — **Squared**, le premier (le gain croît comme le carré de la rampe, perçu comme une montée régulière), ou **Linear** — leurs noms dans des info-bulles.
- Une vidéo plus courte que deux fondus reçoit deux fondus de la moitié de sa longueur : le son monte, puis retombe aussitôt.
- Tant que **rien n'est entendu** — aucune vidéo ne joue avec son son, et aucune bande son n'est activée au-dessus de 0 % — la case à cocher et les options de Fade sont désactivées, l'info-bulle de la case en donnant la raison ; ses réglages sont conservés pour le retour d'un son.

### Borders (bordures)

- **Désactivées au démarrage**, et revenues à cet état avec **Clear all** et les Reset. Cocher l'onglet **Borders** pour les activer — dans le style **Corners** la première fois — ou les désactiver, leurs réglages conservés.
- Options :
  - le **style** : **Corners** (la signature de l'application, le défaut), **Solid**, **Dashed**, **Dotted** ou **Double** ;
  - l'**épaisseur** (thickness), de 0,1 à 6 % du plus petit côté de la grille (0,6 % par défaut), pour que l'aperçu et chaque taille d'export se ressemblent ;
  - **Opacity**, de 10 à 100 % (pleine par défaut) : l'opacité des crochets d'angle, tels qu'ils recouvrent les images. Activée dans le style Corners seulement ;
  - **Outer frame**, désactivé par défaut : borde aussi la grille elle-même. Désactivé dans le style Corners, dont les crochets sont déjà le cadre ;
  - **Twitter corners**, pour chaque style, activé par défaut, dans le format **Twitter** seulement (voir plus bas).
- **Corners** : un crochet en L sur les images à chacun des quatre coins de la grille, chaque bras couvrant 10 % du bord sur lequel il se trouve — les cellules restent telles quelles.
- Les autres styles laissent un véritable **espace** entre les cellules : la grille garde sa taille et les cellules rétrécissent pour faire de la place — et, avec le cadre extérieur, laissent une marge aussi large autour de la grille. Le style remplit l'espace ; ce qu'il laisse sans peinture (entre les tirets ou les points, à l'intérieur du trait double) est transparent, comme une cellule sans fond (voir Background). Cliquer dans un espace agit sur l'une des deux cellules voisines ; un séparateur se glisse toujours depuis l'espace (voir Redimensionner les cellules).
- **Twitter corners** : Twitter / X affiche une image publiée avec des coins arrondis, qui rogneraient les crochets d'angle. Avec cette option, les **quatre coins extérieurs** de la grille sont arrondis comme Twitter les arrondit — un rayon de 3 % du plus grand côté de la grille, soit ses 16 px sur une image affichée sur environ 540 px de large — et les crochets d'angle et le cadre extérieur suivent la courbe, pour que l'aperçu montre ce que Twitter montrera.
  - L'**aperçu** montre les coins arrondis **découpés**, comme Twitter les montrera. Les **exports** — PNG, JPEG pour le partage, GIF, vidéo MP4 — ne sont jamais découpés : les crochets d'angle et le cadre extérieur remplissent les coins arrondis jusqu'à l'angle droit, leur bord intérieur suivant la courbe, pour que l'arrondi propre à Twitter, quel que soit son rayon à la taille où il affiche l'image, ne découvre jamais un liséré blanc ou transparent. Les styles à espace sans cadre extérieur gardent l'image à cet endroit, pour que Twitter l'arrondisse.
  - Avec les Borders désactivées, les coins sont droits.
  - Dans tout autre **format** (voir Format), l'option est désactivée — son libellé disant *Twitter format only* — son réglage conservé, et les coins sont droits ; de retour en Twitter, ils sont de nouveau arrondis comme réglé.
  - **Twitter corners by default**, un élément cochable du menu **⚙**, activé jusqu'à modification et mémorisé d'une session à l'autre, règle si l'option est activée au démarrage et après **Clear all**. Le modifier laisse la grille ouverte telle qu'elle est.
- **Color** : l'élément **Border color** du menu **⚙**, avec un échantillon de la couleur actuelle, ouvre la boîte de dialogue de couleur standard ; la couleur choisie s'applique aussitôt et est mémorisée d'une session à l'autre, dans le fichier de paramètres (voir Zone de notification et démarrage). Rose vif (hotpink) tant qu'aucune n'est choisie.
- Les bordures s'affichent dans l'aperçu et dans chaque export : PNG, JPEG pour le partage, GIF et vidéo MP4.

## Aperçus

Un fichier qui n'est pas une image est transformé en image quand il peut être prévisualisé. La première correspondance l'emporte :

| Fichier | Devient | Curseur |
|---|---|---|
| GIF animé (deux images ou plus) | Ses images, jouées avec leurs propres délais | Image par image |
| Image que GDI+ sait décoder (png, jpg, bmp, gif, tif…) | L'image elle-même | — |
| Vidéo (mp4, mov, m4v, avi, wmv, mkv, webm, 3gp, mpg, ts…) | Une image, d'abord affichée à 10 % de la durée | Positions espacées d'un pas grossier : durée / 100, jamais moins de 1 s |
| PDF | Une page entière, rendue avec son grand côté à 1600 px | Page par page |
| Texte, quelle que soit son extension — un `.rtf` ou `.html` avec ses styles | Le texte dans une police à chasse fixe (voir plus bas) | Page par page, quand le texte en demande plusieurs |
| Tout le reste dont Windows affiche une vignette dans l'Explorateur (webp, heic, certains documents…) | Cette vignette | — |

- Un fichier qu'aucun d'eux ne gère est ignoré, avec un message dans la ligne d'état ; l'application ne dessine jamais d'icône ni d'image de remplacement à la place.
- Une vidéo dont Windows n'a pas le codec (HEVC sans son extension du Store, certains mkv / avi) se rabat sur sa vignette Windows, s'il y en a une.
- Le curseur est celui de l'effet **Frames** (voir Effets), jamais dans la sortie. L'image le suit en direct pendant le glissement.
- Le **texte** est reconnu à son contenu : 1 Mo au plus, UTF-8 ou UTF-16 avec marque d'ordre des octets, et aucun octet NUL dans ses 8 premiers Ko. L'extension décide seulement de la façon de le lire : un fichier `.rtf`, ou un `.htm` / `.html`, est lu comme un texte riche collé (voir Texte collé), avec son gras, son italique, ses couleurs et ses surlignages — du texte brut quand rien ne peut en être lu ; toute autre extension, comme du texte brut. Il est rendu sur des pages à la forme de sa cellule, à la taille de la cellule sur le plus petit canevas (son grand côté à 1200 px), et remis en page quand la cellule change (disposition, échange, nombre d'images, séparateur relâché, autre format), en gardant la position de lecture.
- **Texte lisible** : la police est la plus grande taille entre 24 et 96 px à laquelle tout le texte tient sur une page ; sous 24 px, le texte est paginé à 24 px à la place. Comme le canevas n'est jamais plus étroit que la largeur à laquelle aucune image n'est réduite (voir Taille du canevas), le texte a au moins cette hauteur dans la sortie. Les PDF sont rendus entiers, donc leurs petits caractères peuvent rester illisibles dans une petite cellule.

### Texte collé

- `Ctrl+V` avec un texte dans le presse-papiers, ou un texte glissé depuis une autre application, l'ajoute comme une image rendue comme un fichier texte (voir plus haut) : même police à chasse fixe, mêmes tailles lisibles, mêmes pages, même défilement quand il est long. Aucun fichier ne se trouve derrière.
- Ce que contient le presse-papiers ou le glissement est pris dans cet ordre, le premier présent l'emportant : fichiers, image (`Ctrl+V` seulement — ainsi les cellules Excel se collent toujours comme une image), texte riche (RTF, comme Word le donne, sinon HTML, comme les navigateurs le donnent), texte brut.
- Un texte riche conserve son **gras**, son *italique*, son souligné, son barré, ses couleurs de texte et ses surlignages ; ses polices et ses tailles ne sont pas conservées, la page reste dans la police à chasse fixe.
- Quand tout le texte repose sur un fond — du code copié depuis un éditeur en thème sombre — la page prend ce fond, et le texte sans couleur propre devient clair sur fond sombre.
- Un HTML sans texte, comme une image glissée depuis un navigateur, cède la place au texte brut qui l'accompagne : l'adresse de l'image s'affiche comme un texte.
- Un texte vide n'ajoute rien ; un texte de plus de 1 048 576 caractères est refusé. Les deux le disent dans la ligne d'état.

## Contenu animé

Une cellule contenant un **contenu multiple** le joue, en direct dans l'aperçu et dans la vidéo exportée :

| Contenu | Joue | Une boucle dure |
|---|---|---|
| Vidéo | Ses images, décodées l'une après l'autre | La durée de la vidéo |
| GIF animé | Ses images, chacune pendant son propre délai (délais de 0 ou 10 ms étirés à 100 ms, comme le font les navigateurs) | La somme de ses délais |
| PDF de plusieurs pages | La page suivante chaque seconde | 1 s par page |
| Texte plus long que sa cellule | Chaque seconde, la vue descend d'une demi-page, gardant la moitié basse de la vue précédente en haut, jusqu'à ce que la fin du texte s'affiche | 1 s par vue |

Un PDF d'une seule page, un texte qui tient dans sa cellule, un GIF d'une seule image et les images simples restent fixes.

- **Aperçu en direct** : chaque cellule joue sur une seule horloge, donc les pages et les vues changent ensemble, chacune depuis le point de départ de son effet Frames. Une image qui arrive dans une cellule — par n'importe quelle voie, dans une cellule vide ou en en remplaçant une autre — et une image retirée **font repartir la grille de zéro** : chaque image animée depuis son point de départ, au même instant, la bande son depuis son début, pour que l'aperçu joue ce que l'export donne ; une image figée reste sur son image ; un échange ou un changement de disposition ne change rien. Survoler une cellule ne la retient plus : figer passe par l'effet Frames.
- **Ligne de progression** : chaque cellule en lecture — et chaque cellule déplacée par son effet Animations, sur le cycle du mouvement — affiche, le long de son bord inférieur juste à l'intérieur du contour de sélection, une ligne vert fluo qui grandit depuis le bord gauche au fil de sa boucle — lue sur l'horloge à chaque repeinte, donc elle glisse même pour un PDF ou un texte qui ne change que chaque seconde — et repart de zéro à chaque boucle. Aucune sur un contenu figé ; sur la cellule sélectionnée, les barres de Blur ou de Crop prennent sa place tant qu'elles s'affichent. Dans l'aperçu seulement, jamais dans les exports.
- **Son** : les sons de chaque vidéo avec son sont **mixés**, chacun à son Volume — une vidéo figée ou coupée n'ajoute rien — et la bande son par-dessus quand elle est activée (voir Soundtrack) ; le Fade fait entrer et sortir le mix (voir Fade). Dans l'aperçu, chaque son joue au rythme de sa propre vidéo (retenu avec elle, bouclant avec elle) ; la vidéo exportée porte le mix, chaque son depuis le point de départ de sa vidéo, bouclant avec elle, en une seule piste AAC.
- **Export** : dès qu'un contenu joue (non figé), qu'une cellule bouge par son effet Animations, ou qu'une bande son est activée, **Save** écrit une **vidéo MP4** (H.264, son AAC) au lieu d'un PNG, et **Copy** met un fichier MP4 dans le presse-papiers (écrit dans `%TEMP%\ImageGridFusion`, un fichier par export, nettoyé au démarrage suivant), collable dans l'Explorateur, les applications de messagerie ou le courrier. Les boutons nomment ce qu'ils produisent : **Copy PNG** / **Copy MP4**, **Save PNG…** / **Save MP4…**.
  - La **flèche ▾** à droite de Copy et de Save ouvre un menu qui force le format pour cet export seulement : **GIF** ou **MP4 Video**. Elles sont désactivées tant que rien ne joue — la flèche de Save avec elles, celle de Copy restant activée pour son **JPEG for sharing** (voir Sortie) ; pour une image fixe d'un contenu animé, le figer avec l'effet Frames. Les deux flèches se rouvrent dès qu'une dernière vidéo existe, pour **Copy last** et **Save last** (voir Sortie).
  - Un **GIF** boucle indéfiniment et n'a pas de son ; chaque image reçoit sa propre palette de 256 couleurs. Copié, il va dans le presse-papiers à la fois comme fichier et dans le format de presse-papiers GIF, que certaines applications collent directement. Un grand canevas à 30 i/s donne des GIF lourds.
  - Chaque contenu démarre depuis le point de départ de son effet Frames (son début sans lui), et un contenu figé reste sur son image ; une cellule dont l'effet Animations est activé bouge depuis son état de départ. La vidéo ou le GIF dure aussi longtemps que la boucle la plus longue — le cycle d'une animation comptant comme une — les plus courtes repartant jusqu'à ce qu'elle se termine. 30 i/s (les images d'un GIF durent 3 ou 4 centièmes de seconde, donc la durée reste exacte). Cette durée s'affiche en permanence dans la barre du bas, à gauche de Copy (voir Sortie, *Length readout*).
  - Le canevas est dimensionné une seule fois, à partir des premières images (voir Taille du canevas), et arrondi à la baisse à des dimensions paires ; les bandes gardent la couleur de la première image.
  - La ligne d'état nomme les fichiers dont le son est dans l'export, la bande son comprise. Un son que Windows ne sait pas réencoder est laissé hors du mix, avec une note dans la ligne d'état.
- **Pendant un export**, la ligne d'état affiche la progression avec un bouton **Cancel**, et la grille est verrouillée : pas d'ajout, de retrait, d'échange, d'effacement, de changement de disposition ni d'effets. L'animation continue de jouer. Fermer la fenêtre ne fait que la masquer et l'export continue ; quitter (voir Zone de notification et démarrage) annule d'abord l'export. Un export annulé ne laisse aucun fichier.
- Un **export qui se termine alors que l'application n'est pas au premier plan** — MP4 ou GIF, Copy ou Save, terminé ou échoué — attire l'attention : le bouton de la barre des tâches clignote jusqu'à ce que la fenêtre passe au premier plan ; la fenêtre masquée dans la zone de notification, une notification de l'icône dit ce qui a été copié ou enregistré (cliquer dessus ouvre la fenêtre). Rien pour une annulation, ni quand l'application est au premier plan.

## Dispositions

Chaque nombre d'images offre plusieurs dispositions, choisies en cliquant sur une miniature dans la bande à gauche de l'aperçu. La bande est toujours affichée, pour que l'aperçu garde sa taille : avec une seule image, elle contient la seule disposition de ce nombre, et sans image la même miniature grisée. La première disposition de chaque nombre est celle par défaut ; l'application démarre dessus, et y revient chaque fois que le nombre d'images change.

Depuis le haut, la bande contient la bascule miroir (voir Miroir), les dispositions en dessous, puis un en-tête **More ▸** : cliquer dessus affiche les dispositions supplémentaires de ce nombre (voir Autres dispositions), cliquer sur **More ▾** les masque de nouveau. Elle démarre repliée, et se replie de nouveau chaque fois que le nombre d'images change ; repliée alors qu'une des dispositions supplémentaires est active, elle garde la miniature de celle-ci affichée. Une bande plus haute que la fenêtre défile, avec la molette de la souris ou sa barre de défilement ; les miniatures gardent leur taille.

L'image **1** prend toujours la cellule vedette (la grande) ; les autres images suivent dans l'ordre de lecture (gauche→droite, haut→bas). Les ratios des cellules sont donnés pour un canevas 1,91:1 (le format Twitter) : en dessous de 1, cela convient aux portraits et aux captures d'écran de téléphone, autour de 1,9 aux paysages, au-dessus de 3 aux panoramas.

**1 image** - remplit tout le canevas

```
+-------------------+
|                   |
|         1         |
|                   |
+-------------------+
```

**2 images**

```
Two columns (default)  Two rows               Two thirds + one third
+---------+---------+  +-------------------+  +------------+------+
|         |         |  |         1         |  |            |      |
|    1    |    2    |  +-------------------+  |     1      |  2   |
|         |         |  |         2         |  |            |      |
+---------+---------+  +-------------------+  +------------+------+
0.95 · 0.95            3.82 · 3.82            1.27 · 0.64
```

**3 images**

```
Big left (default)     Three columns          Featured               Big top
+---------+---------+  +------+-----+------+  +------------+------+  +-------------------+
|         |    2    |  |      |     |      |  |            |  2   |  |         1         |
|    1    +---------+  |  1   |  2  |  3   |  |     1      +------+  +---------+---------+
|         |    3    |  |      |     |      |  |            |  3   |  |    2    |    3    |
+---------+---------+  +------+-----+------+  +------------+------+  +---------+---------+
0.95 · 1.91 · 1.91     0.64 each              1.27 each              3.82 · 1.91 · 1.91
```

**4 images**

```
Grid (default)         Four columns           Featured               Big left               Big top
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +-------------------+
|    1    |    2    |  |    |    |    |    |  |            |  2   |  |         |    2    |  |         1         |
+---------+---------+  | 1  | 2  | 3  | 4  |  |     1      |  3   |  |    1    |    3    |  +------+-----+------+
|    3    |    4    |  |    |    |    |    |  |            |  4   |  |         |    4    |  |  2   |  3  |  4   |
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +------+-----+------+
1.91 each              0.48 each              1.27 · 1.91 ×3         0.95 · 2.87 ×3         3.82 · 1.27 ×3
```

### Autres dispositions

Sous le groupe **More** de la bande, de 2 à 4 images. Plusieurs d'entre elles sont une disposition ordinaire à d'autres proportions — *Bricks* est la *Grid* dont la ligne verticale est brisée — proposées ici avec des proportions exactes en un clic.

**2 images**

```
Two thirds + one third, stacked   Three quarters + one quarter
+-------------------+             +--------------+----+
|                   |             |              |    |
|         1         |             |      1       | 2  |
+-------------------+             |              |    |
|         2         |             +--------------+----+
+-------------------+
2.87 · 5.73                       1.43 · 0.48
```

**3 images**

```
Three rows             Big centre             Big top, uneven        Corner
+-------------------+  +----+---------+----+  +-------------------+  +------------+------+
|         1         |  |    |         |    |  |         1         |  |            |      |
+-------------------+  | 2  |    1    | 3  |  +------------+------+  |     1      |      |
|         2         |  |    |         |    |  |     2      |  3   |  |            |  2   |
+-------------------+  +----+---------+----+  +------------+------+  +------------+      |
|         3         |                                                |     3      |      |
+-------------------+                                                +------------+------+
5.73 each              0.95 · 0.48 ×2         3.82 · 2.55 · 1.27     1.91 · 0.64 · 3.82
```

**4 images**

```
Four rows              Big centre             Tall left, mixed
+-------------------+  +----+---------+----+  +------+-------------+
|         1         |  |    |         | 3  |  |      |      2      |
+-------------------+  | 2  |    1    +----+  |  1   +------+------+
|         2         |  |    |         | 4  |  |      |  3   |  4   |
+-------------------+  +----+---------+----+  +------+------+------+
|         3         |  0.95 · 0.48 · 0.95 ×2  0.64 · 2.55 · 1.27 ×2
+-------------------+
|         4         |
+-------------------+
7.64 each

Uneven grid            Bricks                 Corner
+------------+------+  +------------+------+  +------------+------+
|     1      |  2   |  |     1      |  2   |  |            |      |
+------------+------+  +------+-----+------+  |     1      |  2   |
|     3      |  4   |  |  3   |     4      |  |            |      |
+------------+------+  +------+------------+  +------------+------+
2.55 · 1.27 ×2 · 2.55  2.55 · 1.27 ×2 · 2.55  |     3      |  4   |
                                              +------------+------+
                                              1.91 · 0.95 · 3.82 · 1.91
```

Dans *Big centre*, la cellule vedette est au milieu : l'image 2 va à sa gauche, et avec 3 images l'image 3 à sa droite.

### Miroir

La bascule en haut de la bande retourne la disposition active le long de son axe asymétrique : haut↔bas pour *Big top* et *Two thirds + one third, stacked*, gauche↔droite pour les autres dispositions asymétriques (*Two thirds + one third*, *Big left*, *Featured*, et parmi les supplémentaires *Three quarters + one quarter*, *Big top, uneven*, *Corner*, *Big centre* avec 4 images, *Tall left, mixed*, *Uneven grid*, *Bricks*). Elle est désactivée sur les dispositions symétriques, où retourner ne ferait que réordonner les images, et elle se désactive chaque fois que la disposition ou le nombre d'images change. Les images gardent leurs cellules : l'image 1 suit la cellule vedette.

```
Big left, mirrored
+---------+---------+
|    2    |         |
+---------+    1    |
|    3    |         |
+---------+---------+
```

Une disposition redimensionnée se retourne avec ses tailles : la grande cellule reste grande, de l'autre côté.

### Redimensionner les cellules

Faire glisser le **séparateur** entre deux cellules pour donner plus de place à l'une d'elles : le curseur devient ↔ ou ↕ à moins de 4 px de lui. Un séparateur ne déplace que les cellules de ses deux côtés — le long séparateur de *Big left* déplace la grande cellule et chaque cellule empilée à côté, le court entre deux cellules empilées seulement ces deux-là — donc la grille peut devenir irrégulière. L'aperçu suit en direct, lissé une fois le séparateur relâché.

- **Grid** (4 images), et les dispositions supplémentaires dont les quatre cellules se rejoignent en croix (*Uneven grid*, *Bricks*, *Corner* avec 4 images) : tant que les deux lignes de la croix sont droites, chacun de ses quatre bras se déplace seul, entre deux cellules ; en déplacer un brise sa ligne, et l'autre ligne se déplace alors d'une seule pièce, les quatre cellules avec elle, jusqu'à ce que la ligne brisée soit de nouveau droite. *Bricks* démarre avec sa ligne verticale brisée.
- **Minimum** : un séparateur s'arrête là où une cellule qu'il déplace passerait sous 10 % de la largeur (ou de la hauteur) du canevas.
- **Magnétique** : à moins de 6 px, il retombe exactement à sa place dans la disposition, ou dans l'alignement d'un séparateur parallèle — comme l'autre bras d'une ligne brisée.
- **Retour aux tailles de la disposition** : double-cliquer sur un séparateur pour le remettre ; cliquer de nouveau sur la miniature active, ou sur le **Reset** des effets, pour les remettre tous. La miniature active garde la forme propre à la disposition.
- Les tailles appartiennent à la grille, pas aux images : échanger deux cellules ou remplacer une image les conserve ; choisir une autre disposition ou changer le nombre d'images repart sur les tailles propres de la disposition. Elles ne sont pas conservées d'un lancement à l'autre.
- Sur la cellule sélectionnée, une barre de Blur ou de Crop posée sur son bord — ou l'un de leurs coins qui s'y trouve — est saisie avant le séparateur ; le séparateur reste accessible depuis la cellule voisine, ou une fois cet onglet désélectionné ou son effet désactivé.
- Un texte est remis en page pour sa nouvelle cellule une fois le séparateur relâché. Pas pendant un export.

## Règles d'ajustement

- Chaque image est mise à l'échelle pour remplir sa cellule, sans espace entre les cellules — sauf si les Borders en laissent un, les cellules rétrécissant pour lui (voir Borders).
- Jusqu'à un seuil de l'axe qui déborde peut être rogné au total, réparti également des deux côtés — 15 % (7,5 % par côté).
- Au-delà de ce seuil, l'image est rognée exactement à ce seuil et centrée, et les bandes restantes (et les pixels transparents) sont remplies d'une couleur de fond — choisie automatiquement comme ci-dessous, sauf si l'effet Background en fixe une autre ou aucune (voir Background) :
  - le propre fond de l'image, quand au moins trois côtés de la partie que la cellule montre portent une couleur uniforme (identique ou très proche, bruit JPEG et légers dégradés compris) — une photo de produit sur fond blanc reçoit des bandes blanches même si son sujet est surtout rouge. Un côté où le sujet touche le bord, ou un côté en grande partie transparent, ne compte pas ;
  - sinon la couleur la plus fréquente de toute l'image.
  Les côtés sont ceux de la partie réellement affichée, après le recadrage et chaque effet (zoom, focus, rotation et angle fin), donc la couleur les suit. Une animation garde la même couleur pendant qu'elle joue.
  La zone qu'une image déplacée au-delà des bords de sa cellule découvre est remplie de la même façon, à partir des côtés de la partie encore affichée.
- Avec l'effet **Crop**, la partie gardée tient lieu d'image entière : la règle, et la couleur automatique, s'appliquent à elle.
- Le seuil est fixe : pour rogner davantage, zoomer ; pour rogner moins, dézoomer ; et déplacer l'image pour choisir ce que la cellule montre (voir l'effet Zoom).
- La même règle s'applique que l'image source soit trop petite (agrandie) ou trop grande (réduite).
- L'orientation EXIF est appliquée au chargement, donc les photos de téléphone apparaissent droites.

## Taille du canevas

La résolution de sortie est gardée aussi haute que possible pour que les images sources ne soient pas réduites inutilement : le canevas est de la taille à laquelle aucune image — la partie gardée d'une image recadrée — n'est réduite dans la disposition active, avec ses cellules telles que redimensionnées, au format choisi (voir Format), son **grand côté** borné entre 1200 et 4096 px — la largeur d'un canevas Twitter, Landscape ou Free large, la hauteur d'un canevas Story ou Portrait (675 × 1200 à 2304 × 4096 en Story).

## Annuler et rétablir

- `Ctrl+Z` annule le dernier changement, encore et encore, jusqu'aux **50** derniers ; `Ctrl+Y` ou `Ctrl+Shift+Z` rétablit ce qui a été annulé. Un nouveau changement après une annulation abandonne ce qui pouvait encore être rétabli.
- Tout ce dont la grille est faite est couvert : images ajoutées, remplacées, supprimées ou échangées, **Clear all**, chaque réglage d'effet, activé / désactivé et Reset, la disposition, son miroir et les séparateurs, le format et les effets globaux. Non couverts : la sélection et les onglets sélectionnés, ce que règle le menu ⚙ (la couleur des bordures entre autres), l'explorateur de fichiers, la dernière vidéo.
- Un **geste est une étape** : un glissement du clic au relâchement (un séparateur, les barres de blur ou de crop, un déplacement, un échange ✥, un curseur), une rafale de molette. Une étape est prise une fois que la grille est restée immobile environ 0,3 s, donc deux changements plus rapprochés que cela — deux clics très rapides — n'en font qu'un.
- La ligne d'état nomme ce qui a été annulé ou rétabli et combien d'étapes restent dans ce sens : `↶ Undone: image deleted — 4 more`, `↷ Redone: Blur — nothing more`.
- Les indicateurs verts de ce qui a changé apparaissent un instant sur la cellule, quel que soit l'onglet sélectionné : le pourcentage de zoom, les barres du flou, les bords du recadrage, les guides du bord ou du centre sur lequel une image est revenue, la position d'une image déplacée — affichés 1 s, puis s'effaçant. Les barres ne peuvent pas être saisies à ce moment-là : sélectionner leur onglet pour les déplacer.
- Une étape touchant une seule cellule la sélectionne ; les autres laissent la sélection telle quelle. Remettre des images ou en retirer fait repartir la grille de zéro, comme les ajouter ou les supprimer ; un échange, un changement de disposition ou d'effet non.
- Dans la zone de recherche de l'explorateur de fichiers, ces touches annulent plutôt le texte saisi. Verrouillé pendant un export, et pendant qu'un geste est en cours. L'historique démarre vide à chaque lancement — les fichiers déposés sur l'icône du `.exe` en sont le point de départ — et n'est pas sauvegardé.

## Sortie

- Bouton **Copy** / `Ctrl+C` : met le résultat en pleine résolution dans le presse-papiers, à la fois comme bitmap standard (cellules transparentes sur blanc) et dans le format de presse-papiers PNG (transparence conservée) ; tant qu'un contenu joue, un fichier MP4 à la place. Sa flèche ▾ copie un GIF ou une vidéo MP4 (voir Contenu animé), ou un **JPEG for sharing**.
  - **JPEG for sharing**, toujours disponible : une copie légère pour les applications de messagerie qui limitent la taille des images (WhatsApp : 16 Mo), où le PNG en pleine résolution peut être trop lourd. L'image fixe, son grand côté réduit à 2560 px au plus (jamais agrandi), cellules transparentes sur blanc, qualité JPEG 90 — généralement 1 à 2 Mo. Il va dans le presse-papiers comme un fichier `.jpg` (écrit dans `%TEMP%\ImageGridFusion`, nettoyé au démarrage suivant), collé tel quel par les applications de messagerie, l'Explorateur ou le courrier, plus la même image comme bitmap pour Paint ou Word. `Ctrl+C` reste la copie complète.
- Bouton **Copy last MP4** / **Copy last GIF**, à droite de la ▾ de Copy — aussi la dernière entrée du menu ▾ de Copy, avec l'heure de sa génération : remet dans le presse-papiers la dernière vidéo ou le dernier GIF que Copy a générés dans cette session, sans le générer de nouveau — une longue génération n'est pas perdue quand une autre copie écrase le presse-papiers avant d'être collée. Quoi que la grille soit devenue depuis, même vidée. Il affiche **Copy last video**, désactivé, tant qu'aucune n'a été générée ; une copie PNG, un JPEG for sharing ou un Save ne remplacent rien, un export annulé ou échoué non plus. Le survoler indique ce qu'il contient — heure de génération, taille, durée, son, temps d'encodage — et *the grid has changed since* quand c'est le cas. Le fichier reste dans `%TEMP%\ImageGridFusion` jusqu'au prochain démarrage ; disparu avant, le bouton le dit et se vide.
- Bouton **Save** / `Ctrl+S` : enregistre le résultat en fichier PNG ; tant qu'un contenu joue, en vidéo MP4. Sa flèche ▾ enregistre un GIF ou une vidéo MP4.
  - **Save last MP4…** / **Save last GIF…**, la dernière entrée du menu ▾ de Save : enregistre la dernière vidéo que Copy a générée (voir plus haut) là où vous le choisissez — son fichier copié là, rien de généré.
- **Length readout** (durée affichée) `⏱ 12.5 s`, à gauche de Copy, toujours affiché : ce que durerait la vidéo MP4 que Copy et Save produiraient — la boucle jouée la plus longue (un cycle Animations compris), ou la longueur de la bande son sur des images fixes — mis à jour au fil des changements de la grille ; `⏱ —` tant qu'ils produisent un PNG. Des secondes avec une décimale quelle que soit la durée, comme l'écrivent les résumés d'export. Le survoler donne le détail : le fichier qui fixe la durée, puis chaque autre contenu animé avec sa boucle et combien de fois il joue (un contenu figé : *frozen, a still*), et la bande son, coupée ou bouclée. Un affichage, pas un bouton : cliquer dessus ne fait rien.
- Une ligne d'état rend compte des retours et des erreurs (fichiers ignorés sans aperçu, fichiers en trop ignorés, images retirées, ce qu'une annulation ou un rétablissement a changé, confirmation ou échec d'une copie ou d'un enregistrement).

## Zone de notification et démarrage

- L'application affiche une icône dans la zone de notification de Windows (barre d'état système) aussi longtemps qu'elle tourne.
- **Fermer la fenêtre** (son **×**, `Alt+F4`, ou *Close window* dans la barre des tâches) ne fait que la masquer : l'application continue de tourner, sa grille inchangée.
- **Cliquer** sur l'icône de la zone de notification ramène la fenêtre. Un **clic droit** dessus ouvre un menu : **Open**, ou **Quit** pour fermer complètement l'application. Quitter est le seul moyen de sortir ; fermer la session ou éteindre Windows la ferme aussi.
- **Start with Windows** : le bouton **⚙** de la barre du bas ouvre un menu avec cette option cochable, désactivée par défaut. Cochée, l'application est lancée au début de la session, masquée : seule l'icône de la zone de notification apparaît.
  - Elle est stockée sous forme de raccourci `ImageGridFusion.lnk` dans votre dossier Démarrage (`shell:startup`), qui lance l'exe avec `--tray`. Pas de registre, pas de droits administrateur.
- Le même menu contient **Border color** et **Twitter corners by default** (voir Borders), et **File explorer folder…** et **File explorer pages per load** (voir Explorateur de fichiers).
  - Si l'exe est déplacé, le raccourci le suit au prochain lancement depuis son nouvel emplacement (toute copie de l'exe lancée reprend l'enregistrement).
  - Désactiver l'application dans *Paramètres → Applications → Démarrage* de Windows n'est pas reflété par l'option.
- **Taille de la fenêtre** : la taille qu'avait la fenêtre à sa dernière fermeture ou à son dernier masquage est mémorisée dans le fichier de paramètres et utilisée au lancement suivant, quelle que soit l'échelle de l'écran. Une fenêtre fermée agrandie ou réduite rouvre à sa taille normale, non agrandie ; une taille supérieure à celle de l'écran sur lequel elle s'ouvre est réduite à sa zone de travail. Sans rien de mémorisé, la fenêtre s'ouvre à sa taille par défaut.
- Plusieurs instances peuvent tourner côte à côte, chacune avec sa propre fenêtre et son icône de zone de notification ; la dernière fenêtre fermée fixe la taille mémorisée.
- **Titre secondaire** : `ImageGridFusion.exe --title "Undo / redo"` ouvre une fenêtre intitulée *Image Grid Fusion — Undo / redo* — dans sa barre de titre, son bouton de la barre des tâches et Alt+Tab — pour distinguer des instances qui tournent côte à côte (Claude Code passe le nom de sa session, voir `CLAUDE.md`). Se combine avec `--tray` et avec des fichiers, dans n'importe quel ordre ; la valeur est l'argument qui suit immédiatement `--title`. Manquante ou vide, elle est ignorée ; donnée deux fois, la dernière l'emporte. L'info-bulle de l'icône de la zone de notification l'affiche aussi, sur une deuxième ligne sous *Image Grid Fusion* — coupée par `…` au-delà des 127 caractères que Windows permet dans une info-bulle. Elle n'est jamais mémorisée.
- **Derniers dossiers** : le sélecteur **Add images**, **Choose a soundtrack** et les boîtes de dialogue Save s'ouvrent sur le dossier qu'ils ont utilisé en dernier, mémorisé d'une session à l'autre — un pour Add images, un pour la bande son, un partagé par les exports et **Save last…**. Seul un fichier choisi dans la boîte de dialogue le met à jour : un dépôt ou un collage non. Un dossier disparu depuis (supprimé, disque débranché) cède la place à son plus proche parent encore présent. Jusqu'à un premier enregistrement, les boîtes de dialogue Save s'ouvrent sur le dossier du fichier de la première image, sinon *Pictures*.
- **Fichier de paramètres** : chaque réglage ci-dessus — couleur des bordures, Twitter corners by default, dossier de l'explorateur de fichiers, panneau, largeur, taille des tuiles, pages par chargement, vue et dossier ouvert, taille de la fenêtre, derniers dossiers — est conservé dans `settings.json`, à côté de l'exe, comme `files.index` et `favorites.txt` ; chaque copie de l'exe a le sien. Un réglage n'a aucune commande dans l'application et se modifie à la main dans le fichier : le **zoom maximum**, `"MaxZoom"`, en pourcentage — 2000 par défaut, de 200 à 10 000. Il est lu au lancement, donc un changement prend effet au suivant, et l'application réécrit la valeur qu'elle applique chaque fois que le fichier ne la contient pas : absente ou illisible → 2000, au-delà de 10 000 → 10 000, en dessous de 200 → 200. L'application n'utilise jamais le registre : les paramètres qu'une ancienne version gardait dans `HKCU\Software\ImageGridFusion` sont déplacés dans le fichier au premier lancement, puis cette clé est supprimée, et un ancien enregistrement de Start with Windows (`HKCU\…\Run`) devient le raccourci du dossier Démarrage.

## Compilation et exécution

Voir [CONTRIBUTING.md § Build](CONTRIBUTING.md#build) (en anglais).

## Technique

C# / WinForms sur .NET 10, avec `System.Drawing` (GDI+) et une interpolation bicubique de haute qualité. Les aperçus et les exports n'utilisent que les composants propres à Windows, aucune bibliothèque tierce : `Windows.Data.Pdf` pour les PDF, Media Foundation pour les vidéos (`Windows.Media.Editing` pour les images fixes du curseur Frames, le Source Reader pour la lecture image par image, le Sink Writer pour l'export MP4), l'encodeur GIF de WIC pour l'export GIF, et `IShellItemImageFactory` du Shell pour les vignettes.

## Prévu

- Un design de cadre dédié au cas de l'image seule.
- De l'OCR sur les images indexées, pour que la recherche de l'explorateur de fichiers trouve aussi les mots qu'elles contiennent.
- La liste de l'explorateur de fichiers détachée dans une fenêtre à part, à placer à côté de la fenêtre principale ou sur un autre écran.

## Licence

[MIT](LICENSE) © Manofgoa. Contributions : voir [CONTRIBUTING.md](CONTRIBUTING.md) ; problèmes de sécurité : voir [SECURITY.md](SECURITY.md).
