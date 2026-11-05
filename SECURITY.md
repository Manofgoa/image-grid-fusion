<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](SECURITY.fr.md)

# Security

Image Grid Fusion is a desktop app that works offline: it opens the files you give it and writes the
images, videos and settings you ask for. It connects to the network only in two places, always on
your action:

- **The URL zone** of the file explorer: when you click **Load**, it fetches the media behind the URL
  — an X post through X's public syndication endpoint, a direct link to an image or a video, or another
  site's through yt-dlp.
- **The Component updates window**: when it opens and on **Check now**, it asks each component's
  source — Hugging Face, GitHub, NuGet, winget — for its latest version; it downloads a component only
  when you click **Install** or **Update**, each file checked against its SHA-256 when it is known
  beforehand. The window also opens when a feature needs a component that is missing.

Nothing else is sent: no account, no telemetry. The faces, the visual analysis and the text read in
your images stay on this computer.

Found a vulnerability? Please report it privately, through the repository's **Security** tab →
**Report a vulnerability**, rather than in a public issue. Only the latest version on `main` is
supported.
