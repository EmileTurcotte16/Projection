using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Office2010.PowerPoint;
using Microsoft.Win32;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Projection { 

    //Types de fichiers
    public enum FileType
    {
        Excel,
        Ppt,
        Img,
        Vid,
        Audio,
    }

    //Classe pour définir les variables d'un fichier
    public class FileData : IEquatable<FileData>
    {
        public FileData(string name, string path)
        {
            this.Name = name;
            this.Path = path;
        }

        public string Name { get; set; } //Nom du fichier
        public string Path { get; set; } //Emplacement du fichier
        public FileType Type { get; set; } //Type de fichier

        //Fonction pour vérifier si un fichier est en double
        public bool Equals(FileData? file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            return this.Path == file.Path;
        }
    }

    //Classe pour les images présentées
    public class FrameData
    {
        public FrameData(FileData bgFile, bool showBg, bool bgMuted)
        {
            this.BgFile = bgFile;
            this.ShowBg = showBg;
            this.BgMuted = bgMuted;
        }

        public FileData BgFile { get; set; } //Arrière-plan
        public bool ShowBg { get; set; } //Montrer l'arrière-plan?
        public bool BgMuted { get; set; } //Audio de l'arrière-plan
    }

    public partial class MainWindow : Window
    {
        private bool ExcelEnabled = true; //Montrer Excel?
        private bool MultimediaEnabled = true; //Montrer le multimédia?

        private System.Windows.Point dragStartPoint; //Point pour vérifier si la distance de drag and drop est assez grande

        private bool AudioPlaying = false; //Audio en train de jouer?
        private bool UserChangingAudioPos = false; //L'utilisateur veut il changer la position de l'audio

        private List<FrameData> frames = new List<FrameData>(); //Liste des présentations
        private FileData nextFrame; //Fichier suivant

        private List<MonitorData> monitors = new List<MonitorData>();

        //Fonction pour trouver un parent d'un objet avec un type T
        private T FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T) return parentObject as T;
            return FindVisualParent<T>(parentObject);
        }

        public MainWindow()
        {
            InitializeComponent();

            ExcelList.ItemsSource = new ObservableCollection<FileData>();
            PptList.ItemsSource = new ObservableCollection<FileData>();
            ImgList.ItemsSource = new ObservableCollection<FileData>();
            VidList.ItemsSource = new ObservableCollection<FileData>();
            AudioList.ItemsSource = new ObservableCollection<FileData>();

            DispatcherTimer audioTimer = new DispatcherTimer();
            audioTimer.Tick += AudioTimerTick;
            audioTimer.Start();
        }

        //Change la valeur de l'intensité pour celle du bouton quand le bouton est cliqué
        private void ChangeMasterValue(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            double value = Convert.ToDouble(button.Tag);

            Master.Value = value;
        }

        //Changer le texte lorsque la valeur d'intensité est changée
        private void MasterValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UInt16 value = Convert.ToUInt16((Master.Value * 10));

            MasterValue.Content = String.Format("{0}%", value.ToString());
            CurrentPresenter.SetOpacity(Master.Value / 10);
        }

        //Montrer/Cacher Excel
        private void ChangeShowExcel(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ExcelEnabled = !ExcelEnabled;

            button.Background = ExcelEnabled ? Brushes.Green : Brushes.Red;
        }

        //Montrer/Cacher le multimedia
        private void ChangeShowMultimedia(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            MultimediaEnabled = !MultimediaEnabled;

            button.Background = MultimediaEnabled ? Brushes.Green : Brushes.Red;

            NextPresenter.ChangeShowState(MultimediaEnabled);
        }

        //Afficher un Ppt
        private void ChangeToPpt(object sender, RoutedEventArgs e)
        {
            Ppt.BorderThickness = new Thickness(5);
            Img.BorderThickness = new Thickness(0);
            Vid.BorderThickness = new Thickness(0);

            PptList.Visibility = Visibility.Visible;
            ImgList.Visibility = Visibility.Hidden;
            VidList.Visibility = Visibility.Hidden;

            Previous.Visibility = Visibility.Visible;
            Next.Visibility = Visibility.Visible;

            Tab.Content = "Power-point";
        }

        //Afficher une image
        private void ChangeToImg(object sender, RoutedEventArgs e)
        {
            Img.BorderThickness = new Thickness(5);
            Ppt.BorderThickness = new Thickness(0);
            Vid.BorderThickness = new Thickness(0);

            ImgList.Visibility = Visibility.Visible;
            PptList.Visibility = Visibility.Hidden;
            VidList.Visibility = Visibility.Hidden;

            Previous.Visibility = Visibility.Hidden;
            Next.Visibility = Visibility.Hidden;

            Tab.Content = "Image";
        }

        //Afficher une video
        private void ChangeToVid(object sender, RoutedEventArgs e)
        {
            Vid.BorderThickness = new Thickness(5);
            Ppt.BorderThickness = new Thickness(0);
            Img.BorderThickness = new Thickness(0);

            VidList.Visibility = Visibility.Visible;
            PptList.Visibility = Visibility.Hidden;
            ImgList.Visibility = Visibility.Hidden;

            Previous.Visibility = Visibility.Hidden;
            Next.Visibility = Visibility.Hidden;

            Tab.Content = "Vidéo";
        }

        //Fonction pour ajouter un fichier à la liste
        private void AddFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            fileDialog.Filter = "Tous les fichiers supportés (*.xlsx;*.pptx;*.png;*.jpg;*.jpeg;*.gif;*.mp4;*.mov;*.wmv;*.mp3;*.wav;*.m4a)" +
                "|*.xlsx;*.pptx;*.png;*.jpg;*.jpeg;*.gif;*.mp4;*.mov;*.mp3;*.wmv;*.wav;*.m4a|Fichiers Excels (*.xlsx)|*.xlsx" +
                "|Fichiers Power-point (*.pptx)|*.pptx|Fichiers images (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif" +
                "|Fichiers vidéos (*.mp4;*.mov;*.wmv)|*.mp4;*.mov;*.wmv|Fichiers audios (*.mp3;*.wav;*.m4a)|*.mp3;*.wav;*.m4a";

            if (fileDialog.ShowDialog() == true)
            {
                foreach (string filepath in fileDialog.FileNames)
                {
                    FileData data = new FileData(System.IO.Path.GetFileName(filepath), filepath);

                    ObservableCollection<FileData> files = null;

                    switch (System.IO.Path.GetExtension(data.Path))
                    {
                        case ".xlsx":
                        {
                            data.Type = FileType.Excel;

                            files = ExcelList.ItemsSource as ObservableCollection<FileData>;

                            break;
                        }

                        case ".pptx":
                        {
                            data.Type = FileType.Ppt;

                            files = PptList.ItemsSource as ObservableCollection<FileData>;

                            break;
                        }

                        case ".png":
                        case ".jpg":
                        case ".jpeg":
                        case ".gif":
                        {
                            data.Type = FileType.Img;

                            files = ImgList.ItemsSource as ObservableCollection<FileData>;

                            break;
                        }

                        case ".mp4":
                        case ".mov":
                        case ".wmv":
                        {
                            data.Type = FileType.Vid;

                            files = VidList.ItemsSource as ObservableCollection<FileData>;

                            break;
                        }

                        case ".mp3":
                        case ".wav":
                        case ".m4a":
                        {
                            data.Type = FileType.Audio;

                            files = AudioList.ItemsSource as ObservableCollection<FileData>;

                            break;
                        }
                    }

                    if (files == null) return;

                    if (!files.Contains(data))
                    {
                        files.Add(data);
                    }
                }
            }
        }

        //Fonction pour retirer un fichier de la liste
        private void RemoveFile(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            FileData data = button.DataContext as FileData;
            if (data == null) return;

            ListBox list = FindVisualParent<ListBox>(button);
            if (list == null) return;

            ObservableCollection<FileData> files = list.ItemsSource as ObservableCollection<FileData>;
            if (files == null) return;

            if(files.Contains(data))
            {
                files.Remove(data);
            }
        }

        //Fonction qui mets à jour le dernier point cliqué pour déplacer un item
        private void ChangeStartPoint(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);
        }

        //Fonction qui vérifie si l'utilisateur veux vraiment déplacer un item
        private void PreviewFileDrop(object sender, MouseEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(null);
            Vector diff = dragStartPoint - point;
            if(e.LeftButton == MouseButtonState.Pressed && 
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListBox list = sender as ListBox;
                ListBoxItem item = FindVisualParent<ListBoxItem>(((DependencyObject)e.OriginalSource));
                if (item == null) return;
                DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
            }
        }

        //Fonction qui déplace un item dans une liste de fichiers
        private void FileDropped(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem)
            {
                FileData source = e.Data.GetData(typeof(FileData)) as FileData;
                if(source == null) return;

                FileData target = ((ListBoxItem)sender).DataContext as FileData;
                if(target == null) return;

                ListBox list = FindVisualParent<ListBox>((ListBoxItem)sender);
                if (list == null) return;

                ObservableCollection<FileData> files = list.ItemsSource as ObservableCollection<FileData>;
                if (files == null) return;

                if (files.Contains(source))
                {
                    files.Move(files.IndexOf(source), files.IndexOf(target));
                }
            }
        }

        //Présenter l`élément lorsqu'il y a un double clique
        private void FileDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FileData data = ((ListBoxItem)sender).DataContext as FileData;
            if (data == null) return;

            switch(data.Type)
            {
                case FileType.Ppt:
                case FileType.Img:
                case FileType.Vid:
                {
                    NextPresenter.Render(new FrameData(data, MultimediaEnabled, true));

                    nextFrame = data;

                    break;
                }

                case FileType.Audio:
                {
                    AudioPlayer.Source = new Uri(data.Path, UriKind.Absolute);
                    AudioPlayer.Play();
                    AudioPlaying = true;

                    break;
                }

                default: break;
            }
        }

        //Retourner à l'élément précédent
        private void Back(object sender, RoutedEventArgs e)
        {
            if (frames.Count < 1) return;
            
            nextFrame = frames.Last().BgFile;

            FrameData lastFrame;
            if (frames.Count > 1)
            {
                lastFrame = frames[frames.Count - 2];
            } else
            {
                lastFrame = new FrameData(null, false, false);
            }

            CurrentPresenter.Render(lastFrame);
            NextPresenter.Render(new FrameData(nextFrame, MultimediaEnabled, true));

            frames.Remove(frames.Last());
        }

        //Présenter le prochain élément
        private void Go(object sender, RoutedEventArgs e)
        {
            FrameData frame = new FrameData(nextFrame, MultimediaEnabled, false);

            CurrentPresenter.Render(frame);

            if (!(nextFrame == null && (frames.Count == 0 || frames.Last().BgFile == null)))
            {
                frames.Add(frame);
            }

            nextFrame = NextPresenter.Next();
        }

        //Fonction pour faire jouer ou pauser l'audio
        private void PlayPauseAudio(object sender, RoutedEventArgs e)
        {
            if (AudioPlayer.Source == null) return;

            AudioPlaying = !AudioPlaying;

            AudioPlayImg.Data = AudioPlaying ? Geometry.Parse("M0 0 L0 48 L16 48 L16 0 M40 0 L24 0 L24 48 L40 48 Z") : Geometry.Parse("M0 0 L0 64 L48 32 Z");

            if (AudioPlaying)
            {
                AudioPlayer.Play();
            } else
            {
                AudioPlayer.Pause();
            }
        }

        //Fonction quand l'audio commence
        private void AudioBegin(object sender, RoutedEventArgs e)
        {
            AudioPlayImg.Data = Geometry.Parse("M0 0 L0 48 L16 48 L16 0 M40 0 L24 0 L24 48 L40 48 Z");

            AudioDuration.Content = AudioPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");

            AudioSlider.IsEnabled = true;
            AudioSlider.Maximum = AudioPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            AudioSlider.Value = 0;
        }

        //Lorsqu'on veut changer la position de l'audio
        private void AudioBeginChangePos(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            UserChangingAudioPos = true;
        }

        //Changer la position de l'audio
        private void ChangeAudioPos(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Slider slider = sender as Slider;
            if(slider == null) return;

            AudioPlayer.Position = TimeSpan.FromSeconds(slider.Value);
            UserChangingAudioPos = false;
        }

        //Fonction pour mettre à jour la position de l'audio
        private void AudioTimerTick(object sender, EventArgs e)
        {
            if (AudioPlayer.Source == null) return;
            if (UserChangingAudioPos) return;
            AudioPosition.Content = AudioPlayer.Position.ToString(@"mm\:ss");
            AudioSlider.Value = AudioPlayer.Position.TotalSeconds;
        }

        //Fonction quand l'audio finit
        private void AudioEnd(object sender, RoutedEventArgs e)
        {
            AudioPlayImg.Data = Geometry.Parse("M0 0 L0 64 L48 32 Z");
            AudioPosition.Content = "00:00";
            AudioDuration.Content = "--:--";

            AudioSlider.Value = 0;
            AudioSlider.IsEnabled = false;

            AudioPlaying = false;

            AudioPlayer.Source = null;
        }

        //Changer le volume lorsque le slider est changé
        private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;
            if(slider == null) return;

            AudioPlayer.Volume = slider.Value/10;
        }

        //Changer le volume lorsque le slider est changé
        private void UpdatePos(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(UserChangingAudioPos)
            {
                TimeSpan time = new TimeSpan(0, 0, (int)e.NewValue);
                AudioPosition.Content = time.ToString(@"mm\:ss");
            }
        }

        //Ouvrir la fenêtre de réglages
        private void OpenSettingsWindow(object sender, RoutedEventArgs e)
        {
            Settings win = new Settings(monitors);
            if(win.ShowDialog() == false)
            {
                monitors = win.returnValue;
            }
        }
    }
}
