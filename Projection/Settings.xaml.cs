using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Interop;
using DocumentFormat.OpenXml.Office.PowerPoint.Y2021.M06.Main;
using System.Runtime.InteropServices;

namespace Projection
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    ///

    //TODO: Mettre les fenêtres en appliquant

    public enum ScreenMode
    {
        Aucun,
        Control,
        Projecteur,
        Dupliquer,
    }

    public class MonitorData
    {
        public MonitorData(ScreenMode screenMode, Screen screen)
        {
            ScreenMode = screenMode;
            MonitorScreen = screen;
        }

        public ScreenMode ScreenMode { get; set; }
        public ScreenMode? NextMode { get; set; }
        public Screen MonitorScreen { get; set; }
    }

    public partial class Settings : Window
    {
        private SelectedScreen selectedWindow; //Fenêtre qui détermine quel écran est lequel
        private List<MonitorData> monitors = new List<MonitorData>();
        public List<MonitorData> returnValue { get { return monitors; } }

        public Settings(List<MonitorData>? savedData)
        {
            InitializeComponent();

            Closing += OnClose;

            Screen mainScreen = Screen.FromHandle(new WindowInteropHelper(this).EnsureHandle());

            foreach (Screen screen in Screen.AllScreens)
            {
                ScreenMode mode;
                MonitorData previousData = null;

                if (savedData != null && savedData.Count > 0)
                {
                    foreach(MonitorData monitor in savedData)
                    {
                        if(screen.Equals(monitor.MonitorScreen))
                        {
                            previousData = monitor;
                        }
                    }
                }

                if (mainScreen.Equals(screen))
                {
                    mode = ScreenMode.Control;
                } else if(previousData != null && previousData.ScreenMode != ScreenMode.Control)
                {
                    mode = previousData.ScreenMode;
                } else
                {
                    mode = ScreenMode.Aucun;
                }

                monitors.Add(new MonitorData(mode, screen));
            }

            MonitorsList.ItemsSource = monitors;

            selectedWindow = new SelectedScreen();

            List<ScreenMode> modes = Enum.GetValues(typeof(ScreenMode)).Cast<ScreenMode>().ToList<ScreenMode>();
            modes.Remove(ScreenMode.Control);

            ScreenModeChoice.ItemsSource = modes;
        }

        //Fermer les autres fenêtres lorsque cette fenêtre se ferme
        private void OnClose(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            selectedWindow?.Close();
        }

        //Enregistrer les réglages et fermer
        private void Ok(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            this.Close();
        }

        //Fermer sans enregistrer les réglages
        private void Cancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //Enregistrer les réglages
        private void Apply(object sender, RoutedEventArgs e)
        {
            ApplySettings();
        }

        //Enregistrer les réglages
        private void ApplySettings()
        {
            foreach(MonitorData data in monitors)
            {
                data.ScreenMode = data.NextMode ?? data.ScreenMode;
                data.NextMode = null;

                if(data.Equals(MonitorsList.SelectedItem))
                {
                    switch (data.ScreenMode)
                    {
                        case ScreenMode.Aucun: ScreenModeLabel.Content = "Mode: Aucun"; break;
                        case ScreenMode.Control: ScreenModeLabel.Content = "Mode: Contrôle présentation"; break;
                        case ScreenMode.Projecteur: ScreenModeLabel.Content = "Mode: Projection principale"; break;
                        case ScreenMode.Dupliquer: ScreenModeLabel.Content = "Mode: Copie projection"; break;
                    }
                }
            }
        }

        //Changer d'écran sélectionné
        private void ScreenSelected(object sender, SelectionChangedEventArgs e)
        {
            MonitorData data = MonitorsList.SelectedItem as MonitorData;

            if (data == null) return;

            selectedWindow.Left = data.MonitorScreen.Bounds.Left;
            selectedWindow.Top = data.MonitorScreen.Bounds.Top;
            selectedWindow.Show();

            switch(data.ScreenMode)
            {
                case ScreenMode.Aucun: ScreenModeLabel.Content = "Mode: Aucun"; break;
                case ScreenMode.Control: ScreenModeLabel.Content = "Mode: Contrôle présentation"; break;
                case ScreenMode.Projecteur: ScreenModeLabel.Content = "Mode: Projection principale"; break;
                case ScreenMode.Dupliquer: ScreenModeLabel.Content = "Mode: Copie projection"; break;
            }

            ScreenModeChoice.SelectedItem = null;
            ScreenModeChoice.IsEnabled = !(data.ScreenMode == ScreenMode.Control);
        }

        //Changer le mode de l'écran
        private void ScreenModeChanged(object sender, SelectionChangedEventArgs e)
        {
            MonitorData data = MonitorsList.SelectedItem as MonitorData;
            ScreenMode? mode = ScreenModeChoice.SelectedItem as ScreenMode?;

            if(data == null || mode == null) return;

            data.NextMode = mode;
        }
    }
}
