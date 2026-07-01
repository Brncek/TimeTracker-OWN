using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TimeTracker
{
    /// <summary>
    /// Interaction logic for ArchiveDatePicker.xaml
    /// </summary>
    public partial class ArchiveDatePicker : Window
    {

        public bool Succesfule { get; set; } = false;
        public DateTime? Start {  get; set; }
        public DateTime? End { get; set; }

        public string Note { get; set; } = string.Empty;

        public ArchiveDatePicker()
        {
            InitializeComponent();

            ErrorText.Text = string.Empty;
        }

        public ArchiveDatePicker(DateTime start, DateTime end)
        {
            InitializeComponent();

            DatePiskerStart.SelectedDate = start;
            DatePickerEnd.SelectedDate = end;

            ErrorText.Text = string.Empty;
        }

        private void OK_click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if (DatePiskerStart.SelectedDate == null || DatePickerEnd.SelectedDate == null)
            {
                ErrorText.Text = "Dates have to be selectd";
                return;
            }

            if (DatePiskerStart.SelectedDate > DatePickerEnd.SelectedDate)
            {
                ErrorText.Text = "End can not be before start";
                return;
            }

            Succesfule = true;

            Start = DatePiskerStart.SelectedDate;
            End = DatePickerEnd.SelectedDate;
            Note = DataPickerNote.Text;
            DialogResult = true;
        }
    }
}
