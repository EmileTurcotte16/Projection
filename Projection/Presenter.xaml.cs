using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Projection
{
    /// <summary>
    /// Interaction logic for Presenter.xaml
    /// </summary>
    public partial class Presenter : UserControl
    {
        private FrameData currentData = null; //Image présentée en ce moment

        public Presenter()
        {
            InitializeComponent();            
        }

        //Changer l'image présentée
        public void Render(FrameData data)
        {
            Media.Source = null;
            currentData = data;

            if (data == null || data.BgFile == null) return;

            switch(data.BgFile.Type)
            {
                case FileType.Img:
                case FileType.Vid:
                {
                    Media.Source = new Uri(data.BgFile.Path, UriKind.Absolute);

                    Media.Visibility = data.ShowBg ? Visibility.Visible : Visibility.Collapsed;
                    Media.IsMuted = (data.BgMuted || !data.ShowBg);

                    Media.Play();

                    break;
                }
            }
        }

        //Changer la visibilitée de l'image présentée
        public void ChangeShowState(bool state)
        {
            if (currentData == null) return;
            currentData.ShowBg = state;
            Media.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
            Media.IsMuted = (!state || currentData.BgMuted);
        }

        //Fonction pour mettre en boucle ou cacher un gif ou une vidéo
        private void Loop(object sender, RoutedEventArgs e)
        {
            Media.Position = new TimeSpan(0, 0, 0, 0, 1);
            Media.Play();
        }

        //Passer à la prochaine image
        public FileData Next()
        {
            if (currentData == null || currentData.BgFile == null) return null;

            FileData NextFile = null;

            switch (currentData.BgFile.Type)
            {
                default: break;
            }

            Render(new FrameData(NextFile, currentData.ShowBg, currentData.BgMuted));

            return NextFile;
        }

        //Changer l'opacité de l'image présentée
        public void SetOpacity(double master)
        {
            PresenterGrid.Opacity = master;
            Media.Volume = master;
        }
    }
}
