using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.AccessControl;
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

            var res = MessageBox.Show("Are you sure ?", "Info", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (res != MessageBoxResult.OK)
            {
                return;
            }

            var end = DateTime.Now;

            timeMutex.WaitOne();

            if (pauseStart != null)
            {
                pauseTime += end - pauseStart;
                pauseStart = null;
            }

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
            var res = MessageBox.Show("Are you sure ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK)
            {
                return;
            }

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

            var data = database.GetInTime(start.Value, end.Value.AddDays(1));

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

            ManualAddDate.SelectedDate = DateTime.Now;

            if (data.Count > 0)
            {
                var maxDate = data.Select(i => i.Start.Date).Max();
                var minDate = data.Select(i => i.End.Date).Min();

                ExportDateStart.SelectedDate = minDate;
                ExportDateEnd.SelectedDate = maxDate;
            }

            data.Reverse();

            double sumTime = 0;

            foreach (var line in data)
            {
                sumTime += line.TimeWorked.TotalHours;

                var infoline = new InfoLine(line, Reload, database.Delete);
                DataStack.Children.Add(infoline);

                var separator = new Separator();

                separator.Opacity = 0.25;

                DataStack.Children.Add(separator);
            }

            var name = database.GetActualName;

            CustomerNameBox.Text = name;
            Archive_CustomerNameBox.Text = name;
            Archive_Detail_CustomerNameBox.Text = name;

            TimeSumBox.Text = $"{sumTime.ToString("F2").Replace(",", ".")}";
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

            var hours = int.Parse(ManualAddTimeH.Text);
            var minutes = int.Parse(ManualAddTimeM.Text);

            start = start.Value.AddHours(hours).AddMinutes(minutes);

            var end = start.Value.AddHours(time);

            database.WriteTime(start.Value, end, end - start.Value );

            Reload();
        }

        private void AddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new InputBox();
            inputBox.ShowDialog();

            var name = inputBox.GetOutput();

            database.AddCustomer(name);
            
            Reload();
        }

        private void ChangeCustomer_Click(object sender, RoutedEventArgs e)
        {
            if(start != null)
            {
                StopBT_Click(this, e);

                if(start != null)
                {
                    return;
                }
            }

            var customerBox = new CustomerChooser(database.GetCustomers(), database.GetActualName);
            customerBox.ShowDialog();

            var id = customerBox.SelectedId;
            
            if (id != -1)
            {
                database.ChangeCustomer(id);
                Reload();
            }
        }

        private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you shore ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK)
            {
                return;
            }

            if (!database.RemoveActualCustomer())
            {
                MessageBox.Show(this, "There has to be at least one customer", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

            Reload();
        }

        private void RenameCustomer_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new InputBox();
            inputBox.ShowDialog();

            var name = inputBox.GetOutput();

            database.RenameActualCustomer(name);

            Reload();
        }

        private void ClearCustomer_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you shore ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.OK)
            {
                database.ClearTimeDBUser();
                Reload();
            }
        }

        public void Add_To_Archive_CLick(object sender, RoutedEventArgs e)
        {
            var timeInfos = database.GetAll();

            ArchiveDatePicker datePicker;
            
            if (timeInfos.Count > 0)
            {
                var maxDate = timeInfos.Select(i => i.Start.Date).Max();
                var minDate = timeInfos.Select(i => i.End.Date).Min();
                datePicker = new ArchiveDatePicker(minDate, maxDate);
            }
            else
            {
                datePicker = new ArchiveDatePicker();
            }

            datePicker.ShowDialog();

            if (!datePicker.Succesfule) return;

            var data = database.GetInTime(datePicker.Start!.Value, datePicker.End!.Value.AddDays(1));

            database.SaveToArchive(data, datePicker.Note);

        }

        public void Open_Archive_Click(object sender, RoutedEventArgs e)
        {
            MainPage.Visibility = Visibility.Collapsed;
            ArchivePage.Visibility = Visibility.Visible;
            ReloadArchive();
        }

        public void Close_Archive_Click(object sender, RoutedEventArgs e)
        {
            ArchivePage.Visibility = Visibility.Collapsed;
            MainPage.Visibility = Visibility.Visible;
        }

        public void Delete_Archive_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you shore ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK)
            {
                return;
            }
            
            database.DeleteArchiveOfUser();
            ReloadArchive();
        }

        public void Close_Archive_Detail_Click(object sender, RoutedEventArgs e)
        {
            ArchiveDetailPage.Visibility = Visibility.Collapsed;
            ArchivePage.Visibility = Visibility.Visible;
        }

        private void Hcheck(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ManualAddTimeH.Text, out var h))
            {
                if (h < 0 || h >= 24)
                {
                    ManualAddTimeH.Text ="0";
                }

                return;
            }

            ManualAddTimeH.Text = "0";
        }

        private void Mcheck(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ManualAddTimeM.Text, out var m))
            {
                if (m < 0 || m >= 60)
                {
                    ManualAddTimeM.Text = "0";
                }

                return;
            }

            ManualAddTimeM.Text = "0";
        }

        private void ReloadArchive()
        {
            var data = database.GetArchiveInfos();

            ArchivePanel.Children.Clear();

            data = data.OrderBy(d => d.ID).ToList();

            foreach (var item in data)
            {
                var separator = new Separator();
                separator.Margin = new Thickness(2);

                ArchivePanel.Children.Add(separator);

                Action<int> deleteAction = (index) =>
                {
                    database.DeleteArchiveInfo(index);
                    ReloadArchive();
                };

                Action<ArchiveInfo> detailAction = (archiveInfo) =>
                {
                    ArchivePage.Visibility = Visibility.Collapsed;
                    ArchiveDetailPage.Visibility = Visibility.Visible;

                    DetailPanel.Children.Clear();

                    foreach (var item in archiveInfo.timeInfos)
                    {
                        var sepa = new Separator();
                        sepa.Margin = new Thickness(2);
                        DetailPanel.Children.Add(sepa);

                        var Infoline = new InfoLine(item, () => { }, (i) => { }, false);

                        DetailPanel.Children.Add(Infoline);
                    }
                };

                ArchivePanel.Children.Add(new ArchiveInfoLine(item, deleteAction, detailAction));
            }
        }
    }

    public class InfoLine : StackPanel
    {
        public InfoLine(TimeInfo info, Action reload, Action<int> delete, bool showDeleteBt = true)
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
                Content = "X",
                Width = 70    
            };


            button.Click += (s, i) =>
            {
                var res = MessageBox.Show("Are you sure ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.OK)
                {
                    delete(info.ID);
                    reload();
                }
            };


            this.Children.Add(l1);
            this.Children.Add(l2);
            this.Children.Add(l3);

            if (showDeleteBt)
                this.Children.Add(button);
        }

    }

    public class ArchiveInfoLine : StackPanel
    {
        public ArchiveInfoLine(ArchiveInfo info, Action<int> delete, Action<ArchiveInfo> detail)
        {
            Margin = new Thickness(5);
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 35;

            var l0 = new Label();
            l0.Content = info.Note;
            l0.Width = 100;
            l0.VerticalAlignment = VerticalAlignment.Center;

            var l1 = new Label();
            l1.Content = info.Start.ToString("yyyy.MM.dd");
            l1.Width = 150;
            l1.VerticalAlignment = VerticalAlignment.Center;
            var l2 = new Label();
            l2.Content = info.End.ToString("yyyy.MM.dd");
            l2.Width = 150;
            l2.VerticalAlignment = VerticalAlignment.Center;
            var l3 = new Label();

            TimeSpan timeSum = new();

            foreach (var item in info.timeInfos)
            {
                timeSum += item.TimeWorked;
            }

            l3.Content = timeSum.ToString(@"hh\:mm\:ss");
            l3.Width = 100;
            l3.VerticalAlignment = VerticalAlignment.Center;

            Button button0 = new Button
            {
                Margin = new Thickness(5),
                Content = "Detail",
                Width = 70
            };

            Button button2 = new Button
            {
                Margin = new Thickness(5),
                Content = "Export",
                Width = 70
            };

            Button button = new Button
            {
                Margin = new Thickness(5),
                Content = "X",
                Width = 70
            };

            button0.Click += (s, i) =>
            {
                detail(info);
            };

            button.Click += (s, i) =>
            {
                var res = MessageBox.Show("Are you sure ?", "WARNING", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.OK)
                {
                    delete(info.ID);
                }
            };

            button2.Click += (s, i) =>
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = "report.xlsx"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    string path = saveFileDialog.FileName;
                    XLSXPrinter.Export(path, info.timeInfos);
                }
            };

            this.Children.Add(l0);
            this.Children.Add(l1);
            this.Children.Add(l2);
            this.Children.Add(l3);
            this.Children.Add(button0);
            this.Children.Add(button2);
            this.Children.Add(button);
        }
    }
}
