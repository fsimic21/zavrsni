using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using ServiceLayer;
using System.Windows.Media;
using EntitiesLayer;
using Emgu.CV.Structure;

namespace PresentationLayer {
    public partial class AddCar : UserControl {
        CarService service;
        int num;
        private LicensePlateUtil plateUtil;
        public AddCar(CarService service, int num) {
            this.service = service;
            this.num = num;
            plateUtil = new LicensePlateUtil();
            InitializeComponent();

        }
        private void LoadImageButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {

                string filePath = openFileDialog.FileName;
                DisplayImage(filePath);
                var grayImage = plateUtil.ProcessImage(filePath);
                GrayImage.Source = ConvertMatToBitmapImage(grayImage);
                LicenceTextBox.Text = plateUtil.ExtractLicensePlateText(grayImage);
                var carColor = plateUtil.DetectCarColor(filePath);
                ColorTextBox.Text = ColorTranslator.ToHtml(carColor);
                ColorTextBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(carColor.R, carColor.G, carColor.B));

            }
        }
        private void AddCarButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            Car car = new Car() {
                Color = ColorTextBox.Text,
                Id = num++,
                LicencePlate = LicenceTextBox.Text
            };
            service.AddCar(car);
            this.Content = new UcManager(service);
        }
        private void DisplayImage(string filePath) {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            LoadedImage.Source = bitmap;
        }
        private Bitmap BitmapImageToBitmap(BitmapImage bitmapImage) {
            using (var outStream = new MemoryStream()) {
                var enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                return new Bitmap(outStream);
            }
        }
        private BitmapImage ConvertMatToBitmapImage(Mat mat) {
            using (var memoryStream = new MemoryStream()) {
                mat.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}
