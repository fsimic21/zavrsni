
using DataAccessLayer;
using ServiceLayer;
using System.Windows;

namespace PresentationLayer
{
    public partial class MainWindow : Window
    {
        CarService service;
        CarRepository repository;
        public MainWindow()
        {
            InitializeComponent();
            repository = new CarRepository();
            service = new CarService(repository);
        }

        private void HomeScreen_Click(object sender, RoutedEventArgs e)
        {
            UcHomeScreen screen = new UcHomeScreen();
            MainContent.Content = screen;
        }

        private void Camera_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void Image_Click(object sender, RoutedEventArgs e) {
            UcImage screen = new UcImage(service);
            MainContent.Content = screen;
        }

        private void Manage_Click(object sender, RoutedEventArgs e) {
            UcManager screen = new UcManager(service);
            MainContent.Content = screen;
        }
    }
}
