using ImageGridFusion.Explorer;
using ImageGridFusion.Imaging;
using ImageGridFusion.Web;

namespace ImageGridFusion.Components;

/// <summary>
/// Every downloadable component, one per upstream, in the order the Component updates window lists them: the engines'
/// models, the runtimes they run on, the URL zone's tools, the emoji fonts. Their pinned files stay with the code that
/// reads them (<see cref="OcrEngineFiles"/>, <see cref="FaceEngineFiles"/>…); a new downloadable component is registered
/// here, with its group, its purpose and its source. See workfiles/20261102-component-update-verification.md.
/// </summary>
internal static class ComponentRegistry
{
    public const string OcrModels = "ocr-models";
    public const string OnnxRuntime = "onnx-runtime";
    public const string SkiaSharp = "skiasharp";
    public const string FaceDetection = "face-yunet";
    public const string FaceRecognition = "face-sface";
    public const string VisionModels = "vision-siglip2-models";
    public const string VisionTokenizer = "vision-siglip2-tokenizer";
    public const string CaptionModels = "caption-florence2";
    public const string CutoutSubject = "cutout-isnet";
    public const string CutoutPick = "cutout-mobilesam";
    public const string YtDlp = "yt-dlp";
    public const string Deno = "deno";
    public const string Ffmpeg = "ffmpeg";

    // The winget tools' pinned versions: the ones current when the window was built, installed by `winget --version`.
    private const string YtDlpVersion = "2026.10.26";
    private const string DenoVersion = "2.9.8";
    private const string FfmpegVersion = "N-127043-g5a54fcf75e-20260930";

    /// <summary>Every component, in the window's order.</summary>
    public static IReadOnlyList<Component> All { get; } = Build();

    public static Component Get(string id) => All.First(c => c.Id == id);

    /// <summary>The components of the face engine, the vision engine…: what a feature needs.</summary>
    public static IReadOnlyList<Component> Of(params string[] ids) => [.. ids.Select(Get)];

    /// <summary>The emoji fonts' components.</summary>
    public static IReadOnlyList<Component> EmojiFontComponents => [.. All.Where(c => c.Group == ComponentGroup.EmojiFonts)];

