using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TimeTracker
{
    /// <summary>
    /// Interaction logic for CustomerChooser.xaml
    /// </summary>
    public partial class CustomerChooser : Window
    {
        private int _selectedIndex = -1;

        public int SelectedId => _selectedIndex;

        public CustomerChooser(List<CustomerInfo> customerInfos, string selected)
        {
            InitializeComponent();

            foreach (var item in customerInfos)
            {
                var button = new Button();
                button.Content = $"{item.Name}";
                button.Click += (x, y) =>
                {
                    _selectedIndex = item.ID;
                    DialogResult = true;
                };

                if (item.Name == selected)
                {
                    button.IsEnabled = false;
                }

                Customers.Children.Add(button);
            }
        }
    }
}
