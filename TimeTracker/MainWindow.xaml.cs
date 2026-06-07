using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace TimeTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime? start;
        private DateTime? pauseStart;
        private TimeSpan? pauseTime;
        private Mutex timeMutex = new();
        private Thread? timer;

        private TimesDatabase database = new();

        public MainWindow()
        {
            InitializeComponent();
            Reload();
        }

        private void StartBT_Click(object sender, RoutedEventArgs e)
        {
            if (start == null)
            {
                timeMutex.WaitOne();
                start = DateTime.Now;
                timeMutex.ReleaseMutex();
                pauseTime = new TimeSpan();

                timer = new Thread(() =>
                {
                    while (start != null)
                    {
                        TimeSpan time;


                        timeMutex.WaitOne();

                        if (start == null)
                        {
                            timeMutex.ReleaseMutex();
                            break;
                        }

                        if (pauseStart == null)
                        {
                            time = DateTime.Now - start.Value - pauseTime!.Value;
                        }
                        else
                        {
                            time = pauseStart.Value - start.Value - pauseTime!.Value;
                        }

                        timeMutex.ReleaseMutex();

                        SteTimeLabel(time);

                        Thread.Sleep(1000);
                    }
                });

                timer.Start();

            }
            else
            {
                var now = DateTime.Now;

                timeMutex.WaitOne();

                pauseTime += now - pauseStart;
                pauseStart = null;

                timeMutex.ReleaseMutex();
            }

            PauseBT.IsEnabled = true;
            StopBT.IsEnabled = true;
            DeleteBT.IsEnabled = true;
            StartBT.IsEnabled = false;
        }

        private void PauseBT_Click(object sender, RoutedEventArgs e)
        {
            timeMutex.WaitOne();
            pauseStart = DateTime.Now;
            timeMutex.ReleaseMutex();

            PauseBT.IsEnabled = false;
            StopBT.IsEnabled = true;
            DeleteBT.IsEnabled = true;
            StartBT.IsEnabled = true;
        }

        private void StopBT_Click(object sender, RoutedEventArgs e)
        {
            var end = DateTime.Now;

            timeMutex.WaitOne();


            database.WriteTime(start!.Value, end, end - start!.Value - pauseTime!.Value);

            start = null;
            pauseStart = null;
            pauseTime = null;

            timeMutex.ReleaseMutex();

            SteTimeLabel(null);

            PauseBT.IsEnabled = false;
            StopBT.IsEnabled = false;
            DeleteBT.IsEnabled = false;
            StartBT.IsEnabled = true;

            Reload();
        }

        private void DeleteBT_Click(object sender, RoutedEventArgs e)
        {
            timeMutex.WaitOne();

            start = null;
            pauseStart = null;
            pauseTime = null;

            timeMutex.ReleaseMutex();

            SteTimeLabel(null);

            PauseBT.IsEnabled = false;
            StopBT.IsEnabled = false;
            DeleteBT.IsEnabled = false;
            StartBT.IsEnabled = true;
        }
        
        private void ExportBT_Click(object sender, RoutedEventArgs e)
        {
            var start = ExportDateStart.SelectedDate;
            var end = ExportDateEnd.SelectedDate;

            if (start == null || end == null)
            {
                MessageBox.Show("Please select export dates !", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (start > end)
            {
                MessageBox.Show("The start date has to be befor end date !", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var data = database.GetInTime(start.Value, end.Value);

            if (data.Count == 0)
            {
                MessageBox.Show("No times in selected dates. ", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.FileName = "report.xlsx";

            if (saveFileDialog.ShowDialog() == true)
            {
                string path = saveFileDialog.FileName;

                XLSXPrinter.Export(path, data);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (start != null)
            {
                StopBT_Click(sender, null!);
            }
        }

        private void SteTimeLabel(TimeSpan? time)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string labelText;

                if (time != null)
                {
                    labelText = $"Time: {time.Value.ToString(@"hh\:mm\:ss")}";
                }
                else
                {
                    labelText = "Time: 00:00:00";
                }

                TimerLabel.Content = labelText;
            });
            
        }

        private void ClearBT_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you shore ?", "WARNING",  MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.OK)
            {
                database.ClearDB();
                DataStack.Children.Clear();
            }
        }

        private void Reload()
        {
            DataStack.Children.Clear();

            var data = database.GetAll();

            foreach (var line in data)
            {
                var infoline = new InfoLine(line, Reload, database.Delete);
                DataStack.Children.Add(infoline);
                DataStack.Children.Add(new Separator());
            }
        }

        private void AddManual_Click(object sender, RoutedEventArgs e)
        {
            var start = ManualAddDate.SelectedDate;

            if (start == null)
            {
                MessageBox.Show("Please select start date !", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(
                    ManualAddTime.Text.Replace(",", "."),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double time))
            {
                MessageBox.Show("Please enter a valid time !", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (time <= 0)
            {
                MessageBox.Show("Time has to be positive !", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var end = start.Value.AddHours(time);

            database.WriteTime(start.Value, end, end - start.Value );

            Reload();
        }
    }

    public class InfoLine : StackPanel
    {
        public InfoLine(TimeInfo info, Action reload, Action<int> delete)
        {
            Margin = new Thickness(5);
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 35;
            
            var l1 = new Label();
            l1.Content = info.Start.ToString("yyyy.MM.dd HH:mm:ss");
            l1.Width = 150;
            l1.VerticalAlignment = VerticalAlignment.Center;
            var l2 = new Label();
            l2.Content = info.End.ToString("yyyy.MM.dd HH:mm:ss");
            l2.Width = 150;
            l2.VerticalAlignment = VerticalAlignment.Center;
            var l3 = new Label();
            l3.Content = info.TimeWorked.ToString(@"hh\:mm\:ss");
            l3.Width = 100;
            l3.VerticalAlignment = VerticalAlignment.Center;
            Button button = new Button
            {
                Margin = new Thickness(5),
                Content = "Remove",
                Width = 70    
            };


            button.Click += (s, i) =>
            {
                var res = MessageBox.Show("Are you shore ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.OK)
                {
                    delete(info.ID);
                    reload();
                }
            };


            this.Children.Add(l1);
            this.Children.Add(l2);
            this.Children.Add(l3);
            this.Children.Add(button);
        }

    }
}