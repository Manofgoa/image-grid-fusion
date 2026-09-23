namespace ImageGridFusion.UI;

internal sealed class MainForm : Form
{
    public MainForm(string[] args)
    {
        Text = "Image Grid Fusion";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 560);
    }
}