    private static List<Component> Build()
    {
        var ocr = OcrEngineFiles.Files;
        var list = new List<Component>
        {
            NuGet(OcrModels, ComponentGroup.Engines, "OCR engine — RapidOCR models", "Apache 2.0",
                "Reads the text in images and PDF pages for the content search (💡), in a pass of its own after Windows OCR",
                [.. ocr.Where(f => f.Package == "rapidocrnet")]),
            Hf(FaceDetection, "Face engine — YuNet", "MIT", "2023mar",
                "Finds the faces in the indexed images, for the People view (the face analysis)", FaceEngineFiles.Folder, [FaceEngineFiles.DetectionFile]),
            Hf(FaceRecognition, "Face engine — SFace", "Apache 2.0", "2021dec",
                "Tells the faces apart, to group them into people in the People view", FaceEngineFiles.Folder, [FaceEngineFiles.RecognitionFile]),
            Hf(VisionModels, "Vision engine — SigLIP 2 models", "Apache 2.0", "d1114256",
                "Sees what the images show: their tags, the similarity search under 💡 and Find similar images (the visual analysis)", VisionEngineFiles.Folder, VisionEngineFiles.ModelFiles),
            Hf(VisionTokenizer, "Vision engine — SigLIP 2 tokenizer", "Apache 2.0", "3f9f96cb",
                "Turns a search's words into what the SigLIP 2 text model reads, for the similarity search under 💡", VisionEngineFiles.Folder, [VisionEngineFiles.TokenizerFile]),
            Hf(CaptionModels, "Caption engine — Florence-2", "MIT", "d59e0797",
                "Writes a sentence describing each image, found by the search under 💡 (the captions)", VisionEngineFiles.Folder, CaptionEngineFiles.Files),
            new(CutoutSubject, ComponentGroup.Engines, "Cutout engine — IS-Net", "Finds an image's main subject, for the Cutout's Subject mode", "Apache 2.0",
                CutoutEngineFiles.Folder, Version("v0.0.0", CutoutEngineFiles.SubjectFile), new GitHubReleaseSource("danielgatis/rembg", "v0.0.0", CutoutEngineFiles.SubjectModel, null)),
            Hf(CutoutPick, "Cutout engine — MobileSAM", "MIT", "0d3b4033",
                "Picks the object under a click, for the Cutout's Pick mode", CutoutEngineFiles.Folder, CutoutEngineFiles.PickFiles),
            NuGet(OnnxRuntime, ComponentGroup.Runtimes, "ONNX Runtime", "MIT",
                "Runs every engine's models: needed by all of them. Loaded once an engine has run: replaced at the next start",
                [.. ocr.Where(f => f.Name == OcrEngineFiles.OnnxRuntime)]),
            NuGet(SkiaSharp, ComponentGroup.Runtimes, "SkiaSharp", "MIT",
                "Decodes the images for RapidOCR, the OCR engine. Loaded once RapidOCR has run: replaced at the next start",
                [.. ocr.Where(f => f.Name == OcrEngineFiles.SkiaSharp)]),
            Tool(YtDlp, Web.YtDlp.Tool, YtDlpVersion, "Unlicense", "https://github.com/yt-dlp/yt-dlp",
                "Downloads the media of a URL the URL zone's own resolvers do not know"),
            Tool(Deno, YtDlpCompanions.Deno, DenoVersion, "MIT", "https://deno.com",
                "Lets yt-dlp read YouTube's formats, to load the largest ones"),
            Tool(Ffmpeg, YtDlpCompanions.Ffmpeg, FfmpegVersion, "GPL 3.0", "https://github.com/yt-dlp/FFmpeg-Builds",
                "Joins a video track and its sound into one MP4 for yt-dlp, to load the largest formats"),
        };

        foreach (var font in EmojiFonts.Pinned)
        {
            // The release's tag in the pinned URL: …/releases/download/<tag>/<asset>, or raw…/<owner>/<repo>/<tag>/<path>.
            string[] parts = font.File.Url.Split('/');
            string tag = font.Asset is not null ? parts[^2] : parts[5];
            list.Add(new Component(
                "emoji-" + font.Family.ToLowerInvariant().Replace(' ', '-'),
                ComponentGroup.EmojiFonts,
                font.Family,
                $"Draws a text layer's emojis in {font.Family}'s style, picked in the Emoji drop-down beside the text's font",
                FontLicense(font.Family),
                EmojiFonts.Folder,
                Version(tag, font.File),
                new GitHubReleaseSource(font.Repository, null, font.Asset, font.Path)) { Accepts = EmojiFonts.IsFont });
        }

        return list;
    }

    private static Component Hf(string id, string name, string license, string label, string purpose, string folder, IReadOnlyList<EngineFile> files)
    {
        var version = Version(label, [.. files]);
        return new Component(id, ComponentGroup.Engines, name, purpose, license, folder, version, HuggingFaceSource.Of(version.Files));
    }

    private static Component NuGet(string id, ComponentGroup group, string name, string license, string purpose, IReadOnlyList<OcrEngineFiles.EngineFile> files)
    {
        string package = files[0].Package;
        var source = new NuGetSource(package, files.ToDictionary(f => f.Name, f => f.Entry));
        var version = new ComponentVersion(files[0].Version, [.. files.Select(f => new ComponentFile(f.Name, source.PackageUrl(f.Version), f.Sha256.ToLowerInvariant(), 0, f.Entry))]);
        return new Component(id, group, name, purpose, license, OcrEngineFiles.Folder, version, source);
    }

    private static Component Tool(string id, WingetTool tool, string version, string license, string homepage, string purpose) =>
        new(id, ComponentGroup.WebTools, tool.Name, purpose, license, tool.Folder, new ComponentVersion(version, []), new WingetSource(tool, homepage));

    private static ComponentVersion Version(string label, params EngineFile[] files) =>
        new(label, [.. files.Select(f => new ComponentFile(f.Name, f.Url, f.Sha256.ToLowerInvariant(), f.Size))]);

    private static string FontLicense(string family) => family switch
    {
        "Twemoji Mozilla" => "Graphics CC-BY 4.0",
        "Noto Color Emoji" => "OFL 1.1",
        "OpenMoji" => "CC BY-SA 4.0",
        _ => "Apache 2.0",
    };
}
