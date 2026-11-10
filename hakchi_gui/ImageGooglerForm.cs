using com.clusterrr.hakchi_gui.Controls;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui
{
    public partial class ImageGooglerForm : Form
    {
        public Image Result { get; private set; }
        private string query;
        public ImageGooglerForm(NesApplication app)
        {
            InitializeComponent();
            query = BuildQuery(app);
        }

        /// <summary>Requete Google Images partagee (formulaire, GoogleCoverSource).</summary>
        public static string BuildQuery(NesApplication app)
        {
            return BuildQuery(app.Name, app.Metadata.AppInfo.GoogleSuffix);
        }

        public static string BuildQuery(string name, string googleSuffix)
        {
            return (name ?? "") + " " + googleSuffix + " (box|cover) art";
        }

        public static string[] GetImageUrls(NesApplication app)
        {
            return ImageGoogler.GetImageUrls(BuildQuery(app));
        }

        private void ImageGooglerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            imageGoogler.Dispose();
        }

        private void imageGoogler_OnImageDoubleClicked(Image image)
        {
            DialogResult = DialogResult.OK;
            Result = image;
            Close();
        }

        private void ImageGooglerForm_Shown(object sender, System.EventArgs e)
        {
            imageGoogler.Queries.Add(new ImageGoogler.SearchQuery()
            {
                Query = query,
                AdditionalVariables = ""
            });
            imageGoogler.RunQuery();
        }
    }
}
