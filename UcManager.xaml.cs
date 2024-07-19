using EntitiesLayer;
using Microsoft.IdentityModel.Tokens;
using ServiceLayer;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PresentationLayer {
    /// <summary>
    /// Interaction logic for UcManager.xaml
    /// </summary>
    public partial class UcManager : UserControl {
        CarService service;
       
        public UcManager(CarService service) {
            InitializeComponent();
            this.service = service;
            CarsDataGrid.ItemsSource = service.GetAllCars();
        }

        private void AddCarButton_Click(object sender, RoutedEventArgs e) {
            var cars = service.GetAllCars();
            int num = 1; 
            if (cars.Count > 0) {
                var lastCar = cars[cars.Count - 1]; 
                num = lastCar.Id + 1;
            }
            AddCar screen = new AddCar(service, num);
            Content = screen;
        }



        private void DeleteCarButton_Click(object sender, RoutedEventArgs e) {
            service.DeleteCar(CarsDataGrid.SelectedItem as Car);
            CarsDataGrid.ItemsSource = service.GetAllCars();

        }
    }
}
