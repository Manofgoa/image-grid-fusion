[<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English](SECURITY.md) · <img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français

# Sécurité

Image Grid Fusion est une application de bureau qui fonctionne hors ligne : elle ouvre les fichiers que
vous lui donnez et écrit les images, vidéos et réglages que vous demandez. Elle ne se connecte au
réseau qu'à deux endroits, toujours à votre demande :

- **La zone URL** de l'explorateur de fichiers : quand vous cliquez sur **Charger**, elle récupère le
  média derrière l'URL — un post X par le point d'accès public de syndication de X, un lien direct vers
  une image ou une vidéo, ou celui d'un autre site par yt-dlp.
- **La fenêtre Mises à jour des composants** : à son ouverture et sur **Vérifier maintenant**, elle
  demande à la source de chaque composant — Hugging Face, GitHub, NuGet, winget — sa dernière version ;
  elle ne télécharge un composant que lorsque vous cliquez sur **Installer** ou **Mettre à jour**,
  chaque fichier vérifié par son SHA-256 quand il est connu à l'avance. La fenêtre s'ouvre aussi
  quand une fonctionnalité a besoin d'un composant absent.

Rien d'autre n'est envoyé : aucun compte, aucune télémétrie. Les visages, l'analyse visuelle et le
texte lu dans vos images restent sur cet ordinateur.

Vous avez trouvé une vulnérabilité ? Signalez-la en privé, depuis l'onglet **Security** du dépôt →
**Report a vulnerability**, plutôt que dans un ticket public. Seule la dernière version sur `main` est
prise en charge.
