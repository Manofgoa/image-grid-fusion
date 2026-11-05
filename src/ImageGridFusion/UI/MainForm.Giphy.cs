using ImageGridFusion.Resources;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>
/// The GIPHY view's app settings, in the ⚙ menu: the user's API key, typed in a dialog and kept in the settings
/// file, and the content rating its requests carry, <c>r</c> by default. See workfiles/20261105-online-library-giphy.md
/// § API Key, § The GIPHY View.
/// </summary>
internal sealed partial class MainForm
{
    private readonly ToolStripMenuItem _giphyKeyItem = new(Strings.MainSettingsGiphyKey) { ToolTipText = Strings.MainSettingsGiphyKeyTip };
    private readonly ToolStripMenuItem _giphyRatingItem = new(Strings.MainSettingsGiphyRating) { ToolTipText = Strings.MainSettingsGiphyRatingTip };

    /// <summary>The GIPHY entries of the ⚙ menu, and the explorer given the key and the rating saved.</summary>
    private void InitGiphySettings()
    {
        UiIcons.Set(this._giphyKeyItem, UiIcons.ApiKey);
        UiIcons.Set(this._giphyRatingItem, UiIcons.ContentRating);
        this._giphyKeyItem.Click += (_, _) => this.AskGiphyKey();
        foreach (string rating in Giphy.Ratings)
        {
            string choice = rating;
            var item = new ToolStripMenuItem(RatingText(choice)) { Tag = choice };
            item.Click += (_, _) => this.SetGiphyRating(choice);
            this._giphyRatingItem.DropDownItems.Add(item);
        }

        this._explorer.GiphyApiKey = AppSettings.GiphyApiKey;
        this._explorer.GiphyRating = AppSettings.GiphyRating;
        this._explorer.GiphyCalls = AppSettings.GiphyCalls;
        this._explorer.GiphyKeyRequested += (_, _) => this.AskGiphyKey();
        this._explorer.GiphyCallsChanged += (_, _) => this.SaveGiphyCalls();
    }

    /// <summary>A call sent to GIPHY: the last hour's calls kept, so the quota counted survives a restart.</summary>
    private void SaveGiphyCalls()
    {
        try
        {
            AppSettings.SaveGiphyCalls(this._explorer.GiphyCalls);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            this.ShowStatus(Texts.Format(Strings.MainStatusNotSavedGiphyCalls, ex.Message), error: true);
        }
    }

    /// <summary>The ⚙ menu's GIPHY entries ticked from the settings as they are now.</summary>
    private void SyncGiphySettings()
    {
        foreach (ToolStripMenuItem item in this._giphyRatingItem.DropDownItems)
        {
            item.Checked = (string)item.Tag! == this._explorer.GiphyRating;
        }
    }

    /// <summary>A content rating as the ⚙ menu names it, in the UI language.</summary>
    private static string RatingText(string rating) => rating switch
    {
        "g" => Strings.MainGiphyRatingG,
        "pg" => Strings.MainGiphyRatingPg,
        "pg-13" => Strings.MainGiphyRatingPg13,
        _ => Strings.MainGiphyRatingR,
    };

    /// <summary>
    /// The GIPHY API key typed — shown in clear, as it lies in the settings file — then kept and given to the explorer;
    /// an empty one forgets it.
    /// </summary>
    private void AskGiphyKey()
    {
        using var dialog = new NameDialog(Strings.MainGiphyKeyTitle, Strings.MainGiphyKeyPrompt, AppSettings.GiphyApiKey ?? "");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string key = dialog.Value;
        try
        {
            AppSettings.SaveGiphyApiKey(key);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            this.ShowStatus(Texts.Format(Strings.MainStatusNotSavedGiphyKey, ex.Message), error: true);
        }

        this._explorer.GiphyApiKey = key.Length > 0 ? key : null;
    }

    /// <summary>A content rating picked in the ⚙ menu: kept, and the GIPHY view's list asked again with it.</summary>
    private void SetGiphyRating(string rating)
    {
        try
        {
            AppSettings.SaveGiphyRating(rating);
        }
        catch (Exception ex) when (AppSettings.IsSaveError(ex))
        {
            this.ShowStatus(Texts.Format(Strings.MainStatusNotSavedGiphyRating, ex.Message), error: true);
        }

        this._explorer.GiphyRating = rating;
    }
}
