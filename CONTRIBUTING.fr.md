[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](CONTRIBUTING.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Contribuer

Merci de votre intérêt pour Image Grid Fusion. Les tickets et les pull requests sont les bienvenus.

## Compilation

- Nécessite le SDK .NET 10, sur **Windows 10 version 2004 ou ultérieure**.
- Exécuter : `dotnet run --project src/ImageGridFusion`
- Publier : `dotnet publish src/ImageGridFusion -c Release` →
  `src/ImageGridFusion/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/ImageGridFusion.exe`,
  un exe unique ReadyToRun dépendant du framework, qui nécessite le .NET 10 Desktop Runtime et
  Windows 10 version 2004 ou ultérieure.

## Tests

- Exécuter : `dotnet test tests/ImageGridFusion.Tests` — MSTest, une dépendance des tests seuls, jamais livrée.
- Les tests couvrent ce qui se vérifie sans la fenêtre (le cache des icônes, le dessin des icônes emoji) ;
  vérifiez tout le reste à la main, dans l'application lancée.

## Police des drapeaux

`src/ImageGridFusion/Imaging/TwemojiFlags.ttf`, les drapeaux des textes, est faite à partir de
[Twemoji Mozilla v0.7.0](https://github.com/mozilla/twemoji-colr/releases/tag/v0.7.0) par
[`tools/make-flag-font.py`](tools/make-flag-font.py), qui dit ce qu'il modifie. Pour la refaire, avec
Python et fontTools :

```bash
pip install fonttools
python tools/make-flag-font.py Twemoji.Mozilla.ttf src/ImageGridFusion/Imaging/TwemojiFlags.ttf
```

Ses dessins sont ceux de Twemoji, sous CC-BY 4.0 : voir `src/ImageGridFusion/Imaging/TwemojiFlags.LICENSE.md`.

## Avant de modifier le code

- [RULES.md](RULES.md) (en anglais) tient les règles que suit chaque modification — effets, barres
  d'outils, format de sortie, historique d'annulation, réglages, langue de l'interface. Une
  modification qui enfreint l'une d'elles met la règle à jour dans la même pull request.
- [GLOSSARY.fr.md](GLOSSARY.fr.md) donne à chaque mot son unique sens : employez ses termes dans le
  code, les commentaires et la documentation.
- [`workfiles/`](workfiles) (en anglais) garde la conception de chaque fonctionnalité, l'origine des
  règles.

## Pull requests

- Une fonctionnalité ou une correction par pull request, en anglais : code, commentaires, messages de
  commit.
- Suivez le code qui l'entoure : nommage, densité des commentaires, idiomes.
- Aucune nouvelle dépendance : l'application n'utilise que les composants propres à Windows, voir
  [README § Technique](README.fr.md#technique).
- Une modification visible par l'utilisateur met à jour [README.md](README.md) et
  [README.fr.md](README.fr.md) ; un nouveau texte de l'interface s'ajoute en anglais et en français
  (`Resources/Strings.resx`, `Resources/Strings.fr.resx`).
